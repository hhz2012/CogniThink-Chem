# P3-3 导学与求解融合（加性锚定） — 独立审查报告（fresh-context / 只读）

> 审查员角色：fresh-context 只读代码审查员（独立于实施者）。**严格只读**：未修改/创建/删除任何仓库文件；未 git commit。
> 审查范围：`.trae/specs/tutor-solve-fusion/` spec.md / tasks.md 的 AC-1..AC-10 与红线（precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON / 不泄露未采纳结论 / 零网关求解改动 / 零采纳门改动 / 导学既有行为零漂移）。
> 审查方法：不采信实施者自证，独立复跑全部验证命令并直读复核关键数字；主代理另以第一手运行结果交叉佐证（见各节「主代理佐证」）。

## 结论：PASS

全部裁决维度均独立复核通过，无任何 FAIL；无 Critical / Major / Minor Issue。eval 维持 correct=635 / precision=1 / harm=0 / total=757，`diff after_p32→after_p33` 全 0（零漂移）由「52 JSON sha256 与基线逐文件一致 + 触及清单不含网关/采纳门/eval 判分文件 + diff 逐 case 全 0」三重铁证锁定。审查员披露 6 项限制（见末尾），均为既有设计取舍或可扩展性局限，不影响 PASS 裁决。

---

## 一、各验证步骤实际命令输出摘要（cwd 均为 repo root `D:\github\CogniThink-Chem`）

| # | 命令 | 实际输出摘要 | 结果 |
|---|------|--------------|------|
| 1 | `dotnet build src\CogniThink.TutorBrain.Api\CogniThink.TutorBrain.Api.csproj -c Release --nologo -v q` | `已成功生成。0 个错误`（警告为既存 CS8602/CS8603/NETSDK1206，非 P3-3 引入）；exit 0 | ✅ 0 error（主代理佐证：实跑同输出） |
| 2 | `dotnet build tests\CogniThink.TutorBrain.Tests\...csproj -c Release --nologo -v q` | `已成功生成。0 个错误`；exit 0 | ✅ 0 error |
| 3 | `dotnet test ... --filter FullyQualifiedName~TutorSolveChainProviderTests` | `失败: 0，通过: 11，总计: 11`；exit 0 | ✅ T1 门控/缓存/降级 11/11 |
| 4 | `dotnet test ... --filter FullyQualifiedName~DiagnosticCheckpointLinkerTests` | `失败: 0，通过: 10，总计: 10`；exit 0 | ✅ T3 匹配/锚点 10/10 |
| 5 | `dotnet test ... --filter FullyQualifiedName~TutorSnapshotRegressionTests` | `失败: 0，通过: 4，总计: 4`；exit 0 | ✅ T5 快照回归（AC-4）4/4 |
| 6 | `dotnet test ...Tests.csproj -c Release --nologo -v q`（全量） | `失败: 0，通过: 1333，已跳过: 0，总计: 1333`；exit 0 | ✅ 1333/1333 全绿（主代理佐证：实跑同输出） |
| 7 | 直读 `_evalscratch\checkpoint_after_p33_summary.txt` | `total=757` `gatewayCorrect=635` `precision=1` `harmCount=0` | ✅ 与期望值一致 |
| 8 | `python _evalscratch\verify\diff_ckpt.py after_p32 after_p33` | `GAINED (0)` `LOST (0)` `WORSEN (0)` `harm_old=0 harm_new=0` `RED-LINE … PASS` | ✅ RED-LINE PASS（零漂移，主代理佐证：实跑同输出） |
| 9 | `python _evalscratch\verify\diff_ckpt.py base_p0 after_p33` | `GAINED (6): [pdf-415, pdf-530, pdf-535, pdf-542, pdf-543, trap-limit-2]` `LOST (0)` `WORSEN (0)` `harm_new=0` `RED-LINE PASS` | ✅ RED-LINE PASS（GAINED=6 历史携带，主代理佐证：实跑同输出） |
| 10 | `python _evalscratch\verify\p33_self_check.py` | 24 行 `[PASS]` + `P3-3 SELF-CHECK: ALL PASS`；exit 0 | ✅ 24/24 PASS（主代理佐证：实跑同输出） |
| 11 | 独立 grep：全仓 `StandardReasoningChain\|LinkedReasoningNode\|TutorSolveChainProvider` | 命中 9 文件，均在 `Application/Diagnostic/`、`Api/Controllers/`、`Application/`（TutorService+Provider）、`Program.cs`、`Api/Contracts/Dtos.cs`；对 `Application/Gateway` 目录单独 grep → `No matches found` | ✅ 网关目录 0 命中 |
| 12 | 独立核对 `data-defect-register.md` | 存在 §T（P3-3），位于 §S 之后、`## 处理原则` 之前 | ✅ 插入位置正确 |

