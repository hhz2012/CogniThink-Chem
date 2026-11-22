# B-judge-API 批 实现任务队列

> 父规格：[spec.md](./spec.md)。基线 checkpoint：`after_b_ext`。
> 状态字段仅取：pending / in_progress / blocked / completed / cancelled。
> 每个任务完成时回填「Completion Evidence」。

## 锁定的回归期望值（AC5，从 `after_b_ext` 一手捕获，迁移后必须字节级相等）

| 求解器 | 案例 | 期望 FinalResult（基线字节） | 迁移后节点 |
|---|---|---|---|
| MS+JA | pdf-051 | `(1)√ (2)√ (3)× (4)× (5)×` | **非空**（5 子中 2 个 ctx 子发节点，3 个 engine 子不发 → 节点数=3：2 个 ctx-id 节点 + 1 个汇总节点） |
| JA | pdf-120 | `×` | 非空 |
| JA | pdf-121 | `√` | 非空 |
| JA | pdf-122 | `√` | 非空 |
| JA | pdf-450 | `(1)× (2)× (3)√ (4)× (5)× (6)× (7)× (8)× (9)×` | 非空（9 子全 factId） |
| JA | pdf-466 | `√` | 非空 |
| JA | pdf-469 | `×` | 非空 |
| JA | pdf-470 | `√` | 非空 |
| JA | pdf-473 | `×` | 非空 |
| JA | pdf-480 | `(1)√ (2)× (3)× (4)√ (5)× (6)× (7)× (8)× (9)× (10)√` | 非空（10 子全 factId） |
| JA | pdf-119 | `√` | **非空**（命中事实库 `JS-016`，见 spec §5.2 勘误） |
| MS+JA | pdf-468 | `×` | **空**（R4，仅引擎检查项判定） |
| MS+JA | pdf-471 | `√` | **空**（R4，仅引擎检查项判定） |
| MS+JA | pdf-472 | `√` | **空**（R4，仅引擎检查项判定） |

> 非空集合 = 11 例（pdf-051/119/120/121/122/450/466/469/470/473/480）；空集合 = 3 例（pdf-468/471/472）。覆盖率脚本（AC6）硬断言两者恰为此集合，无预期外案例获得节点。pdf-119 基线探测曾误判为空，实现期直接运行时核实真实命中事实库 `JS-016`（见 spec §5.2 勘误）。

---

## Task 1: FactChoiceMatcher 加法重载 + FactBase/Composite 暴露命中 id

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC1, AC2
- **Changes**:
  1. `FactChoiceMatcher.cs`：新增 `internal static (StatementVerdict Verdict, List<string> HitIds) JudgeStatementWithIds(string stem, string statement)`，复用现有 `ChoiceFactKeys` 遍历、`NormOpt`/`OptContains` 判定口径；命中事实时收集 `FactStatement.Id`，verdict 冲突时仍返回 `Undecided`（与原逻辑一致，不蒙数）。**原 `JudgeStatement` 改为内部委托**：`return JudgeStatementWithIds(stem, statement).Verdict;`（丢弃 id，字节级行为不变）。
  2. `FactBaseStatementJudge.cs`：新增方法（如 `JudgeWithIds(stem, statement) → (Verdict, List<string>)` 或带 out id）委托 `FactChoiceMatcher.JudgeStatementWithIds`，暴露命中 id；原 `Judge` 不变。
  3. `CompositeStatementJudge.cs`：新增聚合方法暴露命中 id——按原顺序尝试 FactBase/Counting/RedoxElectron，**仅 FactBase 命中时返回非空 id**；Counting/RedoxElectron 命中返回空 id 集合（引擎推导，R4 不发节点）；原 `Judge` 不变。
  4. **不改** `IStatementJudge.Judge` 原签名（3 个实现者的 `Judge` 方法签名字节级不变），保证 5 个消费者 `.cs` + 5 个测试文件零改动。
- **Test Requirements**:
  - (rule) 原 `FactChoiceMatcher.JudgeStatement` 行为字节级不变：既有 `StatementJudgeTests` 与 `Batch3*` choice 相关测试全过（0 新增失败）。
  - (rule) 新重载：取一条命中 `judgment-statement-facts` 的陈述（如 pdf-120 的 core），断言 `HitIds` 含对应事实 `Id`、`Verdict` 与原方法一致；未命中陈述 → `HitIds` 空 + `Undecided`。
  - (rule) verdict 冲突（多条命中且 verdict 不一致）→ 返回 `Undecided` 且不返回部分 id（不蒙数）。
  - (rule) `CompositeStatementJudge` 新方法：FactBase 命中 → id 非空；Counting/RedoxElectron 命中样例 → id 为空集合。
  - (rule) `dotnet build` 0 error、0 新增 warning。
