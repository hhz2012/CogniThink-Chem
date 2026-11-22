# B-judge-API 迁移批 · fresh-context 独立复核报告

> 复核范围：`.trae/specs/unified-reasoning-dag-b-judge-api/`。工作目录 `d:\github\CogniThink-Chem`，分支 `main`，HEAD `5027fe8`。严格只读（未编辑/创建/删除任何受版本控制文件，未 git commit；仅运行 build/test/python 产生构建与测试输出）。注意：工作树同时混有未提交的 b-externalize 批改动，build/test 在整棵合并工作树上执行。

## 闸门 1：dotnet build（Debug）→ 0 error / 0 新增 warning

- **命令**：`dotnet build d:\github\CogniThink-Chem\CogniThink.Chem.sln -c Debug -v minimal`（输出落盘 `_evalscratch\review_build_debug.log`）
- **关键输出**：`已成功生成。` / `0 个错误` / `13 个警告`
- **warning 拆解**（核对基线 7 个预存代码 warning）：
  - `AdaptiveTeachingOrchestrator.cs(78,13) CS8602`、`(110,13) CS8602` — 基线预存 ×2
  - `CrystalCellValidator.cs(240,16) CS8603` — 基线预存（CrystalCellValidator 属 b-externalize 批改动，非本批；warning 为预存）
  - `DiagnosticTeachingEngineTests.cs(181,23) CS1998` — 基线预存
  - `DiagnosticErrorPronePointsTests.cs(261,23)`、`(269,23) CS1998` — 基线预存 ×2
  - `HardBonesTraceTests.cs(114,60) CS8604` — 基线预存
  - 以上 7 个代码 warning = 基线全部；另 6 个 `NETSDK1206`（RDKitDotNetCore RID，环境级，非代码）。
- **本批 10 个改动/新增文件命中次数 = 0**（FactChoiceMatcher / FactBaseStatementJudge / CompositeStatementJudge / CorrectnessContextRules / EquationCorrectnessJudge / MultiSubCorrectnessSolver / JudgmentStatementAggregator / JudgeApiReasoningNodes + 3 个新测试文件均未出现在 warning 列表）。
- **结论：PASS**（0 error，0 新增 warning）。

## 闸门 2：全量 dotnet test → 0 failed

- **命令**：`dotnet test d:\github\CogniThink-Chem\CogniThink.Chem.sln -c Debug --no-build -v minimal`（输出落盘 `_evalscratch\review_full_test.log`）
- **关键输出**：
  - `已通过! - 失败: 0，通过: 913，已跳过: 0，总计: 913 - CogniThink.Chem.Tests.dll`
  - `已通过! - 失败: 0，通过: 1392，已跳过: 0，总计: 1392 - CogniThink.TutorBrain.Tests.dll`
  - 合计 **2305 passed / 0 failed / 0 skipped**，exit 0。
- **结论：PASS**（与预期 Chem ~913 + TutorBrain ~1392 完全吻合，0 新增跳过）。

## 闸门 3：两次 eval diff（对已存在 checkpoint，不重跑 eval）

- **命令 1**：`python _evalscratch\verify\diff_ckpt.py after_b_ext after_b_judge`
- **输出**：
  ```
  [after_b_ext vs after_b_judge] old=757(skip 0) new=757(skip 0)
  GAINED (0): []
  LOST   (0): []
  WORSEN null->new-harm (0): []
  IMPROVE harm->cleared (0): []
  harm_old=0  harm_new=0
  RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
  ```
- **结论：PASS**（GAINED=0 / LOST=0 / WORSEN=0 / harm_old=0 harm_new=0 → RED-LINE PASS，与预期完全一致）。

- **命令 2**：`python _evalscratch\verify\diff_ckpt.py base_p0 after_b_judge`
- **输出**：
  ```
  [base_p0 vs after_b_judge] old=757(skip 0) new=757(skip 0)
  GAINED (6): ['pdf-415', 'pdf-530', 'pdf-535', 'pdf-542', 'pdf-543', 'trap-limit-2']
  LOST   (0): []
  WORSEN null->new-harm (0): []
  harm_old=0  harm_new=0
  RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
  ```
- **结论：PASS**（GAINED=6 为跨批次累计、非本批，可接受；LOST=0 / WORSEN=0 / harm=0，与预期一致）。

## 闸门 4：覆盖率自检

