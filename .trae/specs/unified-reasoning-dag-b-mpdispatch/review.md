# B-MpDispatch 迁移批 — fresh-context 只读独立复核报告

**复核范围**：`LlmGatewayService.MultiPartDispatch.cs` 的 `TryDispatchMultiPartAsync` 在聚合多部分大题各子问结论时，把丢弃的子响应 `ReasoningNodes` 链进父响应（新增 `MultiPartReasoningNodes.Merge` 纯函数聚合器 + 6 条单测）。纯加性元数据，不改任何判决字段。
**复核方式**：严格只读（未编辑/创建/删除任何受版本控制文件，未 git commit）；仅运行 build/test/python 脚本。工作树含本批未提交改动，所有闸门在整棵工作树上执行。
**基线**：`after_b_impurity`（757 例）；红线锚点 `base_p0`；新 checkpoint `after_b_mpdispatch`（已存在，复核不重跑 eval，仅对现有 checkpoint 做 diff/coverage 只读核对）。

> 关键背景（spec §5 一手实证）：基线 `checkpoint_after_b_impurity.jsonl` 中 `taskType == "multi_part"` 计数 = **0**（0/27 K.1 多部分大题可由现有求解器全覆盖子问，`TryDispatchMultiPartAsync` 恒返回 null），故分发路径在 eval 中不被触发 → 本批对 eval **平凡零影响**；传播正确性**只能靠单测证明**。

## 闸门 1 — dotnet build（Debug + Release，强制 --no-incremental）→ 0 error，warning 核对

**命令**：
```
dotnet build CogniThink.Chem.sln -c Debug --no-incremental
dotnet build CogniThink.Chem.sln -c Release --no-incremental
```

**关键输出**：两配置均 `已成功生成 / 0 个错误 / 13 个警告 / EXITCODE=0`。

13 个 warning 构成（与基线完全吻合）：
- 7 个预存代码 warning：`AdaptiveTeachingOrchestrator.cs` CS8602×2（L78/L110）、`CrystalCellValidator.cs` CS8603（L240）、`DiagnosticTeachingEngineTests.cs` CS1998（L181）、`HardBonesTraceTests.cs` CS8604（L114）、`DiagnosticErrorPronePointsTests.cs` CS1998×2（L261/L269）。
- 6 个 NETSDK1206 环境 warning（RDKitDotNetCore RID）。

**本批 3 文件在 warning 中命中数**（grep 两配置 build 日志）：`MultiPartReasoningNodes` → 0、`LlmGatewayService.MultiPartDispatch` → 0、`MultiPartReasoningNodesTests` → 0。

**结论：PASS**（0 error；13 warning 与基线一致；本批文件 0 新增 warning）。

## 闸门 2 — 全量 dotnet test → 0 failed

**命令**：`dotnet test CogniThink.Chem.sln -c Debug --no-build`

**关键输出**：
```
已通过! - 失败: 0，通过:  913，已跳过: 0，总计:  913 - CogniThink.Chem.Tests.dll
已通过! - 失败: 0，通过: 1402，已跳过: 0，总计: 1402 - CogniThink.TutorBrain.Tests.dll
EXITCODE=0
```
合计 **2315 passed / 0 failed / 0 skipped**（Chem 913 + TutorBrain 1402 = 基线 2309 + 本批新增 6，0 新增跳过）。

**结论：PASS**。

## 闸门 3 — 合并器定向单测 → 6 passed

**命令**：`dotnet test --filter "FullyQualifiedName~MultiPartReasoningNodesTests"`

**结果**：**6 passed / 0 failed / 0 skipped**（覆盖 AC1 全子传播+汇总去重、AC2 全空不臆造、AC3 部分空仅传有节点子、AC4 Step 连续重排、AC11 子节点原样克隆+原始子响应未被修改+汇总仅在有子节点时追加）。

**结论：PASS**（传播正确性的唯一证明途径，因 eval 不触发分发路径）。

## 闸门 4 — 两次 eval diff（不重跑 eval）

① `after_b_impurity` → `after_b_mpdispatch`：
```
[after_b_impurity vs after_b_mpdispatch] old=757(skip 0) new=757(skip 0)
GAINED (0): []
LOST   (0): []
WORSEN null->new-harm (0): []
IMPROVE harm->cleared (0): []
harm_old=0  harm_new=0
RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
```

② `base_p0` → `after_b_mpdispatch`：
```
[base_p0 vs after_b_mpdispatch] old=757(skip 0) new=757(skip 0)
GAINED (6): ['pdf-415', 'pdf-530', 'pdf-535', 'pdf-542', 'pdf-543', 'trap-limit-2']
LOST   (0): []
WORSEN null->new-harm (0): []
harm_old=0  harm_new=0
RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
```