- **Completion Evidence**: ✅ 已完成。`FactChoiceMatcher` 新增 `JudgeStatementWithIds` 加法重载，原 `JudgeStatement` 改为 `=> JudgeStatementWithIds(...).Verdict`（字节级委托，行为不变）；`FactBaseStatementJudge` 新增 `JudgeWithIds`、原 `Judge` 委托；`CompositeStatementJudge` 新增 `JudgeWithIds`（仅 FactBase 命中带 id，Counting/RedoxElectron 命中返回空 id 集合，R4），原 `Judge` 委托。`IStatementJudge.Judge` 原签名未改（3 实现者 + 5 消费者零改动）。新增 `JudgeApiContractTests.cs`（7 测）覆盖：命中带 id（GC-004-B）/未命中空 id/委托等价/FactBase 命中/Composite FactBase 带 id/Composite Counting 命中空 id（R4）/Composite 委托等价。`dotnet test --filter JudgeApiContractTests|StatementJudgeTests`：36 passed / 0 failed。build 0 error、0 新增 warning（7 个预存 warning 均不在改动文件）。

---

## Task 2: CorrectnessContextRules 外置 + EquationCorrectnessJudge 携带 ctx id

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC3
- **Changes**:
  1. 新建 `data/knowledge/correctness-context-rules.json`（`version`/`source` 元数据 + `rules[]`，每条含 `id` / `match[]`（触发子串）/ `regex`（C# 正则字符串，可空）/ `reason` / `verdict`）。逐字拷入 Rule A（酸性 NO₃⁻ 氧化亚硫酸根→BaSO₄）与 Rule B（亚铜氨配离子 +1 电荷）的触发 token、正则、reason 文本。建议 id：`ccr-acid-nitrate-sulfite` / `ccr-cuprous-ammine-charge`。
  2. `CorrectnessContextRules.cs`：`Lazy<>` 或 `RuleStore` 直读 JSON；`Evaluate` 改为遍历 JSON 规则：对 `match[]` token 走同一 `NormalizeForSearch` + `OptContains`/`Contains` 匹配，`regex` 从字符串编译后匹配；命中返回 `(id, reason)`（新返回类型或 out 参数）。**保留**通用 `NormalizeForSearch`（纯文本归一化，非化学事实）；**移除**规则特有的 `HasSulfiteSource` / 物种 token / reason 字面量（全部进 JSON）。
  3. `EquationCorrectnessJudge.cs`：`Verdict` 不改原字段（`Symbol`/`Reasons`）；ctx 命中时把规则 id 带出（新方法 `JudgeWithIds` 或 `Judge` 内部经 `CorrectnessContextRules` 拿到 id，由聚合器读取）。**引擎检查项路径（`FirstCertainError` / `RiskTags`）不携带 id**（无真实 data/rules 条目，R4）。
  4. 原 `CorrectnessContextRules.Evaluate` 调用方（`EquationCorrectnessJudge.Judge`）行为字节级不变：reason 文本与原逐字一致（来自 JSON），pdf-051 判决不变。
- **Test Requirements**:
  - (rule) grep `CorrectnessContextRules.cs`：`BaSO3` / `BaSO4` / `亚铜氨` / `H2SO3` / `Cu(NH3)` / `硝酸根` 命中数 = 0。
  - (rule) pdf-051：用 `MultiSubCorrectnessSolver.SplitSubQuestions` 拆 5 子，逐子断言判决符号与基线相等（`√/√/×/×/×`），其中 2 个 ctx 子携带对应规则 id、3 个 engine 子 id 为空。
  - (rule) 引擎检查项路径：构造一个守恒错误方程（如 `H2 + O2 = H2O` 不配平），断言 `Judge` 返回 `×` 但**无 ctx id**（不臆造）。
  - (rule) `dotnet build` 0 error、0 新增 warning；既有 `EquationCorrectnessJudge` 相关测试全过。