- **命令**：`python _evalscratch\verify\b_judge_coverage.py`
- **关键输出**：
  ```
  non-empty: b_ext=78  b_judge=89  added=11  removed=0
  added  ids: ['pdf-051','pdf-119','pdf-120','pdf-121','pdf-122','pdf-450','pdf-466','pdf-469','pdf-470','pdf-473','pdf-480']
  removed ids: []
  added set == EXPECTED_ADDED (11) OK
  R4 stay-zero OK: ['pdf-468', 'pdf-471', 'pdf-472'] all 0 nodes (baseline & after)
  ...（14 例 conclusion unchanged 逐行打印）
  correct: b_ext=635 b_judge=635   adopted: b_ext=635 b_judge=635   harm: b_ext=0 b_judge=0
  ALL B-JUDGE-API COVERAGE ASSERTIONS PASSED
  ```
- **结论：PASS**。added 集合恰 = {pdf-051,119,120,121,122,450,466,469,470,473,480}（11 例），kept-empty = {pdf-468,471,472}（3 例），removed=∅，与预期逐字一致。

## 闸门 5：代码契约核对（只读 grep / 读 / git diff）

| 核对项 | 方法 | 结果 |
|---|---|---|
| `IStatementJudge.Judge` 原签名未改 | 读 `IStatementJudge.cs` 未在改动列表；grep `Verdict Judge(string stem, string statement)` 命中接口（L28）+ 4 实现者（Composite L23 / Counting L14 / FactBase L14 / RedoxElectron L33） | **PASS**：原签名在接口与全部实现者完整保留；`IStatementJudge.cs` 未被修改 |
| `Verdict` 原 record 结构未改 | 读 `EquationCorrectnessJudge.cs:28` = `public sealed record Verdict(char? Symbol, List<string> Reasons)`；git diff 该文件 hunk 起自 @@ -32，未触及 line 28 | **PASS**：record 定义未动；所有 `new Verdict(...)` 调用仅被元组包裹，构造参数不变 |
| 采纳门 `BuildGatewayAnswerAsync` 未被本批改动 | grep 定位其在 `LlmGatewayService.cs` / `LlmGatewayService.MultiPartDispatch.cs` / `ReasoningContracts.cs`；`git status` 与 `git diff --stat` 均不含这些文件 | **PASS**：采纳门及其所在文件不在本批改动集 |
| `CorrectnessContextRules.cs` 无化学事实残留 | grep `BaSO3\|BaSO4\|亚铜氨\|H2SO3\|Cu(NH3\|硝酸根` → 0 命中 | **PASS**（AC3）：规则特有子串已全部外置 |
| 无 `_Tmp*.cs` 残留 | Glob `**/_Tmp*.cs` → 无文件 | **PASS** |
| 本批新增/改动文件无硬编码密钥/密码/连接串 | grep `api_key\|secret\|password\|connectionstring\|conn_str\|bearer\|sk-`（大小写不敏感）于 8 个源文件 + 3 个新测试文件 + 新 JSON → 0 命中 | **PASS** |

补充：`git diff HEAD` 核对 7 个本批源文件均为**纯加性**——原 `Judge`/`JudgeStatement`/`Judge`(Equation) 签名保留并改为内部委托新方法（`JudgeWithIds` / `JudgeStatementWithIds` / `EvaluateWithId`）丢弃 id；`FinalResult`/`DerivationSteps`/`Ok`/`Confidence`/return-null 路径字节级不变；引擎检查项路径一律返回 `CtxRuleId=null`（R4）。`CorrectnessContextRules.cs` 重写为 `Lazy<>` + `DataRoot.Resolve()` 直读 JSON，仅保留通用 `NormalizeForSearch`/`Contains`。

## 闸门 6：可溯源抽样

**(a) checkpoint 独立抽样**（直接读 `checkpoint_after_b_judge.jsonl`，取 `gatewayReasoningNodesCount` 与 `gatewayConclusion`）：

| 案例 | 节点计数 | conclusion（字节） | 判定 |
|---|---|---|---|
| pdf-051 | **3** | `(1)√ (2)√ (3)× (4)× (5)×` | >0，与预期（2 ctx 子 + 1 汇总）一致 |
| pdf-119 | 2 | `√` | >0（JS-016 命中） |
| pdf-120 | 2 | `×` | >0 |
| pdf-450 | 10 | `(1)× … (9)×` | >0（9 fact + 1 汇总） |
| pdf-480 | 11 | `(1)√ … (10)√` | >0（10 fact + 1 汇总） |
| pdf-468 | **0** | `×` | R4 留空 ✓ |
| pdf-471 | **0** | `√` | R4 留空 ✓ |
| pdf-472 | **0** | `√` | R4 留空 ✓ |

