# Tasks — P2-3 图像与文本求解融合（交叉验证显式化 + 可观测 + 回归保护）

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/image-text-fusion/spec.md)
> 基线：`after_p22`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：审计证实 pdf-272 曲线中间量误采纳已在阶段3F 修复，当前 harm=0；本 spec **预期 eval 正确性增益 = 0**（结构/卫生项），零行为变更。
> 批准门禁：CQ-1=A / CQ-2=a / CQ-3=a / CQ-4 零数据改动 经用户确认后，才开始 T1 实现代码。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：图像资产覆盖面 / 引擎分层路由 / 评估 OR 关系 / pdf-272 修复现状 / provenance 缺口 | completed（spec 阶段已实证） | 证据入 spec §2 |
| T1 | `EngineResponse` 加性 `ResultProvenance` 字段 + 各路由分支赋值标签（不改 FinalResult/Ok/TaskType/判决） | completed | 加性字段 + 7 分支赋值；build 0 error |
| T2 | 评估侧诊断：`QaCaseResult` 加 `GatewayResultProvenance` + 赋值（采纳门之前）+ `MapQaCase` 快照（不计分） | completed | checkpoint 新键，不影响判分 |
| T3 | 回归测试：curve 中间量读数不被采纳（pdf-272/259 + 合成）+ 既有正确不回归（2 molecule hit / 6 crystal / curve choice）+ 契约加性 | completed | [ImageTextFusionRegressionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ImageTextFusionRegressionTests.cs) 4/4 PASS；全量 2216 passed/0 failed |
| T4 | 全量 eval + 红线 diff：相对 after_p22 GAINED/LOST/WORSEN/harm 全 0；correct=635；不蒙数；provenance 分布诊断（curve_reading 采纳=0） | completed | `after_p23`（757/757 failed=0）；两次 diff RED-LINE PASS；[p23_diag.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_diag.py) PASS |
| T5 | 台账闭环：登记 P2-3 触及范围 + 显式融合策略 + provenance 分布 | completed | [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §P（P.1/P.2/P.3） |
| T6 | 红线自检：密钥扫描 / 未改 JSON 数据 / 未改采纳门判决 / provenance 在采纳门 0 引用 / checkpoint 逐 case 不变 / 不蒙数 / 契约加性 / 所有 FinalResult 赋值点同步设 provenance | completed | [p23_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_self_check.py) 23/23 PASS |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/image-text-fusion/review.md) |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §2：129/757 题带图像资产（graph47/curve40/molecule41/crystal6）；引擎 `CallEngineAsync` 已分层（确定性求解器优先于图像读数，隐式交叉验证）；评估侧 `GatewayCorrect = 文本 OR 结构化命中`（L220），precision=1 由答案键校验构造保证；pdf-259/272 harm 已在阶段3F 修复（`HarmFixEquilibriumCurveTests`），当前 harm=0、`adopted∧¬correct=0`；残余 latent 风险 = curve 原始读数数值巧合匹配答案键（0 观测）；召回缺口 pdf-391/522（taskType=unknown 未采纳）不在默认范围。

