# Tasks — P2-1 统一推理 DAG 与结论三态契约

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/unified-reasoning-dag/spec.md)
> 基线：`after_p15b`（757 题，635 正确，unknown=27，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：预期 eval 正确性增益 = 0（结构卫生，见 spec §9）；目标 = 统一契约落地 + 示范可溯源 + 信息不足显式化，零回归。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：两栈契约分叉证据 + 消费者/静默 null 点 + eval 字段边界 | completed（spec 阶段已实证） | 证据入 spec §2 |
| T1 | 定义 `ReasoningNode` / `ConclusionState` / `EvidenceLevel` 类型 + `EngineResponse` 加性字段 | completed | `ReasoningContracts.cs` + `GatewayContracts.cs` 加性字段；build 0 error |
| T2 | `ConclusionState` 统一派生赋值点（只读现有信号，不改采纳门）+ 单测映射 | completed | `ConclusionStateDeriver` + ProcessStudentMessage 调用点 + OQ-5 诊断字段贯通 QaCaseResult/EvalRunner；映射单测 10 passed |
| T3 | 4 个示范 solver 填充 `ReasoningNodes`（ConceptFact / ReactionKb / 原子结构 / VSEPR）：命中节点带 knowledge_id/rule_id、有序无跳步、末节点 Conclusion=FinalResult | completed | 4 solver 命中路径填充 + `ReasoningSolverTraceabilityTests` 5 passed（含 R4 无命中不臆造守卫） |
| T4 | 全量 eval + 红线 diff：相对 after_p15b GAINED/LOST/WORSEN/harm 全 0；correct=635；adopted∧¬correct=0 | completed | `after_p21` checkpoint 757/757 failed=0；diff after_p15b→after_p21 RED-LINE PASS（全 0）；diff base_p0→after_p21 RED-LINE PASS（LOST/WORSEN/harm=0，GAINED=6 携带）；p21_verify：correct=635、adopted∧¬correct=0、派生 0 不一致、Confirmed=641/Insufficient=116/CandidateSet=0 |
| T5 | 台账闭环：登记 P2-1 触及范围 + 待迁移 solver 清单（非目标） | completed | `data-defect-register.md` 新增 §N（N.1 触及文件清单 / N.2 待迁移 13 生产者 / N.3 映射对照表） |
| T6 | 红线自检：密钥扫描 / 未改 JSON 数据 / 未改评估判分口径 / 未改采纳门 / 不蒙数 | completed | [p21_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p21_self_check.py) 11/11 PASS |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/unified-reasoning-dag/review.md) — PASS（9/9 维度，0 Issue） |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §2（Core 三态/可溯源现状、网关平坦字符串现状、消费者与静默 null 点、eval 字段边界、Api→Core 引用）。

## T1 — 定义统一契约类型 + 加性字段
**Status**: completed
**Outputs**:
- 新建 [ReasoningContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs)：`ReasoningNode`（Step/Description/Premises/RuleId?/KnowledgeId?/Conclusion?/EvidenceLevel）、`ConclusionState` 枚举（Confirmed/CandidateSet/Insufficient）、`EvidenceLevel` 枚举（KnowledgeFact/EngineDerived/Candidate/Trend）。
- 编辑 [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) `EngineResponse`：**加性**新增 `ReasoningNodes: List<ReasoningNode>`（默认空）+ `ConclusionState: ConclusionState`（默认 Insufficient 或 Confirmed，见 T2 派生）。不删改既有字段。
**Verification**: `dotnet build` 0 error；JSON 既有键不变（AC-2）。
**Notes**: 落点按 spec CQ-1 选项 A（保留 `DerivationSteps` 不动）。若用户批准时改选 B/C，本任务重定落点。

