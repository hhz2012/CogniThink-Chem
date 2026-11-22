# P2-3 图像与文本求解融合（交叉验证显式化 + 可观测 + 回归保护）

> **状态**：**已批准**（2026-11-22，用户确认 CQ-1=A / CQ-2=a / CQ-3=a / CQ-4 零数据改动；批准后开始实现）。
> **基线**：`after_p22`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> **路线图定义**：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) L351-354 — 当前图像资产（curve/graph/molecule/crystal）在网关是独立分支，与文本事实链是「或」关系（命中即返回）。需评估：哪些题应「图像读数 + 化学事实交叉验证」后才采纳，避免中间量误采纳（spec 已提及 pdf-272 曲线中间量问题）。验收 = 依赖图题采纳结论均经化学真值交叉核对；中间量不被误采纳。依赖 P1-2（多部分大题常含图）。
> **红线（不可破）**：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> **诚实口径（先声明）**：审计发现路线图点名的「pdf-272 曲线中间量误采纳」**已在阶段3F（AC-22）修复**，当前 `harm=0`、`adopted∧¬correct=0`。故本 spec **预期 eval 正确性增益 = 0**（与 P2-1/P2-2 同为结构/卫生项）。价值在于：把当前**隐式**的「确定性化学求解器优先于图像原始读数」分层**显式化**、加**可观测诊断**（采纳结论来源 provenance）、加**回归保护**，并为 P2-4 前端分层讲解提供「结论来自图像读数还是化学事实交叉验证」的置信标注。不强行改采纳判决、不 LOST 任何已判正题。

## 1. 背景与问题

路线图 P2 目标是让求解「可追溯、可解释」。P2-1 统一了推理 DAG 与结论三态，P2-2 加了趋势级机理解释链。P2-3 关注**图像资产与文本事实的融合**：当前 curve/graph/molecule/crystal 资产在网关是独立分支，与文本事实链在评估侧是 **`GatewayCorrect = 文本判分 OR 结构化命中`** 的「或」关系（[GatewayVsLlmEvaluationService.cs L220](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L220)）。

风险点（路线图已点名）：曲线/图像**原始读数常是中间量**（某点浓度、某坐标值），并非题目最终答案；若它恰好与答案键匹配，会被当作正确结论采纳 → **假阳性 → harm**。历史上 pdf-259 / pdf-272 正是此 harm，已修复（见 §2.4）。本 spec 评估当前融合策略是否充分，并把隐式防护显式化、可观测化、回归化，防止回退。

## 2. 现状审计（只读证据，精确到文件/行号；基线 after_p22）

### 2.1 图像资产覆盖面（129/757 题）

探针 [p23_audit_probe2.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_audit_probe2.py) 读 `checkpoint_after_p22.jsonl` 的 `case.imageAssets`：

| 资产类型 | 题数 | correct | adopted | structuredHit | Insufficient | Confirmed-未采纳 |
|----------|------|---------|---------|---------------|--------------|------------------|
| graph | 47 | 31 | 31 | 0 | 16 | 0 |
| curve | 40 | 28 | 28 | 0 | 11 | 1（pdf-272） |
| molecule | 41 | 34 | 34 | 2 | 5 | 2（pdf-391, pdf-522） |
| crystal | 6 | 6 | 6 | 0 | 0 | 0 |

> 注：`structuredHit=true` 全库仅 2 题（均 molecule，均 correct）；curve/graph 的结构化命中走关键词路径或被门控拦截，故 recorded `structuredAssetKind` 为 null（见 [L219](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L219)：仅 hit 时记录 kind）。

### 2.2 引擎层：隐式交叉验证已存在（分层优先）

[CallEngineAsync L379-452](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L379-L452) 的路由顺序是**确定性化学求解器优先于图像原始读数**：

1. `equilibrium` 求解器抢占曲线（L386-390）：命中（Ok=true）直接返回，未命中回退曲线 → 防止曲线中间量抢先。
2. `choice` → `CurveChoiceValidator.TryDecide`（L394-409）：用曲线点集真值逐选项判定，返回选择字母。
3. `choice` → `ConceptChoiceValidator.TryDecide`（L416-431）：Concept 事实判定优先于原始读数（注释点名 pdf-077 反例）。
4. `TryRouteGraph` / `TryRouteCurve` / `TryRouteMolecule`（L435-442）：图像原始读数。
5. `CrystalCellValidator.TrySolve`（L448-452）→ 标记 `concept_fact` 走答案键校验门控。

→ **这就是事实上的交叉验证**：化学真值（平衡/概念/晶胞）先于图像原始读数产出。但它是**隐式**的（仅代码注释），无可观测信号，未来改动易破坏。

### 2.3 评估层：OR 关系 + 答案键校验保证 precision=1

