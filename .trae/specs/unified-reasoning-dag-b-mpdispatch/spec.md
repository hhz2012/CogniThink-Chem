# B-MpDispatch 批：把 MultiPartDispatch 聚合时丢弃的子响应 ReasoningNodes 链进父响应

**父规格**：[unified-reasoning-dag/spec.md](../unified-reasoning-dag/spec.md)（P2-1.x 可溯源推理链，R4 红线：有真实锚点必须线程化、无锚点留空）。
**基线 checkpoint**：`after_b_impurity`（B-外置 / B-judge-API / B-impurity 已迁移，757 例）。红线锚点 `base_p0`。

## 1. 问题

统一推理链（父 spec R4）要求：网关生产者命中真实规则/知识时，必须在 `EngineResponse.ReasoningNodes` 中携带 `KnowledgeId`/`RuleId`；无真实锚点则留空。B 类审计（[data-defect-register.md §N.2](../../../data-defect-register.md) L617）确认 `LlmGatewayService.MultiPartDispatch.cs` 的 `TryDispatchMultiPartAsync` 在聚合多部分大题各子问结论时，**只取 `subResp.FinalResult`（L44），丢弃子响应本体**——父响应（L54-64 构造）未设置 `ReasoningNodes`。

后果：当一条多部分大题经分发链路被全部子问解出并聚合产出父结论时，父响应的推理链为空，无法溯源到各子问命中的知识/规则。这与父 spec「父级聚合结论必须可溯源到子级真实锚点」的目标不符（在子求解器已迁移、子响应已带节点的前提下，此处是纯传播缺口）。

## 2. 目标

1. 把各子问 `subResp.ReasoningNodes` 聚合进父响应 `ReasoningNodes`：子节点重排为从 1 连续的 `Step`、标注子问归属；末尾追加 1 个汇总节点（`Conclusion` = 聚合 `FinalResult`，`Premises` = 去重后的子节点非空 `KnowledgeId`）。
2. **R4 安全**：全部子问均无节点时，父响应 `ReasoningNodes` 保持为空（不臆造汇总节点）；部分子有节点时仅传播有节点的子，空子不产生节点。
3. **零行为漂移**：`Ok` / `FinalResult` / `DerivationSteps` / `ConstraintNote` / `Confidence` / 所有 return-null 路径 / `TaskType` 语义与基线字节级一致；唯一新增的是 `ReasoningNodes` 字段赋值。
4. **可测**：合并逻辑抽为纯函数，单测直接验证传播正确性与 R4 安全（因 eval 生产路径不触发，见 §5）。

## 3. 非目标

- 不改 `LlmGatewayService.cs` 主体的 66 处 `new EngineResponse`（那是混合 B 类、表面积最大、最后做，见 register L616）；本批**仅**改 `LlmGatewayService.MultiPartDispatch.cs` 这一个文件（加新增测试）。
- 不改 `BuildGatewayAnswerAsync` / 采纳门 / AND 判分 / `IsAnswerCorrect`（仅加性元数据，不影响判分与采纳）。
- 不改子求解器（子响应节点已由前几批迁移就位；本批只消费、不生产子节点）。
- 不新增化学事实、不改任何 JSON 数据文件（本批是纯传播，无新真值）。
- 不改变 `TaskType` 赋值位置（仍由 `ProcessStudentMessageAsync` L117 在分发成功时设 `multi_part`）。
- 不提交 commit（除非用户明确要求）。

## 4. 约束与红线（必须全部守住）