## T2 — ConclusionState 统一派生
**Status**: completed
**Outputs**: 一处派生赋值（在 `CallEngineAsync` 归一点或 `EngineResponse` 构造后），只读 `Ok` / `NeedMoreCondition` / `FinalResult`：
- `Ok==true` 且 `FinalResult` 非空 → `Confirmed`
- `Ok==null` 或 `NeedMoreCondition==true` → `Insufficient`
- 求解器自报候选 → `CandidateSet`（当前无生产路径，预留）
- `Ok==false` → `Confirmed`（spec CQ-3，待批准确认）
**Verification**: 单测覆盖全部映射分支；断言派生不写回 `Ok`/`FinalResult`/`TaskType`（采纳门输入不变）。
**Notes**: 严禁修改 [BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L660) 逻辑。

## T3 — 示范 solver 填充 ReasoningNodes
**Status**: completed
**Outputs**: 在 4 个有真值源/knowledge_id 的 solver 命中路径填充 `ReasoningNodes`：
1. ConceptFactSolver（事实库唯一匹配 → KnowledgeId 非空，EvidenceLevel=KnowledgeFact）
2. ReactionKbWriterSolver（KB 唯一匹配 → RuleId/KnowledgeId，EvidenceLevel=KnowledgeFact）
3. AtomicStructureSolver（PeriodicTable 查表 → KnowledgeId，EvidenceLevel=EngineDerived）
4. VseprSolver（查表 → KnowledgeId，EvidenceLevel=EngineDerived）
要求：节点 `Order` 从 1 连续无跳步；命中节点 `KnowledgeId`/`RuleId` 至少一个非空；末节点 `Conclusion` == `FinalResult`；`Premises` 填题干锚点或上步结论。**无命中路径不填**（留空，避免假可溯源，spec R4）。
**Verification**: 单测：每个示范 solver 取 ≥1 条已判正题（pdf-id 入参），断言 `ReasoningNodes` 非空 + 可溯源 + 无跳步 + 末节点结论一致。
**Notes**: 其余 14 个 solver 保持 `DerivationSteps` 字符串、`ReasoningNodes` 留空，登记入台账「待迁移」（T5），不强行填充。

## T4 — eval + 红线 diff
**Status**: completed
**Outputs**:
- `run_eval_resume.ps1 -Tag after_p21 -Env Production` → 757/757 failed=0。
- `diff_ckpt after_p15b after_p21`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS。
- `diff_ckpt base_p0 after_p21`：LOST=0 / WORSEN=0 / harm=0（GAINED 维持 P1-4 携带的 6）。
- 诊断断言：`adopted∧¬correct=0`（AC-8）；`ConclusionState` 分布可统计（Insufficient 占比与原 Ok=null 一致，证明派生无遗漏）。
**Verification**: 上述全过；correct=635 不变（诚实增益 0）。
**Notes**: 若 LOST≠0 → 停下根因（必是 T2/T3 意外改了 Ok/FinalResult/采纳门），修后复跑，不得带差异收口。

## T5 — 台账闭环
**Status**: completed
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §N（P2-1 契约统一）：
- N.1 本 spec 触及文件清单（content attestation；自 P0 未提交故非 git diff）：新增 1（ReasoningContracts.cs）+ 加性修改 8 + 测试 2；未触及 data/ JSON / BuildGatewayAnswerAsync / 判分逻辑。
- N.2 待迁移 solver 清单：grep `DerivationSteps` 全量取证，扣除 4 示范 solver，得 **13 个生产写入点**（11 个独立文件 + LlmGatewayService 内联 ~40 处 + MultiPartDispatch）保持 `DerivationSteps` 字符串、`ReasoningNodes` 留空；附迁移优先级建议（供 P2-1.x / P2-2）。另 5 个 solver 不直写 DerivationSteps，随 LlmGatewayService 迁移覆盖。
- N.3 `ConclusionState` 与原 `Ok`/`NeedMoreCondition` 映射对照表（可审计）：Confirmed 641（Ok:true 615 + Ok:false 26）/ CandidateSet 0 / Insufficient 116，合计 757。

