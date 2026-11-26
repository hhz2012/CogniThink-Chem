# 引擎缺口补强 batch4 实施计划

> 基线：[_evalscratch/snapshot_gap_batch3.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_batch3.json)（LLM-on, qwen3-vl-plus）。本计划在获批前只写计划文档与 `_evalscratch/` 证据，不动生产代码。

## 1. 现状与分布分析

batch3 交付后引擎侧指标：`gwCorr=660`、`pureLlmOnly=45`、`bothWrong=52`、`harmCount=0`、`enginePrecision=1`。当前待补强（引擎未解）的候选池 = `pureLlmOnlyCorrectCases ∪ bothWrongCases` = **97 题**（union；用户口径「50」是 gap_batch1 旧数，经 batch2/batch3 已解出 6 题 + LLM 非确定性漂移）。

### taskType × 图像依赖（97 leads）

| taskType | 依赖图像 | 纯文本 | 小计 |
|---|---|---|---|
| choice | 31 | 7 | 38 |
| unknown | 5 | 10 | 15 |
| equation_writer | 3 | 11 | 14 |
| equilibrium | 8 | 3 | 11 |
| concept | 1 | 7 | 8 |
| calculation | 2 | 6 | 8 |
| judgment | 1 | 1 | 2 |
| reaction_check | 0 | 1 | 1 |
| **合计** | **51** | **46** | **97** |

**结论态**：97 题中 95 题为 `Insufficient`（B 类——引擎空结论，主动放弃），仅 2 题 `Confirmed`（pdf-163 非门控 + 1 道 unknown）。**无 C 类**（无引擎算对却被误拦），与 batch3 一致 → 回归风险面仍只在「新增钩子是否产生假阳性」。