- `precision=1`、`harm=0`、`LOST=0`、`WORSEN=0`（离线 eval diff 全零）。
- **R4**：无真实锚点不臆造节点——子全空 → 父空；不得为无子节点的多部分题硬造汇总节点。
- **仅加性**：不删改 `Ok` / `FinalResult` / `DerivationSteps` / `ConstraintNote` / `Confidence` / return-null 路径；聚合 `FinalResult` 的字符串构造逻辑字节级不变。
- **CS8601 教训**（b-impurity 批）：`EngineResponse.ReasoningNodes` 为非可空 `List<ReasoningNode>`（默认 `new()`），直接赋合并结果列表（空列表即可），**不得赋 null**。
- 不蒙数：分发的「任一子问未解 → 整体 null」铁律不变（本批不触碰该门控）。
- Windows PowerShell 5（无 `&&`/`||`）；Python 路径 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`；`_evalscratch/` gitignore。

## 5. 基线事实（已一手核实，非盲信登记）

- **`multi_part` 在 eval 中计数为 0（运行时探针实证，非盲信 P1-2 登记）**：解析 `_evalscratch/checkpoint_after_b_impurity.jsonl`（757 例，Production、LLM 关闭）→ `taskType == "multi_part"` 的案例数 = **0**；`gatewayReasoningNodesCount` 分布 = `{0:667, 1:69, 2:18, 3:1, 10:1, 11:1}`。这与 P1-2 review（`after_p12`，item 9d）实证一致，且是 P1-2 AC-4 校准为 ≥0 的**预期结果**：27 道 K.1 多部分大题中 0 道能由现有求解器全覆盖子问（阻塞子问为自由文本解释 / 图像依赖 / 其他计算域），故 `TryDispatchMultiPartAsync` 在 eval 中恒返回 null，**生产路径不被触发**。
  - **直接含义**：本批对 eval 是**平凡零影响**——`after_b_impurity → after_b_mpdispatch` 的 `diff_ckpt` 将全零（GAINED/LOST/WORSEN/harm 均 0），`multi_part` 计数仍为 0，无任何案例的 `gatewayReasoningNodesCount` 发生变化。传播正确性**不能靠 eval 证明，只能靠单测**（AC8/AC9 仍强制跑 eval 与覆盖率自检，用于证明本批未意外波及主链）。
- **投影/落盘链路确认**（读源码核实）：
  - `ProcessStudentMessageAsync` L113-118：`dispatched` 非空时 `engineResponse = dispatched` 并设 `engineRequest.TaskType = multi_part`；最终 `GatewayResult.EngineResponse = engineResponse`（L136）。
  - 评估侧 [GatewayVsLlmEvaluationService.cs:212](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs)：`result.GatewayReasoningNodesCount = gwResult.EngineResponse.ReasoningNodes?.Count ?? 0;` → 父响应节点会落盘 checkpoint。
  - 学生面投影 [GatewaySolveProjection.cs:75](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs)：`view.ReasoningNodes = resp.ReasoningNodes;`（仅 `Confirmed` 时下发）→ 父响应节点会透传前端。
  - 故**只要给 MultiPartDispatch 返回的 `EngineResponse` 赋上 `ReasoningNodes`，节点即贯通 checkpoint 与前端视图**（当分发路径被命中时）。
- **有物可传**：`CallEngineAsync`（[LlmGatewayService.cs:379](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 起）是各求解器 `return Task.FromResult(solverResponse)` 的级联，**无中央剥离 `ReasoningNodes` 的逻辑**。子响应若带节点则原样保留（前几批已让 MultiSubCorrectness / JudgmentStatementAggregator / ImpurityTrap / HydrolysisPh / Vsepr / AtomicStructure / ConceptFact 等求解器命中路径产节点）。
- **契约**：`EngineResponse.ReasoningNodes` 非可空 `List<ReasoningNode>` 默认 `new()`（[GatewayContracts.cs:159](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs)）；`ReasoningNode` 字段见 [ReasoningContracts.cs:51-80](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs)（`Step` 从 1 连续无跳步 / `Description` / `Premises` / `RuleId?` / `KnowledgeId?` / `Conclusion?` / `EvidenceLevel`）。
- **聚合范式模板**（镜像）：[JudgeApiReasoningNodes.Build](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/JudgeApiReasoningNodes.cs)——遍历有真实 id 的命中 → 每 id 一个节点、`Step` 自增连续；末尾追加 1 个汇总节点（`Conclusion` = 整题 FinalResult，`Premises` = 去重非空 KnowledgeId）；命中全空 → 返回空列表（调用方保持留空，R4）。本批合并器同构。
- **可测性**：`Api.csproj` L31 已设 `<InternalsVisibleTo Include="CogniThink.TutorBrain.Tests" />` → 合并器可声明为 `internal static`，测试程序集直接调用，无需走私有方法反射。
- **既有测试**：[MultiPartUtilityTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartUtilityTests.cs)（测 `MultiPartUtility` 检测/切分）、[MultiPartAndScoringTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartAndScoringTests.cs)（测 `AllKeywordsHit` AND 判分）——均不涉及 `ReasoningNodes`，本批加性改动不破坏它们（前提是行为字节级不变）。

## 6. 验收标准（Acceptance Criteria）

> 类型仅 `rule`（客观可验二元）或 `rubric`（评分维度）。

- **AC1 (rule)**：传播正确性（全子带节点）。构造一份多部分聚合输入（≥2 个子问，每个子响应均带 ≥1 个 `ReasoningNode`，含 `KnowledgeId`/`EvidenceLevel`/`Conclusion`），合并后父响应 `ReasoningNodes`：① 包含全部子节点（总数 = 各子节点数之和）；② `Step` 从 1 连续重排、无跳步无重复；③ 每个子节点可归属其子问序号；④ 末尾恰好 **1** 个汇总节点，其 `Conclusion` == 聚合 `FinalResult`、`Premises` == 去重后的全部非空 `KnowledgeId` 集合。证据：单元测试逐项断言。
- **AC2 (rule)**：R4 全空安全。全部子响应 `ReasoningNodes` 为空（Count==0）时，合并结果为空列表（Count==0，非 null），**不**追加汇总节点（不臆造）。证据：单元测试 `Assert.Empty`。
- **AC3 (rule)**：R4 部分空。部分子带节点、部分子为空时，仅带节点的子贡献节点 + 末尾汇总；空子不产生任何节点（不臆造）；汇总节点 `Premises` 仅含实际存在的非空 `KnowledgeId`。证据：单元测试断言空子贡献 0 节点。
- **AC4 (rule)**：Step 连续不变量。合并后节点 `Step` 恰为 `1..K`（K = 子节点总数 + 1 汇总），严格单调、无跳步、无重复。证据：单元测试对 `Steps` 做 `Assert.Equal(Enumerable.Range(1, K), nodes.Select(n=>n.Step))`。
- **AC5 (rule)**：零行为漂移。`Ok` / `FinalResult` / `DerivationSteps` / `ConstraintNote` / `Confidence` / 所有 return-null 路径 / `TaskType` 语义与基线字节级一致；**唯一**新增是 `ReasoningNodes` 赋值；聚合 `FinalResult` 的 `"(idx)result "` 构造逻辑未改。证据：代码审查 + 既有 `MultiPartUtilityTests` / `MultiPartAndScoringTests` 全过 + 单测断言聚合 `FinalResult` 与原逻辑等价。
- **AC6 (rule)**：`dotnet build` 0 error；本批改动/新增文件（`LlmGatewayService.MultiPartDispatch.cs` + 新测试文件，及合并器若独立成文件则计入）在 warning 中命中 0 次（0 新增 warning；基线预存 warning 与 NETSDK1206 环境 warning 不计）。证据：build 输出 + grep。
- **AC7 (rule)**：全量 `dotnet test CogniThink.Chem.sln` 0 failed、0 新增跳过（基线 2309 passed 维持：Chem 913 + TutorBrain 1396）。证据：`dotnet test` 汇总行。
- **AC8 (rule)**：离线 eval 新 checkpoint（tag `after_b_mpdispatch`）对 `after_b_impurity` 的 `diff_ckpt`：`GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`；对 `base_p0` 的 `diff_ckpt`：`LOST=0 / WORSEN=0 / harm=0 → PASS`（`GAINED` 为跨批次累计，非本批）。证据：`diff_ckpt.py` 输出。
- **AC9 (rule)**：覆盖率自检（`_evalscratch/verify/b_mpdispatch_coverage.py`，gitignored）：因 `multi_part=0`（§5 实证），断言 `after_b_impurity → after_b_mpdispatch` 的 `gatewayReasoningNodesCount` 变化案例集合为空（`added == ∅`、`removed == ∅`）；`taskType==multi_part` 计数仍为 0；`correct` / `adopted` / `harm` 不变。输出 `ALL ASSERTIONS PASSED`（防本批意外波及主链节点计数）。证据：脚本输出。
- **AC10 (rule)**：[data-defect-register.md §N.2](../../../data-defect-register.md) 与父 [tasks.md](../unified-reasoning-dag/tasks.md) 更新：`LlmGatewayService.MultiPartDispatch.cs` 行（L617）标记 ✅ 已迁移（B-MpDispatch 批）→ 分级 `A·done`；分级统计 B 由 1 核减为 **0**、A·done 由 10 增至 **11**；建议执行顺序 (5) 中 MultiPartDispatch 部分标完成（注意 (5) 仍含 `LlmGatewayService` 主体迁移，不在本批）。证据：文件 diff。
- **AC11 (rubric)**：代码质量。合并逻辑抽为纯 `internal static` 函数（镜像 `JudgeApiReasoningNodes`），可独立单测、无副作用；子节点原样克隆（仅重排 `Step` + 标注子问归属，不改 `KnowledgeId`/`RuleId`/`Conclusion`/`EvidenceLevel`）；汇总节点仅在存在子节点时追加；无化学事实硬编码、无密钥。评分 1-5（1=逻辑内联无法单测或有臆造；3=可单测但子节点字段被改写；5=纯函数、子节点原样克隆、R4 安全、无硬编码），通过阈值 ≥ 4。证据：代码 + 单测 + grep。

## 7. 开放问题

- [x] **eval 命中为 0 时 AC 如何定**：已查实 `multi_part=0` 是 P1-2 AC-4 校准 ≥0 的预期结果（0/27 子问可全覆盖），非缺陷。本批传播正确性靠单测（AC1-AC4）保证；eval 双 diff（AC8）与覆盖率自检（AC9）用于证明零波及主链，其全零为平凡成立、可接受。覆盖率断言兼容 0 added（`added == ∅`）。
- [ ] 是否需要端到端集成测试（经 `ProcessStudentMessageAsync` 用真实多部分题干触发分发并断言父节点）？Task 1 先探测是否存在「整题 `Ok!=true` 且各子问均可解出带节点」的干净题干；若有则补 1 条集成测试，若无则以合并器单测 + 接线一行审查为准（不阻塞）。
