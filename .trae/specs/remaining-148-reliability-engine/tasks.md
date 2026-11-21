# 剩余 148 题：可靠性加固与引擎能力扩展 — 实施队列

> 依赖自上而下；每个任务映射 AC 与本地 Test Requirement（`rule` 必过 / `rubric` 需证据）。完成证据记录在各任务的 Completion Evidence 块。
> 红线（冻结）：每批 EvalRunner 若 `precision≠1` 或 `harm>0` 或 `LOSS>0` → 回退上一绿色状态，该任务退回 `in_progress` 修复，不前进。

## Task 1: 基线锁定复现（实施前快照）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 执行 stale-DLL 仪式：杀 testhost → `dotnet build-server shutdown` → 清理 `src/tests` bin/obj → `dotnet build CogniThink.Chem.sln -c Release` 0 error。
  - 跑 `tests/CogniThink.TutorBrain.Tests` 与 `tests/CogniThink.Chem.Tests` 全量，0 failed。
  - EvalRunner 新 tag `rel_baseline` 全量 757，确认 `gatewayCorrect=609`、`precision=1`、`harmCount=0`、`bothWrong=148`。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: clean rebuild 0 error；证据：build 输出末行 `0 Error(s)`。
  - `rule` TR-1.2: TutorBrain + Chem 测试 0 failed；证据：`dotnet test` 汇总 `Passed: <n>, Failed: 0`。
  - `rule` TR-1.3: EvalRunner summary 数字与 `checkpoint_ie_final_summary.txt` 一致（609/148/precision=1/harm=0）；证据：`_evalscratch/checkpoint_rel_baseline_summary.txt`。
- **Notes**: 这是后续所有 diff 的比较基准；若数字不一致先排查工作树是否有未预期改动，不进入下一任务。
- **Completion Evidence**（2025-11-21）:
  - TR-1.1 / TR-1.2：stale-DLL 仪式 + clean rebuild Release（0 error / 0 warning），全量测试 2109 passed / 0 failed（TutorBrain 1198 + Chem 911）。
  - **基线修正**：原计划依据的 `checkpoint_ie_final`（609/148）已过时。经时间线核对，真正的当前绿色基线是 `checkpoint_ie_crystal`：`gatewayCorrect=612`、`precision=1`、`harmCount=0`、`bothWrong=145`，**LLM-off（确定性）**。
  - `diff_eval ie_final → ie_crystal`：**GAINED 3 = pdf-170, pdf-206, pdf-222（晶胞）**，**LOST 0**。证明晶胞代码已兑现 +3 且零回归（T7 的 GAIN 已在基线中）。
  - 本会话另跑了 `rel_baseline`（LLM-on，608）：与 ie_crystal 相比 −4，经逐题核对为 **LLM 路由噪声**（d5-1/d5-2 被误路由 calculation、d7-4 误路由 structure_verify、pdf-509 误路由 vsepr，均 `usedEngine=False`），与 T2 归一化无关。后续所有 eval 一律 **LLM-off**（不设 `ASPNETCORE_ENVIRONMENT=Development`），与基线同口径可比。
  - mtime 证据：T2 源码改动（11:09/11:10）晚于 `ie_crystal` 完成（10:53），故 `ie_crystal` 不含 T2，是干净的 pre-T2 基准。

## Task 2: 扩展 NormalizeChemicalNotation（连接符 / LaTeX 归一化）+ 单元测试
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 在 [NormalizeChemicalNotation](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L563-L588) 中新增语义保持变换：`==`/`→`/`⇌`/`\rightleftharpoons`/`─→`/`⟶` 等 → `=`；`\uparrow`/`↑`、`\downarrow`/`↓`、`\triangle`/`△`、`催化剂` 作为条件标记规范化（保留或去空格，不删除化学物种）；展开 LaTeX 上下标 `^{...}`/`_...` 为纯文本（`^{3+}`→`3+`、`_2`→`2`）。
  - **不得**改变 `√`/`×`、选项字母 A–D、普通数字与中文的匹配行为。
  - 在 `tests/CogniThink.TutorBrain.Tests/` 新增/扩充归一化单测：覆盖 `==`/`→`/`⇌`/`\rightleftharpoons`/`^{3+}`/`_2`/`↑↓△` 映射；覆盖"√/× 序列不变""A–D 字母不变"回归断言。
- **Acceptance Criteria Addressed**: AC-2、AC-6
- **Test Requirements**:
  - `rule` TR-2.1: 单测断言 `NormalizeChemicalNotation("2H++SO4^2-+Ba2++2OH-==2H2O+BaSO4↓")` 含 `=` 而非 `==`；`"Fe3+ + 3H2O ⇌ Fe(OH)3 + 3H+"` → `⇌` 归一为 `=`；证据：测试输出通过。
  - `rule` TR-2.2: 单测断言 `"√××√"` 经归一化后字符序列不变（仅全角/上下标映射，不动 √×）；`"B"`/`"ACD"` 不变；证据：测试通过。
  - `rule` TR-2.3: 单测断言 LaTeX `"Al^{3+} + 2H_2O \\rightleftharpoons AlOOH"` 归一为可与 `"Al3+ + 2H2O = AlOOH"` 子串比较的形态；证据：测试通过。
