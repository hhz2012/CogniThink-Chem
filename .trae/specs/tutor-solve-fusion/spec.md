# Spec — P3-3 导学与求解融合（标准推理链锚定 + 诊断回溯 DAG）

> **状态**：草稿，待用户批准后实施。批准决策：CQ-1=加性锚定（已确认，2026-11-22）/ CQ-5=后端+前端最小展示（已确认，2026-11-22）。
> **基线**：`after_p32`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> **路线图**：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) §P3-3 —「让 miniapp 的导学流（submit-step）消费网关求解结果作为『标准推理链』，而非各自维护；错因诊断回溯到求解 DAG 节点。」依赖 P2-1（统一 DAG 契约，已交付）、P2-4（前端 /solve 入口，已交付）。
> **红线（不可破）**：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON / 不泄露未采纳结论。破线即回退。
> **诚实口径**：本 spec **预期 eval 正确性增益 = 0**。理由：导学流不在 757 eval 覆盖内（grep 实证 0 命中），且本 spec **不改网关求解 / 采纳门 / 判分**，故 `diff after_p32→after_p33` 必为 0。价值 = 让导学流拿到网关「标准推理链」作为参照、诊断可回溯到求解 DAG 节点；**不替换手排步骤、不改判分源**。

## 1. 背景与目标

路线图 P3-3 要求把「导学分步流」与「网关题目求解」融合为单一推理链来源。T0 审计（§2）实证：当前两者是**完全分离的两条链**——导学流跑在 DB 手排的 `Problem.Steps` + 知识图谱诊断上，**从不调用** `LlmGatewayService`；网关求解产出的 `ReasoningNodes` DAG 只在 `/api/gateway/solve`（P2-4）暴露，导学流拿不到，诊断也无法回溯到 DAG 节点。

本 spec 采用**加性锚定（additive anchoring）**策略（CQ-1 已确认）：
- 不改导学判分源（手排 `Problem.Steps` / `TutorStateMachine` / `ChemistryReasoningEngine`）；
- 不改网关求解 / 采纳门 / `BuildGatewayAnswerAsync` / 任何 eval 路径；
- 仅让导学流**按需获取**网关标准推理链（`ReasoningNodes` DAG），**加性**下发到 `submit-step` 响应，并让诊断检查点按 `knowledge_id` 链接到对应 DAG 节点；
- 网关 DAG 为空的题（非 4 个示范 solver 家族）**优雅降级**，行为与现状逐字节一致。

## 2. T0 只读审计（证据，精确到文件/行号）

