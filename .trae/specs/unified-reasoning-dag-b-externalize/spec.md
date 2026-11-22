# B-外置批：把 4 个求解器的硬编码化学事实外置为 JSON 并线程化 ReasoningNodes

**父规格**：[unified-reasoning-dag/spec.md](../unified-reasoning-dag/spec.md)（P2-1.x 可溯源推理链，R4 红线）。
**基线 checkpoint**：`after_t2_ph`（Tier 1 + Tier 2 已迁移，757 例）。

## 1. 问题

统一推理链（spec R4）要求：网关生产者命中真实规则/知识时，必须在 `EngineResponse.ReasoningNodes` 中携带 `KnowledgeId`/`RuleId`；无真实锚点则留空。Tier 1/Tier 2 已迁移 3 个**已有 JSON 锚点**的求解器（CrystalCellValidator / HalfReactionBalancer / HydrolysisPhCalculator）。

B 类审计（见 [data-defect-register.md §N.2](../../../data-defect-register.md)）确认另有 4 个求解器的**化学真值硬编码在 C# 字面量中**，没有可线程化的 id，因而 `ReasoningNodes` 留空：

| 求解器 | 硬编码事实 |
|---|---|
| `KnownEquationWriterSolver` | GaN 强碱离子方程、实验室制 SO2 分子方程（各含方程/推导/约束文本） |
| `ProductWriterSolver` | 6 条少量/过量产物骨架（FeBr2×2、NaAlO2×2、Ca(HCO3)2、NaCN） |
| `RedoxGatewaySolver` | R2 还原产物表（MnO4×3/ClO/Cr2O7/NO3×2/浓H2SO4）、R2 电解阳极产物、P3 可行性规则×2；**R1 角色识别由 Core 计算，无表、无锚点** |
| `ConceptVerdictSolver` | SiO2 碱性氧化物断言 → ×（正则 + 判决 + 推导） |

这些事实目前是 C# `const string` / 内联字面量，违反「化学事实只存 JSON」的项目红线，也使 R4 可溯源无法落地。

## 2. 目标

1. 把上述 4 个求解器的**全部化学真值**移出 C#、写入带稳定 `id` 的 JSON 数据文件。
2. 命中时线程化 `KnowledgeId = entry.Id` 到 `ReasoningNodes`（`Conclusion == FinalResult`，`EvidenceLevel` 恰当），使基线中 `gatewayReasoningNodesCount == 0` 的命中案例变为 > 0。
3. **零行为漂移**：`FinalResult` / `DerivationSteps` / `ConstraintNote` / `Ok` / `Confidence` / 所有 return-null 路径与基线字节级一致。
4. **R4 安全**：无真实锚点的路径（含 RedoxGateway R1）保持 `ReasoningNodes` 为空，不臆造节点。

## 3. 非目标

- 不改 `BuildGatewayAnswerAsync`、`Ok`、`FinalResult`、`TaskType` 的语义（仅加性元数据）。
- 不改任何**既有** JSON 数据文件（concept-facts.json / acid-constants.json 等）——只**新增**文件。
- 不把 RedoxGateway R1（Core 计算的氧化态角色）强行挂知识锚点；R1 保持节点为空。
- 不引入新的命中路径、不放宽任何触发条件（不蒙数）。
- 不改 `CogniThink.Chem.Core`（RuleStore/DataRoot 不动）；加载沿用 HalfReactionBalancer 的直读模式。

## 4. 约束与红线（必须全部守住）