- **Completion Evidence**: ✅ 已完成。新建 `data/knowledge/correctness-context-rules.json`（2 条规则，id=`ccr-acid-nitrate-sulfite`/`ccr-cuprous-ammine-charge`，触发 token/正则/reason 全部外置，reason 文本逐字拷自原 .cs）。`CorrectnessContextRules.cs` 重写为直读 JSON（`Lazy<>` + `DataRoot` 镜像 B-外置批范式），新增 `EvaluateWithId → (id, reason)`，原 `Evaluate` 委托丢弃 id；移除 `HasSulfiteSource` 与所有规则特有化学子串，仅保留通用 `NormalizeForSearch`/`Contains`。`EquationCorrectnessJudge` 新增 `JudgeWithIds → (Verdict, CtxRuleId)`：ctx 命中携带 id，引擎检查项/√/null 路径 CtxRuleId=null（R4）；原 `Judge` 委托丢弃 id，`Verdict` record 结构未改。AC3 grep `CorrectnessContextRules.cs` 目标子串（BaSO3/BaSO4/亚铜氨/H2SO3/Cu(NH3)/硝酸根）命中 0。新增 `CorrectnessContextRulesExternalizationTests.cs`（7 测）：Rule A/B 带 id、无语境无 id+√、引擎 × 无 id（R4）、√ 无 id、null 无 id、pdf-051 五小题逐子仅 sub3/sub5 带 id。`dotnet test --filter CorrectnessContextRulesExternalizationTests|EquationCorrectnessJudgeTests`：50 passed（43 回归 + 7 新）/ 0 failed。build 0 error、0 新增 warning。

---

## Task 3: MultiSubCorrectnessSolver + JudgmentStatementAggregator 线程化 ReasoningNodes

- **Status**: completed
- **Depends On**: Task 1, Task 2
- **Priority**: high
- **覆盖 AC**: AC4, AC5
- **Changes**:
  1. `MultiSubCorrectnessSolver.cs`：`TrySolve` 逐小题调用判官时，取子判决的命中 id（factId 来自 Task 1 的 Composite/FactBase；ctxId 来自 Task 2 的 EquationCorrectnessJudge）。为每个**携带非空真实 id** 的子小题建 1 个 `ReasoningNode`（`KnowledgeId`/`RuleId`=id、`EvidenceLevel=KnowledgeFact`、`Conclusion`=该子判决文本如 `(2)×`、Step 递增）；末尾追加汇总节点（`Conclusion`=整题 `FinalResult`、`Premises` 引用各命中 id）。`engine-only` 子不建节点、不占 Step。`FinalResult` 仍由原 `FormatVerdict` 生成（字节级不变）；`DerivationSteps`/`Ok`/`Confidence` 不动。
  2. `JudgmentStatementAggregator.cs`：同口径——方程小题取 ctx id（Task 2），概念小题取 fact id（Task 1 的 Composite 新方法），有 id 子建节点 + 末尾汇总节点。`FormatVerdict` 输出字节级不变。
  3. 两求解器 return-null 路径（无确定判决）保持 `ReasoningNodes` 空（不臆造）。
  4. 测试：在 `ReasoningSolverTraceabilityTests.cs`（或新建 `JudgeApiTraceabilityTests.cs`）加覆盖——14 例精确 FinalResult 字节相等 + 11 例节点非空带 id + 3 例空 + pdf-051 节点数恰为 3 + Step 连续 + 末节点 Conclusion==FinalResult（复用公共断言 helper）。
- **Test Requirements**:
  - (rule) 上表 14 例逐项 `Assert.Equal(expectedFinalResult, resp.FinalResult)`（取自 `BuildTestCases()` 真实题干，分别走 MS 与 JA 路径）。
  - (rule) 11 例：`resp.ReasoningNodes` 非空且至少一节点 `KnowledgeId`/`RuleId` 非空；3 例（pdf-468/471/472）：`ReasoningNodes` 为 null 或 Count==0。
  - (rule) pdf-051：`resp.ReasoningNodes.Count == 3`（2 个 ctx-id 子节点 + 1 个汇总节点，3 个 engine 子不发）——精确防臆造。
  - (rule) 节点 Step 连续 1..N 无跳步；末节点 `Conclusion == resp.FinalResult`（复用 `ReasoningSolverTraceabilityTests` 公共断言）。
  - (rule) `dotnet build` 0 error、0 新增 warning。