**结论：PASS**（① 全零 RED-LINE PASS，平凡成立因 multi_part=0；② GAINED=6 为前批次累计、非本批，LOST/WORSEN/harm 均 0）。

## 闸门 5 — 覆盖率自检

**命令**：`python _evalscratch\verify\b_mpdispatch_coverage.py`

**关键输出**：
```
after_b_impurity total=757   after_b_mpdispatch total=757
case count 757 == 757 OK
multi_part count: b_impurity=0  b_mpdispatch=0
multi_part count == 0 in both OK (dispatch path untriggered, eval impact trivially zero)
non-empty node cases: b_impurity=90  b_mpdispatch=90  added=0  removed=0
added  ids: []
removed ids: []
added == empty AND removed == empty OK
cases with any gatewayReasoningNodesCount delta: 0  []
no case changed its gatewayReasoningNodesCount OK
correct: b_impurity=635 b_mpdispatch=635   adopted: b_impurity=635 b_mpdispatch=635   harm: b_impurity=0 b_mpdispatch=0
correct / adopted / harm unchanged OK

ALL B-MPDISPATCH COVERAGE ASSERTIONS PASSED
```

**结论：PASS**（added/removed 均空、multi_part 仍 0、0 案例 `gatewayReasoningNodesCount` 有任何 delta、correct/adopted/harm 不变 → 证明本批零波及主链）。

## 闸门 6 — 代码契约核对

| 核对项 | 方法/证据 | 结果 |
|---|---|---|
| AC1：全子带节点 → 全部传播 + 末尾 1 汇总 | `Merge_AllSubsHaveNodes_...`：2+2 子 +1 汇总=5；Step 1..5 连续；汇总 `Premises=={k-a,k-b,k-c}`（去重+首次出现序） | **PASS** ✅ |
| AC2：R4 全空不臆造 | `Merge_AllSubsEmpty_...` 含 1 个 `ReasoningNodes=null!` 子 → `Assert.Empty`，无汇总 | **PASS** ✅ |
| AC3：R4 部分空仅传有节点子 | `Merge_PartialEmpty_...`：空子问 (2) 贡献 0 节点，汇总仅含实际 kid | **PASS** ✅ |
| AC4：Step 连续不变量 | 3 测断言 `Assert.Equal(Enumerable.Range(1,K), ...)`，原子 Step 混乱（5/100/0）仍连续 | **PASS** ✅ |
| AC5：仅加性、聚合串字节不变 | `git diff`：`parts` 类型 `string→EngineResponse`、存子响应本体、聚合改 `sub.FinalResult.Trim()`（与原 `.Trim()` 时点等价，`aggregated` 字节不变）、唯一新增 `ReasoningNodes = Merge(...)`；`Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`/3 处 return-null/`TaskType` 未动 | **PASS** ✅ |
| AC11：子节点原样克隆、原始子响应未被修改 | 测试断言 `sub1.ReasoningNodes[0].Step` 仍为 99；`Premises` 经 `.ToList()` 克隆、`RuleId`/`KnowledgeId`/`Conclusion`/`EvidenceLevel` 原样拷贝 | **PASS** ✅ |
| CS8601 防回归 | `ReasoningNodes` 直接赋合并列表（空列表亦可），从不赋 null | **守住** ✅ |
| 无 `_Tmp*.cs` 残留 | 扫描 src/tests | **0** ✅ |
| 无硬编码密钥 | grep 本批 3 文件 `sk-\|api_key\|Bearer\|secret` | **全 0** ✅ |
| 未改 `data/*.json` | `git status --porcelain` 无 data/ 改动 | **0** ✅ |
| 分发点/采纳门/判分未被本批改动 | `git diff --name-only` 中 `BuildGatewayAnswer`/`IsAnswerCorrect`/`GatewayVsLlmEvaluation` 命中数 = 0 | **0** ✅ |

**结论：PASS**（R4 安全、纯加性、无臆造、无密钥、无数据改动）。

## 闸门 7 — 台账核对

- `data-defect-register.md §N.2`：L612「✅ 标记的 11 个」含 B-MpDispatch；L617 `LlmGatewayService.MultiPartDispatch.cs` 行标 ✅ 已迁移 (B-MpDispatch 批) → `A·done`；L631 分级统计 `A=11`、`B=0`；L633 执行顺序 (5) ✅、剩余仅 `LlmGatewayService` 主体。
- 父 `.trae/specs/unified-reasoning-dag/tasks.md`：Status 追加 B-MpDispatch 完成；分级 `A·done=11`、`B=0`；执行顺序 item 5 ✅、剩余 `LlmGatewayService`。
- `git grep` 权威文件无残留 `B=1`/`A·done=10`。

**结论：PASS**。