## T6 — 红线自检
**Status**: completed
**Verification**（脚本化，结果入 review 证据）：[p21_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p21_self_check.py) **11/11 PASS**：
- 密钥扫描：P2-1 触及的 11 个 .cs 文件 grep `sk-|api_key|Bearer|secret` → **0 命中**。
- 未改数据：触及文件均不在 `data/` 下 → 无 JSON 数据改动。
- 未改评估判分 / 采纳门不变：`ConclusionState` 在 `GatewayVsLlmEvaluationService.cs` 仅出现 2 次（L83 字段定义 + L200 `EvaluateCaseAsync` 赋值），均在 `BuildGatewayAnswerAsync` 定义（L664）**之前** → 方法体内无引用；checkpoint 逐 case 比对 `after_p15b` vs `after_p21`：`gatewayAnswer` / `gatewayOk` / `gatewayConclusion` 三者 diff 均 = **0**（采纳门输入输出完全不变）。
- 不蒙数：`after_p21` 全局 `adopted∧¬correct` = **0**（凡采纳必判正；ReasoningNodes 仅在命中路径填充，命中即经 IsAnswerCorrect 兜底）。
- 表面积 / 契约加性：`EngineResponse` 同时保留 `DerivationSteps`（旧）+ `ReasoningNodes` / `ConclusionState`（新，默认值不破坏 wire）。
- build：`dotnet build -c Release` → **0 error / 0 warning**（本轮复跑）。

## T7 — 独立 review
**Status**: completed
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：build / test / eval diff（两次）/ 采纳门逐 case 不变断言 / 可溯源抽样 / 密钥扫描 / 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 未改数据与评估判分 / 契约加性不破坏 wire / 示范链可溯源。子代理仅返回报告文本，由主代理落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/unified-reasoning-dag/review.md)。
**Result**: **PASS**（9/9 维度，0 Issue，无修复项）。独立复跑：build 0 error/0 warning；全量测试 2206 passed / 0 failed（含 P2-1 两个测试类 15 passed）；两次 diff 均 RED-LINE PASS；p21_verify PASS；p21_self_check 11/11 PASS；代码核对确认采纳门未改（ConclusionState 仅 L83/L200，均在 BuildGatewayAnswerAsync L664 之前）、4 示范链可溯源、台账 §N 与代码一致。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 契约落点与迁移范围 | A 增量示范（推荐）/ B 复用 Core 全量替换 / C 新建中立全量 | **A（已批准）** | 见 spec §4 CQ-1。表面积可控、零回归、立即可供 P2-2/P2-4 消费。 |
| CQ-2 | 是否改采纳门 | 不动（定死） | **不动** | 改则破红线；ConclusionState 为观测元数据。 |
| CQ-3 | Ok=false → 三态映射 | → Confirmed（推荐）/ 单独状态 | **→ Confirmed（已批准）** | 引擎确定判错仍属「给出确定结论」。 |
| CQ-4 | 候选集判别实验建议 | 契约占位不实现（推荐）/ 实现 | **契约占位不实现（已批准）** | 当前网关无候选生产路径，实现需接通 solver，属范围外。 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理（独立复跑 build/test/两次 diff/p21_verify/p21_self_check + 代码核对 + 台账核对） | **PASS**（9/9 维度，0 Issue） | 无 |

## Tier 2+ — ReasoningNodes 生产者迁移（P2-1.x）审计与推进