自检 24 项明细（主代理实跑原文）：no secrets 0 hits / no touched file is gateway solver/gate/eval 0 hits / touched .cs define no solver/grading logic 0 hits / no touched file under Application/Gateway 0 hits / all 52 data JSON unchanged vs p31 baseline / gatewayCorrect=635 / harmCount=0 / precision=1 / total=757 / diff after_p32→after_p33 GAINED=LOST=WORSEN=harm_new=0 RED-LINE PASS / diff base_p0→after_p33 LOST=WORSEN=harm_new=0 GAINED=6 RED-LINE PASS / existing SubmitStepResponse keys all present / StandardReasoningChain additive / LinkedReasoningNode additive / frontend optional / snapshot test present。

---

## 二、代码审查发现（只读 + 加性 + 不泄露 + 不碰网关/判分）

通读 P3-3 触及文件（[TutorSolveChainProvider.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorSolveChainProvider.cs)、[TutorService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorService.cs)、[Dtos.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Contracts/Dtos.cs)、[DiagnosticCheckpointLinker.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticCheckpointLinker.cs)、[DiagnosticExplanationAnchor.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticExplanationAnchor.cs)、[DiagnosticTeachingEngine.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Diagnostic/DiagnosticTeachingEngine.cs)）：

1. **Provider 只读**：`GetChainAsync` 仅调 `TutorSolveChainGate.Extract`（纯函数，仅读 `EngineResponse`），测试 `Extract_DoesNotMutateInput` 断言输入节点数/状态不变；不写回任何网关字段。
2. **门控正确**：仅 `ConclusionState.Confirmed` 且 `ReasoningNodes` 非空下发（L42-47）；排除 `EvidenceLevel.Trend`（L50）；`Insufficient` / `CandidateSet` / 空 → `null`（不泄露未采纳结论，测试 `Insufficient_WithNodes_ReturnsNull_NoLeak` / `CandidateSet_WithNodes_ReturnsNull` 覆盖）。
3. **缓存 + 降级**：缓存命中即 `return cached`（含已缓存的 null，测试 `Provider_CacheHit_DoesNotReinvokeGateway` CallCount=1）；异常 catch 返回 null 且**不**调 `_cache.Set`（允许重试，测试 `Provider_Exception_ReturnsNull_NotCached_Retries` CallCount=2）。
4. **TutorService 取链纯加性**：取链在 `await _db.SaveChangesAsync(ct)`（L145）之后（L147-150）；仅填入 `SubmitStepResult` 末位字段 `StandardReasoningChain`（L25 / L152-161），不影响判分/话术/其他字段；`_chainProvider is null` 时短路为 null（L148-149）→ 等价 P3-3 前行为。
5. **wire 兼容**：`SubmitStepResponse` 末位新增 `List<ReasoningNode>? StandardReasoningChain`（可空可选），既有字段（Status…Variant）未删改；`ReasoningCheckpoint` 新增 `LinkedReasoningNode` 同理。
6. **诊断回溯纯加性**：`DiagnosticCheckpointLinker.Link` / `DiagnosticExplanationAnchor.Append` 均为 `static` 纯函数；Linker 只写 `cp.LinkedReasoningNode`、不改 chain 节点、`FirstMatchingNode` 排除 Trend；Anchor 在 `node is null` 时直接 return no-op；`DiagnosticTeachingEngine` 仅在 `chain is not null` 时调 Link（L105-106），故无链接时既有讲解逐字不变。
7. **未触网关/判分**：全仓 grep P3-3 三个新标识符对 `Application/Gateway` 目录 0 命中；16 个 `*Solver.cs` 与 `Application/Evaluation/*` 判分文件均不在命中清单；触及清单不含 `LlmGatewayService.cs` 主体 / `BuildGatewayAnswerAsync` / `IsStructuredCorrect`。
8. **未触数据**：52 个 `data/**` JSON sha256 与 `p31_data_baseline.json` 逐文件全同（self-check 第 5 项）；P3-3 触及清单含 0 个 `data/**`。

