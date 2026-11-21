# 多段复合答案（含 (1)…(2)… 或多关键词）根因关闭与碎片上下文回填

> 触发：用户要求「先完全修复 多段复合答案（含 (1)…(2)… 或多关键词）104 题（占未解 75%），大题某一小问被单独抽成题录，answer 存的是整道大题复合串」这个问题。
> 结论先行：经全量数据取证，**不存在多段复合答案的判分 bug**；真正阻塞是「题干碎片丢失大题上下文」+「引擎能力缺口」。代表性可修题 pdf-174 已修复并验证（618，+1，零回归）。本 spec 把取证落盘、锁定绿色基线，并执行下一批「碎片上下文回填 + 现有求解器解锁」的安全批次。

## 1. 问题陈述与用户假设

用户假设：104 道未解（占 139 未解的 ~75%）属于「多段复合答案」类，根因是「大题某一小问被单独抽成题录，answer 存的是整道大题复合串」，导致判分失败，要求完全修复。

## 2. 取证结论（已落盘证据）

### 2.1 判分侧无 bug（用户假设不成立）

- 数据源：`_evalscratch/checkpoint_rel_half.jsonl`（757 题，LLM-off 确定性）。
- 全库扫描：**adopted 但 gatewayCorrect=false 的题为 0**（`gatewayAnswer` 非空且判错 = 0）。即凡被采纳的引擎结论全部判正，precision=1 由构造保证。
- 机制：
  - [IsAnswerCorrect](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L344-L366) 遍历全部 `AnswerKeywords`，**任一词**经 `NormalizeChemicalNotation` 后被子串包含即判正 → 答对任一子问即算整题正确（pdf-313 先例）。
  - [SplitPdfAnswerKeywords](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1818-L1859) 在评估加载时（[L1752](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1752)/[L1796](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1796)）对 `existing.Count<=1` 的记录重新切分 `(1)(2)`/多关键词，预处理不良的也会被修正。
- 落盘：`_evalscratch/multi_unsolved_ground.tsv`（104 题逐题 taskType / hasRaw / hasAdopted / isPlaceholder / 关键词）。

### 2.2 104 题真实分解

