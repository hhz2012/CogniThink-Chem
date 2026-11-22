# Tasks — P3-3 导学与求解融合（标准推理链锚定 + 诊断回溯 DAG）

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/tutor-solve-fusion/spec.md)
> 基线：`after_p32`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON / 不泄露未采纳结论。破线即回退。
> 诚实口径：预期 eval 正确性增益 = 0（导学流不在 757 eval 内；本 spec 不改网关求解/采纳门/判分，`diff after_p32→after_p33` 必为 0）。
> 批准门禁：CQ-1=a（加性锚定，已确认 2026-11-22）/ CQ-5=a（后端+前端最小展示，已确认 2026-11-22）/ CQ-2/3/4 默认 a / OQ 经用户确认后，才开始 T1 实现。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：导学流与网关求解两条链盘点 / 消费者映射 / DAG 覆盖范围 / eval 覆盖盲区 / 诊断与 DAG 无关联 | completed（spec 阶段已实证，证据入 spec §2） | spec §2 |
| T1 | 新建 `TutorSolveChainProvider`：调 `LlmGatewayService` 取 DAG，按 `ConclusionState` 门控（仅 Confirmed），`problemId` 级缓存，只读不改网关 | completed | 新文件 [TutorSolveChainProvider.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorSolveChainProvider.cs) + 11 单测全绿 |
| T2 | `SubmitStepResponse` 加性字段 `StandardReasoningChain` + `TutorService` 注入取链 + `TutorController` 透传（既有字段不变） | completed | Dtos.cs / TutorService.cs / TutorController.cs / Program.cs 改动（取链在 SaveChanges 后、纯加性） |
| T3 | 诊断回溯 DAG：`ReasoningCheckpoint` 加 `LinkedReasoningNode` + `DiagnosticTeachingEngine` 按 knowledge_id 精确匹配（排除 Trend，取首个）+ `BuildExplanation` 引用锚点 | completed | DiagnosticContracts.cs / DiagnosticTeachingEngine.cs / DiagnosticController.cs + 新 DiagnosticCheckpointLinker.cs / DiagnosticExplanationAnchor.cs + 10 单测全绿 |
| T4 | 前端 miniapp：`tutorApi.ts` 类型加 `standardReasoningChain` + 导学页折叠展示（复用 P2-4 DAG 组件/抽共享组件） | completed | tutorApi.ts + 新 components/ReasoningDag/ + pages/tutor/index.tsx；tsc 对 P3-3 文件 0 error（AC-7 退化口径） |
| T5 | 导学专用单测 + 快照回归：门控（AC-2）/ 匹配（AC-3）/ **既有字段逐 case 不变（AC-4 快照）** | completed | 新 TutorSnapshotRegressionTests.cs（4 场景）：provider=null/返回 null/返回真实链三者既有字段字节一致，仅 StandardReasoningChain 不同 |
| T6 | 全量 eval `after_p33` + 两次红线 diff（after_p32→after_p33 全 0；base_p0→after_p33 LOST/WORSEN/harm=0，GAINED 维持 6） | completed | after_p33：757/757 failed=0 correct=635 harm=0 precision=1；两次 diff 均 RED-LINE PASS |
| T7 | 红线自检：密钥 / 未改网关 solver/采纳门/eval 判分（grep attestation）/ 52 JSON 未变 / 产物不被运行时误引用 / eval diff 全 0 / 快照通过 / 不蒙数 | completed | [p33_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p33_self_check.py) 24/24 PASS |
| T8 | 台账闭环：新增 §T（触及范围 + 加性锚定策略 + 4-solver 覆盖限制 + eval 覆盖盲区声明），插于 §S 与 `## 处理原则` 之间 | completed | [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) §T 已插入 |
| T9 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/tutor-solve-fusion/review.md) 裁决 PASS（全维度、0 Issue） |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §2（15 条，精确到 file:line）。核心结论：导学流 [TutorService](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorService.cs) 全文不引用 `LlmGatewayService`（grep 0 命中），跑在 DB 手排 `Problem.Steps` + `TutorStateMachine`/`ChemistryReasoningEngine` 判分 + 知识图谱 `DiagnosticTeachingEngine` 诊断上；网关 [LlmGatewayService.ProcessStudentMessageAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L93) 产出 `ReasoningNodes` DAG（仅 4 个 solver 填充：ConceptFact / ReactionKb / AtomicStructure / Vsepr）。两链无共享推理链、诊断不回溯 DAG。757 eval 不触导学流（grep `*Eval*.cs` 0 命中）→ eval 覆盖盲区，须靠快照测试守护。

