# 方程引擎增强 P0：equation_writer 错题零假阳性恢复 — 实现计划

基线对照：`t2route`（673 correct / 84 wrong / 9 eq_writer 全 null）。所有改动以 `t2route` 为 diff 基准。审批前不改生产代码。

## Task 1: 探针锁定可恢复集（先行，只读证据）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `tests/CogniThink.TutorBrain.Tests/EquationEngineProbeTests.cs` 追加一个 `EQ_PROBE=1` 门控的只读探针 `Probe_EqWriterP0_Targets`（不影响常规绿态）：
    - 对 9 目标逐题打印当前 `KbLookupMolecular` / `KbLookupIonic` 命中的全部条目 id（解释多命中阻塞或缺条目），以及 `ClassifyTaskDeterministic` 结果。
    - 对全库 757 跑 `ReactionKbWriterSolver.TrySolve`，汇总 `fired` / `IsAnswerCorrect` / 相对 `t2route` 基线的 `wasCorrect`，输出「fired 但判错」清单（必须空）。
  - 落盘 `_evalscratch/eqp0_targets_probe.tsv` 与控制台汇总。据此锁定最终可恢复集 + 每题所需 cue，写入本任务完成证据。
- **Acceptance Criteria Addressed**: AC-3（探针基建）、AC-4（可恢复集锁定）
- **Test Requirements**:
  - `rule` TR-1.1: `EQ_PROBE=1` 下新探针运行成功，输出 TSV 含 9 目标各自的当前命中条目列表与全库 fired/correct 汇总；常规 `dotnet test`（不设 EQ_PROBE）0 failed 且该探针被跳过。证据：探针控制台输出 + TSV 文件存在。
- **Notes**: 本任务只加探针测试（EQ_PROBE 门控），不改生产逻辑；基线 8 命中 / 0 假阳性应在汇总中复现。

## Task 2: 为 ReactionEntry 增加 questionCues 字段（加性零回归）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 在 `src/CogniThink.Chem.Core/Reactions/ReactionEntry.cs`（或其定义处）增加 `List<string> QuestionCues { get; set; } = new();`，JSON 属性名 `questionCues`，默认空。
  - 确认 `ReactionLibrary` 用 `ChemDataLoader.ReadItems<ReactionEntry>` 反序列化时自动读取（属性名大小写不敏感）；空 cues 条目行为不变。
  - 不修改任何既有 JSON 条目（既有条目无 `questionCues` → 反序列化为空列表 → 行为零变化）。
- **Acceptance Criteria Addressed**: AC-2、AC-6
- **Test Requirements**:
  - `rule` TR-2.1: clean rebuild 后 `ReactionLibrary.Default` 载入成功、条目数与基线一致（不因缺字段抛异常）；既有 8 命中 / 0 假阳性在 Task 1 探针中复现（加字段未改变裁决）。证据：`dotnet build` 0 错误 + 探针汇总。
  - `rule` TR-2.2: 逻辑层无化学事实常量；`QuestionCues` 仅承载题干子串。证据：源码审查。
- **Notes**: 若 `ReactionEntry` 在 Chem.Core 之外定义，先定位再改（grep `class ReactionEntry`）。

## Task 3: ReactionKbWriterSolver 实现 cue 过滤 + 带锚点优先裁决
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 2
- **Description**:
  - 在 `KbLookupMolecular` 与 `KbLookupIonic` 的匹配循环中，对每条目追加条件：`entry.QuestionCues` 为空 ∨ 全部 cue 子串出现在 `text` 中。
  - 裁决：先收集「带 cues 且命中」的条目；若非空，则只在其中取恰好 1 条产出（>1 → 返回空）；若为空，则回退到既有「全体命中中恰好 1 条」逻辑（保持 `MultiHitJoinRelax=false` 默认行为）。
  - 保持域外门控（`text` 不含「方程式」、taskType 不在允许集）与 `askIonic/askMolecular` 互斥不变。
- **Acceptance Criteria Addressed**: AC-2、AC-5
- **Test Requirements**:
  - `rule` TR-3.1: 守卫单测：某条目 cues 未全出现 → 不匹配；带 cues 多命中 → 返回空（null）；无带 cues 命中时既有唯一命中题仍产出。证据：新增 xUnit 测试通过。
  - `rule` TR-3.2: Task 1 全库探针重跑：基线 8 命中仍判正、0 假阳性（裁决改动未引入回归）。证据：探针汇总 `newlyFiredWrong=0`。
- **Notes**: 先不加任何新 KB 条目，验证裁决逻辑本身零回归（TR-3.2），再进 Task 4。

