# P2-1 统一推理 DAG 与结论三态契约

> **状态**：已批准（2026-11-22），开始实现。批准决策：CQ-1=A（增量示范）/ CQ-2=不动采纳门（定死）/ CQ-3=`Ok=false`→`Confirmed` / CQ-4=候选集契约占位、不实现判别建议生成 / OQ-4=类型落点 `Api/Application/Gateway` / OQ-5=`ConclusionState` 写入 eval checkpoint 仅作诊断（不计分）。
> **基线**：`after_p15b`（757 题，635 正确，`taskType=unknown`=27，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> **路线图定义**：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) L339-343 — 定义跨栈 `ReasoningNode {step, premises[], rule_id/knowledge_id, conclusion, evidence_level}` 与结论三态（确定/候选集/信息不足）；网关 `DerivationSteps` 与 Core `SolutionStep` 对齐到该契约；输出显式三态，候选集附判别实验建议；验收 = 任一门采纳结论可回溯到 rule/knowledge 节点、无跳步、null 结论显式标「信息不足/能力缺口」而非静默。依赖 P0-1。
> **红线（不可破）**：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。

## 1. 背景与问题

路线图目标是让求解从「事实硬匹配」升级为「可追溯推理」，并为 P2-2（世界模型解释标注 evidence_level）与 P2-4（前端展示推理 DAG）铺路。但当前两套栈的「推理步骤 / 结论」契约**已分叉**：Core 侧已有较丰富的三态 + 可溯源结构，网关侧仍是平坦字符串，无法支撑「任一门采纳结论可回溯到 rule/knowledge 节点」。

本 spec 的目标是**定义统一契约并在网关侧落地示范**，使结论状态显式化、推理节点可溯源，同时**严格不改变当前采纳门与正确性行为**（红线）。

## 2. 现状审计（只读证据，精确到文件/行号）

### 2.1 Core 侧：已有三态 + 可溯源推理步骤

- [InferenceModels.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/InferenceModels.cs)：
  - `InferenceStatus` 枚举（L11-21）：`Solved` / `Underdetermined` / `NoSolution` —— **已是结论三态**（确定 / 信息不足 / 无解），注释明确「绝不猜测」。
  - `InferenceStep`（L48-67）：`Description` / `Evidence` / `KnowledgeId` / `Source` / `Thought`(`ChemicalThought?`) —— **已有 knowledge_id 可溯源** + 化学思想标签。
  - 各推断结果（`ProductInferenceResult` / `IdentificationResult` / `ProcessInferenceResult` / `StoichiometryResult`）均含 `Status: InferenceStatus` + `Steps: List<InferenceStep>`。
  - `CompositionVerdict`（L157-170）：`Must` / `MustNot` / `Maybe` / `Unknown` —— 已有候选集语义。
- [SolutionModels.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Solving/SolutionModels.cs)：
  - `Solution.Status`（L31）= `InferenceStatus`；`IsSolved` 仅在 `Solved` 时为 true（L33），注释「Status 非 Solved 时答案字段不得被当作结论使用」（L22-23）。
  - `SolutionStep`（L86-107）：由 `InferenceStep` 规范化而来，字段 `Order` / `Description` / `Evidence` / `KnowledgeId` / `Source` / `Thought`。

**结论**：Core 侧三态与可溯源已具备；缺 `premises[]`（当前是单字符串 `Evidence`）、缺 `rule_id`（与 `knowledge_id` 未区分）、缺 `evidence_level`（无显式证据强度）。

### 2.2 网关侧：平坦字符串，无可溯源 id，无显式三态