- `precision=1`、`harm=0`、`LOST=0`、`WORSEN=0`（离线 eval diff 全零）。
- 化学事实**只存 JSON**：迁移后 4 个 `.cs` 中不再残留方程/产物/判决等化学真值字面量（grep 可验）。
- 触发识别逻辑保持字节级等价（避免误判/漏判导致精度下降）。
- 不蒙数：无锚点 → 不发节点；域外 → 返回 null。
- 仅加性：不删除/重排已有 `DerivationSteps`，不改 `FinalResult` 文本。
- Windows PowerShell 5：无 `&&`/`||`；Python 路径 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`；`_evalscratch/` gitignore。
- 不提交（除非用户明确要求）。

## 5. 基线事实（已一手核实）

- 数据加载：`Api.csproj` L25 以 `data\**\*.json` + `CopyToOutputDirectory=PreserveNewest` 全局拷贝；`DataRoot.Resolve()` 运行时定位数据根。新增 JSON 文件自动到达 test/eval 输出，**无需改 csproj**。`data/knowledge/` 已被 Tier 1 HalfReaction 验证可达。
- 参考模式：`HalfReactionBalancer.cs`（直读 `data/knowledge/electrode-half-reactions.json` + 私有 entry 类 + `Lazy<>` + 线程化 `KnowledgeId`，`EvidenceLevel.KnowledgeFact`）；`ConceptFactSolver.cs`（RuleStore 读 `data/highschool/concept-facts.json`，`match.Id` → `KnowledgeId`，`KnowledgeFact`）。
- 基线命中案例（`after_t2_ph`，当前 `nodes=0`）——迁移后**唯一应 0→1 的集合**：
  - KnownEquation：`pdf-539`（GaN）、`pdf-456`（SO2）
  - ProductWriter：`d1-1` `d1-2` `d1-3` `d1-4` `d1-5` `pdf-044` `pdf-515`
  - Redox R2 还原产物：`d2-4`；Redox P3：`trap-limit-1` `d8-5`（R2 电解在 eval 中 0 覆盖，仍外置并单测覆盖）
  - ConceptVerdict：`pdf-467`
  - 合计 **13 例**
- **必须保持 0**（R4，无锚点）：Redox R1 `redox-1` `redox-2` `redox-3`（Core OxidationStateAnalyzer 计算）。
- 既有测试已覆盖全部命中/域外案例（`Stage3ESolversTests`、`RedoxGatewaySolverTests`、`ClassificationDeterministicTests`），且**无任何测试断言 `ReasoningNodes` 为空**——加性节点不破坏它们，前提是输出字节级不变。

## 6. 验收标准（Acceptance Criteria）

> 类型仅 `rule`（客观可验二元）或 `rubric`（评分维度）。

- **AC1 (rule)**：`KnownEquationWriterSolver` 的 2 条方程及推导/约束文本仅存于 `data/knowledge/known-equations.json`；`.cs` 中 grep **事实特征子串**（`GaN + OH`、`Na2SO3 + H2SO4 =`、`四羟基合镓(III)酸根`、`强酸制弱酸`、`固液不加热制`）命中数为 0（触发识别 token 如 `text.Contains("GaN")`/`"产生SO2"` 属识别逻辑，允许保留）。JSON 含每条的稳定 `id`。
- **AC2 (rule)**：`ProductWriterSolver` 的 6 条产物骨架仅存于 `data/knowledge/product-rules.json`；`.cs` 匹配逻辑中不再出现骨架物种字面量（`Fe^2+`/`Al(OH)3`/`CaCO3`/`HCN` 等）。JSON 每条含稳定 `id`。
- **AC3 (rule)**：`RedoxGatewaySolver` 的 R2 还原产物表、R2 电解阳极表、P3 规则仅存于 `data/knowledge/redox-rules.json`；对应 C# switch/return 字面量移除。R1 不引入 JSON 表、不发节点。JSON 每条含稳定 `id`。
- **AC4 (rule)**：`ConceptVerdictSolver` 的 SiO2 判决（正则模式 + `×` + 推导/约束）仅存于 `data/highschool/concept-verdicts.json`；`.cs` 不再残留 `SiO2`/`碱性氧化物`/`×` 等化学真值字面量。JSON 含稳定 `id`。
- **AC5 (rule)**：13 个命中案例（取自 `BuildTestCases()` 真实题干）的 `FinalResult` 与基线**字节级相等**，且 `ReasoningNodes.Count >= 1`、首节点 `KnowledgeId` 非空。证据：单元测试逐项断言 `Assert.Equal(expectedFinalResult, resp.FinalResult)` + 节点存在。
- **AC6 (rule)**：Redox R1 的 3 个命中案例（`redox-1/2/3` 题干）`FinalResult` 字节级不变且 `ReasoningNodes` 为空（null 或 Count==0）。证据：单元测试。
- **AC7 (rule)**：既有测试 `Stage3ESolversTests` / `RedoxGatewaySolverTests` / `ClassificationDeterministicTests` 全部通过，0 失败、0 跳过新增。证据：`dotnet test`。
- **AC8 (rule)**：`dotnet build` 0 error；warning 数不高于基线（本批次 0 新增 warning）。证据：build 输出。
- **AC9 (rule)**：离线 eval 新 checkpoint（tag `after_b_ext`）对 `after_t2_ph` 的 `diff_ckpt`：`GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → PASS`。
- **AC10 (rule)**：覆盖率自检：相对 `after_t2_ph`，获得非空节点的案例集合**恰好等于** 13 个预期案例；无案例丢失节点；无预期外案例获得节点（无臆造）；R1 3 例保持 0。证据：覆盖率脚本输出 `ALL ASSERTIONS PASSED`。
- **AC11 (rubric)**：新增 JSON 文件质量——均含 `version`/`source` 元数据、稳定唯一 `id`、结构清晰；无化学事实泄漏回 C#。评分 0-2（0=缺元数据或结构混乱；1=元数据齐全但 id 命名不统一；2=元数据齐全、id 稳定且语义化），通过阈值 ≥ 1。证据：JSON 文件 + grep。
- **AC12 (rule)**：[data-defect-register.md §N.2](../../../data-defect-register.md) 与父 [tasks.md](../unified-reasoning-dag/tasks.md) 更新：4 个求解器标记为已迁移（B-外置批），记录 eval tag 与 13 例翻转证据；分级统计 B 数相应核减。证据：文件 diff。