- **Completion Evidence**: ✅ 已完成。新建共享节点构造器 `JudgeApiReasoningNodes.cs`（`Build(hits, finalResult)`：每个有真实 id 的子→1 节点，`KnowledgeId`=命中 id、`EvidenceLevel=KnowledgeFact`、`Conclusion`=子判决如 `(2)×`；末尾追加 1 个汇总节点，`Conclusion`=整题 `FinalResult`、`Premises`=去重命中 id 列表、无 `KnowledgeId`；Step 从 1 连续无跳步；hits 全空返回空列表）。`MultiSubCorrectnessSolver` 循环改用 `EquationCorrectnessJudge.JudgeWithIds` 取 `ctxId`，仅 ctxId 非空时记录 hit，构造节点赋 `ReasoningNodes`（直接赋列表——`EngineResponse.ReasoningNodes` 非可空，空列表与 null 等价，checkpoint 以 `?.Count ?? 0` 计数，避免 CS8601）。`JudgmentStatementAggregator` 方程分支取 ctxId、概念分支改用 `CompositeStatementJudge.JudgeWithIds` 取 hitIds（Counting/RedoxElectron 命中返回空→不发节点，R4）。`FinalResult`/`DerivationSteps`/`Ok`/`Confidence`/所有 return-null 路径字节级不变。新增 `JudgeApiTraceabilityTests.cs`（14 例 Theory + 3 Fact）：14 例 FinalResult 字节相等、11 例可溯源 / 3 例 R4 留空、pdf-051 恰好 3 节点（2 ctx-id + 1 汇总）Step 1..3、MS 不劫持 pdf-120、域外两求解器均 null。实现期发现基线探测误将 pdf-119 判为留空：直接运行时核实 pdf-119 真实命中事实库 `JS-016`（match=`工业制高纯硅`+`SiHCl3`，verdict=true→√，经化学核对正确），归为可溯源（spec §5.2 已勘误，AC6/AC11 同步改为 11/3）。`dotnet build` 0 error、warning 回到基线 7（0 新增）；`dotnet test --filter "JudgeApiTraceabilityTests|JudgeApiContractTests|CorrectnessContextRulesExternalizationTests|StatementJudgeTests|EquationCorrectnessJudgeTests"` → 103 passed / 0 failed。

---

## Task 4: 全量测试 + build 闸门

- **Status**: pending
- **Depends On**: Task 1, Task 2, Task 3
- **Priority**: high
- **覆盖 AC**: AC7, AC8
- **Changes**:
  1. 全量 `dotnet test`（TutorBrain + Chem）0 failed、0 新增跳过。
  2. `dotnet build -c Release` 0 error；warning 数不高于基线（0 新增）。
  3. 重点确认涉及 `IStatementJudge` / `EquationCorrectnessJudge` 的既有测试全过：`StatementJudgeTests` / `RedoxElectronStatementJudgeTests` / `JudgmentStatementFactsTests` / `CountingChoiceValidator`* / `MultiSelectChoiceValidator`* / `Stage3*`。
- **Test Requirements**:
  - (rule) `dotnet test` 全解决方案：0 failed。
  - (rule) `dotnet build -c Release`：0 error；warning 计数 ≤ 基线。
  - (rule) 上述 6 类既有测试集无失败、无新增跳过。
- **Completion Evidence**: ✅ 已完成。全量 `dotnet test CogniThink.Chem.sln`：CogniThink.Chem.Tests 913 passed / 0 failed / 0 skipped，CogniThink.TutorBrain.Tests 1392 passed / 0 failed / 0 skipped，合计 **2305 passed / 0 failed / 0 skipped**（0 新增跳过），exit 0。`dotnet build -c Release`：**0 error**；12 个 warning = 7 个预存代码 warning（AdaptiveTeachingOrchestrator ×2 CS8602 / CrystalCellValidator CS8603 / Diagnostic* CS1998 ×3 / HardBonesTraceTests CS8604）+ 5 个 NETSDK1206（RDKitDotNetCore RID，环境级，与本批无关）；grep 确认本批 10 个改动/新增文件在 Release warning 中命中 **0 次**。AC7 / AC8 满足。

---

## Task 5: 离线 eval diff + 覆盖率自检 + 台账更新

