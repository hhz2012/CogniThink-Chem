# B-Impurity 批：把 ImpurityTrapSolver 的硬编码化学事实外置为 JSON 并线程化 ReasoningNodes

**父规格**：[unified-reasoning-dag/spec.md](../unified-reasoning-dag/spec.md)（P2-1.x 可溯源推理链，R4 红线）。
**基线 checkpoint**：`after_b_judge`（Tier 1/2 + B-外置 + B-judge-API 已迁移，757 例）。红线锚点 `base_p0`。

## 1. 问题

统一推理链（父 spec R4）要求：网关生产者命中真实规则/知识时，必须在 `EngineResponse.ReasoningNodes` 中携带 `KnowledgeId`/`RuleId`；无真实锚点则留空。B 类审计（见 [data-defect-register.md §N.2](../../../data-defect-register.md) L622）确认 `ImpurityTrapSolver` 仍有 **1 条化学真值硬编码在 C# 字面量**中，没有可线程化的 id，因而 `ReasoningNodes` 留空，且违反「化学事实只存 JSON」的项目红线。

当前硬编码（[ImpurityTrapSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ImpurityTrapSolver.cs)）：
- 触发：stem 含（`高锰酸钾`/`KMnO4`）+ `乙烯` +（`除`/`除去`/`去除`）
- 结论载荷（化学真值）：`FinalResult`「不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。」、3 条 `DerivationSteps`、`ConstraintNote`、`ErrorTags=["E04"]`

## 2. 目标

1. 把上述化学真值载荷移出 C#、写入带稳定 `id` 的 JSON 数据文件 `data/knowledge/impurity-traps.json`。
2. 命中时线程化 `KnowledgeId = entry.Id` 到 `ReasoningNodes`（`Conclusion == FinalResult`，`EvidenceLevel = KnowledgeFact`），使基线中 `gatewayReasoningNodesCount == 0` 的命中案例 `d9-1` 变为 > 0。
3. **零行为漂移**：`FinalResult` / `DerivationSteps` / `ConstraintNote` / `ErrorTags` / `Ok` / `Confidence` / 所有 return-null 路径与基线字节级一致。
4. **R4 安全**：无命中路径保持 `ReasoningNodes` 为空，不臆造节点；不放宽任何触发条件（不蒙数）。

## 3. 非目标

- 不改 `LlmGatewayService` 的两个分发点（L835 / L900）——它们仅消费 `EngineResponse`，加性字段不破坏调用方。
- 不改 `BuildGatewayAnswerAsync`、`Ok`、`FinalResult`、`TaskType` 的语义（仅加性元数据）。
- 不改任何**既有** JSON 数据文件——只**新增** `data/knowledge/impurity-traps.json`。
- 不引入新的命中路径、不放宽触发条件（不蒙数）。
- 不改 `CogniThink.Chem.Core`（`DataRoot` 不动）；加载沿用 `ProductWriterSolver` 的 `Lazy<>` + `DataRoot.Resolve()` 直读模式。
- 不提交 commit（除非用户明确要求）。

## 4. 约束与红线（必须全部守住）

