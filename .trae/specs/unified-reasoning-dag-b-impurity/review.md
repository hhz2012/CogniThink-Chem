# B-Impurity 迁移批 — fresh-context 只读独立复核报告

**复核范围**：`ImpurityTrapSolver` 1 条硬编码化学事实（酸性 KMnO4 除甲烷中乙烯→氧化生成 CO2 引入新杂质）外置为带稳定 id 的 JSON，命中时线程化 `KnowledgeId` 进 `EngineResponse.ReasoningNodes`。
**复核方式**：严格只读（未编辑/创建/删除任何受版本控制文件，未 git commit）；仅运行 build/test/python 脚本。工作树混有未提交的 b-externalize + b-judge-api 批改动，所有闸门在整棵合并工作树上执行。
**基线**：`after_b_judge`（757 例）；红线锚点 `base_p0`；新 checkpoint `after_b_impurity`（已存在，不重跑 eval）。

## 闸门 1 — dotnet build（Debug）→ 0 error，warning 核对

**命令**：
```
dotnet clean CogniThink.Chem.sln -c Debug ; dotnet build CogniThink.Chem.sln -c Debug --no-incremental
```

**关键输出**：
```
已成功生成。
    13 个警告
    0 个错误
已用时间 00:00:37.50
EXITCODE=0
```
13 个 warning 构成（与基线完全吻合）：
- 7 个预存代码 warning：`AdaptiveTeachingOrchestrator.cs` CS8602×2（L78/L110）、`CrystalCellValidator.cs` CS8603（L240）、`DiagnosticTeachingEngineTests.cs` CS1998（L181）、`HardBonesTraceTests.cs` CS8604（L114）、`DiagnosticErrorPronePointsTests.cs` CS1998×2（L261/L269）。
- 6 个 NETSDK1206 环境 warning（RDKitDotNetCore RID）。

**本批 3 文件在 warning 中命中数**（grep build 日志）：`ImpurityTrap` → 0、`impurity-traps` → 0、`error` 行 → 0。

**结论：PASS**（0 error；13 warning 与基线一致；本批文件 0 新增 warning）。

## 闸门 2 — 全量 dotnet test → 0 failed

**命令**：`dotnet test CogniThink.Chem.sln -c Debug --no-build`

**关键输出**：
```
已通过! - 失败: 0，通过: 1396，已跳过: 0，总计: 1396 - CogniThink.TutorBrain.Tests.dll
已通过! - 失败: 0，通过:  913，已跳过: 0，总计:  913 - CogniThink.Chem.Tests.dll
EXITCODE=0
```
合计 **2309 passed / 0 failed / 0 skipped**（Chem 913 + TutorBrain 1396 = 基线 1392 + 本批新增 4，0 新增跳过）。

**结论：PASS**。

## 闸门 3 — 两次 eval diff（不重跑 eval）

① `after_b_judge` → `after_b_impurity`：
```
[after_b_judge vs after_b_impurity] old=757(skip 0) new=757(skip 0)
GAINED (0): []
LOST   (0): []
WORSEN null->new-harm (0): []
harm_old=0  harm_new=0
RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
```

② `base_p0` → `after_b_impurity`：
```
[base_p0 vs after_b_impurity] old=757(skip 0) new=757(skip 0)
GAINED (6): ['pdf-415', 'pdf-530', 'pdf-535', 'pdf-542', 'pdf-543', 'trap-limit-2']
LOST   (0): []
WORSEN null->new-harm (0): []
harm_old=0  harm_new=0
RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
```

**结论：PASS**（① 全零 RED-LINE PASS；② GAINED=6 为跨批次累计、非本批，d9-1 不在 GAINED 因其正确性未变，LOST/WORSEN/harm 均 0）。

## 闸门 4 — 覆盖率自检

**命令**：`python _evalscratch\verify\b_impurity_coverage.py`

**关键输出**：
```
after_b_judge total=757   after_b_impurity total=757
non-empty: b_judge=89  b_impurity=90  added=1  removed=0
added  ids: ['d9-1']
removed ids: []
added set == EXPECTED_ADDED (1) OK
  d9-1        nodes 0 -> 1   conclusion unchanged='不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。'
d9-1 flipped 0 -> >0 OK; gatewayConclusion byte-identical OK
correct: b_judge=635 b_impurity=635   adopted: b_judge=635 b_impurity=635   harm: b_judge=0 b_impurity=0

ALL B-IMPURITY COVERAGE ASSERTIONS PASSED
```

**结论：PASS**（added 集合恰 `['d9-1']`、removed=[]、d9-1 节点 0→1、gatewayConclusion 字节不变、correct/adopted/harm 635/635/0 不变）。

## 闸门 5 — 代码契约核对