- [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) `EngineResponse`（L105-151）：
  - `Ok: bool?`（L109）—— true=正确 / false=错误 / **null=不在引擎建模范围**。null 同时承载「超范围」「信息不足」「能力缺口」，无语义区分。
  - `FinalResult: string`（L112）—— 单结论串。
  - `DerivationSteps: List<string>`（L121）—— **平坦字符串列表，无 premises/rule_id/knowledge_id/evidence_level/thought**。
  - `NeedMoreCondition: bool`（L130）+ `ConstraintNote: string`（L124）—— 信息不足的零散信号，未并入统一三态。
  - `Confidence: double`（L127，默认 1.0）—— 存在但未作为 evidence_level 使用。
- 写入点：[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 中 **50+ 处** `DerivationSteps = new List<string> {...}` 或 `= xxx.Reasons`（grep 命中 L395/417/725/744/758/771/833/886/911…2726 区间），分布在 18 个 solver 文件（AtomicStructureSolver / ConceptFactSolver / KnownEquationWriterSolver / ProductWriterSolver / ReactionKbWriterSolver / VseprSolver …）。绝大多数写入的是 ad-hoc 叙述串，**无 knowledge_id 可填**。

### 2.3 消费者与静默 null 点

- **`DerivationSteps` 唯一消费者** = [GenerateReplyAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L2726-L2800)：L2763 `Take(2)` 渲染「关键依据」、L2793 拼入 LLM prompt。**评估 checkpoint 不读 `DerivationSteps`、不读 reply 文本**（见 2.4）。
- **静默 null 点** = [BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L660-L668) 顶部门（L665）：`if (!resp.Ok.HasValue || gw.TaskType is "concept" or "unknown") return null;` —— 把「超范围 / 信息不足 / 未解 / 答案键未命中」全部折叠为无语义 null。回复侧（GenerateReply L2739/L2729）虽对 `Ok=null` 说「超出建模范围」、对 `NeedMoreCondition` 说「需补充信息」，但这是**自然语言回复**，未进入结构化契约，评估侧无法区分。
- **采纳安全不变量**：`BuildGatewayAnswerAsync` 每个分支均 `IsAnswerCorrect(conclusion)` 命中才返回 → `adopted → correct`（precision=1）。该逻辑**不得因本 spec 改动**。

### 2.4 评估 checkpoint 字段（红线不受影响的依据）

[GatewayVsLlmEvaluationService.cs L194-214](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L194-L214) 赋值：`GatewayConclusion`=FinalResult、`GatewayOk`=Ok、`TaskType`、`GatewayUsedEngine`、`GatewayAnswer`（=BuildGatewayAnswerAsync）、`GatewayCorrect`、`GatewayTruthAdopted`、`StructuredHit`/`StructuredAssetKind`。**不含** `DerivationSteps` / reply / 新增元数据字段。

→ 因此：只要不改动 `FinalResult` / `Ok` / `TaskType` / `AssetKind` / `NumericValue*` 的赋值与采纳门逻辑，**新增 `ReasoningNodes` / `ConclusionState` 字段对 eval 正确性结果零影响**，LOST/WORSEN/harm 可证保持 0。

### 2.5 Api 已引用 Core

[CogniThink.TutorBrain.Api.csproj L21](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/CogniThink.TutorBrain.Api.csproj) `ProjectReference Include="..\CogniThink.Chem.Core\..."` —— 网关可直接引用 Core 的 `InferenceStatus` / `InferenceStep` 类型，无需新建共享工程。

## 3. 目标契约定义

### 3.1 `ReasoningNode`（统一推理节点）

新增类型（落点见 CQ-1），字段对齐路线图：

| 字段 | 类型 | 含义 |
|------|------|------|
| `Step` / `Order` | int | 节点序号（保证无跳步、有序） |
| `Description` | string | 本步叙述（中文） |
| `Premises` | `List<string>` | 本步依据的前提（题干条件 / 上一步结论 / 知识条目引用）—— 路线图 `premises[]` |
| `RuleId` | string? | 命中的规则 id（如 `data/rules/rules.json` 条目；无则 null） |
| `KnowledgeId` | string? | 命中的知识点 id（如 `data/highschool/*.json` 条目；无则 null）—— 与 Core `InferenceStep.KnowledgeId` 对齐 |
| `Conclusion` | string? | 本步推出的中间结论（末节点 = 最终结论） |
| `EvidenceLevel` | enum | 证据强度（见 3.3） |

### 3.2 `ConclusionState`（结论三态）

新增枚举，与 Core `InferenceStatus` 语义对齐并向网关暴露：

| 取值 | 对应 Core | 含义 | 触发信号（由现有字段派生，不改行为） |
|------|-----------|------|--------------------------------------|
| `Confirmed` | `Solved` | 确定：引擎给出唯一可溯源结论 | `Ok==true` 且 `FinalResult` 非空 |
| `CandidateSet` | `Underdetermined`（候选保留） | 候选集：多个并列可能，未唯一 | 求解器自报候选（如成分推断 Maybe / 产物多命中）；当前网关暂无生产路径 → 预留 |
| `Insufficient` | `Underdetermined`（信息不足）/ `NoSolution` | 信息不足 / 能力缺口：无法确定 | `Ok==null` 或 `NeedMoreCondition==true` 或 `FinalResult` 空且未命中 |

> 注：`Ok==false`（学生答案错误）在导学场景是「判定为错」，不是结论三态的一部分；三态描述的是**引擎能否给出确定结论**，`Ok=false` 仍属 `Confirmed`（引擎确定地判定错误）。此映射在 CQ-3 确认。

### 3.3 `EvidenceLevel`（证据强度）

新增枚举（路线图 `evidence_level`，并为 P2-2 世界模型预留）：

| 取值 | 含义 | 典型来源 |
|------|------|----------|
| `KnowledgeFact` | 知识库唯一命中（确定真值） | ConceptFactSolver / ReactionKbWriterSolver 唯一匹配 |
| `EngineDerived` | 引擎推导（配平 / 计量 / 查表） | EquationBalancer / PeriodicTable / VSEPR 查表 |
| `Candidate` | 候选未唯一（并列可能） | 成分推断 Maybe / 产物多命中 |
| `Trend` | 趋势级（不给绝对值，P2-2 世界模型用，标注「帮助理解，不作判定依据」） | 预留 P2-2 |

## 4. 设计决策（CQ — 需用户在批准时确认）

### CQ-1：契约落点与迁移范围【关键，决定表面积】

| 选项 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| **A（推荐）增量示范** | 保留 `DerivationSteps: List<string>` 不变（wire/前端/回复零破坏）；`EngineResponse` **新增** `ReasoningNodes: List<ReasoningNode>` + `ConclusionState` 字段；先在**有 knowledge_id 的 solver**（ConceptFactSolver / ReactionKbWriterSolver / 原子结构 / VSEPR 查表）落地示范；其余 solver 保持字符串并标「待迁移」（`ReasoningNodes` 留空或由字符串降级填充，knowledge_id=null） | 表面积可控、零回归、契约立即可供 P2-2/P2-4 消费；与红线兼容 | 示范外 solver 暂无可溯源节点（但不影响正确性） |
| B 复用 Core 全量替换 | 网关 `DerivationSteps` 直接改为 `List<InferenceStep>`（复用 Core 类型），50+ 写入点全部迁移 | 类型最少、与 Core 完全统一 | 表面积巨大（18 文件 50+ 点），大部分 solver 无 knowledge_id 可填（强制填 null → 可溯源空转）；wire JSON 形状变更可能影响未来前端；回归风险高 |
| C 新建中立契约全量 | 新建独立 `ReasoningNode`（Domain 层），Core `SolutionStep` 与网关同时映射到它，全量迁移 | 解耦最彻底 | 重复类型 + 双向映射代码；表面积最大；与「Api 已引用 Core」的现状重叠 |

**推荐 A**：符合「冻结范围、零回归、不蒙数」纪律；先立契约 + 示范可溯源链，其余迁移可后续批次（P2-1.x）或随 P2-2 自然推进。

### CQ-2：是否改动采纳门 `BuildGatewayAnswerAsync`【强约束】

**不动**（非真选项，定死）。`ConclusionState` / `ReasoningNodes` 是**观测性元数据**，由现有信号派生；`BuildGatewayAnswerAsync` 的 null/采纳逻辑保持逐字符不变。任何改动都有 LOST/harm 风险，违反红线。

### CQ-3：`Ok=false` 与三态的映射【待确认】

推荐：`Ok=false` → `ConclusionState=Confirmed`（引擎确定地判定学生答案错误，仍属「给出确定结论」）。错误细节走 `ErrorTags` + `ConstraintNote`，不进三态。请确认此语义。

### CQ-4：候选集的「判别实验建议」【范围】

路线图要求「候选集附判别实验建议」。当前网关暂无生产 `CandidateSet` 路径（成分推断/产物多命中在 Core 有，网关 Live 路径未接通）。**本 spec 仅定义 `CandidateSet` 状态的契约与字段占位**，不实现具体判别实验建议生成（那需要 solver 接通候选路径，属范围外，留后续）。请确认是否接受「先契约占位、不实现建议生成」。

## 5. 范围（In Scope）

1. 定义 `ReasoningNode` / `ConclusionState` / `EvidenceLevel` 类型（落点按 CQ-1 选项 A）。
2. `EngineResponse` 新增 `ReasoningNodes` + `ConclusionState` 字段（**加性**，不删改既有字段）。
3. `ConclusionState` 由现有信号（Ok / NeedMoreCondition / FinalResult）派生的统一赋值点（一处，不改采纳门）。
4. 在 **4 个有 knowledge_id/查表真值源的 solver** 上示范填充 `ReasoningNodes`：ConceptFactSolver、ReactionKbWriterSolver、AtomicStructureSolver（PeriodicTable）、VseprSolver（查表）。要求：命中节点带非空 `KnowledgeId`/`RuleId`，节点有序无跳步，末节点 `Conclusion`=FinalResult。
5. 单测：契约字段 + 三态派生映射 + 示范 solver 节点可溯源 + 采纳门行为不变（回归守卫）。
6. 全量 eval 红线核验（diff 相对 after_p15b：GAINED/LOST/WORSEN/harm 全 0）。
7. 台账登记 + 自检 + 独立 review（沿用 P1-5 T5/T6/T7 模式）。

## 6. 非目标（Out of Scope，明确不做）

- 不一次性迁移全部 50+ `DerivationSteps` 写入点到 `ReasoningNodes`（留后续批次，见 CQ-1）。
- 不实现候选集判别实验建议生成（CQ-4）。
- 不改 `BuildGatewayAnswerAsync` 采纳逻辑（CQ-2）。
- 不接 Core ProblemSolver 到网关 Live 路径（两套栈收敛方向是路线图 §6 决策点 #1，未拍板前不动）。
- 不改 `data/highschool/*.json` / `data/rules/rules.json` 数据。
- 不改评估代码的判分口径（只允许加性读取新字段做诊断，不影响 `GatewayCorrect`）。
- 不做前端展示（P2-4）。

## 7. 验收标准（AC）

| ID | 验收 | 证据 |
|----|------|------|
| AC-1 | `ReasoningNode` 含 Step/Premises[]/RuleId?/KnowledgeId?/Conclusion?/EvidenceLevel；`ConclusionState`（Confirmed/CandidateSet/Insufficient）；`EvidenceLevel` 四值。编译通过。 | 类型定义文件 + build 0 error |
| AC-2 | `EngineResponse` 新增字段为加性（既有字段/JSON 键不变）；旧序列化兼容。 | 对比 build 前后 `GatewayResult` JSON 既有键不变 |
| AC-3 | `ConclusionState` 由统一派生点赋值；映射：Ok=true&FinalResult 非空→Confirmed；Ok=null 或 NeedMoreCondition→Insufficient；候选→CandidateSet。单测覆盖。 | 单测 |
| AC-4 | 4 个示范 solver 各至少 1 条已判正链：`ReasoningNodes` 非空、命中节点 `KnowledgeId`/`RuleId` 非空、节点 Order 连续无跳步、末节点 Conclusion==FinalResult。 | 单测 + eval 抽样 |
| AC-5 | **红线**：`diff_ckpt after_p15b → after_p21`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0。correct=635 不变。 | diff 脚本 RED-LINE PASS |
| AC-6 | `GenerateReplyAsync` 不抛异常；回复文本不回归（DerivationSteps 保留；可选把 ReasoningNodes 摘要加入 prompt，但不改变 Ok 分支语义）。 | 全量 eval 无 failed；抽样回复 |
| AC-7 | null 结论（原静默）在结构化契约中显式 `ConclusionState=Insufficient`，并带原因（ConstraintNote 或能力缺口标记）；评估诊断可区分「超范围」vs「信息不足」。 | 诊断脚本 |
| AC-8 | 采纳门不变量保持：凡 `GatewayTruthAdopted=True` 必 `GatewayCorrect=True`（precision=1）。 | eval 后 `adopted∧¬correct=0` |

## 8. 风险与回退

- **R1 表面积扩散**：若误选 CQ-1 B/C，18 文件改动易引入回归。缓解：选 A，仅加性字段 + 4 solver 示范；diff 逐文件核对。
- **R2 wire 破坏**：若改 `DerivationSteps` 类型，未来前端/P2-4 读取旧键会断。缓解：保留 `DerivationSteps`，新增 `ReasoningNodes`（加性）。
- **R3 三态派生误改采纳**：若派生逻辑意外影响 `Ok`/`FinalResult` 赋值 → LOST。缓解：派生只读、写新字段，单测断言 `BuildGatewayAnswerAsync` 输入（Ok/FinalResult/TaskType）在改动前后逐 case 不变；eval diff 硬把关。
- **R4 可溯源空转**：示范外 solver 的 `ReasoningNodes` 若被默认填充无 id 节点，会给前端「假可溯源」错觉。缓解：无 knowledge_id 的 solver **不填** `ReasoningNodes`（留空），由 `ConclusionState` 单独表达状态；前端据 `ReasoningNodes` 是否为空降级展示。
- **回退**：任一红线破 → `git checkout` 本 spec 触及文件回退到 after_p15b 状态；新增字段为加性，回退不影响基线。

## 9. 诚实口径

- **P2-1 预期 eval 正确性增益 = 0**（与 P1-5 同为结构卫生）。理由：评估 checkpoint 不读推理步骤/回复，采纳门不变；本 spec 只加观测性元数据。价值在：① 为 P2-2 世界模型解释提供 evidence_level 锚点；② 为 P2-4 前端 DAG 展示提供数据；③ 让「信息不足/能力缺口」从静默 null 变为显式可诊断。**不强凑解对题数。**
- 「任一门采纳结论可回溯到 rule/knowledge 节点」在本 spec 结束时**仅对 4 个示范 solver 的命中链成立**；全量覆盖是后续批次目标，不在本 spec 验收内（避免虚报）。

## 10. Open Questions（批准时一并确认）

- OQ-1：CQ-1 选 A / B / C？（推荐 A）
- OQ-2：CQ-3 `Ok=false`→Confirmed 的语义是否认可？
- OQ-3：CQ-4 候选集判别实验建议是否接受「契约占位、不实现生成」？
- OQ-4：`ReasoningNode` 类型放哪个命名空间/文件？（推荐：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs`，与 Core `InferenceModels` 并存；若选 CQ-1 B 则放 Core。）
- OQ-5：是否需要把 `ConclusionState` 也写入 eval checkpoint jsonl（仅诊断，不计分），便于后续观测「信息不足」占比？（推荐：加，只读诊断。）
