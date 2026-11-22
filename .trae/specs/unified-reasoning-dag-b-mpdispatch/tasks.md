# B-MpDispatch 批 实现任务队列

> 父规格：[spec.md](./spec.md)。基线 checkpoint：`after_b_impurity`。红线锚点 `base_p0`。
> 状态字段仅取：pending / in_progress / blocked / completed / cancelled。
> 每个任务完成时回填「Completion Evidence」。

## 关键基线事实（决定 AC 写法，从 `after_b_impurity` 一手捕获）

| 项 | 基线值（运行时实证） | 本批后期望 |
|---|---|---|
| `taskType == "multi_part"` 案例数 | **0**（757 例） | 仍 0（分发路径 eval 不触发） |
| `gatewayReasoningNodesCount` 分布 | `{0:667, 1:69, 2:18, 3:1, 10:1, 11:1}` | 字节级不变（本批零波及主链） |
| `after_b_impurity → after_b_mpdispatch` diff | — | 全零（GAINED/LOST/WORSEN/harm 均 0），平凡成立 |
| 传播正确性证明途径 | — | **单测**（eval 不触发，不能靠 eval 证） |

> 因 `multi_part=0`，本批对 eval 是平凡零影响；AC8/AC9 仍强制跑 eval 与覆盖率自检，目的是证明本批未意外波及主链。覆盖率断言兼容 0 added（`added == ∅`）。

---

## Task 1: 抽 MultiPartReasoningNodes.Merge 纯函数 + 改 MultiPartDispatch 传播 + 单测

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC1, AC2, AC3, AC4, AC5, AC11
- **Changes**:
  1. 新建 `internal static class MultiPartReasoningNodes`（可放 `LlmGatewayService.MultiPartDispatch.cs` 同文件或独立文件；镜像 `JudgeApiReasoningNodes` 的 `internal` + `InternalsVisibleTo` 可达）。
  2. 方法 `internal static List<ReasoningNode> Merge(List<(int Index, EngineResponse Sub)> parts, string aggregatedFinalResult)`：
     - 遍历 `parts`：若 `part.Sub.ReasoningNodes` 为 null/空 → 跳过该子（R4：空子不发节点）。
     - 否则逐个克隆子节点：`Step` 重排为连续自增（从 1），`Description` 前缀标注子问归属（如 `({idx}) ` 或 `【子问({idx})】`），其余字段（`Premises`/`RuleId`/`KnowledgeId`/`Conclusion`/`EvidenceLevel`）**原样拷贝**（AC5/AC11：不改子节点真值字段）。
     - 若合并后 `nodes.Count > 0`：末尾追加 **1** 个汇总节点（`Step` = 下一个连续号、`Description` = 多部分聚合说明、`Premises` = `nodes.Select(n=>n.KnowledgeId).Where(非空).Distinct().ToList()`、`Conclusion` = `aggregatedFinalResult`、`EvidenceLevel` = `KnowledgeFact`，镜像 `JudgeApiReasoningNodes`）。
     - `nodes.Count == 0` 时**不**追加汇总节点，返回空列表（R4，AC2）。
  3. 改 `LlmGatewayService.MultiPartDispatch.cs` 的 `TryDispatchMultiPartAsync`：
     - `parts` 收集从 `List<(int Index, string Result)>` 改为保留子响应本体（`List<(int Index, EngineResponse SubResp)>` 或等价），以便取节点；`parts.Add((idx, subResp))`。
     - 聚合 `aggregated` 字符串仍从 `subResp.FinalResult.Trim()` 构造，**逻辑字节级不变**（AC5）。
     - 返回的 `EngineResponse` 增加 `ReasoningNodes = MultiPartReasoningNodes.Merge(parts, aggregated)`（空列表也直接赋，不赋 null——CS8601 教训）。
     - `Ok` / `FinalResult` / `DerivationSteps` / `ConstraintNote` / `Confidence` / return-null 路径 / `TaskType` **一律不动**。
  4. 探测是否存在端到端干净题干（整题 `Ok!=true` 且各子问均可解出带节点）：若有，补 1 条经 `ProcessStudentMessageAsync` 的集成测试断言父节点；若无，记录为「以合并器单测 + 接线审查为准」，不阻塞（spec OQ）。