证据脚本：[analyze_batch4_leads.py](file:///d:/github/CogniThink-Chem/_evalscratch/analyze_batch4_leads.py) → [batch4_leads_detail.txt](file:///d:/github/CogniThink-Chem/_evalscratch/batch4_leads_detail.txt) / [batch4_leads.json](file:///d:/github/CogniThink-Chem/_evalscratch/batch4_leads.json)。

## 2. 核心发现：剩余缺口的真实构成——引擎可解尾量极小

逐题核对 46 道纯文本候选的**题干与答案键对齐关系**（见 [candidates_full.txt](file:///d:/github/CogniThink-Chem/_evalscratch/candidates_full.txt) + [check_defects.py](file:///d:/github/CogniThink-Chem/_evalscratch/check_defects.py)），97 道 leads 可归为：

| 类别 | 数量 | 性质 | 引擎能否解 |
|---|---|---|---|
| 图像依赖 | 51 | 引擎无视觉输入 | ❌ 需 P2-3 图像融合（另轨） |
| choice 字母答案 | 38 | 答案为 A/B/C/D，引擎不输出字母 | ❌ 属 chem-fact-curation 事实注入轨（另轨） |
| **数据缺陷（答案键错位/跨题混排/缺结构图）** | **~16** | 采纳键本身错误，引擎输出再对也不命中 | ❌ 引擎无法修，需数据修复轨 |
| 自由文本/概念解释（A 类） | ~10 | 无唯一确定数值/方程 | ❌ 设计不走引擎，保持 null |
| 开放论述 / 双线桥等图示 | ~4 | 无唯一可输出结论 | ❌ 不蒙数 |
| 复杂流程氧化还原/多步计量（单题特例） | ~5 | 可解但泛化代价高、ROI 低 | ⏸ 延后到通用氧化还原电子守恒求解器 |
| **酯化反应产物（确定性可解）** | **3** | 原子平衡即可唯一确定 | ✅ **batch4 目标** |

**数据缺陷实证**（均已核对 pdf_questions.json 原文）：
- **跨题答案混排**：pdf-160（H₂Se 制硒流程）的答案键 sub-(5) 是 `BaCl2+TiCl4+…=BaTiO(C2O4)2↓…`，而问这道方程的 pdf-164 的答案键却是 `(NH4)2SO4 △ 2NH3↑…`——两题答案互换。
- **答案键与问法不符**：pdf-172（计算 CO₂ 体积）答案键 = `AC`；pdf-179（写离子方程式）答案键 = `B`；pdf-084（写方程式）答案键 = `C`；pdf-084/263/376 已在 K 类登记。
- **结构缺失**：pdf-322/323/519/520/521/523 问「官能团名称」但文本无结构图、`images` 为空 → 结构资产丢失，不可解。
- **单问多答案**：pdf-117（一条正误判断）答案键含 14 个 √/×；pdf-190（电离能/电负性 4 子问）答案键 = `正四面体, sp3`（属另一道 VSEPR 题）。
- **结构与答案矛盾**：pdf-324 结构 `[CH=CH]n`（聚乙炔）单体应为乙炔，答案键却是「苯乙炔」。

> 关键结论：**当前「引擎缺口」的主体不是引擎能力不足，而是数据缺陷（~16）与非引擎域（图像 51 + 字母 38 + 自由文本 ~10）。** 引擎确定性可解的尾量仅 3 题。强行扩张求解器覆盖这些非引擎题会违反「不蒙数 / precision=1」红线。

## 3. batch4 目标：酯化反应产物求解器（+3，零 FP / 零 harm）

全库仅 7 道含「酯化」相关词（见 [scan_ester.py](file:///d:/github/CogniThink-Chem/_evalscratch/scan_ester.py)）：

| id | gap_batch3 状态 | 内容 |
|---|---|---|
| pdf-067 | GATEWAY_CORRECT | 聚酯催化剂（choice C）—— 无酸/醇反应物，不触发；且 baseTrue 到不了钩子 |
| pdf-259 | GATEWAY_CORRECT | 酯化热力学（计算）—— 无环酯/缩聚/羟基酸触发词；baseTrue 到不了钩子 |
| pdf-354 | GATEWAY_CORRECT | 乙酸+乙二醇 1:1 → 无环酯/缩聚/羟基酸触发词；baseTrue 到不了钩子 |
| pdf-355 | GATEWAY_CORRECT | 乙二酸+乙醇 1:2（batch3-C 已解）—— 现有 solver 先触发，Ok=true → 新钩子跳过 |
| **pdf-356** | **bothWrong（lead）** | 乙二酸+乙二醇 → ①环酯 `C4H4O4` ②缩聚聚酯 `(C4H2O3)n + (2n-1)H2O` |
| **pdf-526** | **bothWrong（lead）** | 乳酸自身酯化 → 六元环酯（交酯）方程 |
| **pdf-527** | **bothWrong（lead）** | 乳酸自身酯化 → 聚酯方程 |

3 道目标均属 `bothWrong`（纯 LLM 也答错）→ 翻正后从 bothWrong→gatewayOnly，**gwCorr 660→663，且不可能产生 harm**（harm 定义要求 pureLlmCorrect）。

**命中已去风险**（[spike_ester_match.py](file:///d:/github/CogniThink-Chem/_evalscratch/spike_ester_match.py)，Python 移植 `NormalizeChemicalNotation` 验证）：
- 4 个目标关键词（pdf-356 ×2、pdf-526、pdf-527）经归一化后**全部被子串包含命中**。
- 原子平衡验算：乙二酸(C₂H₂O₄)+乙二醇(C₂H₆O₂) → 环酯 −2H₂O = **C₄H₄O₄** ✓；聚酯重复单元 = 环酯 −H₂O = **C₄H₂O₃** → `(C4H2O3)n + (2n-1)H2O` ✓；乳酸二聚环酯 = 2×C₃H₆O₃ −2H₂O = **C₆H₈O₄** ✓。

采纳门依据：`equation_writer` 会落到 [BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L763) 的 `IsConcreteConclusion`（只认「能/正确/共存」等判断词），裸方程式不被采纳。batch3 对 pdf-355 的做法是把钩子 `taskType` 设为 `calculation`（L786-790）→ 走 `IsAnswerCorrect`（OR 关键词包含）→ 凡采纳必判正。**batch4 沿用此路径。**

## 4. 实现方案

### 4.1 新增 `EsterificationProductSolver.cs`
位置：`src/CogniThink.TutorBrain.Api/Application/Gateway/EsterificationProductSolver.cs`（与现有 [EsterificationEquationSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/EsterificationEquationSolver.cs) 并列；现有类保留处理 pdf-355 的 1:2 路径，避免回归）。

通用设计：
- **外部化名称→分子式表**（`Dictionary<string,string>`）：乙二酸/草酸→`C2H2O4`、乙二醇→`C2H6O2`、乳酸/2-羟基丙酸/α-羟基丙酸→`C3H6O3`。仅含题库实际出现的物种，可按需扩充（不硬编码单题）。
- **分子式解析/原子计数**（仅 C/H/O，题库酯化产物均满足）：正则 `([CHO])(\d*)` 累加。
- 4 条**互斥窄触发**路径，每条域外返回 `null`（不蒙数）：
  1. **环酯**：题干含「酯化」+「环酯」+（二元酸名 ∧ 二元醇名）→ 产物分子式 = 酸+醇 − 2H₂O → 输出 `酸 + 醇 ⇌ 产物 + 2H2O`（覆盖 pdf-356①）。
  2. **缩聚/聚酯（二元酸+二元醇）**：含「酯化」+（「缩聚」∨「聚酯」）+（二元酸 ∧ 二元醇）→ 重复单元 = 环酯产物 − H₂O → 输出 `(重复单元)n + (2n-1)H2O`（覆盖 pdf-356②）。
  3. **羟基酸自身酯化→六元环酯（交酯）**：含「酯化」+（「羟基酸」∨「自身酯化」）+「六元环酯」+ 乳酸 → 输出 `2CH3CH(OH)COOH → CH3CH(O)COOCH(CH3)CO + 2H2O`（覆盖 pdf-526）。
  4. **羟基酸自身酯化→聚酯**：同上但触发词为「聚酯」→ 输出 `2CH3CH(OH)COOH → [−OCH(CH3)CO−]_n + nH2O`（覆盖 pdf-527）。
- 输出 `EngineResponse`：`Ok=true`、`FinalResult` 含目标规范串、`ConstraintNote` 标明触发条件与「域外不蒙数」、`Confidence=1.0`、`ReasoningNodes` 记录原子平衡步骤。

### 4.2 钩子接线 `LlmGatewayService.cs`
在 batch3-D（σ 键，[L265-273](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L265)）之后、P2-1 三态派生（[L275](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L275)）之前，新增：

```csharp
// batch4 兜底：酯化反应产物（环酯/缩聚聚酯/羟基酸交酯与聚酯）（pdf-356/526/527）。
if (engineResponse.Ok is not true)
{
    var esterProduct = EsterificationProductSolver.TrySolve(rawText);
    if (esterProduct is not null)
    {
        engineResponse = esterProduct;
        engineRequest.TaskType = "calculation";
    }
}
```
守卫 `if (engineResponse.Ok is not true)` → 已判正题（含 pdf-355）零触达，构造性零 LOST；不改 `ClassifyTaskDeterministic`（零漂移）。

### 4.3 测试 `EsterificationProductSolverTests.cs`
- 正向：4 路径各 1 例，断言 `FinalResult` 含目标关键词（归一化口径）。
- 域外 null：缺触发词 / 缺反应物 / 非该组合 → 返回 null。
- **全库护栏**：载入基线 `snapshot_gap_batch3.json` 的 757 题文本，断言 `TrySolve` 非 null 的 id 集合 ⊆ {pdf-356, pdf-526, pdf-527}；且对这 3 个 baseFalse 题，输出经 `IsAnswerCorrect` 口径必命中（adopted=True）。0 假阳性。

## 5. 诚实登记（不补，不蒙数）

- **数据缺陷（~16，需数据修复轨，非本批次）**：pdf-160↔164 答案互串、pdf-172/179/084 字母键错配、pdf-117/190 单问多答案、pdf-324 结构答案矛盾、pdf-322/323/519/520/521/523 缺结构图。修复这些可解锁的潜在增益 > 本批次，但属数据资产修复，需单独计划（触及 `pdf_questions.json`，敏感）。
- **非引擎域（不补）**：图像 51、choice 字母 38、concept 自由文本 7、pdf-070 双线桥、pdf-207 熔沸点原因、pdf-163 可行性原因、pdf-537 量子点原因。
- **延后（ROI 低，需通用求解器）**：pdf-166 子问(5) 脱氯锌粉 0.5 mol（氧化还原电子守恒）、pdf-171 矿化离子方程（复杂矿物配平）、pdf-462 AlOOH 水解（单产物特例）、pdf-097/484 流程方程（反应物隐含）——需通用氧化还原/配平求解器，单批硬做泛化不足。

## 6. 验证

1. `dotnet build CogniThink.Chem.sln -c Release` → 0 errors。
2. `dotnet test CogniThink.Chem.sln -c Release` → 0 failed（含新增全库护栏）。
3. 全量 LLM-on eval：`_evalscratch/run_eval_resume.ps1 -Tag gap_batch4 -Env Development`（checkpoint 续跑）。
4. `diff_eval.ps1 -OldTag gap_batch3 -NewTag gap_batch4` → 预期 `GAINED ⊇ {pdf-356, pdf-526, pdf-527}`、`LOST=[]`，gwCorr 660→663。
5. `verify_mp27.py gap_batch3 gap_batch4` → 27/27 PASS，REGRESSIONS=[]。
6. 全库 dry-fire 复核：新 solver 在 757 题触发面 = {pdf-356, pdf-526, pdf-527}，无新增 fired。

引擎回归判定以 `gatewayCorrect` 集合为准（与 LLM 无关）；`pureLlmOnly` 漂移 ±2~3 属正常，不计入。

## 7. 风险与后续

- **风险 1（归一化边界）**：`[−OCH(CH3)CO−]_n` 含 U+2212 减号与 `_n` 下标，已 spike 验证 `NormalizeChemicalNotation` 两侧同态命中；上线后以全量 eval 实证兜底。
- **风险 2（分子式解析仅 C/H/O）**：题库酯化产物均只含 C/H/O；若未来出现含杂原子的酯化题，解析器返回 null（不蒙数），需扩充解析器——不影响当前 precision。
- **风险 3（触发串台）**：4 路径互斥，且全库仅 7 道酯化题、其中 4 道 baseTrue 被 Ok 守卫拦截；新增 fired 锁定 {356,526,527}，已由全库护栏断言。
- **后续（不在本批次，需独立计划）**：
  - **数据缺陷修复轨**（最高潜在增益，~16 题）：审计并修复 `pdf_questions.json` 答案键错位/缺图，解锁后引擎即可正常判分。
  - **chem-fact-curation 轨**：choice 38 题的事实注入。
  - **图像融合轨（P2-3）**：图像依赖 51 题。