| # | 证据 | 位置 | 结论 |
|---|------|------|------|
| 1 | 导学流入口 | [TutorController.cs L21-58](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/TutorController.cs#L21-L58) | `POST /api/tutor/submit-step` → `TutorService.SubmitStepAsync`；响应 `SubmitStepResponse` 含 verdict/errorTag/hint/weakPrereq/chemicalEvidence/variant，**无 DAG 字段** |
| 2 | 导学编排不调网关 | [TutorService.cs L28-54](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorService.cs#L28-L54) | 构造注入 `TutorStateMachine` / `ChemicalInferenceService` / `KnowledgeGraphService` 等，**无 `LlmGatewayService`**；全文件 grep `LlmGatewayService` = 0 命中 |
| 3 | 导学判分源 | [TutorStateMachine.cs L40-87](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorStateMachine.cs#L40-L87) | `HandleAsync` → `IReasoningEngine.EvaluateAsync`（化学用 `ChemistryReasoningEngine`，关键词/矛盾判分）；注释明定「LLM 只负责话术」 |
| 4 | 导学步骤数据 | [Problem.cs L51-103](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Domain/Problem.cs#L51-L103) | `ProblemStep` = 手排 `ExpectedExpression` / `ExpectedKeywords` / `DistractorKeywords` / `Hint*` / `KnowledgeId` / `IsKeyStep`；`Problem.Stem` 纯文本 |
| 5 | 导学侧化学证据 | [TutorService.cs L153-193](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorService.cs#L153-L193) | `BuildChemicalEvidence` 仅在 `IsKeyStep` 调 `ChemicalInferenceService.Evaluate`，独立查询，与网关 DAG 无关 |
| 6 | 诊断推理结构 | [DiagnosticTeachingEngine.cs L45-98](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticTeachingEngine.cs#L45-L98) | `BuildStructureAsync` 从 `KnowledgeGraphService.GetPrerequisiteChainAsync` + DB `KnowledgeNodes` + `ThinkingStrategyLibrary` 构建 `ReasoningStructure`（前置→核心→推理检查点），**不引用网关 `ReasoningNode`** |
| 7 | 诊断判分 | [DiagnosticTeachingEngine.cs L247-253](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticTeachingEngine.cs#L247-L253) | 检查点作答复用 `ChemistryReasoningEngine.Evaluate`；LLM 不参与判定 |
| 8 | 诊断契约 | [DiagnosticContracts.cs L9-75](file:///d:/github/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticContracts.cs#L9-L75) | `ReasoningCheckpoint {Index, KnowledgeId, KnowledgeName, Type, Question, ExpectedKeywords, Explanation, …}`；`ReasoningStructure {ProblemId, StepIndex, StepPrompt, Checkpoints[]}` —— 无 DAG 节点字段 |
| 9 | 网关求解入口 | [LlmGatewayService.cs L93](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L93) | `ProcessStudentMessageAsync(string rawText, imagePaths?, imageAssets?, questionType?, ct)` → `Task<GatewayResult>` |
| 10 | DAG 契约 | [ReasoningContracts.cs L51-80](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs#L51-L80) | `ReasoningNode {step, description, premises[], rule_id?, knowledge_id?, conclusion?, evidence_level}`；`ConclusionState {Confirmed, CandidateSet, Insufficient}`（P2-1） |
| 11 | DAG 覆盖范围 | grep `ReasoningNodes =` in `src/` | **仅 4 个 solver 填充**：ConceptFactSolver / ReactionKbWriterSolver / AtomicStructureSolver / VseprSolver（P2-1 示范）；其余 solver 留空 |
| 12 | P2-4 投影门控 | `frontend-gateway-solve-loop/spec.md` §3.1 + [GatewaySolveProjection.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs) | 仅 `ConclusionState=Confirmed` 下发 `reasoning_nodes` / `final_result`；`Insufficient` 不下发（不泄露未采纳结论）——本 spec 复用同门控 |
| 13 | eval 不触导学流 | grep `*Eval*.cs` 对 `TutorService`/`DiagnosticTeachingEngine`/`SubmitStepAsync` | **0 命中** → 757 eval 只测网关求解路径，不测导学流；导学流回归在 eval 中不可见 |
| 14 | 前端现状 | [tutorApi.ts L9](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts) + P2-4 spec §2 | miniapp 调 `/api/tutor/*`（导学）+ `/api/gateway/solve`（求解，独立页）；两流在前端也分离 |
| 15 | Api 已引用网关类型 | `LlmGatewayService` 为已注册 DI 服务（GatewayController / eval 均注入） | `TutorService` 可直接注入，无需新建工程/共享类型 |

### 审计结论：三个缺口 + 一个覆盖盲区

1. **无共享标准推理链**：网关 = solver 实时产出 `ReasoningNodes` DAG；导学 = 手排 `Problem.Steps` + 知识图谱 `ReasoningStructure`。两者无共享来源。
2. **诊断不可回溯 DAG**：`DiagnosticTeachingEngine` 检查点来自知识图谱/策略库，不引用网关 `ReasoningNode`；导学流根本不跑网关求解。
3. **覆盖率限制**：仅 4 个 solver 家族产出 `ReasoningNodes`，故「标准推理链」仅对路由到这些 solver 的题非空，其余题必须优雅降级。
4. **eval 覆盖盲区（关键风险）**：757 eval 不覆盖导学流 → `diff after_p32→after_p33` 会是 0（证明未碰网关），但**导学流回归 eval 不可见**。必须靠「零网关改动 + 导学专用快照测试 + 加性降级」三重守护。

## 3. 设计（加性锚定）

### 3.1 服务端：导学流按需获取网关标准推理链（只读、缓存、加性）

- 新建 `TutorSolveChainProvider`（`Application/Tutor/` 或 `Application/`），注入 `LlmGatewayService`：
  - `Task<IReadOnlyList<ReasoningNode>?> GetChainAsync(string problemStem, CancellationToken ct)`：
    - 调 `LlmGatewayService.ProcessStudentMessageAsync(problemStem)`（`imagePaths=null` / `imageAssets=null` / `questionType=null`，因导学 `Problem.Stem` 为纯文本）；
    - 复用 P2-4 门控：仅当 `engine_response.conclusion_state == ConclusionState.Confirmed` 且 `reasoning_nodes` 非空时返回节点列表；否则返回 `null`（`Insufficient` / `CandidateSet` / 空 DAG 一律不下发，不泄露未采纳结论）；
    - **只读**：不修改 `GatewayResult` / `EngineResponse`；不写回任何网关字段。
  - **缓存**：以 `problemStem`（或 `problemId`）为 key 的内存缓存（请求级或短 TTL 单例），避免每次 `submit-step` 重跑 LLM + 引擎。缓存命中即返回，不重算。
- `TutorService` 注入 `TutorSolveChainProvider`，在 `SubmitStepAsync` 中（或首次需要时）取链，加性挂到响应。

### 3.2 `SubmitStepResponse` 加性字段（wire 兼容）

- `SubmitStepResponse` 新增可选字段 `StandardReasoningChain: ReasoningNode[]?`（C# 侧；JSON 键 `standard_reasoning_chain`）。
- **仅当** 3.1 返回非空时填充；否则为 `null`/缺省。
- 既有字段（Status / Verdict / ErrorTag / HintLevel / Message / RelatedKnowledgeIds / ChemicalEvidence / Variant 等）**逐字段不变**，wire 向后兼容。
- `TutorController` 透传该字段（[TutorController.cs L38-55](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/TutorController.cs#L38-L55) 加一行映射）。

### 3.3 诊断回溯到求解 DAG 节点

- `ReasoningCheckpoint`（[DiagnosticContracts.cs L9-46](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticContracts.cs#L9-L46)）新增可选字段 `LinkedReasoningNode: ReasoningNode?`（JSON `linked_reasoning_node`）。
- `DiagnosticTeachingEngine.BuildStructureAsync` 构建检查点时，**按 `knowledge_id` 精确匹配** 网关 `ReasoningNodes`（由 3.1 provider 取链后传入）：
  - 匹配规则（CQ-3a）：`checkpoint.KnowledgeId == node.KnowledgeId`，取**首个**匹配节点（确定、不蒙数）；无匹配 → `LinkedReasoningNode = null`。
  - 匹配来源仅限网关 DAG 中 `EvidenceLevel != Trend` 的节点（`Trend` 是 P2-2 世界模型解释，标注「不作判定依据」，不进入导学判分/诊断锚点——避免把趋势级内容当结论）。
- `DiagnosticTeachingEngine.BuildExplanation`：当 `checkpoint.LinkedReasoningNode` 非空时，讲解中引用「标准推理链对应节点：依据 {premises} 推出 {conclusion}（证据强度 {evidence_level}）」作为锚点；为空时保持现有讲解**逐字不变**。
- `DiagnosticStartResult` / `DiagnosticAnswerResult` 透传 `Structure`（含带 `LinkedReasoningNode` 的检查点），无需改契约形状。

### 3.4 前端（miniapp，最小展示）

- `miniapp/src/services/tutorApi.ts`：`SubmitStepResponse` TS 类型新增 `standardReasoningChain?: ReasoningNodeDto[]`（镜像 P2-4 `pages/solve` 的 `ReasoningNode` 类型定义，复用而非重写）。
- 导学页（现有 tutor 页）：当 `submit-step` 返回 `standardReasoningChain` 非空时，渲染一个**折叠区**「标准推理链（引擎推导）」，复用 P2-4 `pages/solve` 的 DAG 节点渲染组件（step / description / premises / rule_id 或 knowledge_id / conclusion / evidence_level）；空则不渲染该区块。
- **不新建页面、不改导学既有 UI 流程、不泄露未采纳结论**（后端门控已保证 `Insufficient` 不下发）。
- 演示目标端：`npm run dev:h5`（同 P2-4，免小程序审核）；不破坏 weapp/tt 构建。

### 3.5 不改的边界（红线）

- 不改 `LlmGatewayService` / 任何 solver / `CallEngineAsync` / `FinalResult` / `Ok` / `TaskType` 赋值。
- 不改采纳门 `BuildGatewayAnswerAsync` / `IsStructuredCorrect` / eval 判分。
- 不改 `TutorStateMachine` / `ChemistryReasoningEngine` 判分逻辑；不改 `Problem.Steps` 手排数据。
- 不改 `data/**` 任何 JSON；不新增化学事实硬编码到 `.cs` / `.tsx`。
- 不把 `Trend`（世界模型解释）节点当作导学判分/诊断结论依据。

## 4. 关键决策点（CQ — 已确认 / 待确认）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 融合深度 | a 加性锚定（推荐）/ b 仅诊断回溯 / c 替换手排步骤 | **a（已确认，2026-11-22）** | 不改判分、零回归、eval 必 0 diff；c 风险高留 P3-3b |
| CQ-2 | 何时跑网关求解 | a 首次取链懒加载并缓存（推荐）/ b 题目加载预取 | **a**（默认） | 按需省成本；缓存避免重复 LLM+引擎 |
| CQ-3 | checkpoint↔node 匹配 | a 精确 `knowledge_id` 相等取首个（推荐）/ b 取全部同知识点节点 | **a** | 确定不蒙数；多节点歧义留后续 |
| CQ-4 | DAG 为空时降级 | a `StandardReasoningChain`=null，诊断不链接，行为与现状逐字节一致（推荐）/ b 用 `DerivationSteps` 平坦串降级 | **a** | 不引入未溯源内容；b 的平坦串无 knowledge_id，不可靠 |
| CQ-5 | 前端范围 | a 后端+前端最小展示（推荐）/ b 仅后端 | **a（已确认，2026-11-22）** | 端到端可演示；复用 P2-4 组件，成本低 |

## 5. 范围（In Scope）

1. 新建 `TutorSolveChainProvider`：调网关取 DAG，按 `ConclusionState` 门控，缓存。
2. `SubmitStepResponse` 加性字段 `StandardReasoningChain` + `TutorController` 透传。
3. `ReasoningCheckpoint` 加性字段 `LinkedReasoningNode` + `DiagnosticTeachingEngine` 按 `knowledge_id` 链接（排除 `Trend`）+ `BuildExplanation` 引用锚点。
4. miniapp `tutorApi.ts` 类型 + 导学页折叠展示（复用 P2-4 DAG 组件）。
5. 导学专用单测快照：网关 DAG 为空/Insufficient 时，`SubmitStepResponse` 既有字段与改动前**逐 case 一致**。
6. 全量 eval `after_p33` + 两次红线 diff（after_p32→after_p33 全 0；base_p0→after_p33 LOST/WORSEN/harm=0，GAINED 维持 6）。
7. 台账登记 §T + 自检 + 独立 review（沿用 P3-2 模式）。

## 6. 非目标（Out of Scope，明确不做）

- 不替换手排 `Problem.Steps` / 不改判分源（CQ-1c，留 P3-3b）。
- 不迁移其余 solver 到 `ReasoningNodes`（留后续 DAG 迁移批次；本 spec 仅消费现有 4-solver 产出）。
- 不改 `LlmGatewayService` / 采纳门 / eval 判分。
- 不改 `data/**` JSON；不新增化学事实到 `.cs` / `.tsx`。
- 不做图片题导学 DAG（导学 `Problem.Stem` 纯文本；图像题留后续）。
- 不做小程序发布/审核（仅 dev:h5 演示，同 P2-4）。
- 不消除「4-solver 覆盖限制」——诚实披露 + 优雅降级。

## 7. 验收标准（AC）

| ID | 验收 | 证据 |
|----|------|------|
| AC-1 | `dotnet build -c Release` 0 error（P3-3 触及文件 0 新警告）。 | build 输出 |
| AC-2 | 门控单测：`Confirmed` 题 → `StandardReasoningChain` 非空且仅含 `Confirmed` 路径节点（无 `Trend`）；`Insufficient` / `CandidateSet` → `null`；网关 DAG 为空 → `null`。 | 单测 |
| AC-3 | 匹配单测：`DiagnosticTeachingEngine` 对 `knowledge_id` 命中的检查点填 `LinkedReasoningNode`（来自网关 DAG，首个匹配，非 `Trend`）；未命中 → `null`。 | 单测 |
| AC-4 | **快照回归（关键）**：对网关 DAG 为空/`Insufficient` 的题集，`SubmitStepResponse` 既有字段（除新增 `StandardReasoningChain` 外）与改动前**逐 case 字节一致**。证明导学判分/话术零漂移。 | 快照测试脚本 |
| AC-5 | **红线 eval**：`diff_ckpt after_p32 → after_p33`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0，correct=635 不变；`diff_ckpt base_p0 → after_p33`：LOST/WORSEN/harm=0（GAINED 维持 6 携带）。 | diff 脚本 RED-LINE PASS |
| AC-6 | 不泄露未采纳结论：`Insufficient` 题 `StandardReasoningChain` 恒 `null`；前端不展示任何确定答案（同 P2-4 AC-5）。 | 单测 + 抽样 |
| AC-7 | 前端：`dev:h5` 下对一道 `Confirmed` 导学题，submit-step 后折叠区展示「标准推理链」DAG；对 `Insufficient`/空 DAG 题不展示该区块；既有导学 UI 不回归。若 Taro 环境不可跑，退化为「类型编译通过 + 字段在响应中存在」（同 P2-4 风险条款）。 | 演示/截图 |
| AC-8 | 表面积加性：触及文件不含任何网关 solver / `LlmGatewayService` 主体 / 采纳门 / eval 判分文件（grep  attestation）；新增字段为加性，不删改既有字段/JSON 键。 | 触及清单 + grep |
| AC-9 | 台账 `data-defect-register.md` 新增 §T（P3-3 触及范围 + 加性锚定策略 + 4-solver 覆盖限制登记 + eval 覆盖盲区声明）。 | 台账 §T |
| AC-10 | T9 fresh-context 只读子代理 review → PASS。 | review.md |

## 8. 风险与回退

- **R1 导学流无 eval 覆盖**：回归在 757 eval 中不可见。缓解：AC-4 快照测试断言既有字段逐 case 不变；本 spec 不改判分（CQ-1a）；加性降级（CQ-4a）。三重守护。
- **R2 性能：每次 submit-step 跑网关**（LLM 解析 + 引擎）。缓解：`problemId` 级缓存（CQ-2a），首次取链后复用；不阻塞判分主路径（取链失败/超时时 `StandardReasoningChain=null` 降级，不影响作答）。
- **R3 演示环境 LLM 不可用**：网关有 rule/keyword 兜底（P2-4 已验证），但 DAG 可能为空 → 降级为不展示。演示题优先选路由到 4 个示范 solver 且 `Confirmed` 的文本题。
- **R4 4-solver 覆盖限制**：仅部分题有 DAG。缓解：诚实披露（§9）；CQ-4a 优雅降级；不在本 spec 迁移其余 solver。
- **R5 `Trend` 节点误用**：世界模型解释被当作判定依据。缓解：3.3 明确排除 `EvidenceLevel=Trend` 节点进入 `LinkedReasoningNode`；单测覆盖。
- **R6 wire 破坏**：新增字段改了 `SubmitStepResponse` 形状。缓解：仅加性可选字段，既有键不变；前端缺省容忍（`?` 可选）。
- **回退**：任一红线破 → 回退本 spec 触及文件到 `after_p32` 状态。新增字段/类/provider 均为加性，回退不影响基线；`SubmitStepResponse` 与 `ReasoningCheckpoint` 删字段即可恢复 wire。

## 9. 诚实口径

- **P3-3 预期 eval 正确性增益 = 0**：导学流不在 757 eval 内，且本 spec 不改网关求解/采纳门/判分。`diff after_p32→after_p33` 必为 0（AC-5 硬验证）。
- **「标准推理链」仅对 4 个示范 solver 家族（ConceptFact / ReactionKb / AtomicStructure / Vsepr）路由到的题非空**；其余题 `StandardReasoningChain=null`，导学行为与现状逐字节一致（AC-4 快照验证）。不夸大覆盖率。
- **不替换手排步骤**：判分源仍是 `Problem.Steps` + `ChemistryReasoningEngine`；网关 DAG 仅作「参照 / 诊断锚点」，不参与对错判定。
- **导学流正确性回归靠专用快照测试守护，不靠 757 eval**（已在 §8 R1 声明）。
- `Trend`（P2-2 世界模型）节点不进入导学诊断锚点，避免把趋势级解释当结论。

## 10. Open Questions（批准时一并确认）

- OQ-1：CQ-1 / CQ-5 已确认（加性锚定 + 后端前端最小展示）。是否认可 CQ-2 / CQ-3 / CQ-4 的默认（a）选项？
- OQ-2：缓存策略——`problemId` 级内存短 TTL 单例是否可接受？还是请求级即可（每次 submit-step 最多取一次）？
- OQ-3：前端 DAG 渲染是否直接复用 P2-4 `pages/solve` 的组件（若组件未抽离则就地抽一个共享组件），还是导学页内联最小渲染？（推荐抽共享组件，避免重复。）
