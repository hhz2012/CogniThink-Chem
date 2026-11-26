# 数据缺陷修复计划（data-defect-repair）

> 状态：**已获批并执行完毕（2026-11-26）**。执行结果见 `data-defect-register.md` W 类；LLM-on eval 快照 `_evalscratch/snapshot_gap_datafix1.json`。
> 基线快照：`_evalscratch/snapshot_gap_batch4.json`（gwCorr=663, harm=0, precision=1）。

## 1. 背景与目标

`pdf_questions.json` 是判分真值源。batch4 登记（`data-defect-register.md` K.0）列出约 16 处疑似数据缺陷：答案键与题干问法不符、跨题答案混排、退化键（单字母/整串 √×）造成 pureLlm 假阳性、缺结构图等。这些缺陷使「答对也不被采纳」或「答错也蒙对」，污染指标可信度，并可能在后续引擎扩展时成为键位障碍。

**本计划目标**：在 `precision=1 / harm=0 / 引擎层 LOST=0` 红线下，逐题用证据核对并修复**高置信、低风险**的数据缺陷，提升判分真值的完整性与可信度，为后续引擎增益清障。

## 2. 核心发现（诚实预期，决定计划价值）

审计前必须先澄清一个关键问题：**修答案键能否直接带来 gwCorr 增益？**

### 2.1 判分链路事实（已读源码确认）

- `GatewayVsLlmEvaluationService.RunCaseAsync`：`GatewayCorrect = IsAnswerCorrect(gatewayAnswer, c) || StructuredHit`。
- `BuildGatewayAnswerAsync`（[L763](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L763-L901)）**从不返回 `pureAnswer`**；`ApplyChoiceNarrowingGuard`（[L1017](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1017-L1020)）仅透传引擎结论。
- 即 **gwCorr 完全由引擎真值结论驱动**；LLM 答案只计入 `pureLlmCorrect`，不影响 gwCorr。

### 2.2 全库扫描结果（`_evalscratch/scan_conclusion_misscored.py`）

在 `gap_batch4` 快照中扫描「引擎已产出 `FinalResult`（`gatewayConclusion` 非空）但 `gatewayCorrect=False`」的题——这是「错键挡住正确结论」的唯一形态——**结果仅 2 题，且均非错键问题**：
- pdf-163：引擎结论是占位串「已解析出相关物质与离子」（被正确剔除，键本身无误）。
- pdf-391：引擎解偏了题（产出分子特征而非题目所问），路由问题。

**结论：当前快照中没有任何一题是「引擎答出正确结论、却被错误键拦下」。所有候选缺陷题的引擎结论均为空（`Insufficient`）。**

### 2.3 因此

| 指标 | 修复答案键的预期影响 |
|---|---|
| gwCorr（引擎） | **+0 / -0**（引擎对这些题本就无解，修键不翻正也不丢失） |
| pureLlmCorrect | 可能 +1~2（消除退化键后，正确答案得以命中） |
| harm / precision | 不变（0 / 1），前提是离线回归确认 LOST=[] |

**即：本批次数据修复不产生即时 gwCorr 增益，其价值在于判分真值与指标的完整性、消除 pureLlm 假阳性、并为后续引擎扩展消除键位障碍。** 若用户期望即时 gwCorr 增益，需另启引擎扩展批次（为这些题型补 solver），不在本计划范围。

## 3. 审计方法与证据

证据全部位于 `_evalscratch/`（已 gitignore）：
- `audit_data_defects.py` / `data_defect_audit.txt`：候选题的题库原文 + 快照状态。
- `dump_fulltext.py` / `candidates_fulltext.txt`：候选题完整题干/选项/答案/键/NOTE。
- `scan_misscored_truth.py` / `scan_conclusion_misscored.py`：全库扫描错键误判。
- `spike_key_fix.py`：移植 `NormalizeChemicalNotation` 验证拟修键的命中行为（正确答案命中、错误答案不误命中）。

