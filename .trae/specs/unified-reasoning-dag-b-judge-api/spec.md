# B-judge-API 批：扩展判官裁决契约暴露命中 id，线程化 2 个聚合器的 ReasoningNodes

**父规格**：[unified-reasoning-dag/spec.md](../unified-reasoning-dag/spec.md)（P2-1.x 可溯源推理链，R4 红线）。
**基线 checkpoint**：`after_b_ext`（B-外置批完成，757 例，diff 相对 `after_t2_ph` 全零；恰 13 例推理节点 0→1、R1 3 例保持 0）。
**本批性质**：契约扩展 + 可溯源线程化 + 1 处硬编码化学事实外置。结构卫生，预期 eval 正确性增益 = 0。

## 1. 问题

父 spec R4 要求：网关生产者命中真实规则/知识时，必须在 `EngineResponse.ReasoningNodes` 携带 `KnowledgeId`/`RuleId`；无真实锚点则留空，禁止假可溯源。

[data-defect-register.md §N.2](../../../data-defect-register.md) 审计确认，仍有 **2 个判官-API 聚合器** 的子判决真值来自判官，但**判官裁决契约不暴露命中 id**，导致这两个求解器命中时 `ReasoningNodes` 只能留空：

| 求解器 | 真值源 | 阻塞点 |
|---|---|---|
| `MultiSubCorrectnessSolver` | 逐小题 `EquationCorrectnessJudge.Judge` → 仅取 `verdict.Reasons` | `Verdict` record 不携带命中 id |
| `JudgmentStatementAggregator` | 方程小题 `EquationCorrectnessJudge`；概念小题 `CompositeStatementJudge`（FactBase/Counting/RedoxElectron） | `IStatementJudge.Judge` 只返 `Verdict` 枚举，`FactChoiceMatcher.JudgeStatement` 丢弃了 `FactStatement.Id` |

其中 `EquationCorrectnessJudge` 的 `×` 判决有两个来源：① `ChemicalCheckerService.Check` 的引擎检查项（`RiskTags`，**无 data/rules 条目 id**，属 `EngineDerived`）；② `CorrectnessContextRules` 的 **2 条硬编码语境规则**（Rule A 酸性 NO₃⁻ 氧化亚硫酸根生成 BaSO₄；Rule B 亚铜氨配离子 +1 电荷）——目前规则文本硬编码在 C#，无稳定 id，且违反「化学事实只存 JSON」红线。

## 2. 目标

1. **加法扩展**判官裁决契约，暴露子判决的真实命中 id：概念陈述走 `FactChoiceMatcher` → 命中事实 `Id`；语境规则外置为 JSON → 稳定 `Id`。**不改** `IStatementJudge.Judge` 原签名、**不改** `EquationCorrectnessJudge.Verdict` 原结构（保证既有消费者零改动）。
2. 把 `CorrectnessContextRules` 的 2 条语境规则外置为带稳定 `id` 的 JSON（`data/knowledge/correctness-context-rules.json`），`.cs` 不再残留化学事实子串。
3. `MultiSubCorrectnessSolver` 与 `JudgmentStatementAggregator` 在子判决携带真实 id 时线程化 `ReasoningNodes`（`KnowledgeId`/`RuleId` + `EvidenceLevel.KnowledgeFact`，Step 连续无跳步，末节点 `Conclusion`==`FinalResult`）；engine-only 子判决不发节点。
4. **零行为漂移**：`FinalResult` / `DerivationSteps` / `ConstraintNote` / `Ok` / `Confidence` / 所有 return-null 路径与基线字节级一致。
5. **R4 安全**：引擎检查项判定的子判决（无真实 id）保持不发节点；`CountingStatementJudge` / `RedoxElectronStatementJudge` 判定的子判决（引擎推导，无暴露 id）不发节点。

## 3. 非目标（明确不做）