**(b) 定向测试**：`dotnet test CogniThink.Chem.sln --filter "JudgeApiTraceabilityTests|JudgeApiContractTests|CorrectnessContextRulesExternalizationTests" -c Debug --no-build` → `已通过! - 失败: 0，通过: 31，已跳过: 0，总计: 31`（与 7+7+17=31 一致）。读 `JudgeApiTraceabilityTests.cs` 确认断言存在：Step 连续 `Enumerable.Range(1, steps.Count)`（L36-37）、末节点 `Conclusion==resp.FinalResult`（L40/L99-100）、pdf-051 `nodes.Count==3`（L91）、仅 2 个 ctx 子携带 id / 3 个 engine-only 子未臆造（L95）、Step 1..3 无跳步（L103-104）、14 例 FinalResult 字节相等（L73）。

- **结论：PASS**（11 例可溯源 + 3 例诚实留空，节点结构合规，无臆造）。

## 闸门 7：台账核对

- **`data-defect-register.md §N.2`**：
  - 引言 L612：「除下表 ✅ 标记的 **9 个**…**B-judge-API 批**：MultiSubCorrectnessSolver / JudgmentStatementAggregator」。
  - L623 `MultiSubCorrectnessSolver` 行 → `✅ 已迁移 (B-judge-API 批)` → 分级 `A·done`。
  - L624 `JudgmentStatementAggregator` 行 → `✅ 已迁移 (B-judge-API 批)` → 分级 `A·done`。
  - L631 分级统计：「已迁移 **A=9** … **B=2**（外置 1：ImpurityTrap；传播 1：MultiPartDispatch）」。
  - L633 建议执行顺序 (3)：`✅ B-judge-API 批已完成`。
- **父 `.trae/specs/unified-reasoning-dag/tasks.md`**：
  - L108 Status 标 B-judge-API 批已完成；L111「已迁移 **A·done = 9**」含本批两 solver；L113「**B = 2**」；L122 执行顺序 (3) `✅ 已完成：B-judge-API 批`。
- **结论：PASS**（两行已标已迁移 A·done；A·done=9、B=2；父 tasks.md 同步）。

## 裁决维度逐项结论

| 维度 | 结论 | 证据 |
|---|---|---|
| precision=1（无臆造） | ✅ 满足 | 仅真实 id 命中子建节点；engine-only / Counting / RedoxElectron 路径返回空 id → 不发节点；3 例 R4 留空、pdf-051 恰 3 节点（非 5） |
| harm=0 | ✅ 满足 | 两次 diff `harm_new=0`；覆盖率 `harm 0→0` |
| LOST=0 | ✅ 满足 | 两次 diff `LOST=0` |
| WORSEN=0 | ✅ 满足 | 两次 diff `WORSEN=0` |
| R4 无臆造 | ✅ 满足 | diff 显示引擎检查项/√/null 路径 `CtxRuleId=null`；Composite 对 Counting/RedoxElectron 返回空 id 集合；覆盖率脚本硬断言 3 例保持 0 |
| 未改既有 JSON 数据与评估判分 | ✅ 满足 | `git status` 仅见新增（`??`）JSON，无既有 JSON 被修改（`M`）；b-judge-api 源文件均在 `Gateway/`，未触碰 evaluation；`correct` 635→635、`adopted` 635→635 |
| 契约加性不破坏消费者 | ✅ 满足 | 接口/record/原方法签名未改，原方法内部委托；全量测试 0 failed（既有 StatementJudge/EquationCorrectnessJudge/choice 测试全过） |
| 11 可溯源 + 3 诚实留空 | ✅ 满足 | 覆盖率脚本 + checkpoint 独立抽样双重确认 |

## DISCREPANCY

**未发现任何与预期不符之处。** 所有闸门输出与 spec/tasks 预期逐字一致，无 DISCREPANCY。

## 总体裁决：**APPROVE**

本批为纯加性元数据扩展 + 2 条化学事实外置，7 道闸门全部独立复跑通过，红线（precision=1 / harm=0 / LOST=0 / WORSEN=0 / R4 无臆造 / 不改既有 JSON 与判分 / 契约不破坏消费者 / 11+3）全部守住，零回归。

### 残留风险（非阻塞，提示性）
1. **工作树混合提交**：当前未提交工作树同时含 b-judge-api 批与 b-externalize 批改动，build/test 在合并树上通过。落盘 commit 时由主代理负责按批切分（本批文件清单见任务说明），不影响本批正确性结论。
2. **NETSDK1206 warning**：6 个 RDKitDotNetCore RID 警告为环境级、预存，与本批无关。
3. **pdf-119 历史探测勘误**：spec §5.2 已记录基线探测曾误将 pdf-119 判为留空，实现期运行时核实真实命中 `JS-016`；本次独立复跑 checkpoint 确认 pdf-119 计数=2、conclusion=`√`，勘误成立，无风险。