## T1 — TutorSolveChainProvider（取链 + 门控 + 缓存）
**Status**: completed — 新建 [TutorSolveChainProvider.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorSolveChainProvider.cs)：`GetChainAsync` 按 `ConclusionState` 门控（仅 Confirmed+非空下发，排除 Trend），进程内缓存（含缓存 null），异常降级 null 且不缓存，只读不改网关。`Program.cs` 注册 `ITutorSolveChainCache`（单例）+ `TutorSolveChainProvider`（Scoped）。单测 11/11 全绿（门控/缓存/降级/防泄露/纯只读）。
**Outputs**:
- 新建 `TutorSolveChainProvider`（落点 `src/CogniThink.TutorBrain.Api/Application/` 或 `Application/Tutor/`），注入 `LlmGatewayService`：
  - `GetChainAsync(string problemStem, CancellationToken) : Task<IReadOnlyList<ReasoningNode>?>`
  - 调 `ProcessStudentMessageAsync(problemStem)`（imagePaths/imageAssets/questionType 均 null，因 `Problem.Stem` 纯文本）；
  - 门控：仅 `engine_response.ConclusionState == Confirmed` 且 `ReasoningNodes` 非空时返回节点（过滤 `EvidenceLevel == Trend`）；否则 `null`；
  - **只读**：不修改 `GatewayResult` / `EngineResponse` 任何字段。
  - 缓存：`problemId`（或 problemStem 哈希）为 key 的内存缓存（单例，短 TTL 或进程内静态）；命中直接返回，不重算。取链异常/超时 → 返回 `null` 降级，不抛。
- 单测：Confirmed→非空且无 Trend；Insufficient/CandidateSet/空→null；缓存命中不重复调用（mock 计数）。
**Constraints**: 不改 `LlmGatewayService` / solver；不写回网关字段。
**Verification**: 单测全过；build 0 error。

## T2 — SubmitStepResponse 加性字段 + 取链接入
**Status**: completed — [Dtos.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Contracts/Dtos.cs) `SubmitStepResponse` 末位加可选 `StandardReasoningChain`；[TutorService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorService.cs) 构造可选注入 `TutorSolveChainProvider`，在 `SaveChangesAsync` 之后取链、纯加性挂到 `SubmitStepResult` 末位；[TutorController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/TutorController.cs) 透传。既有字段未删改，wire 向后兼容。
**Outputs**:
- `SubmitStepResponse`（Contracts）新增 `StandardReasoningChain: ReasoningNode[]?`（JSON `standard_reasoning_chain`），可选、缺省 null。
- `TutorService` 注入 `TutorSolveChainProvider`，在 `SubmitStepAsync` 取链（首次/缓存），加性挂到 `SubmitStepResult` → `TutorController` 透传到响应。
- 取链为**非阻塞旁路**：失败/null 时 `StandardReasoningChain=null`，不影响判分主路径。
- 既有字段逐字段不变。
**Verification**: 单测：取链成功→响应含链；取链 null→既有字段与无 provider 时一致（为 AC-4 快照铺路）。

## T3 — 诊断回溯 DAG 节点
**Status**: completed — [DiagnosticContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticContracts.cs) `ReasoningCheckpoint` 加可选 `LinkedReasoningNode`；新建纯函数 [DiagnosticCheckpointLinker.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticCheckpointLinker.cs)（按 `knowledge_id` 精确匹配取首个、排除 Trend、不改 chain 节点）+ [DiagnosticExplanationAnchor.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticExplanationAnchor.cs)（node null 时 no-op，既有讲解逐字不变）；[DiagnosticTeachingEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticTeachingEngine.cs) 仅 chain 非空时链接 + 锚点；[DiagnosticController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/DiagnosticController.cs) 透传。单测 10/10 全绿。
**Outputs**:
- `ReasoningCheckpoint`（[DiagnosticContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticContracts.cs)）新增 `LinkedReasoningNode: ReasoningNode?`（JSON `linked_reasoning_node`）。
- `DiagnosticTeachingEngine.BuildStructureAsync` 接受网关 DAG（由 provider 传入或参数注入），构建检查点时按 `checkpoint.KnowledgeId == node.KnowledgeId` 精确匹配，取首个非 Trend 节点填入 `LinkedReasoningNode`；无匹配→null。
- `BuildExplanation`：`LinkedReasoningNode` 非空时，讲解引用「标准推理链对应节点：依据 {premises} 推出 {conclusion}（{evidence_level}）」；为空时现有讲解逐字不变。
- `DiagnosticStartResult`/`DiagnosticAnswerResult` 透传 `Structure`（形状不变，仅检查点多字段）。
**Verification**: 单测：命中→LinkedReasoningNode 非空且来自 DAG、非 Trend；未命中→null；讲解在有/无链接时的差异符合预期。