| 子类 | 数量 | 说明 |
|------|------|------|
| 纯 null（引擎无产出） | 96 | 能力/路由缺口：choice 42、unknown 26、calculation 18、equilibrium 9、judgment 2、concept 1、atomic_structure 1 |
| 有原始产出但被采纳门正确过滤 | 8 | 5 个 reaction_check 占位（`ChemicalInferenceService`「已匹配到相关反应」/「已解析出相关物质与离子」，见 [ChemicalInferenceService.cs L200](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry/ChemicalInferenceService.cs#L200)）；2 个分子分析器域错误（pdf-391/522）；1 个曲线读数（pdf-272）。**全部正确过滤 → 0 harm、0 adopted-wrong** |

> 关键：这 8 题的占位/错域产出**不是**被误采纳的错误答案——[采纳门](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L716-L750) 已用签名过滤剔除，故 precision 不受影响。它们只是「未真正解题」。

### 2.3 真正根因：题干碎片丢失大题上下文（非 answer 字段）

以 pdf-174 为例：
- 评估用的**陈旧数据副本**（`_evalscratch/apirel/data/pdf_questions.json`，mtime 11/19）中，题干是碎片：`（2）"还原酸浸"时，MnO2 反应的离子方程式为____`——缺少反应物 `H2SO4`/`Na2SO3`（它们在大题工艺流程图上下文里）。
- 碎片题干 → [ManganeseDioxideSulfiteIonic](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs#L248-L254) 守卫 `HasSulfite`/`HasAcid` 失败 → 返回 null → 回退 `_chem.Evaluate` 产出占位 → 未解。
- 当前**源数据**（`src/CogniThink.TutorBrain.Api/data/pdf_questions.json`，已修改未提交，mtime 13:08）已把 pdf-174 题干回填为完整大题上下文（含 `H2SO4`、`Na2SO3`）→ 模板命中 → 产出「离子方程式：MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O」→ 判正。

### 2.4 pdf-174 已修复并验证（绿色基线 618）

- `_evalscratch/checkpoint_pdf174fix_summary.txt`（mtime 13:15，晚于源数据回填 13:08）：`gatewayCorrect=618`、`precision=1`、`harmCount=0`、`bothWrong=139`。
- 相对 `rel_half`（617）：**GAIN 1（pdf-174）/ LOST 0**。
- 已回填的 9 题（pdf-009/012/020/065/174/233/243/244/293）在 pdf174fix 中状态：除 pdf-244（calculation，仍 null）外其余均判正；pdf-174 为本次 +1。
- 全量测试当前工作树：**2139 passed / 0 failed**（TutorBrain 1228 + Chem 911）。

### 2.5 剩余规模

源数据相对陈旧评估副本仅 9 题题干有差异 → 「碎片上下文丢失」当前源数据中仍有 ~95 题（104 多段未解 − pdf-174 − 其他非碎片）是**真碎片**（大题上下文从未被采集），或属于**能力缺口**（choice/calc/equilibrium/organic 无解求解器）。这些无法靠一次判分修复解决。

## 3. 冻结红线（每批必过，否则回退）

1. `precision = 1`（凡采纳必判正）、`harmCount = 0`、`LOST = 0`（相对上一绿色 tag）。
2. 任何题干回填/答案键改动必须附**官方源溯源**（写入 `note`，指向习题册/答案册 page + num），禁止臆造题干内容。
3. 化学事实/答案真值只在 JSON/资产，不在逻辑层硬编码。
4. 不放宽采纳门去接受占位结论；无唯一解一律 null。
5. 每批：FullBankGuard 全绿 → 全量测试 0 failed → eval 新 tag → diff 确认 LOST=0。任一破坏则整批回退。

## 4. 范围与非目标

**本 spec 范围（M1 批次）：**
- 把取证结论与 pdf-174 绿色基线（618）落盘锁定。
- 对剩余 103 多段未解做**分类审计**：(a) 碎片且大题上下文可经官方源恢复 + 现有求解器可命中 → 回填候选；(b) 需要新增求解器能力；(c) 题干本身不完整/数据缺陷。
- 对 (a) 类执行**题干上下文回填**（官方源溯源），并用现有求解器解锁，全红线验证。

**非目标（M1 不做，需后续独立 spec）：**
- 新增求解器能力（多方程安全连接、可行性判决、催化剂识别、弱酸电离/Ka、有机推理、choice 覆盖扩展）——属能力扩张，各自需 FullBankGuard + eval。
- 对 (c) 类真·不完整题的大规模重抽取（需官方源 OCR 重跑，独立数据工程）。

## 5. 验收标准（AC）

- AC-1：取证落盘，证明「多段复合答案无判分 bug」（0 adopted-wrong 全库扫描 + 机制引用）。
- AC-2：锁定绿色基线 = checkpoint_pdf174fix（618 / precision=1 / harm=0）；diff rel_half→pdf174fix = GAIN 1 / LOST 0 落盘。
- AC-3：完成剩余 103 多段未解的分类审计 TSV（a/b/c 三类，逐题理由）。
- AC-4：对 (a) 类候选执行回填 + 解锁，新 eval tag 满足 LOST=0 / precision=1 / harm=0；GAIN 题全部有官方源溯源。
- AC-5：data-defect-register.md 同步：已解题移除，(b)/(c) 类登记真实排除理由。

## 6. 验证方式

- 全库扫描脚本（见 tasks.md TR 引用）复算 0 adopted-wrong。
- `_evalscratch/diff_eval.ps1 -OldTag rel_half -NewTag pdf174fix` → GAIN/LOST。
- 审计 TSV 落盘 `_evalscratch/multi_remaining_audit.tsv`。
- 每批回填后：`dotnet test CogniThink.Chem.sln -c Release --no-restore` 0 failed；`powershell -ExecutionPolicy Bypass -File _evalscratch\run_eval_resume.ps1 -Tag <new> -Env Production`；diff 上一绿色 tag LOST=0。

## 7. 批准门

本 spec 的 M1 批次（审计 + 回填候选解锁）需用户批准后执行。特别确认：
1. 是否同意「判分无 bug，根因是题干碎片 + 能力缺口」这一取证结论？
2. M1 先做「分类审计 + 回填可被现有求解器解锁的碎片子集」，能力缺口与真·不完整题 defer 到后续 spec——是否认可此范围？