判分键的生成规则（`SplitPdfAnswerKeywords`，[L1968](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1968-L2009)）：
- 若 `answerKeywords` 已有 **>1 个**元素 → 原样采用。
- 否则（0/1 个）→ 从 `answer` 字段按 `,，、;；` / 换行 / 2+ 空格拆分，剥离 `(1)(2)` 序号前缀。
- 因此**修复时同时写 `answer` 与 `answerKeywords`**，并保证二者一致。

## 4. 逐题修复方案

### Tier 1 — 本批次执行（高置信，正确答案可由题库自身 NOTE / 相邻答案 / 平凡推导确定）

| id | 题型 | 缺陷 | 正确答案（证据） | 修复内容 | 风险 |
|---|---|---|---|---|---|
| **pdf-172** | calc | 问「固定 CO2 ___ L（标况）」计算题，键=`AC`（属另一道 Cr/Cu 电子排布选择题，NOTE 为证） | `89.6 L`：产物 M=466 g/mol → 0.466 kg=1 mol → 含 4 mol CO₃²⁻ → 固定 4 mol CO₂ → 4×22.4=89.6 L | `answer`/`answerKeywords` → `89.6 L` | 极低（平凡化学计量） |
| **pdf-179** | fill | 问「用离子方程式表示置换加 Fe 的目的」，键=`B`（属另一道短周期元素选择题，NOTE 为证） | `Fe + Cu2+ = Fe2+ + Cu`（与 pdf-164 答案子问(3) 及本题 NOTE 一致） | `answer`/`answerKeywords` → `Fe + Cu2+ = Fe2+ + Cu` | 极低（题库自身给出） |
| **pdf-164** | fill | 问「生成 BaTiO(C₂O₄)₂ 的化学方程式」，键却是 `(2)(NH4)2SO4… (3)Fe+Cu2+…`（跨题混排，属黄铜矿题） | `BaCl2 + TiCl4 + 2(NH4)2C2O4 + H2O = BaTiO(C2O4)2↓ + 4NH4Cl + 2HCl`（pdf-160 答案子问(5) 原文，与本题 NOTE 吻合） | `answer`/`answerKeywords` → 该方程式 | 低（题库自身给出） |
| **pdf-117** | judge | 题干是单条正误判断「ⅣA 族单质都是半导体材料（ ）」，键却是 14 个 √/×（属另一份 14 小题卷），含 √ 与 × 双侧 → 任意带 √/× 的回答均判正（假阳性） | `×`（NOTE 明确「(1)(×)…并非都是半导体材料」） | `answer`/`answerKeywords` → `×` | 极低（NOTE 明证） |
| **pdf-324** | fill | 题干「高分子 [CH=CH]n 单体官能团名称 + 化学名称」，键=`碳碳三键, 苯乙炔`；但 [CH=CH]n 为聚乙炔，单体是乙炔（无苯基），NOTE「苯乙炔」与题干结构矛盾（OCR 丢苯基导致 NOTE 错配） | 官能团 `碳碳三键`，单体 `乙炔`（题干 [CH=CH]n 明证） | `answer`/`answerKeywords` → `碳碳三键, 乙炔` | 低（题干结构明确；子串「乙炔」会命中「苯乙炔」是系统固有子串取舍，且引擎不采纳 concept 结论，不影响 gwCorr/harm） |

`spike_key_fix.py` 已验证以上 5 个键：正确答案命中、错误答案（含旧错误键与易混答案）不误命中（pdf-324 子串取舍除外，见上表说明）。

### Tier 2 — 存疑 / 需原题核对，**本批次不动**