- 不改 `IStatementJudge.Judge(string, string)` 原签名、不改 `EquationCorrectnessJudge.Verdict` 原字段（仅新增重载/方法，原方法字节级不变）。
- 不把引擎检查项（`RiskTags`）映射为 `rule_id`——没有对应的 `data/rules` 真实条目，映射即假可溯源，违反 R4。
- 不为 `CountingStatementJudge` / `RedoxElectronStatementJudge` 判定的子判决发节点（引擎推导，当前无暴露 id；其 id 线程化留待后续批次评估是否外置/查表）。
- 不改 `BuildGatewayAnswerAsync`、`Ok`、`FinalResult`、`TaskType`、`Confidence` 的语义（仅加性元数据）。
- 不改任何**既有** JSON 数据文件（仅**新增** `correctness-context-rules.json`）。
- 不引入新的命中路径、不放宽任何触发条件（不蒙数）。
- 不改 `CogniThink.Chem.Core`。
- 不做前端展示（P2-4）。

## 4. 约束与红线（必须全部守住）

- `precision=1`、`harm=0`、`LOST=0`、`WORSEN=0`（离线 eval diff 全零）。
- 化学事实**只存 JSON**：外置后 `CorrectnessContextRules.cs` 不再残留规则特有的化学事实子串（见 AC3 grep 目标）。
- 触发识别逻辑保持字节级等价（避免误判/漏判导致精度下降）；`FactChoiceMatcher.JudgeStatement` 原方法行为字节级不变。
- 不蒙数：无真实 id 命中的子判决 → 不发节点；域外 → 返回 null。
- 仅加性：不删除/重排已有 `DerivationSteps`，不改 `FinalResult` 文本，不改采纳门。
- Windows PowerShell 5：无 `&&`/`||`；Python 路径 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`；`_evalscratch/` gitignore。
- 临时探测文件（`_JudgeApi*Probe.cs`）已删除，不入库。
- 不提交 commit（除非用户明确要求）。

## 5. 基线事实（一手核实，ground truth）

### 5.1 命中案例与当前状态（直接调用求解器，`after_b_ext` 基线）

`MultiSubCorrectnessSolver`（MS）与 `JudgmentStatementAggregator`（JA）在 757 例中命中 **14 个 distinct 案例**，当前**全部 `gatewayReasoningNodesCount=0`**（证据：`_evalscratch/verify/probe_out.txt`，gitignored 留存）：

| 求解器 | 案例 | 当前 FinalResult（基线字节） |
|---|---|---|
| MS+JA | pdf-051 | `(1)√ (2)√ (3)× (4)× (5)×` |
| JA | pdf-119 | `√` |
| JA | pdf-120 | `×` |
| JA | pdf-121 | `√` |
| JA | pdf-122 | `√` |
| JA | pdf-450 | `(1)× (2)× (3)√ (4)× (5)× (6)× (7)× (8)× (9)×` |
| JA | pdf-466 | `√` |
| MS+JA | pdf-468 | `×` |
| JA | pdf-469 | `×` |
| JA | pdf-470 | `√` |
| MS+JA | pdf-471 | `√` |
| MS+JA | pdf-472 | `√` |
| JA | pdf-473 | `×` |
| JA | pdf-480 | `(1)√ (2)× (3)× (4)√ (5)× (6)× (7)× (8)× (9)× (10)√` |

> 调度：`LlmGatewayService.CallJudgment` 中 `MultiSubCorrectnessSolver.TrySolve` 先调（早返回），`JudgmentStatementAggregator.TrySolve` 后调；reaction_check 路径也先调 MS。上表 MS 标记的 4 例（pdf-051/468/471/472）网关实际答案来自 MS，其余来自 JA。**本批两个求解器都要迁移**，确保无论哪条调度路径命中都能发节点。

### 5.2 逐小题 id 分布（`_JudgeApiIdProbe`，已运行并删除探测文件，结果留存 `_evalscratch/verify/probe_id_out.txt`）

对 14 例逐小题判定真值源，统计 `factId`（命中事实库，有 `Id`）/ `ctxId`（命中语境规则，外置后有 `Id`）/ `engineOnly`（仅引擎检查项，无 id）：

```
[pdf-051] subs=5  factId=0  ctxId=2  engineOnly=3  anyRealId=True
[pdf-119] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-120] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-121] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-122] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-450] subs=9  factId=9  ctxId=0  engineOnly=0  anyRealId=True
[pdf-466] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-468] subs=1  factId=0  ctxId=0  engineOnly=1  anyRealId=False
[pdf-469] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-470] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-471] subs=1  factId=0  ctxId=0  engineOnly=1  anyRealId=False
[pdf-472] subs=1  factId=0  ctxId=0  engineOnly=1  anyRealId=False
[pdf-473] subs=1  factId=1  ctxId=0  engineOnly=0  anyRealId=True
[pdf-480] subs=10 factId=10 ctxId=0  engineOnly=0  anyRealId=True
SUMMARY: casesWithFactId=10 casesWithContextId=1 casesWithAnyRealId=11 of 14
```

> ⚠️ **基线探测勘误（实现期核实）**：原 `probe_id_out.txt` 对 pdf-119 误报 `factId=0 / anyRealId=False`——其检测逻辑对该例的 FactBase 命中漏判，返回了全零计数（`engineOnly` 也为 0，说明未正确归类）。实现期直接调用 `JudgmentStatementAggregator.TrySolve` 运行时核实：pdf-119（`工业制高纯硅…SiHCl3…`）真实命中事实库 `JS-016`（match=`工业制高纯硅`+`SiHCl3`，verdict=true→√，reason 经化学核对正确，与题库 answer=√ 对齐），`anyRealId=True`。Task 1 仅加性暴露 id、判定逻辑字节级不变，故该命中在基线 `after_b_ext` 即已存在并驱动 √；本批只是把它线程化为节点。因此**可溯源 = 11 例、R4 留空 = 3 例**（仅 pdf-468/471/472）。

**结论（本批价值上限 + R4 处理的核心依据）：**

- **将获得非空节点的案例 = 11 例**（`casesWithAnyRealId`）：
  - factId 命中 10 例：`pdf-119 / 120 / 121 / 122 / 450 / 466 / 469 / 470 / 473 / 480`（概念陈述命中 `judgment-statement-facts` 等事实库，底层 `FactStatement.Id` 真实存在；pdf-119 命中 `JS-016`，见上方勘误）。
  - ctxId 命中 1 例：`pdf-051`（5 小题中 2 个命中语境规则，外置后得 id；另 3 个 engine-only → 不发节点。故 pdf-051 节点数 < 子数，属部分覆盖，R4 合规）。
- **必须保持节点为空的案例 = 3 例**（R4，无真实 id）：`pdf-468 / 471 / 472`（方程小题仅由引擎检查项判定，无 id）。
- 因此 eval 可见的节点翻转为 **11 例（0→>0）**，**不是 14 例**。任何"恰 11 例翻转 + 3 例保持 0 + 无预期外案例"的偏差都意味着臆造或漏发，必须停下根因。

### 5.3 契约爆炸半径（决定"加法 vs 改签名"选择的依据）

- `IStatementJudge`：实现者 3 个（`FactBaseStatementJudge` / `CountingStatementJudge` / `RedoxElectronStatementJudge`）；消费者 5 个 `.cs`（`CompositeStatementJudge` / `JudgmentStatementAggregator` / `CountingChoiceValidator` / `MultiSelectChoiceValidator` / `LlmGatewayService`）+ 5 个测试文件（含 `StatementJudgeTests` / `RedoxElectronStatementJudgeTests` / `JudgmentStatementFactsTests` 等）。
- `EquationCorrectnessJudge.Judge` / `Verdict`：消费者 3 处（`LlmGatewayService` reaction_check L817、judgment L880；`MultiSubCorrectnessSolver`；`JudgmentStatementAggregator`）。
- → **改原签名会波及 5 消费者 + 5 测试文件 + 2 处网关调用**，回归风险高。本批**强制走加法**：新增返回 id 的重载/方法，原方法签名字节级不变、内部委托新方法丢弃 id，既有调用方零改动。

### 5.4 加载与节点范式（沿用 B-外置批已验证模式）

- 数据加载：`Api.csproj` 以 `data\**\*.json` 全局拷贝到输出，新增 JSON 自动到达 test/eval，无需改 csproj；`RuleStore.Load()` / `DataRoot.Resolve()` 已验证可达 `data/knowledge/`。
- 节点范式（沿用 `ConceptVerdictSolver` / `RedoxGatewaySolver.Respond`）：命中节点 `KnowledgeId`=entry.Id、`EvidenceLevel=KnowledgeFact`；测试公共断言（`ReasoningSolverTraceabilityTests` L12-42）要求：节点非空、Step 连续 1..N、至少一个节点 `KnowledgeId`/`RuleId` 非空、末节点 `Conclusion`==`FinalResult`。
- 聚合器节点建模（本批定）：每个有真实 id 的子小题 → 1 个节点（`KnowledgeId`/`RuleId`=命中 id，`Conclusion`=该子判决文本如 `(2)×`，`EvidenceLevel=KnowledgeFact`，Step 递增）；末尾追加 1 个汇总节点（`Conclusion`=整题 `FinalResult`，`Premises` 引用各命中 id，无独立 knowledge_id/rule_id——属派生汇总而非知识命中，不违反 R4「无命中不臆造知识锚点」的本意；公共断言只要求「至少一个」节点带 id，已被子节点满足）。`engine-only` 子小题**不建节点、不占 Step**。

## 6. 验收标准（Acceptance Criteria）

> 类型仅 `rule`（客观可验二元）或 `rubric`（评分维度）。

- **AC1 (rule)**：`FactChoiceMatcher` 新增返回命中事实 id 的加法重载（如 `JudgeStatementWithIds(stem, statement) → (StatementVerdict, List<string> hitIds)`），原 `JudgeStatement` 签名字节级不变且行为等价（内部委托新重载、丢弃 id）。命中多条且 verdict 冲突时仍返回 `Undecided`（与原逻辑一致）。证据：build 0 error + 既有 `Batch3` choice 测试 / `StatementJudgeTests` 全过。
- **AC2 (rule)**：`FactBaseStatementJudge` 与 `CompositeStatementJudge` 通过**新方法**暴露子判决命中 id（不改 `IStatementJudge.Judge` 原签名）；仅 `FactBaseStatementJudge` 命中的子判决携带非空 id；`CountingStatementJudge` / `RedoxElectronStatementJudge` 判定的子判决不携带 id（返回空 id 集合）。证据：单测断言 FactBase 命中路径 id 非空、Counting/Redox 命中路径 id 为空。
- **AC3 (rule)**：`CorrectnessContextRules` 的 2 条语境规则外置为 `data/knowledge/correctness-context-rules.json`，每条含稳定唯一 `id`（建议 `ccr-acid-nitrate-sulfite` / `ccr-cuprous-ammine-charge`）+ 触发 token/正则 + reason + verdict 方向；`CorrectnessContextRules.cs` 中 grep 规则特有化学事实子串（`BaSO3`、`BaSO4`、`亚铜氨`、`H2SO3`、`Cu(NH3)`、`硝酸根`）**命中数为 0**（触发 token 与正则一并外置，`.cs` 仅保留通用文本归一化 `NormalizeForSearch` 与匹配循环）。`EquationCorrectnessJudge` 命中语境规则时携带该 id；引擎检查项路径不携带。证据：grep 输出 0 + 单测 pdf-051 判决字节不变且携带 id。
- **AC4 (rule)**：`MultiSubCorrectnessSolver` 与 `JudgmentStatementAggregator` 在子判决携带真实 id 时填充 `ReasoningNodes`：每个有 id 子→1 节点（`KnowledgeId`/`RuleId` 非空、`EvidenceLevel=KnowledgeFact`、`Conclusion`=子判决），末尾汇总节点 `Conclusion`==`FinalResult`，Step 从 1 连续无跳步；`engine-only` 子不建节点。`FinalResult` / `DerivationSteps` / `Ok` / `Confidence` 与基线字节级一致。证据：单测 14 例逐项。
- **AC5 (rule)**：14 个命中案例（取自 `BuildTestCases()` 真实题干，直接调用求解器）的 `FinalResult` 与 `after_b_ext` 基线**字节级相等**（期望值见 §5.1 表）。证据：单元测试 `Assert.Equal(expected, resp.FinalResult)` 14 项全过。
- **AC6 (rule)**：覆盖率自检：相对 `after_b_ext`，获得非空 `ReasoningNodes` 的案例集合**恰好等于** 11 例（`pdf-051/119/120/121/122/450/466/469/470/473/480`），且每例至少一个节点 `KnowledgeId`/`RuleId` 非空；`pdf-468/471/472` 保持 `ReasoningNodes` 为空；无预期外案例获得节点。证据：覆盖率脚本输出 `ALL ASSERTIONS PASSED`。
- **AC7 (rule)**：既有测试全过——`StatementJudgeTests` / `RedoxElectronStatementJudgeTests` / `JudgmentStatementFactsTests` / `CountingChoiceValidator` 与 `MultiSelectChoiceValidator` 相关测试 / `Stage3*` 涉及 `IStatementJudge` 与 `EquationCorrectnessJudge` 的测试全部通过；全量 `dotnet test` 0 failed、0 跳过新增。证据：`dotnet test`。
- **AC8 (rule)**：`dotnet build` 0 error；warning 数不高于基线（本批 0 新增 warning）。证据：build 输出。
- **AC9 (rule)**：离线 eval 新 checkpoint（tag `after_b_judge`）对 `after_b_ext` 的 `diff_ckpt`：`GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → PASS`；`correct` 不变。证据：diff 脚本 RED-LINE PASS。
- **AC10 (rubric)**：节点质量——11 例翻转案例的节点 Step 连续无跳步、至少一个节点带真实 id、末节点 `Conclusion`==`FinalResult`；`engine-only` 子判决（如 pdf-051 的 3 个子、pdf-468/471/472 全部）未被臆造节点；`Counting`/`Redox` 判定子（若有）未发节点。评分 0-2（0=有臆造节点或末节点结论不一致；1=节点正确但 Step/描述粗糙；2=全部合规且描述清晰），通过阈值 ≥1。证据：单测 + eval 抽样。
- **AC11 (rule)**：[data-defect-register.md §N.2](../../../data-defect-register.md) 与父 [tasks.md](../unified-reasoning-dag/tasks.md) 更新：`MultiSubCorrectnessSolver` / `JudgmentStatementAggregator` 标记为已迁移（B-judge-API 批），记录 eval tag `after_b_judge`、11 例翻转 + 3 例 R4 留空证据；分级统计：已迁移 A·done 7→**9**，B 4→**2**（仅剩 `ImpurityTrap` 外置 + `MultiPartDispatch` 传播）。证据：文件 diff。

## 7. 风险评估（本批重点）

> 用户要求"评估风险"。以下为本批识别的全部实质风险、发生条件、影响与缓解措施。

### R1 契约爆炸半径导致回归【高】
- **条件**：若改 `IStatementJudge.Judge` 原签名或 `EquationCorrectnessJudge.Verdict` 结构，波及 5 个 `.cs` 消费者 + 5 个测试文件 + 2 处网关调用。
- **影响**：任一调用方适配遗漏 → 编译失败或运行时判决漂移 → harm / LOST。
- **缓解**：强制加法（AC1/AC2）——新重载/新方法，原方法字节级不变、内部委托新方法丢弃 id；既有调用方零改动。AC7 全量测试 + AC9 eval diff 双兜底。

### R2 R4 违反：对 engine-only 子判决臆造节点【高】
- **条件**：实现时若对"凡确定判决（√/×）一律发节点"，会给 3 例 engine-only 案例（pdf-468/471/472）及 pdf-051 的 3 个 engine-only 子发无 id 或假 id 节点。
- **影响**：假可溯源，违反父 spec R4；前端展示伪造的知识锚点。
- **缓解**：节点构造门控在"子判决携带非空真实 id"上（factId 或 ctxId）；engine-only 子不进入节点构造分支。AC6 覆盖率脚本断言 3 例保持 0、pdf-051 带真实 id 节点数 = 2（总节点 3 含汇总）；AC10 rubric 检查无臆造。

### R3 加法重载与原方法判定口径漂移【中】
- **条件**：`JudgeStatementWithIds` 收集 id 的逻辑若干扰 verdict（如命中多条时返回了与原方法不同的 `Undecided` 判定），会影响 choice 瀑布链与判断题正确性。
- **影响**：判断题 / 选择题答案变化 → harm / WORSEN。
- **缓解**：原 `JudgeStatement` 内部直接 `return JudgeStatementWithIds(...).Verdict`（丢弃 id），保证字节级等价；不重写判定逻辑。AC7 既有 `StatementJudgeTests` / `Batch3` 全过 + AC9 eval diff 全零。

### R4 语境规则外置后触发等价性漂移【中】
- **条件**：把 2 条规则的正则/子串从 C# 移到 JSON 后，若匹配口径有任何差异（如 `NormalizeForSearch` 未对 JSON 中的 token 同样归一化、正则转义丢失），pdf-051 的 ctx 判决可能从 × 漂为 √ 或 null。
- **影响**：pdf-051（及任何未来 ctx 案例）FinalResult 变化 → LOST / harm。
- **缓解**：JSON 存触发 token + 正则字符串，C# 从 JSON 编译正则、对 token 走同一 `NormalizeForSearch`；保留原 `HasSulfiteSource` 语义为 JSON match 集合。单测断言 pdf-051 的 5 个子判决符号与基线逐项相等（AC5 覆盖整题 FinalResult，任务级单测额外断言逐子）。AC9 eval diff 全零硬把关。

### R5 eval 可见价值低于 14，预期管理【低（诚实性）】
- **条件**：若误报"14 例全部翻转为有节点"，与实际 11 例不符。
- **影响**：虚报覆盖率，破坏台账可信度。
- **缓解**：spec §5.2 明确写明 11 例上限 + 3 例 R4 留空的依据；AC6 覆盖率脚本硬断言恰 11 例。本批诚实口径：eval 正确性增益 = 0，可溯源覆盖 +11 例（其中 pdf-051 为部分覆盖）。

### R6 化学事实子串残留 .cs（B-外置批前车之鉴）【中】
- **条件**：外置 `CorrectnessContextRules` 时，若只移 reason 文本而把物种/反应事实留在 `.cs`，违反「化学事实只存 JSON」。
- **影响**：红线破损；AC grep 不可满足（若目标设得过宽则误判，过窄则漏检）。
- **缓解**：AC3 明确把触发 token **与**正则一并外置到 JSON，`.cs` 仅留通用归一化 + 匹配循环；grep 目标精确到规则特有物种子串（`BaSO3`/`亚铜氨`/`H2SO3`/`Cu(NH3)`/`硝酸根`），这些是事实/触发一体内容，全部外置；通用归一化（上下标转换）不命中。改完 re-grep 确认。

### R7 节点 Step 连续性 / 末节点 Conclusion 不一致【低】
- **条件**：多小题聚合器为有 id 子建节点时，若 engine-only 子也占 Step 或末节点 Conclusion 未设为整题 FinalResult。
- **影响**：违反公共断言（`ReasoningSolverTraceabilityTests` L31-42）→ 新测试失败；或前端 DAG 展示跳步。
- **缓解**：§5.4 定的节点建模（engine-only 子不占 Step；末尾汇总节点 Conclusion=FinalResult）；AC4/AC10 断言。

### 回退
任一红线破（AC9 diff 非零 / AC6 出现臆造或漏发 / AC7 既有测试失败）→ `git checkout` 本批触及文件回退到 `after_b_ext` 状态；新增 JSON 文件与加性方法为加性，回退不影响基线。

## 8. 诚实口径

- **eval 正确性增益 = 0**（与 B-外置批同为结构卫生）：评估 checkpoint 不读推理节点，采纳门不变；本批只加观测性元数据 + 外置 2 条事实。
- **可溯源覆盖增量 = 11 例**（`gatewayReasoningNodesCount` 0→>0），其中 pdf-051 为部分覆盖（5 子中 2 子有 id）；3 例（pdf-468/471/472）诚实留空（R4，无真实 id）。**不强凑 14 例全覆盖。**
- `CountingStatementJudge` / `RedoxElectronStatementJudge` 判定的子判决本批不发节点（引擎推导无暴露 id），登记为后续批次评估项，不在本批验收内（避免虚报）。
