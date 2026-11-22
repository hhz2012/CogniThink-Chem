# B-Impurity 批 实现任务队列

> 父规格：[spec.md](./spec.md)。基线 checkpoint：`after_b_judge`。
> 状态字段仅取：pending / in_progress / blocked / completed / cancelled。
> 每个任务完成时回填「Completion Evidence」。

## 锁定的回归期望值（AC2，从 `after_b_judge` 一手捕获，迁移后必须字节级相等）

| 求解器 | 案例 | 期望 FinalResult（基线字节） | 迁移后节点 |
|---|---|---|---|
| ImpurityTrap | d9-1 | `不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。` | **非空**（1 节点：命中 `impurity-traps.json` 条目，`KnowledgeId`=条目 id） |

> 非空集合 = 1 例（`d9-1`）；空/域外集合 = 3 例（`乙烯使酸性高锰酸钾褪色` / `溴水除甲烷中乙烯` / `KMnO4 受热分解方程`）保持 return-null。覆盖率脚本（AC8）硬断言获得节点的案例集合恰为 `{d9-1}`，无预期外案例获得节点。基线命中集合已由 checkpoint 实证（757 例 grep 特征结论恰 1 例）。

---

## Task 1: 新建 impurity-traps.json + 重写 ImpurityTrapSolver 加载 JSON + 线程化 KnowledgeId + 单测

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC1, AC2, AC3, AC9
- **Changes**:
  1. 新建 `data/knowledge/impurity-traps.json`（`version`/`source` 元数据 + `entries[]`，每条含稳定语义化 `id` / `finalResult` / `derivationSteps[]` / `constraintNote` / `errorTags[]`）。逐字拷入当前硬编码的化学真值载荷（`FinalResult` / 3 条 `DerivationSteps` / `ConstraintNote` / `ErrorTags=["E04"]`），**文本字节级不变**。建议 id：`impurity-kmno4-ethylene-co2`。
  2. `ImpurityTrapSolver.cs`：新增私有 entry 类 + `ImpurityTrapsData` + `Lazy<>` + `LoadTraps()`（`DataRoot.Resolve()` 拼 `knowledge/impurity-traps.json`，`JsonSerializer` 用 `PropertyNameCaseInsensitive`+`ReadCommentHandling.Skip`+`AllowTrailingCommas`，try/catch 返回空字典），镜像 `ProductWriterSolver` 范式。
  3. 触发识别 `if ((text.Contains("高锰酸钾")||text.Contains("KMnO4")) && text.Contains("乙烯") && (text.Contains("除")||text.Contains("除去")||text.Contains("去除")))` **保持字节级不变**；命中后从 JSON 取条目构造 `EngineResponse`（`FinalResult`/`DerivationSteps`/`ConstraintNote`/`ErrorTags` 全部来自 JSON 条目），不再内联字面量。
  4. 命中时加性填充 `ReasoningNodes`：1 节点（`Step=1`、`KnowledgeId=entry.Id`、`Conclusion=FinalResult`、`EvidenceLevel=KnowledgeFact`、`Premises` 引用 JSON 条目）。JSON 条目缺失/加载失败 → 行为：`LoadTraps` 返回空 → 命中但 `TryGetValue` 失败 → 返回 null（与 b-externalize 一致；实际 JSON 经 csproj 全局拷贝始终存在，eval 将以 diff 全零兜底）。
  5. XML doc 注释重写为通用描述（参照 `ProductWriterSolver`），不含化学真值载荷字面量（避免 AC1 grep 命中）。
  6. `Ok`/`IsStudentAnswerCorrect`/`Confidence`/所有 return-null 路径字节级不变。
- **Test Requirements**:
  - (rule) 新建 `tests/CogniThink.TutorBrain.Tests/ImpurityTrapExternalizationTests.cs`：命中 stem → `FinalResult` 与基线字节相等（`Assert.Equal` 精确串）、`ReasoningNodes.Count==1`、节点 `KnowledgeId=="impurity-kmno4-ethylene-co2"`、`Conclusion==FinalResult`、`EvidenceLevel==KnowledgeFact`。
  - (rule) 3 域外 stem → `Assert.Null`（R4 不臆造）。
  - (rule) grep `ImpurityTrapSolver.cs`：`氧化生成 CO₂|引入新杂质|应用溴水|1,2-二溴乙烷|达不到除杂目的|强氧化性` 命中数 = 0。
  - (rule) `dotnet build` 0 error、0 新增 warning。