- [L220](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L220)：`GatewayCorrect = IsAnswerCorrect(gatewayAnswer, c) || result.StructuredHit`。
- [L222](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L222)：`GatewayTruthAdopted = !string.IsNullOrEmpty(GatewayAnswer) || StructuredHit`。
- `IsStructuredCorrect`（[L417-431](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L417-L431)）：仅 `molecule`/`curve` 且非 `choice` 才比；`graph` 返回 false（关键词路径覆盖）；`choice` 返回 false。
- precision=1 构造保证：两条路径都要求与答案键匹配（`IsAnswerCorrect` / `MatchMolecule` / `MatchCurveNumeric`）。当前 `adopted∧¬correct=0`（探针 [p23_audit_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_audit_probe.py) 实证）。

**残余 latent 风险**：`curve` 原始读数若**数值巧合**等于答案键数值，`MatchCurveNumeric` 会判命中 → 假阳性 → harm。当前 0 观测，但无结构性防护（仅靠 `IsAnswerCorrect` 关键词拦截，数值型答案可能漏拦）。

### 2.4 已知 harm 已修复（pdf-259 / pdf-272，阶段3F / AC-22）

[HarmFixEquilibriumCurveTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/HarmFixEquilibriumCurveTests.cs) 两层修复：
1. **门控收紧**：`BuildGatewayAnswerAsync` 的 equilibrium 分支只采纳 `IsAnswerCorrect` 通过的结论（曲线中间量读数被拦截 → 不采纳 → 无 harm）。
2. **引擎抢占**：`CallEngineAsync` 中 equilibrium 确定性求解器优先于曲线路由（命中即返回，未命中回退曲线）。

当前 pdf-272 状态（探针实证）：`gatewayConclusion="x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8"`，`gatewayConclusionState=Confirmed`，但 `gatewayTruthAdopted=false`、`gatewayCorrect=false` → **曲线读数产出但被正确拦截，不采纳、不蒙数**。`harm=0`。

### 2.5 召回缺口（非 harm，不在本 spec 默认范围）

pdf-391 / pdf-522：molecule 特征产出（`Confirmed`：分子式/不饱和度/杂化）但未采纳——因 `taskType=unknown` 触发 `BuildGatewayAnswerAsync` 顶部 guard（[L673](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L673)：`concept`/`unknown` → return null）。这是**召回缺口**（假阴性），非 precision/harm 问题。是否在本 spec 顺手修复见 CQ-3（需路由漂移预校验，沿用 P1-4 模式）。

### 2.6 红线不受影响的依据

只要不改动 `FinalResult` / `Ok` / `TaskType` / `AssetKind` / `NumericValue*` 的赋值，不改 `BuildGatewayAnswerAsync` / `IsStructuredCorrect` 的判决逻辑，**仅追加 provenance 诊断字段 + 文档 + 回归测试**，对 eval 正确性结果零影响（checkpoint 不读新字段判分）。LOST/WORSEN/harm 可证保持 0。

## 3. 目标契约定义

### 3.1 显式融合策略（文档化，不改行为）

把 §2.2 的隐式分层写成**显式可审计策略**（本 spec §3 即文档源；代码注释同步）：

| 优先级 | 产出者 | provenance 标签 | 说明 |
|--------|--------|-----------------|------|
| 1（最高） | `CallEquilibrium` | `equilibrium` | 确定性平衡求解，先于曲线读数 |
| 2 | `CurveChoiceValidator` | `curve_choice` | 曲线点集逐选项判定（choice） |
| 3 | `ConceptChoiceValidator` | `concept_choice` | Concept 事实判定（choice），先于原始读数 |
| 4 | `TryRouteGraph` | `graph` | 图形结构化 |
| 5 | `TryRouteCurve` | `curve_reading` | **曲线原始读数（中间量高风险类）** |
| 6 | `TryRouteMolecule` | `molecule` | 分子结构化特征 |
| 7 | `CrystalCellValidator` | `crystal` | 晶胞确定性求解（标 concept_fact） |
| 8（兜底） | 通用文本事实瀑布 | `text_fact` / null | 既有 solver（原子结构/VSEPR/计算/概念等） |

### 3.2 可观测诊断：采纳结论 provenance（加性，不计分）

- `EngineResponse` 加性新增 `ResultProvenance: string?`（JSON key `result_provenance`，默认 null），由各路由分支在产出 `FinalResult` 时赋值（仅设标签，不改其它字段）。
- 评估侧 `QaCaseResult` 加性新增 `GatewayResultProvenance`（checkpoint jsonl key `gatewayResultProvenance`）+ `MapQaCase` 快照 `resultProvenance`；仅观测，**不参与 `GatewayCorrect` / `GatewayTruthAdopted` 计算**（沿用 OQ-5 模式：诊断字段在采纳门之前赋值，采纳门不读）。
- 价值：可统计「被采纳的结论中有多少来自 `curve_reading`（高风险类）」，一旦未来出现 curve_reading 被采纳且判正，立即可见 → 防回退。当前预期 curve_reading 被采纳数 = 0（因门控拦截）。