## 逐 AC 裁决

| AC | 类型 | 裁决 | 证据 |
|---|---|---|---|
| AC1 | rule | PASS | 单测：全子带节点 → 5 节点（4 子 + 1 汇总）、Step 1..5 连续、子节点标注归属、汇总 Conclusion/Premises 正确 |
| AC2 | rule | PASS | 全子空（含 null 子）→ `Assert.Empty`，不臆造汇总 |
| AC3 | rule | PASS | 部分空 → 空子贡献 0、汇总 Premises 仅含实际 kid |
| AC4 | rule | PASS | 原子 Step 混乱仍重排为严格连续 1..K |
| AC5 | rule | PASS | `git diff` 纯加性；聚合串字节不变；判决字段/return-null/TaskType 未动；既有多部分测试全过 |
| AC6 | rule | PASS | 两配置 0 error / 13 warning（=基线）；本批 3 文件 0 warning 命中 |
| AC7 | rule | PASS | 全量 2315 passed / 0 failed / 0 skipped |
| AC8 | rule | PASS | ① 全零 RED-LINE PASS；② LOST/WORSEN/harm=0 PASS |
| AC9 | rule | PASS | 覆盖率脚本 ALL ASSERTIONS PASSED；added/removed 空、multi_part=0、0 案例节点变化 |
| AC10 | rule | PASS | 台账 A·done=11、B=0、MultiPartDispatch 行已迁移；父 tasks.md 同步 |
| AC11 | rubric | PASS (5/5) | 纯 internal static 函数、子节点原样克隆、汇总仅在有子节点时追加、无密钥/无 _Tmp/无硬编码，≥4 阈值通过 |

## 裁决维度逐项结论

| 维度 | 结论 | 证据 |
|---|---|---|
| precision=1（无臆造） | ✅ | 覆盖率 added/removed 均空；AC2/AC3 单测拦截空子臆造 |
| harm=0 | ✅ | diff `harm_new=0`；覆盖率 harm 0→0 |
| LOST=0 | ✅ | 两次 diff 均 `LOST=0` |
| WORSEN=0 | ✅ | 两次 diff 均 `WORSEN=0` |
| R4 无臆造 | ✅ | 全子空→父空（不造汇总）；部分空仅传有节点子；汇总仅在存在子节点时追加 |
| 未改评估判分与采纳门 | ✅ | `git diff --name-only` 无判分/采纳门文件；correct/adopted/harm 全不变（635/635/0） |
| 契约加性不破坏消费者 | ✅ | 全量 test 0 failed；既有 `MultiPartUtilityTests`/`MultiPartAndScoringTests` 仍过；`ReasoningNodes` 非可空直接赋列表（CS8601 守住） |
| 合并器纯函数可测 | ✅ | `internal static`，`InternalsVisibleTo` 可达，6 测独立验证 |
| multi_part 平凡零影响有单测兜底 | ✅ | eval 不触发分发路径（multi_part=0），传播正确性由 6 条单测证明 |

## DISCREPANCY

**未发现任何与预期不符之处。** 7 个闸门输出与 spec/tasks 预期逐字一致，无 DISCREPANCY。

## 总体裁决：**APPROVE**

本批变更自洽且隔离：新增 `MultiPartReasoningNodes.Merge` 纯函数把各子问 `ReasoningNodes` 链进父响应（子节点 Step 重排连续 + 标注子问归属 + 末尾 1 汇总节点；子全空则父留空，R4 不臆造），`LlmGatewayService.MultiPartDispatch.cs` 仅加性赋值 `ReasoningNodes`，所有判决字段与 return-null 路径字节级不变；新增文件 0 warning；全量测试 0 failed；两次 eval diff 全零（multi_part=0 平凡零影响）；覆盖率硬断言 0 案例节点计数变化；台账同步（A·done 10→11、B 1→0）。

### 残留风险/观察（非阻塞）
1. `multi_part=0` 导致分发路径在 eval 中不触发，本批传播正确性**只能靠单测证明**——这是 spec OQ 明确接受的范围（测试程序集无 `LlmGatewayService` 夹具、无自然题干触发全子可解分发），6 条单测已全覆盖（全子/全空/部分空/Step 连续/子节点克隆/汇总条件）。
2. checkpoint 仅持久化节点计数而非节点明细，节点内容正确性由 `MultiPartReasoningNodesTests` 运行时断言兜底（已通过）。
3. 未提交 commit（用户未要求）；工作树变更仅限于 `LlmGatewayService.MultiPartDispatch.cs` + 新增 `MultiPartReasoningNodes.cs`/`MultiPartReasoningNodesTests.cs` + 本批 spec 目录 + 两本台账，`_evalscratch/` 取证产物已 gitignore。