## T1 — EngineResponse 加性 ResultProvenance + 路由赋值
**Status**: completed — [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) `EngineResponse` 加性新增 `ResultProvenance: string?`（默认 null，JSON key `result_provenance`，旧字段全保留）；[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `CallEngineAsync` 7 个融合分支（equilibrium/curve_choice/concept_choice/graph/curve_reading/molecule/crystal）在产出结论时设标签——**仅设标签，不改 FinalResult/Ok/TaskType/AssetKind**。全量 build 0 error；7 警告全为既有。
**Outputs**:
- [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) `EngineResponse` **加性**新增 `ResultProvenance: string?`（默认 null；JSON key `result_provenance`）。不删改既有字段。
- [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `CallEngineAsync` 各产出分支（equilibrium / curve_choice / concept_choice / graph / curve_reading / molecule / crystal）在构造 `EngineResponse` 时设 `ResultProvenance` 标签（spec §3.1 表）。**仅设标签，不改 FinalResult/Ok/TaskType/AssetKind**。
- 通用文本瀑布（兜底）默认 null 或 `text_fact`（见实现时最小改动原则，不破坏既有分支）。
**Constraints**: 纯加性；不改任何判决字段；provenance 标签是字符串常量，不硬编码化学事实。
**Verification**: build 0 error；单测断言各路由设对 provenance 且 FinalResult/Ok/TaskType 不变。

## T2 — 评估侧诊断字段（不计分）
**Status**: completed — [GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) `QaCaseResult` 加性新增 `GatewayResultProvenance`，在 `BuildGatewayAnswerAsync` 调用**之前**赋值 `= gwResult.EngineResponse.ResultProvenance`（沿用 P2-2 OQ-5 模式）；[EvalRunner Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.EvalRunner/Program.cs) `MapQaCase` 快照加 `resultProvenance`。诊断字段**不参与** `GatewayCorrect`/`GatewayTruthAdopted` 计算，采纳门不读。
**Outputs**:
- [GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) `QaCaseResult` 加性新增 `GatewayResultProvenance`；在 `BuildGatewayAnswerAsync` 调用**之前**赋值 `= gwResult.EngineResponse.ResultProvenance`（沿用 `GatewayExplanationNodesCount` 模式，OQ-5）。
- [EvalRunner Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.EvalRunner/Program.cs) `MapQaCase` 快照加 `resultProvenance`。
**Constraints**: 诊断字段**不参与** `GatewayCorrect` / `GatewayTruthAdopted` 计算；采纳门不读。
**Verification**: build 0 error；checkpoint jsonl 新键 `gatewayResultProvenance` 存在且非空题数合理。

## T3 — 回归测试
**Status**: completed — 新建 [ImageTextFusionRegressionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ImageTextFusionRegressionTests.cs) 4 测试全绿：契约加性（ResultProvenance 默认 null 且不改其它字段）；pdf-259 equilibrium 标 `provenance=equilibrium` 且结论内容/可采纳性不变（纯加性）；pdf-272 回退曲线标 `provenance=curve_reading` 且不被答案键采纳（核心回归守卫）；合成中间量读数不被采纳。全量测试 2216 passed / 0 failed / 0 skipped。
**Outputs**: 新建测试（建议 `ImageTextFusionRegressionTests.cs`）：
- 断言 `curve_reading` provenance 的结论永不被采纳：用 pdf-272 / pdf-259 真题干 + 合成中间量读数（如 `"x=2.5 时 y=0.8"`），断言 `BuildGatewayAnswerAsync` 返回 null / `IsAnswerCorrect` false。
- 断言既有正确不回归：2 个 molecule structuredHit 题、6 个 crystal 题、curve choice 题（t4cur-01..06）保持 correct=true（可引用既有 case 生成器）。
- 断言契约加性：`EngineResponse` 保留旧字段 + `ResultProvenance` 默认 null。
**Verification**: 新测试全绿；全量 `dotnet test` 0 failed（期望 ~2212 + N）。

## T4 — eval + 红线 diff
**Status**: completed — `after_p23`（Production LLM off）757/757 completed、failed=0，correct=635 / precision=1 / harm=0。`diff_ckpt after_p22 after_p23`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS（零行为变更）。`diff_ckpt base_p0 after_p23`：GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0 → PASS。诊断 [p23_diag.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_diag.py)：`adopted∧¬correct=0`；checkpoint 含 `gatewayResultProvenance`；**核心红线 curve_reading 被采纳=0**；provenance 分布见 §P.2（concept_choice 250 / equilibrium 13 / crystal 6 / curve_choice 6 / graph 1 / molecule 4 / curve_reading 1[pdf-272, 未采纳] / null 476）。
**Outputs**:
- `run_eval_resume.ps1 -Tag after_p23 -Env Production` → 757/757 failed=0。
- `diff_ckpt after_p22 after_p23`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS。
- `diff_ckpt base_p0 after_p23`：LOST=0 / WORSEN=0 / harm=0（GAINED 维持 6 携带）。
- 诊断断言：`adopted∧¬correct=0`；`curve_reading` provenance 被采纳（adopted=true）题数 = 0；correct=635 不变；统计各 provenance 标签的 adopted/correct 分布。
**Verification**: 全过。若 LOST≠0 或 harm≠0 → 停下根因（必是 T1 意外改了判决字段），修后复跑。

## T5 — 台账闭环
**Status**: completed — [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §P（插入于 §O 与 `## 处理原则` 之间）：P.1 触及文件清单（5 个 .cs，content attestation，无 data/**）；P.2 显式融合策略表 + provenance 标签 + after_p23 观测分布；P.3 verdict-unchanged + 不蒙数 + curve_reading 采纳=0 实证。
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §P（P2-3）：
- P.1 触及文件清单（content attestation，无 data/**）。
- P.2 显式融合策略表（spec §3.1）+ provenance 标签定义。
- P.3 `after_p23` provenance 分布（各标签 adopted/correct 计数；curve_reading 采纳=0）+ verdict-unchanged 实证。

## T6 — 红线自检
**Status**: completed — [p23_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_self_check.py) 23/23 PASS：密钥扫描 0 命中；未改 data/ JSON；`Provenance` 在 Evaluation 服务仅 2 次（字段 L89 + 赋值 L209）且均在 `BuildGatewayAnswerAsync` 定义（L673）之前（采纳门内 0 引用）；checkpoint 逐 case `gatewayAnswer`/`gatewayOk`/`gatewayConclusion` 与 `after_p22` diff=0；`adopted∧¬correct=0`；curve_reading 采纳=0；correct=635；`EngineResponse` 契约加性（5 字段全在）；7 个 provenance 标签常量均在 LlmGatewayService 出现。
**Verification**（脚本化，结果入 review 证据）：
- 密钥扫描：P2-3 改动文件 grep → 0 命中。
- 未改数据：触及文件均不在 `data/` 下（CQ-4）。
- 未改采纳门判决：`BuildGatewayAnswerAsync` / `IsStructuredCorrect` 方法体未变；`ResultProvenance` 在采纳门内 0 引用。
- 解释不泄漏判分：checkpoint 逐 case `gatewayAnswer`/`gatewayOk`/`gatewayConclusion` 与 after_p22 diff=0。
- 不蒙数：`after_p23` 全局 `adopted∧¬correct=0`；`curve_reading` 采纳=0。
- 表面积：仅加性字段 + 路由标签 + 诊断 + 测试 + 台账 + scratch。
- 契约加性：`EngineResponse` 保留旧字段 + `ResultProvenance`。
- 赋值完整性：所有 `FinalResult` 赋值点（grep）同步设 `ResultProvenance` 或显式留空（兜底），无遗漏分支。

## T7 — 独立 review
**Status**: completed — fresh-context 只读子代理独立复跑 build（0 error，P2-3 触及文件 0 警告）/ test（2216 passed/0 failed，4 新测试全绿）/ 两次红线 diff（after_p22→after_p23 与 base_p0→after_p23 全 PASS，零行为变更，correct=635 不变）/ p23_self_check（23/23 PASS）/ 采纳门 ResultProvenance 0 引用核对 / provenance 分布核对 / curve_reading 采纳=0 断言核对 / 密钥扫描 / 台账 §P 自洽核对 / 策略表与代码一致核对 → 裁决 **PASS（7/7 检查，10 维度，0 Issue，无修复项）**。报告落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/image-text-fusion/review.md)。
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：build / test / eval diff（两次）/ 采纳门逐 case 不变断言 / provenance 分布核对 / curve_reading 采纳=0 断言 / 密钥扫描 / 台账核对 / 3.1 策略表与代码一致。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 未改数据与判决 / 契约加性。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/image-text-fusion/review.md)。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | P2-3 行为范围 | A 仅显式化+诊断+回归保护（零行为变更）/ B 引入交叉验证门改判决 | **A（已批准）** | 见 spec §4 CQ-1 |
| CQ-2 | provenance 字段落点 | a EngineResponse（运行时可观测）/ b 仅 checkpoint | **a（已批准）** | 见 spec §4 CQ-2 |
| CQ-3 | 是否顺手修 molecule 召回 | a 不碰 / b 修（带漂移预校验） | **a（已批准）** | 见 spec §4 CQ-3 |
| CQ-4 | 数据 | 零数据改动 | **已批准** | 见 spec §4 CQ-4 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理 | PASS（7/7，0 Issue） | 无修复项；见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/image-text-fusion/review.md) |