## Task 4: 补录目标 KB 条目 + cues，探针迭代验证
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 3
- **Description**:
  - 按 Task 1 锁定的可恢复集，在 `data/knowledge/reactions/reactions-inorganic.json`（离子条目按既有约定归入合适文件）补录配平正确的条目，配 `questionCues` 使其在目标题唯一裁决、且不在其他题误触发。
  - 已有条目（如 RX-IN-FE-010 对应 pdf-010）优先复用：为其追加 `questionCues` 而非新建重复条目；若复用会影响其他题（探针回归），则新建带 id 的条目并让带 cues 优先裁决生效。
  - 每补一条 / 改一个 cue：rebuild → 跑 Task 1 全库探针 → 确认目标题 `fired=True ∧ isCorrect=True` 且全库 `newlyFiredWrong=0`；不通过则收紧 cue 或撤回该条目，绝不留假阳性。
  - 产出结论前缀（`化学方程式：/离子方程式：`）与 `ExpandMatchVariants` 沿用，确保归一化后命中答案碎片（pdf-462 的 LaTeX 关键词由 `NormalizeConnectorsAndLatex` 对齐，探针实证）。
- **Acceptance Criteria Addressed**: AC-3、AC-4、AC-6、AC-7
- **Test Requirements**:
  - `rule` TR-4.1: 每补一条目后探针汇总 `newlyFiredWrong=0`；最终目标恢复集中每题探针行 `isCorrect=True`。证据：迭代探针 TSV 留痕。
  - `rule` TR-4.2: 新增化学真值全部位于 `reactions/*.json`，逻辑层无硬编码方程。证据：JSON diff + 源码审查。
  - `rubric` TR-4.3: 恢复目标题数量；scale 1-5；anchors 1=0 / 3=≥3 / 4=≥5 / 5=全部 8 可寻址；threshold ≥3 不阻塞后续但影响最终 AC-7；证据：探针 + 后续 eval diff。
- **Notes**: 迭代至上不再有可安全恢复的目标题；pdf-084 与探针不通过题留 null，进 Task 7 记录。

## Task 5: 守卫单元测试补全
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 4
- **Description**:
  - 为 cue 过滤与优先裁决补 xUnit 守卫：正例（目标题干命中正确产出）、反例（cues 缺失→null、带 cues 多命中→null、非方程题→null）。
  - 复用 `GatewayVsLlmEvaluationService.BuildTestCases()` 取真题构造，断言 `IsAnswerCorrect(resp.FinalResult, c)`。
- **Acceptance Criteria Addressed**: AC-2、AC-5
- **Test Requirements**:
  - `rule` TR-5.1: 新增守卫测试全部通过；`dotnet test` 全绿。证据：测试输出 failed=0。

## Task 6: 全量 eval + diff + mp27 + 全量测试
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 5
- **Description**:
  - stale-DLL 仪式后 clean rebuild；`dotnet test CogniThink.Chem.sln --nologo -v q` 全绿。
  - 跑全库 eval：`powershell -ExecutionPolicy Bypass -File _evalscratch\run_eval_resume.ps1 -Tag t2eq -Env Production`（LLM-off 纯引擎）。
  - diff：`diff_eval.ps1 -OldTag t2route -NewTag t2eq`；mp27：`python _evalscratch\verify_mp27.py t2route t2eq`。
  - 核验：GAIN 仅含目标题且均判正；LOSS=0；harm=0；precision=1；mp27 27/27。
- **Acceptance Criteria Addressed**: AC-1、AC-4、AC-7
- **Test Requirements**:
  - `rule` TR-6.1: `dotnet test` failed=0；eval `precision=1`、`harmCount=0`；diff LOSS=0；`verify_mp27` = 27/27。证据：各命令输出。
  - `rubric` TR-6.2: 覆盖净增；scale 1-5；anchors 同 AC-7；threshold ≥3；证据：eval summary + diff 报告。
- **Notes**: 若 diff 出现任何 True→False 或 adopted-but-wrong，立即回退对应 Task 4 条目，不交付。

## Task 7: 不可恢复题排除留痕 + 项目记忆更新
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 6
- **Description**:
  - 在 `_evalscratch/` 写 `eqp0_excluded.md`：列出未恢复目标题 id + 原因（pdf-084 数据缺陷、多命中无法唯一裁决、答案键 [或] 后缀无法子串命中等），并附探针行证据。
  - 视情况更新 `c:\Users\Administrator\.trae-cn\memory\...` 项目记忆（新增方程 cue 机制与本轮恢复结果），仅记录可复用决策。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-7.1: 排除记录覆盖所有未恢复目标题；探针确认这些题 `fired=False`。证据：`eqp0_excluded.md` + 探针 TSV。