- **Test Requirements**:
  - (rule) 新建 `tests/CogniThink.TutorBrain.Tests/MultiPartReasoningNodesTests.cs`：
    - AC1：全子带节点 → 父节点总数 = 子节点之和、`Step` 连续无跳步无重复、子节点可归属子问、末尾恰 1 汇总节点且 `Conclusion==aggregated`、`Premises` 等于去重非空 `KnowledgeId` 集合。
    - AC2：全子空 → `Assert.Empty`（不臆造汇总）。
    - AC3：部分子空 → 空子贡献 0 节点、汇总 `Premises` 仅含实际 `KnowledgeId`。
    - AC4：`Assert.Equal(Enumerable.Range(1, K), nodes.Select(n => n.Step))`。
  - (rule) AC5：既有 `MultiPartUtilityTests` / `MultiPartAndScoringTests` 全过（加性不破坏）；单测断言聚合 `FinalResult` 与原 `"(idx)result "` 构造等价。
  - (rule) AC11：grep 本批 .cs `sk-|api_key|Bearer|secret` = 0；合并器为 `internal static` 纯函数。
- **Completion Evidence**: ✅ 已完成。新建 [MultiPartReasoningNodes.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiPartReasoningNodes.cs)：`internal static List<ReasoningNode> Merge(List<(int Index, EngineResponse SubResp)> parts, string aggregatedFinalResult)`——遍历子响应，空子（null/空 `ReasoningNodes`）跳过（R4），有节点的子逐个克隆（`Step` 重排连续自增、`Description` 前缀 `(idx) ` 标注归属、`Premises`/`RuleId`/`KnowledgeId`/`Conclusion`/`EvidenceLevel` 原样拷贝）；存在子节点时末尾追加 1 个汇总节点（`Conclusion`=aggregated、`Premises`=去重非空 `KnowledgeId`、`EvidenceLevel=KnowledgeFact`），全空则返回空列表不臆造汇总。改 [LlmGatewayService.MultiPartDispatch.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs)：`parts` 改为保留子响应本体 `List<(int Index, EngineResponse SubResp)>`，`parts.Add((idx, subResp))`；聚合 `aggregated` 仍由 `sub.FinalResult.Trim()` 构造（逻辑字节级不变，AC5）；返回 `EngineResponse` 加性赋值 `ReasoningNodes = MultiPartReasoningNodes.Merge(parts, aggregated)`（空列表直接赋、不赋 null，CS8601 教训）；`Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`/return-null/`TaskType` 一律未动。新建 [MultiPartReasoningNodesTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartReasoningNodesTests.cs)（6 测，覆盖 AC1 全子传播+汇总去重 / AC2 全空不臆造 / AC3 部分空仅传有节点子 / AC4 Step 连续重排 / AC11 子节点原样克隆+原始子响应未被修改+汇总仅在有子节点时追加）。`dotnet build -c Debug` → **0 error / 7 warning**（全预存基线：AdaptiveTeachingOrchestrator×2 / CrystalCellValidator / Diagnostic 测试×3 / HardBonesTraceTests，本批 3 文件 0 warning 命中）。定向测试 `MultiPartReasoningNodesTests|MultiPartUtilityTests|MultiPartAndScoringTests` → **26 passed / 0 failed / 0 skipped**（新 6 + 既有 20，既有全过 → 加性不破坏）。密钥扫描本批 .cs（`sk-|api_key|Bearer|secret`）= **0 命中**。端到端集成测试（spec OQ）：测试程序集无 `LlmGatewayService` 夹具、757 例 multi_part=0 无自然题干触发全子可解分发，按 spec OQ 以合并器单测 + 接线一行审查为准，不阻塞。

---

## Task 2: build 闸门 + 全量测试