| id | 原因 |
|---|---|
| pdf-084 | 题干完整，问 2 个方程式，键=`C`（退化）。正确方程可推导（Co₂O₃+11CO→Co₂(CO)₈+3CO₂；2Ce₂(C₂O₄)₃·10H₂O+3O₂→2Ce₂O₃+12CO₂+20H₂O），但 NOTE 表述自相矛盾、与 LLM 快照答案不一致，需核对原题标准答案后再修，避免写入错误真值。 |
| pdf-190 | 4 子问完整，但键=`(1)正四面体,(2)sp3`（属另一份 VSEPR 题）。需为 4 个子问分别撰写键（第一电离能、电负性、原子半径、Ti 价层排布），且走 multipart AND 门——引擎当前不解，修键仅完整性；逐子问标准答案需核对，风险高于收益，暂列第二优先级。 |
| pdf-263 / pdf-376 / pdf-197 / pdf-306 | 均为 **选择题**，改键风险极高（若键本正确而按 LLM 错答改键=污染真值）。pdf-263 问「正确的有几项」与键 `B` 不符；pdf-376 NOTE 称「全部正确」与问「不正确的是」矛盾；pdf-197 题干是两道题拼接（先问「正确的是」后问「错误的是」）键=ABCD；pdf-306 依赖缺失图像且 NOTE 两段自相矛盾。**选择题键一律需原题答案核对，不在本批自动修复。** |
| pdf-160 | 题干仅存引言「…流程如下：」，子问 (1)~(5) 文本缺失（原在图中）；其答案实为 pdf-164 等题的混排源。题干不完整，纯 JSON 无法修复，需原图/OCR 恢复，另案处理。 |

### 排除 — 缺结构图，**纯 JSON 不可修**

pdf-322 / pdf-323 / pdf-519 / pdf-520 / pdf-521 / pdf-523：题干文本仅「中官能团名称为______」，无结构图、`images` 为空。答案键本身可能正确，但题目因缺图不可解。需图像资产恢复，**不在本计划范围**，登记另案。

## 5. 预期结果

- **gwCorr（引擎）**：不变（+0 / -0）。离线回归必须确认 `gatewayCorrect` 集合与基线逐题一致（LOST=[]）。
- **pureLlmCorrect**：pdf-172、pdf-179 预计由 False→True（退化键替换为正确键后，LLM 已答对的内容得以命中）；其余不变。
- **harm / enginePrecision**：0 / 1，保持。
- 不新增、不删除任何引擎可解的判定；仅变更 5 题的判分真值。

## 6. 执行与验证步骤（获批后）

1. **改数据**：编辑 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`，逐题修改 `answer` 与 `answerKeywords`（Tier 1 共 5 题），保持 JSON 合法。不动其他字段、不动 `images`。
2. **构建**：`dotnet build CogniThink.Chem.sln -c Release` → 0 errors。
3. **护栏单测**：`dotnet test CogniThink.Chem.sln -c Release` → 0 failed（确认数据加载无异常、既有护栏不受影响）。
4. **离线确定性回归（先跑，硬门槛）**：`_evalscratch/run_eval_resume.ps1 -Tag gap_datafix1_off -Env Production`（LLM-off）。与 `snapshot_gap_batch4` 做集合 diff，要求 **GAINED=[] 且 LOST=[]**（引擎侧完全不变）。若出现 LOST → 立即回滚，排查。
5. **LLM-on 评估**：`_evalscratch/run_eval_resume.ps1 -Tag gap_datafix1 -Env Development`。`diff_eval.ps1 gap_batch4 gap_datafix1`：确认无 harm、pureLlm 变化方向正确（pdf-172/179 翻正）、gwCorr 不变。
6. **mp27 守卫**：`verify_mp27.py gap_batch4 gap_datafix1` → 27/27 PASS。
7. **更新登记**：在 `data-defect-register.md` 增补本批修复记录（已修 5 题 + 不动题及理由 + 排除项另案）。
8. **收尾**：确认无残留临时探针（grep 无 `*Temp*`），不主动 commit。

## 7. 风险与回滚

- **最大风险**：把正确键改成错键 → 引入新缺陷。缓释：Tier 1 每题正确答案均有题库自身 NOTE 或相邻答案原文为证，且经 `spike_key_fix.py` 命中验证；选择题一律不动。
- **回滚**：仅改 `pdf_questions.json` 一个文件，`git diff` 可逐条核对；离线回归一旦出现 LOST，`git checkout -- src/.../pdf_questions.json` 即回滚。
- **不动红线**：不碰判分逻辑、不碰引擎代码、不碰 `images`，确保引擎行为零变化（离线回归实证）。

## 8. 范围外（明确不做）

- 缺结构图的 6 道官能团题（需图像资产恢复）。
- 选择题答案键（需原题标准答案核对，避免污染真值）。
- 不新增引擎 solver（若要即时 gwCorr 增益，另启引擎批次）。