## T4 — 前端最小展示
**Status**: completed — [tutorApi.ts](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts) `SubmitStepResponse` 加可选 `standardReasoningChain?`；新建 [ReasoningDag](file:///d:/github/CogniThink-Chem/miniapp/src/components/ReasoningDag/index.tsx) 组件（空不渲染）；[pages/tutor/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/tutor/index.tsx) 在反馈区渲染。`tsc --noEmit` 对 P3-3 触及文件 0 error（AC-7 退化口径：Taro dev:h5 未实跑截图，退化为类型编译通过 + 字段在响应中存在）。
**Outputs**:
- `miniapp/src/services/tutorApi.ts`：`SubmitStepResponse` 加 `standardReasoningChain?: ReasoningNodeDto[]`（复用 P2-4 类型定义，若未共享则抽 `types/reasoning.ts`）。
- 导学页：`standardReasoningChain` 非空时渲染折叠区「标准推理链（引擎推导）」，复用 P2-4 `pages/solve` 的 DAG 渲染（抽共享组件或就地最小渲染 step/description/premises/knowledge_id/conclusion/evidence_level）；空则不渲染。
- 不新建页面、不改导学既有流程、不破坏 weapp/tt 构建。
**Verification**: `npm run dev:h5` 可运行（或退化为类型编译通过）；Confirmed 题展示折叠区，Insufficient/空不展示。

## T5 — 导学专用单测 + 快照回归（关键）
**Status**: completed — 新建 [TutorSnapshotRegressionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/TutorSnapshotRegressionTests.cs)（AC-4，4 场景：正确非关键步 / 正确关键步带化学证据 / 三步通关带变式 / 答错带薄弱前置）。每场景在三份独立同种子库上分别跑 provider=null（等价 P3-3 前）/ provider 返回 Insufficient（DAG 空）/ provider 返回 Confirmed 真实链，把结果映射成与 TutorController 一致的 `SubmitStepResponse`，序列化剔除 `standardReasoningChain` 后断言三者字节全等；并断言前两者链为 null、第三者链等于注入节点。4/4 全绿，证明导学判分/话术零漂移。门控（AC-2，11 测）与匹配（AC-3，10 测）由 T1/T3 单测覆盖。
**Verification**:
- 门控单测（AC-2）：Confirmed→链非空无 Trend；Insufficient/CandidateSet→null。
- 匹配单测（AC-3）：命中/未命中行为。
- **快照回归（AC-4，红线性）**：选一组网关 DAG 为空/Insufficient 的导学题（含正确/错误/卡壳分支），跑 `submit-step`，断言 `SubmitStepResponse` 除新增 `StandardReasoningChain` 外的**全部既有字段逐 case 与 after_p32 基线一致**。基线 = 改动前对同题同输入的响应快照（T5 开始时先录 baseline，再实现，再比对）。任一漂移 → 停下根因。
- 快照脚本落 `_evalscratch/verify/p33_tutor_snapshot.py`（或 tests 项目内）。

## T6 — 全量 eval + 红线 diff
**Status**: completed — `run_eval_resume.ps1 -Tag after_p33 -Env Production`：757/757 failed=0，[summary](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_after_p33_summary.txt) correct=635 / harm=0 / precision=1 / total=757。`diff after_p32→after_p33`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS。`diff base_p0→after_p33`：GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS。
**Outputs**:
- `run_eval_resume.ps1 -Tag after_p33 -Env Production` → 757/757 failed=0，correct=635。
- `diff_ckpt after_p32 after_p33`：GAINED/LOST/WORSEN/harm 全 0 → RED-LINE PASS（P3-3 未碰网关求解/采纳门/判分，必为 0）。
- `diff_ckpt base_p0 after_p33`：LOST/WORSEN/harm=0（GAINED 维持 6 携带）。
**Verification**: 两次 diff RED-LINE PASS。若 diff 非 0 → 必是 T1/T2/T3 意外改了网关/eval 路径，立即回退该改动复跑。

## T7 — 红线自检
**Status**: completed — [p33_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p33_self_check.py) 24/24 PASS：密钥 0 命中 / 触及清单不含网关 solver·采纳门·eval 判分（grep attestation，Application/Gateway 目录 0 命中）/ 52 个 data/** JSON sha256 与 p31 基线全同 / 加性字段 wire 兼容（既有键全在、新键可选）/ eval diff 全 0 / 快照测试在位 / after_p33 correct=635 harm=0 total=757 不蒙数。
**Verification**（脚本化 `_evalscratch/verify/p33_self_check.py`）：
- 密钥扫描：P3-3 新增/改动文件 → 0 命中。
- 未改网关/eval 路径：触及文件清单不含 `LlmGatewayService.cs` 主体 / 任何 `*Solver.cs` / `BuildGatewayAnswerAsync` 所在文件 / `IsStructuredCorrect` / eval 判分文件（grep attestation）。
- 未改运行时数据：52 个 `data/**` JSON 逐文件 sha256 与 `after_p32` 基线（`p31_data_baseline.json`）对比 → 全相同。
- 产物/加性字段不破坏 wire：`SubmitStepResponse` / `ReasoningCheckpoint` 既有 JSON 键不变（序列化快照比对）。
- eval diff 全 0（T6）；快照回归通过（T5）。
- 不蒙数：after_p33 correct=635，harm=0，total=757。

## T8 — 台账闭环
**Status**: completed — [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §T（插于 §S 末尾 `---` 与 `## 处理原则` 之间）：T.1 触及文件清单（无 data/** JSON、无网关 solver/采纳门/eval 判分 .cs）/ T.2 加性锚定策略 / T.3 4-solver 覆盖限制登记 / T.4 eval 覆盖盲区声明（导学流回归靠快照测试）/ T.5 eval 零漂移。
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §T（插于 §S 末尾 `---` 与 `## 处理原则` 之间）：T.1 触及文件清单（无 data/** JSON、无网关 solver/采纳门/eval 判分 .cs）/ T.2 加性锚定策略 / T.3 4-solver 覆盖限制登记 / T.4 eval 覆盖盲区声明（导学流回归靠快照测试）/ T.5 eval 零漂移。
**Verification**: §T 插入位置正确；触及清单与实际一致。

## T9 — 独立 review
**Status**: completed — 委托 fresh-context 只读子代理独立复跑：编译 / T1+T3+T5 单测 / 全量 1333 测 / eval summary / 两次红线 diff / 自检 24/24 / 代码审查不变量（provider 只读、未改判分、加性字段、Trend 排除、不泄露未采纳、网关目录 0 命中）。裁决 **PASS**，全维度通过、0 Critical/Major/Minor Issue，披露 6 项非阻断限制。主代理据裁决 + 第一手复跑结果编译 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/tutor-solve-fusion/review.md)。
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：T1 provider 门控/缓存单测 / T3 匹配单测 / T5 快照回归（AC-4）/ T6 eval 两次红线 diff / T7 自检全 PASS / 52 JSON sha256 未变 / 触及清单不含网关/eval 路径 / 代码审查（provider 只读、未改判分、加性字段、Trend 排除、不泄露未采纳）。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 零网关求解改动 / 零采纳门改动 / 导学既有行为零漂移（快照）/ 不泄露未采纳结论 / 诊断回溯正确。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 review.md。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 融合深度 | a 加性锚定 / b 仅诊断回溯 / c 替换手排步骤 | **a（已确认，2026-11-22）** | 不改判分、零回归、eval 必 0 diff |
| CQ-2 | 何时跑网关求解 | a 懒加载缓存 / b 预取 | **a（默认）** | 按需省成本 |
| CQ-3 | checkpoint↔node 匹配 | a 精确 knowledge_id 取首个 / b 取全部 | **a（默认）** | 确定不蒙数 |
| CQ-4 | DAG 为空降级 | a null + 行为不变 / b DerivationSteps 降级 | **a（默认）** | 不引入未溯源内容 |
| CQ-5 | 前端范围 | a 后端+前端最小展示 / b 仅后端 | **a（已确认，2026-11-22）** | 端到端可演示 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理 | PASS（全维度、0 Critical/Major/Minor） | 无修复项；披露 6 项非阻断限制（导学流无 eval 覆盖 / 快照覆盖有限 / 锚点重复插入低概率风险 / 缓存无限增长 / 前端演示退化 / 4-solver 覆盖限制），详见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/tutor-solve-fusion/review.md) |