- **Completion Evidence**: ✅ 已完成。新建 `data/knowledge/impurity-traps.json`（1 条，id=`impurity-kmno4-ethylene-co2`，`version`/`source` 元数据齐全，化学真值载荷文本逐字拷自原硬编码、字节级不变）。`ImpurityTrapSolver.cs` 重写为 `Lazy<>` + `DataRoot.Resolve()` 直读 JSON（镜像 `ProductWriterSolver` 范式），触发识别 `if` 字节级不变，命中后从 JSON 条目构造响应并加性填充 1 个 `ReasoningNode`（`KnowledgeId=trap.Id`、`Conclusion=FinalResult`、`EvidenceLevel=KnowledgeFact`、`Step=1`）；XML doc 改为通用描述。`Ok`/`IsStudentAnswerCorrect`/`Confidence`/return-null 路径不变。新建 `ImpurityTrapExternalizationTests.cs`（4 测）：命中 → `FinalResult` 与基线字节相等 + `ReasoningNodes.Count==1` + `KnowledgeId==impurity-kmno4-ethylene-co2` + `Conclusion==FinalResult` + `EvidenceLevel==KnowledgeFact`；3 域外 → `Assert.Null`（R4）。`dotnet build -c Debug` → 0 error / 13 warning（全基线：7 预存代码 + 6 NETSDK1206 环境），grep build 日志 `ImpurityTrap` = 0 命中（本批文件 0 新增 warning）。AC1 grep `ImpurityTrapSolver.cs` 真值子串（氧化生成 CO₂/引入新杂质/应用溴水/1,2-二溴乙烷/达不到除杂目的/强氧化性）= **0 命中**。定向测试 `ImpurityTrapExternalizationTests|Stage3ESolversTests` → **21 passed / 0 failed / 0 skipped**（新 4 + 既有 17，既有 4 个 trap 测试仍过 → 加性节点不破坏；JSON 经 csproj 拷贝被 `DataRoot` 正确加载，由 `KnowledgeId` 断言通过实证）。AC9 JSON 质量自评 2/2（元数据齐全、id 稳定语义化、结构与 known-equations/product-rules 一致）。

---

## Task 2: 全量测试 + build 闸门

- **Status**: completed
- **Depends On**: Task 1
- **Priority**: high
- **覆盖 AC**: AC4, AC5
- **Changes**:
  1. 全量 `dotnet test CogniThink.Chem.sln`（TutorBrain + Chem）0 failed、0 新增跳过。
  2. `dotnet build -c Release` 0 error；本批改动/新增文件（`ImpurityTrapSolver.cs` + 新 JSON + 新测试文件）在 warning 中命中 0 次。
  3. 重点确认 `Stage3ESolversTests` 既有 4 个 ImpurityTrap 测试全过、0 新增跳过。
- **Test Requirements**:
  - (rule) `dotnet test` 全解决方案：0 failed。
  - (rule) `dotnet build -c Release`：0 error；本批文件 0 warning 命中。
- **Completion Evidence**: ✅ 已完成。全量 `dotnet test CogniThink.Chem.sln -c Debug --no-build` → CogniThink.Chem.Tests **913 passed / 0 failed / 0 skipped** + CogniThink.TutorBrain.Tests **1396 passed / 0 failed / 0 skipped**（基线 1392 + 本批新增 4 = 1396，0 新增跳过），合计 **2309 passed / 0 failed / 0 skipped**，exit 0；既有 `Stage3ESolversTests` 4 个 ImpurityTrap 测试全过。`dotnet build -c Release` → **0 error / 12 warning**（与基线一致：7 预存代码 + 5 NETSDK1206 环境），grep Release 日志 `ImpurityTrap` = **0 命中**（本批 3 个文件 0 新增 warning）。AC4 / AC5 满足。

---

## Task 3: 离线 eval diff + 覆盖率自检