> 权威清单与逐项证据见 [data-defect-register.md §N.2](file:///d:/github/CogniThink-Chem/data-defect-register.md)（13 个生产者，带核实行号）。

**Status**: B 类审计完成（2026-11-22）；Tier 2 快赢 `HydrolysisPhCalculator` 已迁移（`checkpoint_after_t2_ph` diff 全零、pdf-517 推理节点 0→1、无臆造无回归）；**B-外置批已完成**（KnownEquationWriter / ProductWriter / RedoxGateway / ConceptVerdict，`checkpoint_after_b_ext` diff 全零、恰 13 例推理节点 0→1、redox-1/2/3 保持 0、无臆造无回归）；**B-judge-API 批已完成**（MultiSubCorrectnessSolver / JudgmentStatementAggregator，裁决契约加性暴露命中 id + `CorrectnessContextRules` 外置，`checkpoint_after_b_ext`→`after_b_judge` diff 全零、恰 11 例推理节点 0→>0、3 例 R4 留空、14 例 gatewayConclusion 字节不变、无臆造无回归）。**B-Impurity 批已完成**（ImpurityTrapSolver 化学事实外置为 `data/knowledge/impurity-traps.json`，id=`impurity-kmno4-ethylene-co2`，命中线程化 `KnowledgeId`，`checkpoint_after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变、无臆造无回归）。下一批：MultiPartDispatch 传播。

**分级（已逐项核实，不盲信登记标签）**：
- 已迁移 A·done = 10（Tier 1：CrystalCellValidator / HalfReactionBalancer，`checkpoint_after_t1_dag` diff 全零；Tier 2：`HydrolysisPhCalculator`，`checkpoint_after_t2_ph` diff 全零、pdf-517 推理节点 0→1；B-外置批：KnownEquationWriter / ProductWriter / RedoxGateway / ConceptVerdict，`checkpoint_after_b_ext` diff 全零、恰 13 例推理节点 0→1、R1 保持 0；B-judge-API 批：MultiSubCorrectnessSolver / JudgmentStatementAggregator，`checkpoint_after_b_ext`→`after_b_judge` diff 全零、恰 11 例推理节点 0→>0、3 例 R4 留空；B-Impurity 批：ImpurityTrapSolver，`checkpoint_after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变）。
- A 可立即迁移 = 0（`HydrolysisPhCalculator` 本轮已从 B 重判并迁移完成）。
- B = 1：传播 1（MultiPartDispatch——丢弃子响应本体；ImpurityTrap 已在 B-Impurity 批迁移）。
- 混合 = 1：`LlmGatewayService`（实测 **66 处**，非登记的 ~40），含传播/计算/外置三类，表面积最大，须最后迁移。
- C 留空 = 1：`RedoxRatioCalculator`（无 JSON 锚点，挂 id 即假可溯源，违反 R4，诚实留空）。

**更正的登记错误（4+2 处）**：Redox「氧还规则有 rule id」、ProductWriter「可挂 RuleId」、KnownEquation「已知方程模板有 id」——三者均为 C# 硬编码、无 id；LlmGatewayService「~40 处」实为 66 处。另 ImpurityTrap「无知识源」不准、HydrolysisPh「中」低估。

**建议执行顺序**（每步须 `diff_ckpt` 全零 + 命中发节点/无锚留空自检）：
1. ✅ A 重判快赢：`HydrolysisPhCalculator` 已迁移（Tier 2，线程化 `kaKey`，`checkpoint_after_t2_ph` diff 全零、无臆造无回归）。
2. ✅ 已完成：B-外置批（KnownEquationWriter → ProductWriter → RedoxGateway → ConceptVerdict，`checkpoint_after_b_ext` diff 全零、恰 13 例推理节点 0→1、R1 保持 0、无臆造无回归）。
3. ✅ 已完成：B-judge-API 批（MultiSubCorrectness / JudgmentStatementAggregator：裁决契约加性暴露命中 id + `CorrectnessContextRules` 外置，`after_b_ext`→`after_b_judge` diff 全零、恰 11 例 0→>0、3 例 R4 留空、无臆造无回归）。
4. ✅ 已完成：B-Impurity 批（ImpurityTrapSolver 外置为 `data/knowledge/impurity-traps.json`，`after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变、无臆造无回归）。
5. 剩余：`MultiPartDispatch` 传播（B）+ `LlmGatewayService` 传播点 + 逐点子分类计算点。