- `precision=1`、`harm=0`、`LOST=0`、`WORSEN=0`（离线 eval diff 全零）。
- 化学事实**只存 JSON**：迁移后 `ImpurityTrapSolver.cs` 中不再残留化学真值载荷字面量（grep 可验，见 AC1）；触发识别 token（`高锰酸钾`/`KMnO4`/`乙烯`/`除`）属识别逻辑，允许保留（B-外置批 AC1 先例）。
- 触发识别逻辑保持字节级等价（避免误判/漏判导致精度下降）。
- 不蒙数：无锚点 → 不发节点；域外 → 返回 null。
- 仅加性：不删除/重排已有 `DerivationSteps`，不改 `FinalResult` 文本。
- Windows PowerShell 5：无 `&&`/`||`；Python 路径 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`；`_evalscratch/` gitignore。

## 5. 基线事实（已一手核实，非盲信登记）

- **数据加载**：`Api.csproj` 以 `data\**\*.json` + `CopyToOutputDirectory=PreserveNewest` 全局拷贝；`DataRoot.Resolve()` 运行时定位数据根。新增 JSON 自动到达 test/eval 输出，**无需改 csproj**（B-外置批已验证 `data/knowledge/` 可达）。
- **参考模式**：[ProductWriterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ProductWriterSolver.cs)（私有 entry 类 + `ProductRulesData` + `Lazy<>` + `DataRoot.Resolve()` 直读 `data/knowledge/product-rules.json` + try/catch 返回空 + 命中线程化 `KnowledgeId`，`EvidenceLevel.KnowledgeFact`）。
- **基线命中案例（运行时实证，取自 `checkpoint_after_b_judge.jsonl`）**：grep 特征结论 `引入新杂质`/`酸性高锰酸钾` 于 757 例 → **恰好 1 例** = `d9-1`，当前 `gatewayReasoningNodesCount = 0`，`gatewayConclusion` 与硬编码 `FinalResult` 字节一致。迁移后**唯一应 0→1 的集合 = {d9-1}**。（不盲信台账「仅 d9-1」，已 checkpoint 实证；吸取上一批 pdf-119 探测误判教训。）
- **域外案例保持 null（R4）**：现有测试已覆盖——`乙烯使酸性高锰酸钾褪色`（无除杂动作）、`溴水除甲烷中乙烯`（无 KMnO4）、`KMnO4 受热分解方程`（无乙烯+除杂）。这些保持 return-null、`ReasoningNodes` 天然缺席。
- **既有测试**：[Stage3ESolversTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/Stage3ESolversTests.cs) 的 4 个 ImpurityTrap 测试只断言 `Ok`/`FinalResult`/`Contains`，**无任何 `ReasoningNodes` 断言**（grep 确认 0 命中）——加性节点不破坏它们，前提是输出字节级不变。

## 6. 验收标准（Acceptance Criteria）

> 类型仅 `rule`（客观可验二元）或 `rubric`（评分维度）。

- **AC1 (rule)**：`ImpurityTrapSolver` 的化学真值载荷（`FinalResult` / `DerivationSteps` / `ConstraintNote` / `ErrorTags`）仅存于 `data/knowledge/impurity-traps.json`；`.cs` 中 grep **真值特征子串**（`氧化生成 CO₂` / `引入新杂质` / `应用溴水` / `1,2-二溴乙烷` / `达不到除杂目的` / `强氧化性`）命中数为 **0**。JSON 含 `version`/`source` 元数据与每条的稳定 `id`。触发识别 token（`高锰酸钾`/`KMnO4`/`乙烯`/`除`）允许保留在识别逻辑中。证据：grep 输出 0 + JSON 文件存在。
- **AC2 (rule)**：`d9-1` 命中案例（取自 `BuildTestCases()` 真实题干/eval stem）的 `FinalResult` 与基线**字节级相等**，且 `ReasoningNodes.Count == 1`、该节点 `KnowledgeId` 非空且等于 JSON 条目 `id`、`Conclusion == FinalResult`、`EvidenceLevel == KnowledgeFact`。证据：单元测试 `ImpurityTrapExternalizationTests` 逐项断言。
- **AC3 (rule)**：3 个域外案例（`乙烯使酸性高锰酸钾褪色` / `溴水除甲烷中乙烯` / `KMnO4 受热分解方程`）`TrySolve` 仍返回 `null`（`ReasoningNodes` 天然缺席，不臆造）。证据：单元测试 `Assert.Null`。
- **AC4 (rule)**：既有测试 `Stage3ESolversTests` 全部通过，全量 `dotnet test CogniThink.Chem.sln` 0 failed、0 新增跳过。证据：`dotnet test` 汇总行。
- **AC5 (rule)**：`dotnet build` 0 error；本批改动/新增文件（`ImpurityTrapSolver.cs` + 新 JSON + 新测试文件）在 warning 中命中 0 次（0 新增 warning；基线预存 warning 与 NETSDK1206 环境 warning 不计）。证据：build 输出 + grep。
- **AC6 (rule)**：离线 eval 新 checkpoint（tag `after_b_impurity`）对 `after_b_judge` 的 `diff_ckpt`：`GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`。
- **AC7 (rule)**：`diff_ckpt base_p0 after_b_impurity`：`LOST=0 / WORSEN=0 / harm=0 → PASS`（`GAINED=6` 为跨批次累计、非本批，可接受）。
- **AC8 (rule)**：覆盖率自检（`_evalscratch/verify/b_impurity_coverage.py`，gitignored）：相对 `after_b_judge`，获得非空节点的案例集合**恰好等于** `{d9-1}`；无案例丢失节点；无预期外案例获得节点（无臆造）；`d9-1` 的 `gatewayReasoningNodesCount` 从 0 变为 1；`d9-1` 的 `gatewayConclusion` 字节级不变；`correct`/`adopted`/`harm` 不变。输出 `ALL ASSERTIONS PASSED`。
- **AC9 (rubric)**：新增 JSON 文件质量——含 `version`/`source` 元数据、稳定唯一且语义化的 `id`、结构与 `known-equations.json`/`product-rules.json` 一致；无化学事实泄漏回 C#。评分 0-2（0=缺元数据或结构混乱；1=元数据齐全但 id 不语义化；2=元数据齐全、id 稳定语义化、结构一致），通过阈值 ≥ 1。证据：JSON 文件 + grep。
- **AC10 (rule)**：[data-defect-register.md §N.2](../../../data-defect-register.md) 与父 [tasks.md](../unified-reasoning-dag/tasks.md) 更新：`ImpurityTrapSolver` 行标记为已迁移（B-Impurity 批，A·done），记录 eval tag `after_b_impurity` 与 `d9-1` 翻转证据；分级统计 B 由 2 核减为 1（仅剩 `MultiPartDispatch` 传播）。证据：文件 diff。