- **Status**: completed
- **Depends On**: Task 1
- **Priority**: high
- **覆盖 AC**: AC6, AC7
- **Changes**:
  1. `dotnet build CogniThink.Chem.sln -c Debug` 与 `-c Release`：0 error；本批改动/新增文件 0 新增 warning（基线 Debug 13 / Release 12 为预存 + NETSDK1206 环境，不计）。
  2. 全量 `dotnet test CogniThink.Chem.sln`：0 failed、0 新增跳过（基线 Chem 913 + TutorBrain 1396 = 2309 passed 维持）。
  3. 确认既有 `MultiPartUtilityTests` / `MultiPartAndScoringTests` 仍过、0 新增跳过。
- **Test Requirements**:
  - (rule) `dotnet build` 两配置 0 error；grep build 日志本批文件名 = 0 warning 命中。
  - (rule) `dotnet test` 全解决方案 0 failed。
- **Completion Evidence**: ✅ 已完成。`dotnet build -c Debug` → **0 error / 7 warning**；`dotnet build -c Release` → **0 error / 7 warning**（日志存 `_evalscratch/build_release_mpd.log`）。grep 两配置 build 日志本批文件名（`MultiPartReasoningNodes` / `LlmGatewayService.MultiPartDispatch` / `MultiPartReasoningNodesTests`）= **0 warning 命中**；7 个 warning 全为预存基线（AdaptiveTeachingOrchestrator×2 / CrystalCellValidator / DiagnosticErrorPronePointsTests×2 / DiagnosticTeachingEngineTests / HardBonesTraceTests），0 新增。全量 `dotnet test CogniThink.Chem.sln -c Debug --no-build` → CogniThink.Chem.Tests **913 passed / 0 failed / 0 skipped** + CogniThink.TutorBrain.Tests **1402 passed / 0 failed / 0 skipped**（基线 1396 + 本批新增 6 = 1402），合计 **2315 passed / 0 failed / 0 skipped**，exit 0。既有 `MultiPartUtilityTests` / `MultiPartAndScoringTests` 仍过、0 新增跳过。AC6 / AC7 满足。

---

## Task 3: 离线 eval + 双 diff + 覆盖率自检