- **Status**: completed
- **Depends On**: Task 4
- **Priority**: high
- **覆盖 AC**: AC6, AC9, AC10, AC11
- **Changes**:
  1. 离线 eval：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "d:\github\CogniThink-Chem\_evalscratch\run_eval_resume.ps1" -Tag after_b_judge -Env Production`（`LlmAvailable=False`，757 例 0 failed）。
  2. `python _evalscratch\verify\diff_ckpt.py after_b_ext after_b_judge` → `GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`；`correct` 不变（AC9）。
  3. 写 `_evalscratch/verify/b_judge_coverage.py`（gitignored）：断言获得非空节点的案例集合**恰等于** 11 例名单、无丢失、无预期外新增；3 例 R4 留空保持 0；14 例 `gatewayConclusion` 字节级不变；`correctness`/`harm` 不变（AC6）。输出 `ALL ASSERTIONS PASSED`。
  4. 抽查节点质量（AC10 rubric）：11 例 Step 连续、末节点 Conclusion==FinalResult、无 engine-only 子臆造节点；评分 ≥1。
  5. 更新 [data-defect-register.md §N.2](../../../data-defect-register.md)：`MultiSubCorrectnessSolver` / `JudgmentStatementAggregator` 两行标 ✅ 已迁移（B-judge-API 批），记录 eval tag `after_b_judge`、11 例翻转 + 3 例 R4 留空证据；分级统计：A·done 7→**9**，B 4→**2**（仅剩 `ImpurityTrap` 外置 + `MultiPartDispatch` 传播）。
  6. 更新父 [tasks.md](../unified-reasoning-dag/tasks.md)：B 计数与建议执行顺序同步推进（judge-API 批标完成，下一批指向 ImpurityTrap 或 MultiPartDispatch）。
  7. 回填本文件各 Task 的 Completion Evidence；产出最终逐项可核验汇报。**不提交 commit**（除非用户明确要求）。
- **Test Requirements**:
  - (rule) `diff_ckpt` 输出含 `GAINED=0 / LOST=0 / WORSEN=0` 与 `RED-LINE ... -> PASS`。
  - (rule) 覆盖率脚本输出 `ALL ASSERTIONS PASSED`，且打印 added 集合 == 11 例名单、kept-empty == 3 例名单。
  - (rubric, AC10) 抽查 11 例节点：Step 连续 / 末节点 Conclusion==FinalResult / 无臆造 → 评分 ≥1。
  - (rule) register / 父 tasks diff 显示 2 行标已迁移、A·done=9、B=2。
- **Completion Evidence**: ✅ 已完成。离线 eval：`run_eval_resume.ps1 -Tag after_b_judge -Env Production`（`LlmAvailable=False`）→ 757/757 完成、0 失败，写出 `checkpoint_after_b_judge.jsonl` + `.done` 标记 + 摘要。两次 `diff_ckpt.py`：① `after_b_ext after_b_judge` → `GAINED=0 / LOST=0 / WORSEN=0 / harm_old=0 harm_new=0` → **PASS**；② `base_p0 after_b_judge` → `GAINED=6`（跨批次累计，非本批）/ `LOST=0 / WORSEN=0 / harm=0` → **PASS**。覆盖率自检：新建 `_evalscratch/verify/b_judge_coverage.py`（gitignored），断言 added 集合恰 11 例（`pdf-051/119/120/121/122/450/466/469/470/473/480`）、removed=∅、3 例 R4 留空（`pdf-468/471/472`）保持 0、14 例 `gatewayConclusion` 字节不变、correct/adopted/harm 不变（635/635/0）→ 输出 **ALL B-JUDGE-API COVERAGE ASSERTIONS PASSED**（node counts：pdf-051=3、pdf-450=10、pdf-480=11，与"事实数+1 汇总"一致；pdf-119=2 命中 JS-016）。AC10 节点质量已由单测 `JudgeApiTraceabilityTests` 断言（Step 连续、末节点 Conclusion==FinalResult）。台账：`data-defect-register.md §N.2` 两 solver 行标 `A·done`、intro "✅ 标记 7→9"、分级统计 A=7→9 / B=4→2、执行顺序 (3) 标完成；父 `unified-reasoning-dag/tasks.md` Status / A·done(9) / B(2) / 执行顺序 (3) 同步更新。剩余 B=2（`ImpurityTrap` 外置 + `MultiPartDispatch` 传播）。

---

## Task 6: fresh-context 独立 Review（spec mode 强制 gate）

- **Status**: completed
- **Depends On**: Task 5
- **Priority**: high
- **覆盖 AC**: 全部 AC 独立复核
- **Changes**: 委托 fresh-context **只读** 子代理独立复跑：build / 全量 test / eval diff（两次：after_b_ext→after_b_judge 与 base_p0→after_b_judge）/ 覆盖率脚本 / 采纳门逐 case 不变断言 / 可溯源抽样（11 例节点 + 3 例留空）/ 密钥扫描 / 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数（R4 无臆造）/ 未改既有 JSON 数据与评估判分 / 契约加性不破坏既有消费者 / 11 例可溯源 + 3 例诚实留空。子代理仅返回报告文本，由主代理落盘同目录 `review.md`。
- **Test Requirements**:
  - (rule) 独立复跑 build 0 error、test 0 failed、两次 diff 均 RED-LINE PASS、覆盖率脚本 PASS。
  - (rule) 代码核对：`IStatementJudge.Judge` 原签名未改、`Verdict` 原结构未改、采纳门 `BuildGatewayAnswerAsync` 未动、`CorrectnessContextRules.cs` grep 0 残留。
- **Completion Evidence**: ✅ 已完成。独立只读子代理 7 道闸门全部 PASS、零 DISCREPANCY，总体裁决 **APPROVE**（报告落盘 [review.md](./review.md)）。证据：① `dotnet build -c Debug` → 0 error / 13 warning（7 预存代码 + 6 NETSDK1206 环境），本批 10 个改动/新增文件 0 warning 命中；② 全量 `dotnet test CogniThink.Chem.sln` → Chem 913 + TutorBrain 1392 = **2305 passed / 0 failed / 0 skipped**；③ `diff_ckpt after_b_ext after_b_judge` → GAINED=0/LOST=0/WORSEN=0/harm=0 → RED-LINE PASS；`diff_ckpt base_p0 after_b_judge` → GAINED=6（跨批累计）/LOST=0/WORSEN=0/harm=0 → PASS；④ `b_judge_coverage.py` → `ALL B-JUDGE-API COVERAGE ASSERTIONS PASSED`（added 恰 11 例 / kept-empty 恰 3 例 / removed=∅ / correct 635→635 / adopted 635→635 / harm 0→0）；⑤ 契约核对：`IStatementJudge.Judge` 原签名未改、`Verdict` record 未动、采纳门 `BuildGatewayAnswerAsync` 不在本批改动集、`CorrectnessContextRules.cs` grep 0 残留、无 `_Tmp*.cs`、无密钥；⑥ 可溯源抽样：checkpoint 独立读 11 例节点>0 / 3 例=0（pdf-051=3、pdf-119=2、pdf-450=10、pdf-480=11），定向测试 31 passed，节点 Step 连续/末节点 Conclusion==FinalResult/pdf-051 恰 3 节点；⑦ 台账核对：`data-defect-register.md §N.2` 两 solver 行标 A·done、A=9/B=2、执行顺序 (3) 完成，父 tasks.md 同步。残留风险仅提示性：工作树混合 b-externalize 未提交批（commit 时按批切分）、NETSDK1206 环境 warning、pdf-119 勘误已独立复算成立——均非阻塞。

---

## 执行顺序与风险控制

1. **Task 1 与 Task 2 互不写同一文件**（Task 1 = FactChoiceMatcher/FactBase/Composite；Task 2 = CorrectnessContextRules/EquationCorrectnessJudge），可并行或任意先后；各自 build 一次尽早捕获编译错误。
2. **Task 3 依赖 Task 1 + Task 2**（聚合器需要两类 id），在两者完成后进行。
3. 每 Task 的精确 FinalResult 单测是**零漂移第一道闸**（在 eval 前锁定行为等价）。
4. Task 4 全量 test 是第二道闸（契约加法不破坏既有消费者）。
5. Task 5 的 `diff_ckpt` 是第三道闸（覆盖未进单测的案例 + 网关传播）；覆盖率脚本是第四道闸（防臆造 / 防漏发，硬断言恰 11 例翻转 + 3 例留空）。
6. **R4 是本批最高风险点**（R2 风险项）：任何对 engine-only 子发节点的实现都必须在 Task 3 单测（pdf-051 节点数==3、3 例空）与 Task 5 覆盖率脚本双重拦截。若任一闸失败：回对应 Task 修复，不进入下一闸；不蒙混通过。