- **Status**: completed
- **Depends On**: Task 2
- **Priority**: high
- **覆盖 AC**: AC6, AC7, AC8
- **Changes**:
  1. 离线 eval：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "d:\github\CogniThink-Chem\_evalscratch\run_eval_resume.ps1" -Tag after_b_impurity -Env Production`（`LlmAvailable=False`，757 例 0 failed）。
  2. `python _evalscratch\verify\diff_ckpt.py after_b_judge after_b_impurity` → `GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`（AC6）。
  3. `python _evalscratch\verify\diff_ckpt.py base_p0 after_b_impurity` → `LOST=0 / WORSEN=0 / harm=0 → PASS`（`GAINED=6` 跨批次累计，AC7）。
  4. 写 `_evalscratch/verify/b_impurity_coverage.py`（gitignored）：断言获得非空节点的案例集合**恰等于** `{d9-1}`、无丢失、无预期外新增；`d9-1` 节点计数 0→1；`d9-1` `gatewayConclusion` 字节不变；`correct`/`adopted`/`harm` 不变。输出 `ALL ASSERTIONS PASSED`（AC8）。
- **Test Requirements**:
  - (rule) 两次 `diff_ckpt` 输出均含 `RED-LINE ... -> PASS`，且 `after_b_judge→after_b_impurity` 全零。
  - (rule) 覆盖率脚本输出 `ALL ASSERTIONS PASSED`，added 集合 == `{d9-1}`。
- **Completion Evidence**: ✅ 已完成。离线 eval：`run_eval_resume.ps1 -Tag after_b_impurity -Env Production`（`LlmAvailable=False`）→ 757/757 完成、0 失败，写出 `checkpoint_after_b_impurity.jsonl` + `.done` 标记 + 摘要。两次 `diff_ckpt.py`：① `after_b_judge after_b_impurity` → `GAINED=0 / LOST=0 / WORSEN=0 / harm_old=0 harm_new=0` → **RED-LINE PASS**；② `base_p0 after_b_impurity` → `GAINED=6`（与上一批完全相同的 6 个累计 id：pdf-415/530/535/542/543/trap-limit-2，d9-1 正确性未变，非本批）/ `LOST=0 / WORSEN=0 / harm=0` → **PASS**。覆盖率自检：新建 `_evalscratch/verify/b_impurity_coverage.py`（gitignored），断言 added 集合恰 `{d9-1}`、removed=∅、`d9-1` 节点 0→1、`d9-1` `gatewayConclusion` 字节不变、correct/adopted/harm 不变（635/635/0）→ 输出 **ALL B-IMPURITY COVERAGE ASSERTIONS PASSED**（non-empty 89→90，恰 d9-1 翻转）。AC6 / AC7 / AC8 满足。

---

## Task 4: 台账更新

- **Status**: completed
- **Depends On**: Task 3
- **Priority**: high
- **覆盖 AC**: AC10
- **Changes**:
  1. 更新 [data-defect-register.md §N.2](../../../data-defect-register.md)：`ImpurityTrapSolver` 行标 ✅ 已迁移（B-Impurity 批）→ 分级 `A·done`，记录 eval tag `after_b_impurity`、`d9-1` 推理节点 0→1 证据；分级统计 B 由 2 核减为 **1**（仅剩 `MultiPartDispatch` 传播）；建议执行顺序 (4) ImpurityTrap 标完成。
  2. 更新父 [tasks.md](../unified-reasoning-dag/tasks.md)：Status / A·done 计数（9→10）/ B 计数（2→1）/ 执行顺序 (4) 同步。
  3. 回填本文件各 Task 的 Completion Evidence。**不提交 commit**（除非用户明确要求）。
- **Test Requirements**:
  - (rule) register / 父 tasks diff 显示 ImpurityTrap 行标已迁移、A·done=10、B=1。
- **Completion Evidence**: ✅ 已完成。`data-defect-register.md §N.2`：引言已迁移计数 9→**10**（新增 **B-Impurity 批：ImpurityTrapSolver**）；`ImpurityTrapSolver` 行标 ✅ 已迁移 (B-Impurity 批)、分级 `B-低`→`A·done`，记录 eval tag `after_b_impurity`、`d9-1` 节点 0→1、gatewayConclusion 字节不变证据；分级统计 A=9→**10**、B=2→**1**（仅剩 MultiPartDispatch 传播）；建议执行顺序新增 (4) ✅ B-Impurity 批已完成、原剩余顺延为 (5)。父 `.trae/specs/unified-reasoning-dag/tasks.md`：Status 追加 B-Impurity 已完成、A·done=9→**10**、B=2→**1**、执行顺序 item 4 标完成 + item 5 剩余。git status 确认本批仅改 `ImpurityTrapSolver.cs` + 新 `impurity-traps.json` + 新测试 + 两个台账 + 本批 spec 目录，无 `_Tmp*.cs` 残留。AC10 满足。**未提交 commit**（等用户明确要求）。

---

## Task 5: fresh-context 独立 Review（spec mode 强制 gate）

- **Status**: completed
- **Depends On**: Task 4
- **Priority**: high
- **覆盖 AC**: 全部 AC 独立复核
- **Changes**: 委托 fresh-context **只读** 子代理独立复跑：build / 全量 test / 两次 eval diff / 覆盖率脚本 / `d9-1` 节点抽样 / 密钥扫描 / `ImpurityTrapSolver.cs` grep 0 残留 / 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数（R4 无臆造）/ 未改既有 JSON 与评估判分 / 契约加性不破坏消费者 / 化学事实只存 JSON / `d9-1` 可溯源。子代理仅返回报告文本，由主代理落盘同目录 `review.md`。
- **Test Requirements**:
  - (rule) 独立复跑 build 0 error、test 0 failed、两次 diff 均 RED-LINE PASS、覆盖率脚本 PASS。
  - (rule) 代码核对：`ImpurityTrapSolver.cs` grep 真值子串 0 命中、`LlmGatewayService` 分发点未改、采纳门未动、无 `_Tmp*.cs` 残留、无密钥。
- **Completion Evidence**: ✅ 已完成。fresh-context 只读子代理 7 道闸门全部 PASS、零 DISCREPANCY，总体裁决 **APPROVE**（报告落盘 [review.md](./review.md)）。证据：① `dotnet build -c Debug --no-incremental`（clean 后全量重编）→ 0 error / 13 warning（7 基线代码 + 6 NETSDK1206 环境），本批 3 文件 0 warning 命中；② 全量 `dotnet test` → Chem 913 + TutorBrain 1396 = **2309 passed / 0 failed / 0 skipped**；③ 两次 diff 均 RED-LINE PASS（after_b_judge→after_b_impurity 全零；base_p0→after_b_impurity GAINED=6 跨批累计）；④ 覆盖率脚本 `ALL B-IMPURITY COVERAGE ASSERTIONS PASSED`（added 恰 {d9-1}、removed=∅、d9-1 节点 0→1、conclusion 字节不变、correct/adopted/harm 635/635/0 不变）；⑤ 代码核对：AC1 grep 真值子串 0 命中、触发识别 if 字节级未改、HEAD 原字面量与 JSON repr 级相等（零漂移）、分发点/采纳门未动、无 `_Tmp*.cs`、无密钥、JSON 元数据齐全 id 稳定（AC9 2/2）；⑥ 抽样：d9-1 checkpoint 节点 0→1 且 conclusion 字节相等，定向测试 21 passed；⑦ 台账核对：register §N.2 与父 tasks.md 均 A=10/B=1、ImpurityTrap 行 ✅、执行顺序 item 4 完成。残留风险仅提示性（checkpoint 由实现方离线生成但经 diff+覆盖率+直读交叉验证；checkpoint 只存计数不存明细由单测兜底；工作树混合其它未提交批），均非阻塞。

---

## 执行顺序与风险控制

1. **Task 1 单文件聚焦**（仅 `ImpurityTrapSolver.cs` + 新 JSON + 新测试），build 一次尽早捕获编译错误。
2. Task 1 的精确 `FinalResult` 单测是**零漂移第一道闸**（在 eval 前锁定行为等价）。
3. Task 2 全量 test 是第二道闸（契约加法不破坏既有消费者）。
4. Task 3 的 `diff_ckpt` 是第三道闸（覆盖未进单测的案例 + 网关传播）；覆盖率脚本是第四道闸（防臆造 / 防漏发，硬断言恰 `{d9-1}` 翻转）。
5. **R4 是本批最高风险点**：任何域外路径发节点都必须被 Task 1 单测（3 例 null）与 Task 3 覆盖率脚本（无预期外新增）双重拦截。若任一闸失败：回 Task 1 修复，不进入下一闸；不蒙混通过。