- **Status**: completed
- **Depends On**: Task 2
- **Priority**: high
- **覆盖 AC**: AC8, AC9
- **Changes**:
  1. 离线 eval：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "d:\github\CogniThink-Chem\_evalscratch\run_eval_resume.ps1" -Tag after_b_mpdispatch -Env Production`（`LlmAvailable=False`，757 例 0 failed）。
  2. `python _evalscratch\verify\diff_ckpt.py after_b_impurity after_b_mpdispatch` → `GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`（AC8，平凡成立但必跑）。
  3. `python _evalscratch\verify\diff_ckpt.py base_p0 after_b_mpdispatch` → `LOST=0 / WORSEN=0 / harm=0 → PASS`（`GAINED` 跨批次累计，AC8）。
  4. 写 `_evalscratch/verify/b_mpdispatch_coverage.py`（gitignored）：断言 `after_b_impurity → after_b_mpdispatch` 的 `gatewayReasoningNodesCount` 变化案例 `added == ∅` 且 `removed == ∅`；`taskType==multi_part` 计数仍 0；`correct`/`adopted`/`harm` 不变。输出 `ALL ASSERTIONS PASSED`（AC9，防主链被波及）。
- **Test Requirements**:
  - (rule) 两次 `diff_ckpt` 均含 `RED-LINE ... -> PASS`（或 `PASS`），`after_b_impurity→after_b_mpdispatch` 全零。
  - (rule) 覆盖率脚本输出 `ALL ASSERTIONS PASSED`，`added`/`removed` 均空、multi_part 仍 0。
- **Completion Evidence**: ✅ 已完成。离线 eval `run_eval_resume.ps1 -Tag after_b_mpdispatch -Env Production` → **757/757 done / 0 failed**，产出 `checkpoint_after_b_mpdispatch.jsonl` + `snapshot_after_b_mpdispatch.json` + summary。双 diff：① `diff_ckpt after_b_impurity after_b_mpdispatch` → **GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS**（平凡零影响，因 multi_part=0 分发路径不触发）；② `diff_ckpt base_p0 after_b_mpdispatch` → **LOST=0 / WORSEN=0 / harm=0 → PASS**（GAINED=6 为前批次累计 pdf-415/530/535/542/543 + trap-limit-2，非本批）。覆盖率自检脚本 [b_mpdispatch_coverage.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/b_mpdispatch_coverage.py)（`git check-ignore` 确认已忽略）→ 断言 case 757==757、multi_part 0==0、added/removed 均空、0 案例 `gatewayReasoningNodesCount` 有任何 delta、correct 635==635 / adopted 635==635 / harm 0==0 → **ALL B-MPDISPATCH COVERAGE ASSERTIONS PASSED**，exit 0。AC8 / AC9 满足。

---

## Task 4: 台账更新

- **Status**: completed
- **Depends On**: Task 3
- **Priority**: high
- **覆盖 AC**: AC10
- **Changes**:
  1. 更新 [data-defect-register.md §N.2](../../../data-defect-register.md)：`LlmGatewayService.MultiPartDispatch.cs` 行（L617）标 ✅ 已迁移（B-MpDispatch 批）→ 分级 `A·done`，记录 eval tag `after_b_mpdispatch`、传播靠单测保证（multi_part=0 平凡零影响）证据；分级统计 B 由 1 核减为 **0**、A·done 由 10 增至 **11**；建议执行顺序 (5) 中 MultiPartDispatch 部分标完成（注意 (5) 仍含 `LlmGatewayService` 主体 66 处迁移，不在本批，保持剩余）。
  2. 更新父 [tasks.md](../unified-reasoning-dag/tasks.md)：Status 追加 B-MpDispatch 已完成、A·done=10→**11**、B=1→**0**、执行顺序 item 5 同步。
  3. 回填本文件各 Task 的 Completion Evidence。**不提交 commit**（除非用户明确要求）。
- **Test Requirements**:
  - (rule) register / 父 tasks diff 显示 MultiPartDispatch 行标已迁移、A·done=11、B=0。
- **Completion Evidence**: ✅ 已完成。[data-defect-register.md §N.2](file:///d:/github/CogniThink-Chem/data-defect-register.md) 4 处：① L612「✅ 标记的 10 个」→「11 个」并追加 **B-MpDispatch 批：MultiPartDispatch**；② L617 `LlmGatewayService.MultiPartDispatch.cs` 行翻为 ✅ 已迁移 (B-MpDispatch 批) / 分级 `A·done` / 阻塞 `—`（记录子节点 Step 重排+标注归属+末尾汇总、子全空父留空 R4、仅加性不改判决字段、eval diff 全零 multi_part=0 靠单测 6 例、0 案例节点变化）；③ L631 分级统计 `已迁移 A=10`→`A=11`（追加 B-MpDispatch 证据）、`B=1（传播 1：MultiPartDispatch）`→`B=0`；④ L633 建议执行顺序 (5) 标 ✅ B-MpDispatch 已完成，剩余仅 `LlmGatewayService` 传播点+逐点子分类计算点。父 [tasks.md](file:///d:/github/CogniThink-Chem/.trae/specs/unified-reasoning-dag/tasks.md) 4 处：L108 Status 追加 B-MpDispatch 已完成+下一批说明、L111 A·done=10→11 并追加证据、L113 B=1→B=0（B 类全部迁移完毕）、L124 执行顺序 item 5 标 ✅ 完成+剩余 LlmGatewayService。grep 校验：权威文件无残留 `B=1`/`A·done=10`（b-impurity 历史 tasks.md 的 A·done=10/B=1 为当时正确快照，不动）。未提交 commit（用户未要求）。AC10 满足。

---

## Task 5: fresh-context 独立 Review（spec mode 强制 gate）

- **Status**: completed
- **Depends On**: Task 4
- **Priority**: high
- **覆盖 AC**: 全部 AC 独立复核
- **Changes**: 委托 fresh-context **只读** 子代理独立复跑：build / 全量 test / 两次 eval diff / 覆盖率脚本 / 合并器单测 / 代码核对（R4 全空不臆造 / 子节点字段未改 / 仅加性 / 无 `_Tmp*.cs` 残留 / 无密钥 / 未碰采纳门与判分）/ 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数（R4 无臆造）/ 未改评估判分 / 契约加性不破坏消费者 / 合并器纯函数可测 / multi_part 平凡零影响有单测兜底。子代理仅返回报告文本，由主代理落盘同目录 `review.md`。
- **Test Requirements**:
  - (rule) 独立复跑 build 0 error、test 0 failed、两次 diff 均 PASS、覆盖率脚本 PASS、合并器单测 PASS。
  - (rule) 代码核对：R4 全空→空（不臆造汇总）、子节点 `KnowledgeId`/`RuleId`/`Conclusion`/`EvidenceLevel` 原样拷贝、`Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`/采纳门未动、无 `_Tmp*.cs`、无密钥。
- **Completion Evidence**: ✅ 已完成。委托 fresh-context 只读子代理（`general_purpose_task`，独立上下文）独立复跑全部闸门并只读核对代码，裁决 **APPROVE / 11/11 AC 通过 / 0 Issue**。主代理已将报告落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/unified-reasoning-dag-b-mpdispatch/review.md)。子代理独立证据：① `dotnet build` Debug+Release（强制 `--no-incremental`）→ 0 error / 13 warning（=7 预存代码 + 6 NETSDK1206 基线），grep 本批 3 文件 0 warning 命中；② 合并器定向单测 `MultiPartReasoningNodesTests` → 6 passed/0 failed；③ 全量 test → Chem 913 + TutorBrain 1402 = 2315 passed/0 failed/0 skipped；④ `diff_ckpt after_b_impurity after_b_mpdispatch` → 全零 RED-LINE PASS；⑤ `diff_ckpt base_p0 after_b_mpdispatch` → LOST/WORSEN/harm=0 PASS；⑥ `b_mpdispatch_coverage.py` → ALL ASSERTIONS PASSED（added/removed 空、multi_part=0、0 案例节点变化、correct/adopted/harm 不变）；⑦ 代码核对：R4 全空→空不臆造汇总、子节点真值字段原样克隆且原子响应未被修改、`git diff` 纯加性（仅新增 `ReasoningNodes=Merge(...)`、聚合串字节不变、判决字段/return-null/TaskType/采纳门未动）、grep 密钥 0 命中、无 `_Tmp*.cs`、无 `data/*.json` 改动；⑧ 台账核对：A·done=11、B=0、MultiPartDispatch 行已迁移、父 tasks.md 同步、git grep 无残留 B=1/A·done=10。无待修复项。未提交 commit（用户未要求）。

---

## 执行顺序与风险控制

1. **Task 1 单文件聚焦**（仅 `LlmGatewayService.MultiPartDispatch.cs` + 合并器 + 新测试），build 一次尽早捕获编译错误。
2. Task 1 的合并器单测（AC1-AC4）是**正确性第一道闸**——因 eval 不触发分发路径，单测是传播正确性的唯一证明，必须全覆盖（全子带节点 / 全空 / 部分空 / Step 连续）。
3. Task 2 全量 test 是第二道闸（契约加法不破坏既有消费者，尤其是 `MultiPartAndScoringTests` 的精度守卫）。
4. Task 3 的 `diff_ckpt` + 覆盖率脚本是第三道闸：证明本批零波及主链（`added/removed` 均空、multi_part 仍 0）。
5. **R4 是本批最高风险点**：空子不得发节点、全空不得臆造汇总——由 Task 1 单测（AC2/AC3）拦截；若失败回 Task 1 修复，不进入下一闸。
6. **CS8601 防回归**：`ReasoningNodes` 直接赋合并列表（空列表即可），不得赋 null；Task 1 后 rebuild 确认 warning 回基线。