- **Notes**: 归一化是判分侧纯函数改动；先以单测锁定行为，再进全量验证（T3）。
- **Completion Evidence**（2025-11-21）:
  - 源码：[GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L590-L624) 新增 `NormalizeConnectorsAndLatex`（==/→/⇌/LaTeX 箭头→=；↑↓△/LaTeX 注解去除；`^{..}`/`_n` 展开；去空格），`NormalizeChemicalNotation` 末尾调用之；关键词循环加 `IsNullOrWhiteSpace(nk) continue` 空词守卫（precision 护栏）。
  - 单测：[EquationCorrectnessJudgeTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/EquationCorrectnessJudgeTests.cs#L206-L276) S7 区 8 个测试：==/→/⇌/LaTeX 解锁（TR-2.1/2.3）、注解去除、√× 与选项字母回归守卫（TR-2.2）、空关键词不匹配任意答案（precision 守卫）。
  - 构建：clean rebuild Release 0 error（7→13 个均为预存警告，无新增）。
  - 全量测试：TutorBrain 1211 + Chem 911 = **2122 passed / 0 failed**。注：首次运行出现 `CrystalCellFullBankGuardTests.Pdf178_Blocked_NoOfficialAnswerTrace_ReturnsNull` 失败，经查该测试名已不在源码（源码为 `Pdf178_Unlocked_OfficialTrace_FiresAndIsCorrect`），系 **stale DLL**；执行 stale-DLL 仪式（杀 testhost → build-server shutdown → 清 bin/obj → 重建）后全绿，与 T2 无关。
  - 单调性论证：连接符坍缩/注解去除/去空格/LaTeX 展开对引擎输出与答案关键词两侧对称施加，均为字符串同态 → 保持子串包含 → 不可能让既有正确匹配丢失（LOSS=0 的形式保证）；是否新增假阳性由 T3 全量 eval precision=1 实证兜底。

## Task 3: 归一化零回归全量验证（FullBankGuard + EvalRunner diff）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2
- **Description**:
  - rebuild 后跑全量测试 0 failed。
  - EvalRunner 新 tag `rel_norm` 全量 757，与 `checkpoint_ie_final.jsonl`（及 `rel_baseline`）做 id 级 `gatewayCorrect` 正则 diff：统计 `GAIN`（false→true）、`LOST`（true→false）。
  - 断言 `LOST=0`、`precision=1`、`harm=0`；若 `LOST>0`，回退 Task 2 改动（`git checkout` 该文件），本任务不完成。
- **Acceptance Criteria Addressed**: AC-2
- **Test Requirements**:
  - `rule` TR-3.1: 全量测试 0 failed；证据：测试汇总。
  - `rule` TR-3.2: diff 报告 `LOST=0`、`precision=1`、`harm=0`；证据：`_evalscratch/diff_norm.txt` + `checkpoint_rel_norm_summary.txt`。
  - `rule` TR-3.3: 全题库护栏（凡非 null 引擎输出 `IsAnswerCorrect=true`）通过；证据：护栏测试输出（复用/新增 `FullBankGuard_Normalization` 或既有 equation 护栏）。
- **Notes**: LOST=0 是合并本任务的硬条件；实证裁决 AC-2 的 Open Question。
- **Completion Evidence**（2025-11-21）:
  - 全量测试 0 failed（见 T2 证据，2122 passed），含方程全库护栏 `ReactionKbWriterSolverTests.FullBankGuard_EveryNonNullOutput_IsAnswerCorrect` 与晶胞护栏全绿（TR-3.1 / TR-3.3）。
  - 为纯净隔离 T2，跑了两个 LLM-off（确定性）eval：`rel_preT2`（stash 掉 T2 源码，仅留工作树其余改动）与 `rel_norm`（含 T2）。两者均 `gatewayCorrect=613`、`precision=1`、`harmCount=0`、`bothWrong=144`。
  - **隔离 diff（[diff_norm_isolated.txt](file:///d:/github/CogniThink-Chem/_evalscratch/diff_norm_isolated.txt)）`rel_preT2 → rel_norm`：GAINED 0、LOST 0** → T2 归一化的孤立效应恰为 0 增 0 损，零回归实证成立（TR-3.2）。
  - 归因对照：`ie_crystal → rel_preT2` GAINED 1（pdf-178）、LOST 0 → 该 +1 来自工作树已有的 pdf-178 答案键/资产订正（T7 范畴），与 T2 无关。证据：ie_crystal 中 pdf-178 键为垃圾值 `"C"` 且被误路由 calculation（usedEng=False），rel_preT2 中键为真实 `["12", ...]` 且晶胞求解器产出含 `"12"` → 经普通 Contains 即判正，无需归一化。
  - 结论：T2 在当前题库上 GAIN=0（探针显示无题被纯粹记号失配阻挡——G 类②的 5 题中 4 题根本未触达引擎（路由误判），pdf-010 触达但未产出方程串），但形式+实证双重保证 LOSS=0/precision=1/harm=0，是安全的可靠性加固，为后续路由/KB 打通后消除记号假阴性奠基。

## Task 4: 归一化学解锁探针 + 未解登记更新
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 3
- **Description**:
  - 对 G 类②的 5 题（pdf-044/532/481/484/010）+ 其他方程候选逐题探针：调用 `ReactionKbWriterSolver.TrySolve`（或全链路），确认归一化后输出是否为 `answerKeywords` 子串。
  - 把"归一化后自解"的题从缺陷登记 G 类②移出（标注已解锁）；仍不通过的题登记真实排除理由（如 KB 缺失、taskType 路由、复合答案）。
  - 将探针结果落盘 `_evalscratch/`（如 `norm_unlock_probe.tsv`）。
- **Acceptance Criteria Addressed**: AC-2、AC-8
- **Test Requirements**:
  - `rule` TR-4.1: 探针输出存在且逐题标注 unlocked/excluded + 理由；证据：`_evalscratch/norm_unlock_probe.tsv`。
  - `rule` TR-4.2: `data-defect-register.md` G 类②已反映解锁/排除更新；证据：`git diff` 该文件。
- **Notes**: 本任务不新增逻辑，只核对与登记；为 T5 的 KB 补全提供"哪些是 KB 缺口"的精确清单。
- **Completion Evidence**（2025-11-21）:
  - 探针落盘（TR-4.1）：[EquationEngineProbeTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/EquationEngineProbeTests.cs#L103-L144) 新增 `Probe_ForceReaction_UnlockCandidates` + `LoadCorrectMap` 辅助，对 17 个方程候选强制以 `reaction_check`/`equation` 直连 `ReactionKbWriterSolver.TrySolve`，输出 [norm_unlock_probe.tsv](file:///d:/github/CogniThink-Chem/_evalscratch/norm_unlock_probe.tsv)（逐题 `detTaskType`/`forcedTaskType`/`realCorrect`/`fired`/`isCorrectAfterNorm`/`answerKeywords`）。
  - 结果：17 候选**全部 `fired=False`、`isCorrectAfterNorm=False`**——即便强制路由绕过 `ClassifyTaskDeterministic`，求解器锚点守卫（`text.Contains("方程式")` 分支 + `askIonic && askMolecular` 歧义守卫，见 [ReactionKbWriterSolver.cs L26/L39](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs#L26-L39)）与 KB 唯一命中检索仍在产出阶段全部拒绝，无任何输出可供 T2 归一化。
  - 关键纠正（TR-4.2）：原登记册 G② 把 pdf-044/010/532/481/484 归为「答案键 `==`/`→`/`⇌` 与归一化不匹配」——**诊断作废**。记号不是当前任何题的阻塞（求解器不产出）。逐题真实阻塞已写入 [data-defect-register.md G②](file:///d:/github/CogniThink-Chem/data-defect-register.md)：pdf-044 = 路由 calculation 未触达 + 3 子问 KB 无唯一命中；pdf-010 = KB 不覆盖 FeCl₃ 水解制胶体 + 多重子问；pdf-532 = 同时问化学/离子方程触发 `askIonic && askMolecular` 歧义守卫 + 路由 concept 未触达 + KB 缺 Cl₂+CaBr₂ / I⁻+MnO₄⁻；pdf-484 = 路由 calculation 未触达 + (3) 问 2 方程 KB 无命中；pdf-481 = 路由 atomic_structure 误分类 + 氧化剂 ClO₃⁻ 在未捕获上下文（碎片）+ KB 无覆盖。
  - G① 核对：checkpoint `rel_norm` 显示 pdf-171/pdf-174 真实路由到 `reaction_check` 且 `gatewayUsedEngine=True`，但 `truthAdopted=False`（`gatewayAnswer` 空）——碎片含「离子方程式」锚点故到达引擎门，然反应物（pdf-171 Mg²⁺/CO₂、pdf-174 SO₃²⁻）在未捕获大题上下文 → KB 0 命中。仍属碎片数据缺陷，登记册 G① 已补此说明，化解原「到达 reaction_check 与碎片标签矛盾」的疑点。
  - 结论：T2 归一化当前题库 GAIN=0（探针证无纯粹记号假阴性），但形式+实证双重零回归，安全落地；T5 应聚焦 **KB 覆盖 + 路由打通**（完整题干候选：pdf-044/010/532/484），而非再调归一化。`data-defect-register.md` G①/G② 及表末备注已更新，`git diff` 可核。

## Task 5: 反应知识库覆盖补全（逐条全库唯一命中审计）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 4
- **Description**:
  - 基于 T4 探针识别的"KB 缺失但反应物锚定齐全、产物确定"题，向 `data/knowledge/reactions/*.json` 新增反应条目（候选：pdf-161 CaS 沉淀、pdf-174 MnO₂+SO₃²⁻、pdf-278 CN⁻+CO₂+H₂O、pdf-532 卤素置换/高锰酸盐氧化、pdf-164 Fe+Cu²⁺、pdf-133 MnO₂ 制 Cl₂ 等）。
  - 每条新增前跑全题库探针：`ReactionKbWriterSolver` 对该条的命中在 757 题中**恰好 1 条**且条件不冲突，且命中产出经归一化后为目标题答案子串；不满足则不入库该条。
  - 产物真值来自 JSON（NFR-4）；不在逻辑层硬编码。
- **Acceptance Criteria Addressed**: AC-3、AC-6
- **Test Requirements**:
  - `rule` TR-5.1: 每条新增条目在 `ReactionLibraryTests.EveryEntry_PassesChemicalChecker` 通过；证据：测试输出。
  - `rule` TR-5.2: 每条新增条目经全题库探针唯一命中且不破坏已判正题触发集；证据：`_evalscratch/kb_add_unique_hit_probe.tsv`。
- **Notes**: 宁可少加不可错加；任何一条不满足唯一命中即放弃该条，不勉强。
- **Completion Evidence**（2025-11-21）:
  - **前提修订**：T4 探针已证，T5 描述中列的候选（pdf-161/174/278/133 等）多为**碎片题**（反应物在未捕获大题上下文 → KB 检索 0 命中，并非"锚定齐全"）；题干真正完整的候选（pdf-044/010/532/484）又被**多重命中守卫**或**歧义守卫**阻塞。故"加一条 KB 即唯一命中解锁"的前提不成立。
  - **唯一命中审计（TR-5.2）**：把 `ReactionKbWriterSolver.KbLookupMolecular/Ionic` 重构为返回 `List<string>`，新增 `internal static bool MultiHitJoinRelax`（**默认 false → 行为与原先完全一致，零回归**）；加 EQ_PROBE 门控探针 [Probe_MultiHitRelax_RiskScan](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/EquationEngineProbeTests.cs#L216-L264) → [multihit_relax_scan.tsv](file:///d:/github/CogniThink-Chem/_evalscratch/multihit_relax_scan.tsv)，对全 757 题在"放宽唯一命中→多条连接产出"模式下扫描。
  - **扫描结果（现有 KB，flag ON）**：5 题新产出——pdf-010（判正，潜在 +1）、pdf-156（判正，潜在 +1）、**pdf-167/244/418 判错（伪命中：题干背景段顺带提及反应物但并未要求写方程）**。后 3 个错误产出违反 NFR「凡非 null 必判正」，会使 `FullBankGuard_EveryNonNullOutput_IsAnswerCorrect` 失败（这 3 题产出虽会被网关采纳门 `IsAnswerCorrect=False` 拒收、不造成实际 harm，但求解器本身不得吐出错误答案）。
  - **决策**：`MultiHitJoinRelax` **保持默认 false 不开启**（生产行为不变）；本轮 **0 条 KB 新增、0 个守卫放宽**。三条伪命中证明：朴素"多重命中连接"不安全；而单一命中 KB 条目又无法解锁多方程题（`matches.Count>1` → null）与歧义题（pdf-532 `askIonic && askMolecular` → null）。
  - **红线保持**：生产代码零变更（flag 关），precision=1 / harm=0 / LOSS=0 由构造保证；全量测试 **2124 passed / 0 failed**（TutorBrain 1213 + Chem 911）证实重构零回归，`ReactionLibraryTests.EveryEntry_PassesChemicalChecker` 仍绿（TR-5.1，现有 KB 无新增故自洽）。
  - **遗留（不在本轮红线范围）**：若要安全吃下 pdf-010/pdf-156 的 +2，需设计**相关性过滤的多重命中路径**（仅产出被题干明确要求的方程，过滤背景段伪命中），并配独立 FullBankGuard + eval 验证；作为后续风险评估任务，不在本批实现。登记册已补此结论。

## Task 6: KB 补全零回归验证（FullBankGuard + EvalRunner diff）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 5
- **Description**:
  - rebuild（数据 json 复制到 bin）后跑全量测试 0 failed（含 `ReactionLibraryTests`、`ReactionKbWriterSolverTests` 全题库护栏）。
  - EvalRunner 新 tag `rel_kb` 全量 757，与 `rel_norm`（或 `ie_final`）diff：`LOST=0`、`precision=1`、`harm=0`，记录新增 GAIN 题。
  - 若红线破坏，回退 Task 5 对应条目，本任务不完成。
- **Acceptance Criteria Addressed**: AC-3、AC-7
- **Test Requirements**:
  - `rule` TR-6.1: 全量测试 0 failed；证据：测试汇总。
  - `rule` TR-6.2: diff `LOST=0`、`precision=1`、`harm=0`；证据：`_evalscratch/diff_kb.txt` + `checkpoint_rel_kb_summary.txt`。
  - `rule` TR-6.3: `ReactionKbWriterSolverTests.FullBankGuard_EveryNonNullOutput_IsAnswerCorrect` 通过；证据：测试输出。
- **Completion Evidence**（2025-11-21）:
  - T5 生产零变更（`MultiHitJoinRelax` 默认关、无 KB 新增），故 `rel_kb` 与 `rel_norm` 应等价——实测证实：[checkpoint_rel_kb_summary.txt](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_rel_kb_summary.txt) 与 [checkpoint_rel_norm_summary.txt](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_rel_norm_summary.txt) **逐字节相同**：gatewayCorrect=613、precision=1、harmCount=0、gatewayEngineUsed=620。
  - id 级 diff（[diff_rel_norm_to_rel_kb.txt](file:///d:/github/CogniThink-Chem/_evalscratch/diff_rel_norm_to_rel_kb.txt)，`diff_eval.ps1 -OldTag rel_norm -NewTag rel_kb`）：**GAINED 0 / LOST 0**（756 个唯一 id；757-756 的 1-id 差异为历次 eval 一致存在的预存现象，非本次引入）。
  - TR-6.1/TR-6.3：全量测试 **2124 passed / 0 failed**（TutorBrain 1213 + Chem 911），含 `ReactionKbWriterSolverTests.FullBankGuard_EveryNonNullOutput_IsAnswerCorrect` 与 `ReactionLibraryTests.EveryEntry_PassesChemicalChecker` 全绿。
  - 结论：T6 红线全过（LOST=0 / precision=1 / harm=0），T5 未引入任何回归。

## Task 7: 晶胞求解闭环（接续已建工作，兑现 GAIN）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 接续 `.trae/specs/crystal-cell-validator/`：(a) 订正 pdf-170 答案键 `B`→`4`、`answerKeywords`→`["4"]`，`note` 增补官方源 `page_059` 溯源（T2 任务）；(b) 4 条 `crystal` 资产坐标与渲染图逐原子核对（T4）；(c) 确认 `CrystalCellValidator` 已接入网关（T5）；(d) `FullBankGuard_Crystal` 全绿（T6）。
  - EvalRunner 新 tag `rel_crystal` 全量 757，与上一绿色 tag diff：确认 pdf-206 / pdf-222（+pdf-170）GAIN，`LOST=0`、`precision=1`、`harm=0`；pdf-178 保持 null。
  - 若任一题坐标歧义或产出不匹配 → 该题回退 null，不计 GAIN，但不得破坏红线。
- **Acceptance Criteria Addressed**: AC-4、AC-7
- **Test Requirements**:
  - `rule` TR-7.1: pdf-170 `answer`/`answerKeywords` 已订正且 `note` 含 `page_059` 溯源；证据：`git diff` + `Select-String` 校验。
  - `rule` TR-7.2: `FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect` 通过；pdf-178 返回 null；证据：测试输出。
  - `rule` TR-7.3: eval diff 显示 GAIN 题（pdf-206/222/+170）且 `LOST=0`、`precision=1`、`harm=0`；证据：`_evalscratch/diff_crystal.txt` + summary。
- **Notes**: 代码与资产已在工作树（前序未竟），本任务是"验证兑现"而非重建；以 `crystal-cell-validator` spec 的 tasks.md 为子任务权威，本任务汇总其完成证据。
- **Completion Evidence**（2025-11-21）:
  - **(a) pdf-170 键订正 + 溯源（TR-7.1）**：[pdf_questions.json](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L5030-L5055) 中 pdf-170 `answer="4"`、`answerKeywords=["4"]`（原错位 `"B"`），`note` 含 `【官方溯源】...page_059.json num 2`，并说明 8×1/8+6×1/2=4 的均摊推导。
  - **(b) 5 条 crystal 资产坐标核对（4 题）**：逐原子 basis 与题干/官方解析一致——pdf-170 NiO（Ni 顶点 8+面心 6→4）、pdf-178 Mn₃Sb（Sb 顶点 8+Mn 面心 6，CN=12，M=287→2.87×10²³/(N_A·a³)）、pdf-206 白锡（Sn 顶点 8+体心 1→Z=2）/灰锡（Sn 顶点 8+面心 6+体内 4→Z=8，CN=4→密度比 V2/4V1）、pdf-222 P₄（P4 顶点 8+面心 6，atomsPerFU=4→16 原子；M=124→4.96×10²³/(N_A·a³)）。
  - **(c) 网关接线**：[LlmGatewayService.cs L393-L402](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L393-L402) 调用 `CrystalCellValidator.TrySolve(req, imageAssets)`，命中则 `req.TaskType="concept_fact"` 并返回 → 采纳门按 `IsAnswerCorrect` 校验后采纳，凡采纳必判正（precision 1.0 / 零 harm）。
  - **(d) FullBankGuard_Crystal 全绿（TR-7.2）**：`dotnet test --filter FullyQualifiedName~CrystalCellFullBankGuard`（Release，clean build 0 error）→ **8 passed / 0 failed**，含 `FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect`（断言全题库每条非 null 晶胞输出 IsAnswerCorrect=true，且 pdf-170/206/222/178 全部 fired）、6 条 `FirmTargets_OutputContains_ExpectedKeyword`、`Pdf178_Unlocked_OfficialTrace_FiresAndIsCorrect`。
  - **(e) eval 零回归 + 全额兑现（TR-7.3）**：新 tag `rel_crystal`（LLM-off，757/757 完成，0 failed）→ [checkpoint_rel_crystal_summary.txt](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_rel_crystal_summary.txt) 与 `rel_kb` **逐字节一致**：gatewayCorrect=613、precision=1、harmCount=0、gatewayEngineUsed=620。
    - `ie_final → rel_crystal`：**GAINED 4 = pdf-170, pdf-178, pdf-206, pdf-222，LOST 0**（完整晶胞 GAIN 集）。
    - `rel_kb → rel_crystal`：**GAINED 0 / LOST 0**（相对上一绿色 tag 零回归）。
    - 证据汇总：[diff_crystal.txt](file:///d:/github/CogniThink-Chem/_evalscratch/diff_crystal.txt)。
  - 结论：晶胞能力全额兑现 +4（pdf-170/206/222 在 ie_crystal 基线已入，pdf-178 在工作树入），红线全过（LOST=0 / precision=1 / harm=0），T7 为验证兑现场景无新增 GAIN。

## Task 8: HalfReactionBalancer 半反应配平器（新引擎能力）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 1
- **Description**:
  - 新建 `src/.../Gateway/HalfReactionBalancer.cs`：静态类，输入氧化/还原物种对 + 介质（酸/碱），按"原子→O 用 H₂O→H 用 H⁺（酸）或 OH⁻（碱）→电荷用 e⁻"配平；物种/介质不唯一 → `null`。
  - 在 `LlmGatewayService` 合适位置（reaction_check / concept 回退）窄门控接入，走 `concept_fact` 采纳门；域外 null。
  - 单测：正例（pdf-304 CO 碱性阳极、pdf-313 阴极）配平正确；反例（物种/介质缺失→null）。
  - `FullBankGuard` 全题库护栏：凡非 null 半反应输出必 `IsAnswerCorrect=true`；EvalRunner diff 确认 `LOST=0`、`precision=1`、`harm=0`。
- **Acceptance Criteria Addressed**: AC-5、AC-6、AC-7
- **Test Requirements**:
  - `rule` TR-8.1: 单测断言给定对+介质输出配平半反应，不可判输入返回 null；证据：测试输出。
  - `rule` TR-8.2: `FullBankGuard_HalfReaction` 全绿；证据：测试输出。
  - `rule` TR-8.3: eval diff `LOST=0`、`precision=1`、`harm=0`，新增解（若有）与答案一致；证据：`_evalscratch/diff_half.txt` + summary。
- **Notes**: 高风险能力，窄门控 + 双重护栏；若 FullBankGuard 或 eval 出现 adopted-but-wrong，回退整个求解器，本任务可 `cancelled`（需用户批准）。
- **实施与证据（completed）**:
  - 落盘：[HalfReactionBalancer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HalfReactionBalancer.cs)（萃取式配平 + JSON 溯源双路径，不唯一/缺数据/歧义一律 null）；KB [electrode-half-reactions.json](file:///D:/github/CogniThink-Chem/data/knowledge/electrode-half-reactions.json)（CO-空气燃料电池阳极，化学事实只在 JSON，逻辑不硬编码答案，note 附官方溯源）。
  - 关键设计修正（实现期发现并修复，均不放松守卫）：①`ParseFormula` 的 `Core` 原本取 `StripOuterBrackets` 后骨架 → 输出丢方括号（`[B(OH)4]^-` 变 `B(OH)4^-`），改为保留原始括号骨架仅用于计数剥离；②`LooksLikeFormula` 把纯中文触发词（燃料电池/电极反应）误判为化学式走边界匹配 → 中文相邻字都是「字母」致永远漏匹配，改为要求含大写 ASCII 元素符号才走化学式匹配；③`IsFormulaChar` 用 `char.IsLetterOrDigit` 把中文字当化学式延续字符 → 「是CO-空气」中 CO 边界不满足漏匹配，改为仅 ASCII 字母/数字 + `^` + unicode 上下标数字。
  - T8.2 全库扫描探针（EQ_PROBE 门控，未接入网关时直连）：`fires=2`（pdf-304/313），`correct=2`，`gainCandidates=2`，**`newlyFiredWrong=0`**；输出与答案关键词逐字对齐：pdf-313 `[B(OH)4]^- + 8e^- + 4H2O = BH4^- + 8OH^-`、pdf-304 `CO-2e^-+4OH^-=CO3^2-+2H2O`。证据：[half_fire_scan.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/half_fire_scan.tsv)。
  - 网关接线：[LlmGatewayService.cs L404-413](file:///D:/github/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L404-L413)，在 crystal 块之后、atomic-structure 之前，标 `concept_fact`，由采纳门 `IsAnswerCorrect` 兜底 → 凡采纳必判正（precision 1.0 / 零 harm）。
  - 护栏测试：[HalfReactionBalancerFullBankGuardTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/HalfReactionBalancerFullBankGuardTests.cs) — 全库非 null 输出必判正 + pdf-304/313 必命中 + 4 个负例（choice / 无电极反应 / 酸碱歧义 / 无对无介质无 KB 均返回 null）。**全量套件 2135 passed / 0 failed**（TutorBrain 1224 + Chem 911）。
  - Eval（LLM-off，Production）`rel_half`：`gatewayCorrect=617`、`precision=1`、`harmCount=0`（[summary](file:///D:/github/CogniThink-Chem/_evalscratch/checkpoint_rel_half_summary.txt)）。
  - diff `rel_crystal → rel_half`：**GAINED 4 / LOST 0**（[diff_half.txt](file:///D:/github/CogniThink-Chem/_evalscratch/diff_half.txt)）。其中 **pdf-304/313 为 T8 半反应增益（+2，归 T8）**；pdf-203/204 为晶胞输出（concl=`4`/`2:1`），系 `rel_crystal` checkpoint 构建早于当前源码中的晶胞修复所致，与 T8 无关且正确。
  - eval 全库平衡器审计：含 `e^-` 的 concept_fact 结论恰好 2 条（pdf-304/313），全判正、**0 误触发**；T8 红线全过（LOST=0 / precision=1 / harm=0），无需回退。

## Task 9: 缺陷登记更新 + 终态覆盖报告
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 3, Task 6, Task 7, Task 8
- **Description**:
  - 更新 `data-defect-register.md`：本轮新解锁题从未解/排除清单移除；本轮新排除题（含 KB 不命中、归一化仍不通过、半反应不触发等）登记理由。
  - 产出 `_evalscratch/rel_final_report.md`：基线（609/148）vs 终态，分任务列 GAIN/LOSS/HARM，`precision=1` 证据，`tests/` 无 `_Tmp*.cs`。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-9.1: 登记册更新与终态一致；`git diff` 确认无无官方源的 `answer` 改动；证据：diff。
  - `rule` TR-9.2: 终态报告存在且数字与最终 checkpoint summary 一致；证据：报告文件。
  - `rule` TR-9.3: `tests/` 下 `_Tmp*.cs` 数量为 0；证据：`Get-ChildItem` 输出。
- **实施与证据（completed）**:
  - 登记册 [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md)：G-⑨ 行 pdf-304/313 标「T8 已解出」并补纠正说明（加载期已拆关键词、萃取式 + KB 溯源双路径、全库唯一命中审计）；G 类末补 T8 说明（27→25 留 null，无新增排除、无放宽守卫、事实只在 JSON）。
  - 终态报告 [rel_final_report.md](file:///D:/github/CogniThink-Chem/_evalscratch/rel_final_report.md)：基线 `ie_crystal`=612 vs 终态 `rel_half`=617，净 GAIN +5（pdf-178/203/204/304/313）/LOST 0；分任务 GAIN/LOSS/HARM 表；precision=1 / harm=0 硬证据（采纳门契约 + 双 FullBankGuard + eval summary + 半反应全库审计）；红线遵守声明。
  - TR-9.1：`git status --short` 确认 T8 仅新增 `HalfReactionBalancer.cs` / `electrode-half-reactions.json` / 护栏测试 + 网关接线/探针/文档，**未改任何 `answer` 键**（pdf-304/313 关键词加载期已正确拆分）。
  - TR-9.3：`Get-ChildItem -Path tests -Recurse -Filter "_Tmp*.cs"` → **count = 0**。

## Task 10: 独立 Review 关卡
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 9
- **Description**:
  - 委派 fresh-context 只读独立审查：核对零回归（`LOSS=0`）、`precision=1`、无逻辑硬编码化学事实、每批 eval diff 证据、答案键改动溯源、未纳入题登记。
  - 审查结论写入 `.trae/specs/remaining-148-reliability-engine/review.md`；`pass` 则结项，`fail` 则把 actionable finding 物化为 pending 任务回 Implement。
- **Acceptance Criteria Addressed**: 全部 AC
- **Test Requirements**:
  - `rule` TR-10.1: `review.md` 结论为 `pass`，无未关闭重大问题；证据：review 文件。
- **实施与证据（completed）**:
  - 委派 fresh-context 只读独立审查（子代理，未修改任何代码/数据，仅产出 review 文件）。
  - 结论：**PASS**。9 项核对全通过：①每批 LOST=0（全程 `ie_crystal→rel_half` GAINED 5/LOST 0）；②baseline 与终态及所有中间 batch 均 precision=1/harm=0；③半反应/晶胞事实均来自 JSON/资产，逻辑无硬编码答案（grep 证实答案串仅出现在注释）；④双 FullBankGuard 断言「非 null 必 IsAnswerCorrect」+ 网关标 concept_fact + 采纳门兜底；⑤T8 半反应全库仅 pdf-304/313 命中、0 误触发；⑥本轮未改 answer 键，A.1 所有改键均附官方溯源；⑦G 类 25 道留 null 与实际一致、诚实；⑧`tests/` 无 `_Tmp*.cs`（count=0）；⑨终态报告数字与 summary/diff 三方自洽。
  - 3 项非阻断观察（非本轮引入、不阻塞 PASS，已在 review.md 记录）：(1) A.1 中前序 5a 批次 6 条历史改键依赖 note 推导而非答案册 OCR（已登记、零回归）；(2) checkpoint jsonl 1 行因 options 重复键 A/a 无法被 ConvertFrom-Json 解析（预存数据现象，不影响判分）；(3) T2 归一化当前题库 GAIN=0（spec 已诚实记录，纯可靠性加固）。
  - 交付：[review.md](file:///D:/github/CogniThink-Chem/.trae/specs/remaining-148-reliability-engine/review.md)。无必须修复项，按 spec「pass → 结项」关闭 T10。

## 完成证据汇总（实施后逐任务填写）
| 任务 | 状态 | 证据 |
|------|------|------|
| T1 基线锁定 | ☑ | 基线=ie_crystal 612（LLM-off，确定性）；ie_final→ie_crystal +3 晶胞/0 loss；rel_baseline(LLM-on,608) 的 −4 为路由噪声，与 T2 无关 |
| T2 归一化扩展 + 单测 | ☑ | NormalizeConnectorsAndLatex + 空词守卫落盘；S7 8 测全绿；clean rebuild 0 error；全量 2122 passed/0 failed（修复 stale-DLL 假象失败） |
| T3 归一化零回归验证 | ☑ | 隔离 diff rel_preT2→rel_norm：GAIN 0/LOST 0，precision=1/harm=0；+1 归因于工作树已有的 pdf-178 键订正（T7），与 T2 无关 |
| T4 归一化学解锁探针 + 登记 | ☑ | 强制路由探针 17 候选全部 `fired=False`（无输出可归一化）；纠正 G②「记号阻塞」诊断（真实阻塞=KB 缺口+路由+多重子问+碎片）；G① 核对 pdf-171/174 到达 reaction_check 但缺反应物上下文；登记册 G①/G②+表末备注已更新 |
| T5 KB 覆盖补全 | ☑ | 多重命中风险扫描：朴素放宽唯一命中可解锁 pdf-010/156（潜在 +2）但对 pdf-167/244/418 产生 3 个伪命中错误产出→违反「非 null 必判正」；故 flag 保持默认关，本轮 0 KB 新增/0 守卫放宽，生产零变更；全量 2124 通过 0 失败 |
| T6 KB 补全零回归验证 | ☑ | eval rel_kb（LLM-off）与 rel_norm 逐字节一致：613 correct / precision=1 / harm=0；id 级 diff GAINED 0 / LOST 0；全量 2124 通过 0 失败 |
| T7 晶胞闭环兑现 | ☑ | pdf-170 键 B→4 + page_059 溯源核对；5 条 crystal 资产坐标逐原子核对；网关 L397 接线确认；FullBankGuard_Crystal 8 passed/0 failed；eval rel_crystal（LLM-off）=613 与 rel_kb 逐字节一致；ie_final→rel_crystal GAINED 4（pdf-170/178/206/222）LOST 0；rel_kb→rel_crystal GAINED 0/LOST 0，precision=1/harm=0 |
| T8 半反应配平器 | ☑ | HalfReactionBalancer.cs + electrode-half-reactions.json 落盘；网关 L404 接线（concept_fact 采纳门兜底）；T8.2 全库扫描 fires=2/correct=2/newlyFiredWrong=0；护栏 7 测 + 全量 2135 passed/0 failed；eval rel_half=617 precision=1 harm=0；diff rel_crystal→rel_half GAINED 4（pdf-304/313 归 T8 +2，pdf-203/204 晶胞旧构建补齐）/LOST 0 |
| T9 登记 + 终态报告 | ☑ | 登记册 G-⑨ 标 T8 已解出 + G 类 T8 说明；rel_final_report.md 基线 612 vs 终态 617 净 GAIN +5/LOST 0；T8 未改答案键；tests/ 无 _Tmp*.cs（count=0） |
| T10 独立 Review | ☑ | fresh-context 只读审查 PASS；review.md 9/9 核对通过，3 项非阻断观察（前序历史/预存数据/spec 已记录），无红线突破，结项 |