### 3.3 回归保护（测试，不改行为）

- 扩展回归测试：断言 **`curve_reading` provenance 的结论永不被采纳**（凡 `ResultProvenance=="curve_reading"` → `BuildGatewayAnswerAsync` 返回 null，除非经 equilibrium/concept 交叉验证改写 provenance）。用 pdf-272 / pdf-259 真题干 + 合成中间量读数断言。
- 断言既有正确不回归：2 个 molecule structuredHit 题 + 6 个 crystal 题 + curve choice 题（t4cur-01..06）保持 correct=true。
- 断言 `EngineResponse` 契约加性：旧字段全保留 + 新增 `ResultProvenance`。

## 4. 关键决策点（CQ，需用户确认）

| ID | 问题 | 选项 | 推荐 | 理由 |
|----|------|------|------|------|
| CQ-1 | P2-3 行为范围 | A 仅显式化+诊断+回归保护（**零行为变更**，零风险，预期增益 0）/ B 引入显式交叉验证门：curve_reading 须经化学事实 corroborate 才采纳（改判决，有 LOST 风险） | **A** | 审计证实 harm 已修复、当前全绿；B 改采纳判决有 LOST 风险且无已知收益，违反「不强行改判决」红线。先以 A 把现状钉死、可观测，B 留待未来有 latent harm 实证时再立 spec。 |
| CQ-2 | provenance 字段落点 | a 加到 `EngineResponse`（运行时可观测，P2-4 前端可直接用）/ b 仅 checkpoint 评估侧统计（更小表面积，但前端拿不到） | **a** | 与 P2-1/P2-2 加性字段同构（ReasoningNodes/ExplanationNodes 都在 EngineResponse）；P2-4 前端分层讲解需要 provenance 标注置信；运行时字段不改判决 → 零风险。 |
| CQ-3 | 是否顺手修 molecule 召回缺口（pdf-391/522） | a 不碰（本 spec 聚焦卫生，召回另立 spec）/ b 修（需路由漂移预校验，可能 GAINED 2 但有漂移风险） | **a** | 召回改动涉及 `ClassifyTaskDeterministic` 路由（unknown→molecule），全库 757 预校验成本高且风险独立；P2-3 红线是 zero-regression，不宜夹带。若要做，单独 spec + 独立 review 门禁（沿用 P1-4 模式）。 |
| CQ-4 | 数据 | 复用现有 JSON / 题库，零数据改动 | 零数据改动 | P2-3 不新增化学事实，只加诊断标签与文档；`data/**` 不动。 |

## 5. 验收标准（AC）

1. **零行为变更**：diff `after_p22`→`after_p23` GAINED=0 / LOST=0 / WORSEN=0 / harm=0；correct=635 不变。
2. **不蒙数**：`after_p23` 全局 `adopted∧¬correct=0`；无 `curve_reading` provenance 被采纳（adopted=true）的题。
3. **可观测**：checkpoint 含 `gatewayResultProvenance`；统计各 provenance 标签的 adopted/correct 分布，`curve_reading` 采纳数 = 0。
4. **契约加性**：`EngineResponse` 保留全部旧字段 + 新增 `ResultProvenance`；checkpoint 旧键不变（wire 兼容）。
5. **回归保护**：新测试断言 curve 中间量读数不被采纳（pdf-272/259 + 合成）；既有 2 molecule hit + 6 crystal + curve choice 不回归。
6. **未改采纳门**：`BuildGatewayAnswerAsync` / `IsStructuredCorrect` 判决逻辑未变；`ResultProvenance` 在采纳门内 0 引用。
7. **未改数据**：`data/**` 零改动；P2-3 触及文件全为 .cs（+ 测试 + 台账 + scratch）。
8. **build/test 绿**：全量 build 0 error；全量 test 0 failed。

## 6. 非目标（Out of Scope）

- 不改 `BuildGatewayAnswerAsync` / `IsStructuredCorrect` 的判决逻辑（CQ-1=A）。
- 不追 molecule/graph 召回缺口（CQ-3=a）；召回另立 spec。
- 不新增 JSON 化学事实（CQ-4）。
- 不接前端（P2-4）；provenance 字段预留供 P2-4 消费。
- 不改 LLM 解析 / taskType 路由。

## 7. 风险与取舍

- **最大风险是「为融合而融合」改坏已绿的采纳门**：CQ-1 定死 A（零行为变更），所有改动加性、可证不影响 verdict。
- **provenance 标签赋值遗漏**：若某路由分支忘设 `ResultProvenance`，则该题 provenance=null（不影响判分，仅诊断缺失）；T6 自检会扫描所有 `FinalResult` 赋值点是否同步设 provenance。
- **召回诱惑**：pdf-391/522 的 GAINED 诱惑要抵制——夹带路由改动会破 zero-regression 承诺，CQ-3=a 排除。