| 核对项 | 方法/证据 | 结果 |
|---|---|---|
| AC1：`.cs` 真值特征子串 0 残留 | grep `ImpurityTrapSolver.cs` 模式 `氧化生成 CO₂\|引入新杂质\|应用溴水\|1,2-二溴乙烷\|达不到除杂目的\|强氧化性` | **0 命中** ✅ |
| 触发识别 `if` 字节级未改 | `git diff` 显示 `if` 三行为 context（未改）；仅其上注释行改写 | **一致** ✅ |
| HEAD 原硬编码载荷 == JSON 条目（零漂移） | Python 比对：`FinalResult`/3 条 `DerivationSteps`/`ConstraintNote`/`ErrorTags` 全部 repr 级相等 | **全部 True** ✅ |
| 分发点/采纳门未被本批改动 | `git diff --name-only` 中 `LlmGatewayService`/`BuildGatewayAnswer` 命中数 = 0 | **0** ✅ |
| JSON 元数据 + 稳定 id + 结构一致 | 含 `version`/`source`/`entries[]`，条目 `id=impurity-kmno4-ethylene-co2`；结构与 `known-equations.json`/`product-rules.json` 一致 | **AC9 2/2** ✅ |
| 无 `_Tmp*.cs` 残留 | 扫描 src/tests | **0** ✅ |
| 无硬编码密钥/连接串 | grep 本批文件 `api_key\|secret\|password\|connectionstring\|bearer\|sk-` | **全 0** ✅ |

**结论：PASS**。化学事实仅存 JSON，`.cs` 零残留；契约纯加性。

## 闸门 6 — 可溯源抽样

**checkpoint 抽样**（直接读 `after_b_judge` 与 `after_b_impurity` 的 d9-1）：
```
gatewayReasoningNodesCount: b_judge= 0  b_impurity= 1
gatewayConclusion byte-equal: True
gatewayCorrect: b_judge= True  b_impurity= True
gatewayTruthAdopted: b_judge= True  b_impurity= True
```
**定向测试**：`dotnet test --filter "ImpurityTrapExternalizationTests|Stage3ESolversTests"` → **21 passed / 0 failed / 0 skipped**（新 4 测断言 `KnowledgeId==impurity-kmno4-ethylene-co2`、`Conclusion==FinalResult`、`EvidenceLevel==KnowledgeFact`、`Step=1`、`Count==1` + 3 域外 `Assert.Null`；既有 17 测含 4 个 trap 测试仍过）。

> checkpoint 仅持久化节点计数而非节点明细；节点内容正确性由 `ImpurityTrapExternalizationTests` 运行时断言兜底。「除 d9-1 外无其它案例因本批新增节点」由闸门 4 覆盖率脚本（added 恰 `{d9-1}`）佐证。

**结论：PASS**（d9-1 节点 0→1 可溯源，gatewayConclusion 字节不变）。

## 闸门 7 — 台账核对

- `data-defect-register.md §N.2`：L622 `ImpurityTrapSolver` 行标 ✅ 已迁移 (B-Impurity 批) → `A·done`；L631 分级 `A=10`、`B=1`；L633 执行顺序 (4) ✅；L612 引言计数含 B-Impurity。
- 父 `.trae/specs/unified-reasoning-dag/tasks.md`：Status 追加 B-Impurity 完成；分级 `A·done=10`、`B=1`；执行顺序 item 4 ✅、item 5 剩余。

**结论：PASS**。

## 裁决维度逐项结论

| 维度 | 结论 | 证据 |
|---|---|---|
| precision=1（无臆造） | ✅ | 覆盖率 added 恰 `{d9-1}`、无预期外新增；3 域外单测 `Assert.Null` |
| harm=0 | ✅ | diff `harm_new=0`；覆盖率 harm 0→0 |
| LOST=0 | ✅ | 两次 diff 均 `LOST=0` |
| WORSEN=0 | ✅ | 两次 diff 均 `WORSEN=0` |
| R4 无臆造 | ✅ | 域外 return-null 路径未改；无锚点不构造响应 → 节点天然缺席 |
| 未改既有 JSON 与评估判分 | ✅ | 本批仅新增 `impurity-traps.json`；correct/adopted/harm 全不变（635/635/0） |
| 契约加性不破坏消费者 | ✅ | 全量 test 0 failed；既有 4 个 trap 测试仍过；分发点/采纳门未改 |
| 化学事实只存 JSON | ✅ | AC1 grep 真值子串 0 命中；HEAD 原字面量与 JSON repr 级相等 |
| d9-1 可溯源 | ✅ | checkpoint 计数 0→1、conclusion 字节相等；单测断言节点结构 |

## DISCREPANCY

**未发现任何与预期不符之处。** 7 个闸门输出与 spec/tasks 预期逐字一致，无 DISCREPANCY。

## 总体裁决：**APPROVE**

本批变更自洽且隔离：`ImpurityTrapSolver.cs` 仅将载荷改为从 JSON 读取并加性填充 1 个 `ReasoningNode`，触发识别 `if` 与所有行为路径字节级不变；新增 JSON 与测试文件零 warning；全量测试 0 failed；两次 eval diff 全零；覆盖率硬断言恰 `{d9-1}` 翻转；台账同步。

### 残留风险/观察（非阻塞）
1. `checkpoint_after_b_impurity.jsonl` 由实现方离线生成（`LlmAvailable=False`），本复核未重跑 eval，但其完整性已由 `diff_ckpt` 全零 + 覆盖率脚本 + 直接 d9-1 读 + `.done` 标记交叉验证。
2. checkpoint 仅持久化节点计数而非节点明细，节点内容正确性由 `ImpurityTrapExternalizationTests` 运行时断言兜底（已通过）。
3. 工作树混有 b-externalize / b-judge-api 未提交改动；本批 diff 自身仅限于 `ImpurityTrapSolver.cs` + 新 JSON/测试 + 两本台账 + 本批 spec 目录，在合并工作树上 build/test/diff 全绿。