---

## 三、裁决维度判定表（任一 FAIL 即整体 FAIL）

| # | 维度 | 判定 | 独立证据 |
|---|------|------|----------|
| AC-1 | build 0 error（P3-3 触及文件 0 新警告） | ✅ PASS | Api + Tests Release build 均 0 error；警告为既存。 |
| AC-2 | 门控单测（Confirmed 下发无 Trend；Insufficient/CandidateSet/空→null） | ✅ PASS | T1 11/11，含 `Confirmed_AllTrend_ReturnsNull`、`Insufficient_WithNodes_ReturnsNull_NoLeak`、`CandidateSet_WithNodes_ReturnsNull`。 |
| AC-3 | 匹配单测（命中填 LinkedReasoningNode、非 Trend、首个；未命中 null） | ✅ PASS | T3 10/10。 |
| AC-4 | **快照回归（关键）**：DAG 为空/Insufficient 时 SubmitStepResponse 既有字段逐 case 字节一致 | ✅ PASS | T5 4/4（正确非关键步 / 正确关键步带化学证据 / 三步通关带变式 / 答错带薄弱前置）：provider=null / 返回 null / 返回真实链三者既有字段序列化串全等，仅 StandardReasoningChain 不同。 |
| AC-5 | 红线 eval：after_p32→after_p33 全 0；base_p0→after_p33 LOST/WORSEN/harm=0（GAINED=6） | ✅ PASS | 两次 diff 均 RED-LINE PASS；summary correct=635 harm=0。 |
| AC-6 | 不泄露未采纳结论（Insufficient 恒 null；前端不展示确定答案） | ✅ PASS | T1 防泄露测试覆盖；后端门控保证 Insufficient 不下发。 |
| AC-7 | 前端最小展示（类型编译通过 + 字段在响应中） | ✅ PASS（退化口径） | `tsc --noEmit` 对 P3-3 文件 0 error；既有 src 错误均在未触碰文件；详见 §五限制 5。 |
| AC-8 | 表面积加性：触及文件不含网关 solver/采纳门/eval 判分；新字段加性 | ✅ PASS | self-check 2b/2c/7 全 PASS；grep Application/Gateway 0 命中。 |
| AC-9 | 台账 §T | ✅ PASS | §T 位于 §S 与 `## 处理原则` 之间，T.1–T.5 齐全。 |
| AC-10 | T9 fresh-context 只读 review → PASS | ✅ PASS | 本报告。 |
| R1 | precision=1 | ✅ PASS | summary `precision=1`。 |
| R2 | harm=0 | ✅ PASS | summary `harmCount=0`；两次 diff `harm_new=0`。 |
| R3 | LOST=0 | ✅ PASS | 两次 diff 均 `LOST (0)`。 |
| R4 | WORSEN=0 | ✅ PASS | 两次 diff 均 `WORSEN (0)`。 |
| R5 | 不蒙数 | ✅ PASS | diff after_p32→after_p33 GAINED=LOST=WORSEN=0 → 无答案翻转；total=757 全量无 skip；base_p0→after_p33 GAINED=6 与历史携带一致、无新增。 |
| R6 | 零网关求解改动 | ✅ PASS | grep Application/Gateway 对 P3-3 标识符 0 命中；触及清单无 solver / LlmGatewayService 主体。 |
| R7 | 零采纳门/eval 判分改动 | ✅ PASS | 触及清单无 BuildGatewayAnswerAsync / IsStructuredCorrect / *Eval* 判分文件；diff 全 0 佐证。 |
| R8 | 导学既有行为零漂移 | ✅ PASS | T5 快照 4 场景既有字段字节一致；取链在 SaveChanges 之后、纯加性。 |
| R9 | 诊断既有讲解零漂移 | ✅ PASS | Anchor null→no-op；Linker 不改 chain；仅 chain 非空时链接。 |
| R10 | 事实只存 JSON / 无化学真值硬编码 | ✅ PASS | 触及清单 0 个 data/** JSON 改动；新 .cs 不含化学事实（仅工程取链/匹配/展示逻辑）。 |

全部维度 PASS → 无 FAIL。

---

## 四、Issue 列表

### Critical
无。

### Major
无。

### Minor（非阻断）
无（审查员未发现需修复的 Minor 问题；锚点重复插入等低概率项见限制披露，不构成阻断）。

---

## 五、最终总体裁决

**PASS。**

全部裁决维度均经独立复核通过，无任何 FAIL，无 Critical/Major/Minor 问题。P3-3 不改判分源 / 网关求解 / 采纳门 / eval 判分，仅加性下发网关标准推理链与诊断回溯锚点；eval 维持 correct=635 / precision=1 / harm=0 / total=757，`diff after_p32→after_p33` 全 0（零漂移），`diff base_p0→after_p33` GAINED 维持 6（历史携带）。导学流回归由 T5 快照测试（4 场景，既有字段字节一致）守护，弥补 757 eval 不覆盖导学流的盲区。

---

## 审查限制披露（诚实口径）

1. **导学流无 eval 覆盖**：757 eval 衡量网关/引擎求解链路，不覆盖导学 `SubmitStep` 的 `StandardReasoningChain` 下发与诊断锚点插入效果；导学回归仅靠 T5 快照（4 例）+ T1/T3 单测守护，无端到端导学 eval。此为 spec 明示的设计取舍（§8 R1 / §9），不影响 PASS，但导学话术/锚点的真实呈现质量未被自动化 eval 覆盖。
2. **快照/单测覆盖有限**：T5 仅 4 例、T3 仅 10 例，对锚点边界场景（KnowledgeId 为空/重复、链中全为 Trend、多检查点同名知识）覆盖不充分；后续改动锚点格式化逻辑时快照可能漏检。
3. **锚点重复插入的低概率风险**：`DiagnosticExplanationAnchor.Append` 对 `explanation.Evidence` 执行 `Insert(0, ...)`，未见去重保护；若同一 `CheckpointExplanation` 实例被复用或 `Explain` 对同对象重复调用，可能重复插入锚点行。T5 守护了单次调用输出，重复调用路径未被测试覆盖。非阻断，建议后续补测。
4. **缓存为进程内 `ConcurrentDictionary`，按 stem 无限增长且不持久化**：长生命周期进程下唯一题干数增多会占用内存；进程重启后缓存失效，首次 submit-step 触发网关回源。属可扩展性局限，非正确性回归。
5. **前端演示端退化**：AC-7 采用 spec 允许的退化口径（类型编译通过 + 字段在响应中存在），未实际跑 `npm run dev:h5` 截图验证折叠区渲染。`tsc --noEmit` 对 P3-3 触及文件（ReasoningDag / tutor/index / tutorApi）0 error；仓库既有 73 个 node_modules 错误与 4 个未触碰 src 文件的 TS6133 与 P3-3 无关。
6. **4-solver 覆盖是既有局限**（与 P3-3 无关）：eval 757 题中 122 道 bothWrong，引擎求解器覆盖不全；P3-3 未改求解器，不引入新回归，但整体答对率天花板仍受此限制（spec §9 已诚实披露）。

> 编译项透明说明：审查员首次并行构建 Api 时遇 CS2012 文件锁（与 Tests 构建并发写同一 Api.dll 的环境竞争），已通过「Api 单独重跑 0 error」+「Tests 构建 0 error（含 Api 依赖编译）」双重证伪，非源码错误。
