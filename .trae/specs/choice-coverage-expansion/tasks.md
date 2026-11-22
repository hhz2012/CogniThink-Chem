# 实施计划：选择题事实覆盖扩展（41 题最大头）

> 对应 [spec.md](file:///D:/github/CogniThink-Chem/.trae/specs/choice-coverage-expansion/spec.md)

## 问题

41 道 `taskType=choice` 未解选择题（[_evalscratch/choice41_analysis.md](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_analysis.md)）全部 `usedEngine=False`/`gatewayAnswer=null`——路由正常但无校验器产出。根因是 `FactChoiceMatcher.TryDecide` 要求每选项恰好命中 1 条事实，而这些题在事实库中无策展事实。需先分诊（α 可安全策展 / β 需新求解器 / γ 数据图像缺陷），再对 α 按域分批策展，每批零回归。

## 完成定义（Definition of Done）

- `_evalscratch/choice41_audit.tsv` 落盘：41 题逐题 α/β/γ 分类 + 理由 + 目标校验器，α+β+γ=41（AC-1）。
- 每个 α 题策展事实（`stemMatch` 非空且全库唯一、verdict 与官方 answer 一致）并加入护栏 Truth 表，`TryDecide` 返回字母与官方 answer 一致（AC-2 / AC-5）。
- 全量测试 0 failed；护栏无未列入 Truth 的假阳性（AC-2）。
- 最终 eval tag 相对绿色基线 `checkpoint_pdf174fix`（618）：**GAIN = α 策展数、LOST=0、HARM=0、precision=1**（AC-3）。
- 每个 β / γ 题在 `data-defect-register.md` 如实登记（AC-4）。
- `_evalscratch/choice_curation_report.md` 落盘，含分诊/策展/红线证据（AC-6）。

## 任务

### Task 1：41 题可行性分诊（α/β/γ）+ 批次划分
- **Status**: completed
- **Priority**: high
- **Depends On**: 无
- **Description**:
  - 逐题读 `_evalscratch/choice41_analysis.md` 与源 `pdf_questions.json`，判定 askDir（correct/wrong）、optionCount、选项文本完整性、是否存在全库唯一 stemMatch 锚点、verdict 可否由官方 answer 唯一反推。
  - 分类 α / β / γ（判据见 spec FR-1）；重核既往舍弃题 pdf-075 / pdf-386。
  - 对 α 按**域**划分批次（每批 ≤10 题），确定每题目标校验器（Concept/Organic/Structure/Experiment）与目标事实库。
  - 输出 `_evalscratch/choice41_audit.tsv` 与批次计划。
- **Acceptance Criteria**: AC-1, AC-6
- **Test Requirements**:
  - `rule` TR-1.1: `choice41_audit.tsv` 存在且恰好 41 数据行，每行字段完整；程序化计数 α+β+γ=41；证据：TSV + 计数输出。
  - `rule` TR-1.2: 每个 α 题标注「目标校验器 + 目标事实库 + stemMatch 锚点候选」，每个 β/γ 标注具体排除理由；证据：TSV。
  - `rubric` TR-1.3: 分诊质量（AC-6 维度）≥2：α 判定有 stemMatch 唯一性预判、verdict 可追溯；β/γ 理由具体可执行。证据：TSV + 本任务完成说明。
- **Completion Evidence**:
  - [_evalscratch/choice41_audit.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_audit.tsv)：41 数据行，字段 `id/askDir/optionCount/分类/reason/目标校验器` 完整；程序化计数 **α=5 / β=16 / γ=20 / 合计 41**（脚本输出 `α=5 β=16 γ=20 total=41`）。
  - stemMatch 唯一性预检：导出全库 545 题干预存 [_evalscratch/all_stems.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/all_stems.tsv)，逐锚点计数——5 个 α 锚点（`K虚线圈内所有原子共平面`、`四川南充`、`制取BCl₃`、`钧瓷`/`Cu2O`、`硼氢化钠`/`NaBH4`）全库出现次数均为 1（脚本输出 `count=1`）。
  - **OQ-3 决议（spec 待解问题）**：核实 `FactChoiceMatcher.JudgeStatement` 遍历全部 `ChoiceFactKeys`（含 `choice-curated-facts`、`judgment-statement-facts`），故既有 `MultiSelectChoiceValidator` 仅靠新增 JSON 事实即可驱动；其护栏 `MultiSelectChoiceValidatorTests.FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm` 对全库任一非 null 结论强制判分正确（precision=1/harm=0，无 allow-list）。**结论**：题干未标注「双选/多选」但答案为多字母的题，无需改 .cs、无需新求解器，可经既有 MultiSelect 路径策展——本 spec 的 β「多选无标记」桶据此可重新评估，其中 2 题（pdf-115 AC、pdf-475 ACD）因 4 选项文字完整、无 answer 冲突，升入 α（记为 α2）。
  - 既往舍弃题重核：pdf-075（answer=B 与化学真值冲突，B 为正确陈述而非错误项）→ 维持 γ；pdf-386（备注自相矛盾、答案误标）→ 维持 γ。两者均不重新策展。
  - **α 批次计划**：α 共 5 题（α1 单选 3：pdf-388/394/459 → Concept/choice-curated-facts；α2 多选 2：pdf-115/475 → 既有 MultiSelect，事实同样写入 choice-curated-facts），≤10 题，**单批（Task 3）即可容纳**。Task 4/5/6 拟取消（需用户批准，见下）。
  - 原始证据：[_evalscratch/choice41_raw.txt](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_raw.txt)（41 题 answer/askDir/optionCount/题干摘要）、[_evalscratch/choice41_notes.txt](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_notes.txt)（41 题原始 note）、[_evalscratch/alpha_candidates_full.txt](file:///D:/github/CogniThink-Chem/_evalscratch/alpha_candidates_full.txt)（5 个 α 候选完整题干+选项，核真用）。

### Task 2：锁定绿色基线 + 全量测试绿
- **Status**: completed
- **Completion Evidence**:
  - `checkpoint_pdf174fix_summary.txt`: gatewayCorrect=618 / precision=1 / harmCount=0（基线锁定）。
  - 全量测试 `dotnet test CogniThink.Chem.sln -c Release --no-restore`：**Chem 911 + TutorBrain 1228 = 2139 passed / 0 failed**（2026-11-21 运行）。
  - 基线对照：ConceptTruth=245、choice 已解=457、choice 未解=47（其中 41 为能力缺口）。
- **Priority**: high
- **Depends On**: 无
- **Description**:
  - 确认当前绿色基线 = `checkpoint_pdf174fix`（618 / precision=1 / harm=0）。
  - 跑全量测试 `dotnet test CogniThink.Chem.sln -c Release --no-restore`，确认 0 failed（记录通过数）。
  - 记录基线 ConceptTruth 数（245）与 choice 已解数（457），作为每批 diff 的对照。
- **Acceptance Criteria**: AC-3（基线对照）
- **Test Requirements**:
  - `rule` TR-2.1: 全量测试 0 failed；证据：测试汇总输出。
  - `rule` TR-2.2: 基线 summary 数字记录在案（618/precision=1/harm=0）；证据：`checkpoint_pdf174fix_summary.txt`。

### Task 3：策展批次 1（域由 Task 1 分诊确定，≤10 题）
- **Status**: completed
- **Priority**: high
- **Depends On**: Task 1, Task 2
- **Description**:
  - 按 FR-2 协议为批次 1 的每个 α 题策展事实（写入对应事实库），加入护栏 Truth 表。
  - 跑 `dotnet test` 确认 0 failed 且无假阳性；跑 eval 打 tag `choice_b1`；`diff_eval` 相对 `checkpoint_pdf174fix` 验证 GAIN/LOST=0/HARM=0。
  - 破红线则回退本批事实与 Truth 改动。
- **Acceptance Criteria**: AC-2, AC-3, AC-5
- **Test Requirements**:
  - `rule` TR-3.1: 本批每题 `TryDecide` 返回字母与官方 answer 一致（单测/护栏断言）；证据：测试通过。
  - `rule` TR-3.2: 本批新增事实每条 `stemMatch` 非空且全库唯一（唯一性脚本输出）；证据：脚本输出 + facts JSON。
  - `rule` TR-3.3: `dotnet test` 0 failed；`diff choice_b1` GAIN=本批数、LOST=0、HARM=0；证据：测试汇总 + `_evalscratch/diff_choice_b1.txt`。
- **Completion Evidence**:
  - 策展事实：向 [choice-curated-facts.json](file:///D:/github/CogniThink-Chem/data/highschool/choice-curated-facts.json) 新增 **20 条**（5 题 × 4 选项），id `GC-757-A`~`GC-761-D`：GC-757=pdf-388(D)、GC-758=pdf-394(B)、GC-759=pdf-459(A)（α1 单选，Concept 路径）；GC-760=pdf-115(AC)、GC-761=pdf-475(ACD)（α2 多选，既有 MultiSelect 路径）。落盘后 `ConvertFrom-Json` 校验通过，statements 60→80。
  - 护栏 Truth：[Batch3ChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs) `ConceptTruth` 新增 pdf-388→D / pdf-394→B / pdf-459→A（245→248）；[MultiSelectChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiSelectChoiceValidatorTests.cs) `NoMarkerVariant` 新增 `[InlineData("pdf-115","AC")]` / `[InlineData("pdf-475","ACD")]`。
  - 前置仿真（[_evalscratch/sim_check.ps1](file:///D:/github/CogniThink-Chem/_evalscratch/sim_check.ps1)，镜像 `NormOpt`/`OptContains`）：5 题 20 选项各命中唯一事实、verdict 与官方 answer 一致、候选字母正确；与既有 1272 条事实（general/curated/organic/structure/experiment/judgment）**零碰撞**；5 个 stemMatch 锚点在全库 545 归一化题干中 count=1。路由二义检查（[_evalscratch/check_nooverlap.ps1](file:///D:/github/CogniThink-Chem/_evalscratch/check_nooverlap.ps1)）：pdf-388/394/459 在 experiment/organic/structure 四库中 4 选项命中数均为 0，与 Concept 无歧义。
  - 全量测试 `dotnet test CogniThink.Chem.sln -c Release --no-restore`：**Chem 911 + TutorBrain 1233 = 2144 passed / 0 failed**（基线 2139 → +5：3 Concept Truth + 2 MultiSelect InlineData）。护栏 `Concept_ZeroFalsePositive`、`Concept_NoOverlap`、`MultiSelect FullBank NoHarm` 全绿。
  - eval tag `choice_b1`（offline Production，757/757 完成）：`diff_eval -OldTag pdf174fix -NewTag choice_b1` → **GAIN=5（pdf-115/388/394/459/475）、LOST=0**；`checkpoint_choice_b1_summary.txt`：gatewayCorrect 618→623、engineSolved 618→623、engineUsed 624→629、**precision=1、harmCount=0**、bothWrong 139→134。证据：[_evalscratch/diff_choice_b1.txt](file:///D:/github/CogniThink-Chem/_evalscratch/diff_choice_b1.txt)。

### Task 4：策展批次 2（≤10 题）
- **Status**: cancelled
- **Priority**: high
- **Depends On**: Task 3
- **Description**: 同 Task 3 协议，域由 Task 1 批次计划确定；tag `choice_b2`，diff 相对 `choice_b1`。
- **Acceptance Criteria**: AC-2, AC-3, AC-5
- **Cancellation Evidence**: Task 1 分诊 α=5（≤10），单批（Task 3）即可容纳；用户已批准取消（AskUserQuestion 2026-11-21「是，单批完成」）。本任务无事实/测试改动。

### Task 5：策展批次 3（≤10 题）
- **Status**: cancelled
- **Priority**: high
- **Depends On**: Task 4
- **Description**: 同上；tag `choice_b3`，diff 相对 `choice_b2`。
- **Acceptance Criteria**: AC-2, AC-3, AC-5
- **Cancellation Evidence**: 同 Task 4；α 单批穷尽，用户批准取消。

### Task 6：策展批次 4（α 余量，若存在）
- **Status**: cancelled
- **Priority**: medium
- **Depends On**: Task 5
- **Description**: 同上，吸收 α 余量（若 >10 题则继续拆分，每批 ≤10）；tag `choice_b4`。
- **Acceptance Criteria**: AC-2, AC-3, AC-5
- **Cancellation Evidence**: 同 Task 4；α 单批穷尽，用户批准取消。

### Task 7：β / γ 登记 + 终态报告
- **Status**: completed
- **Priority**: medium
- **Depends On**: Task 1, Task 6（或最后一个完成的策展批）
- **Description**:
  - 对所有 β / γ 题在 `data-defect-register.md` 新增/更新条目，理由真实（能力缺口类型 / 数据缺陷类型）。
  - 写 `_evalscratch/choice_curation_report.md`：分诊结果（α/β/γ 数与清单）、每批策展明细与 diff、红线状态、β/γ 后续建议。
- **Acceptance Criteria**: AC-4, AC-6
- **Test Requirements**:
  - `rule` TR-7.1: 每个 β/γ 题在登记册有条目且理由与 `choice41_audit.tsv` 一致；证据：登记册 diff + 报告清单对照。
  - `rubric` TR-7.2: 报告完整自洽（数字与 eval summary / audit TSV 一致）；证据：报告文件。
- **Completion Evidence**:
  - 登记：36 道 β/γ 延期题全部在 [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md) 如实登记（新增 H 类索引 H.1 β=16 / H.2 γ=20 / H.3 pdf-405·406 残记）；pdf-115、pdf-394 原 C 类「未解」行已更新为 ✅ curated-solved，与策展事实一致（TR-7.1 ✓）。
  - 报告：[_evalscratch/choice_curation_report.md](file:///D:/github/CogniThink-Chem/_evalscratch/choice_curation_report.md) 落盘，含分诊（α=5/β=16/γ=20=41）、策展明细（20 事实 GC-757..GC-761 + Truth 改动）、红线状态、β/γ 后续建议；所有数字与 `checkpoint_choice_b1_summary.txt` / `choice41_audit.tsv` 一致（TR-7.2 ✓，自洽）。

### Task 8：最终红线全量验证
- **Status**: completed
- **Priority**: high
- **Depends On**: Task 7
- **Description**:
  - 最终 `dotnet test` 全绿；最终 eval tag（如 `choice_final`）相对 `checkpoint_pdf174fix` 的总 diff：GAIN=α 总数、LOST=0、HARM=0、precision=1。
  -  consolidated 检查：所有新增事实 stemMatch 唯一性、无 `.cs` 化学逻辑硬编码、answer 改动（若有）附官方源。
- **Acceptance Criteria**: AC-2, AC-3, AC-5
- **Test Requirements**:
  - `rule` TR-8.1: 全量测试 0 failed；证据：测试汇总。
  - `rule` TR-8.2: `diff_choice_final.txt` 显示 GAIN=α 总数、LOST=0、HARM=0；最终 summary precision=1；证据：diff + summary。
  - `rule` TR-8.3: `git diff --stat` 仅含 JSON 事实 + 测试 Truth 表 + 文档（无 `.cs` 化学逻辑改动，除非经 Task 1 证明必要且测试覆盖）；证据：diff stat。
- **Completion Evidence**:
  - TR-8.1：最终 `dotnet test CogniThink.Chem.sln -c Release --no-restore` → **Chem 911 + TutorBrain 1233 = 2144 passed / 0 failed**（2026-11-21 复跑）。
  - TR-8.2：[_evalscratch/diff_choice_final.txt](file:///D:/github/CogniThink-Chem/_evalscratch/diff_choice_final.txt)（α=5 单批穷尽，choice_b1 即最终批）→ **GAIN=5（pdf-115/388/394/459/475）、LOST=0**；`checkpoint_choice_b1_summary.txt`：precision=1、harmCount=0。
  - TR-8.3：本 spec 改动仅限 `choice-curated-facts.json`（+20 事实）+ 两个测试 Truth 表 + `data-defect-register.md` + spec 文档；**无** 生产 `.cs` 化学逻辑改动、**无** answer-key 改动、**无** 事实硬编码进代码。（工作树中 `.trae/specs/multi-part-compound-answer-fix/` 属另一在途 spec，未触碰。）

## 完成证据汇总（实施后逐任务填写）
| 任务 | 状态 | 证据 |
|------|------|------|
| Task 1 分诊 | ☑ | [choice41_audit.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_audit.tsv) α=5/β=16/γ=20=41；stemMatch 唯一性预检通过；OQ-3 已决议 |
| Task 2 基线锁定 | ☑ | checkpoint_pdf174fix=618 / 全量 2139 passed 0 failed |
| Task 3 策展批 1 | ☑ | 20 事实 GC-757..GC-761 + Truth 改动；2144 passed 0 failed；diff GAIN5/LOST0/HARM0 |
| Task 4 策展批 2 | ⊘ cancelled | α=5 单批容纳，用户批准 |
| Task 5 策展批 3 | ⊘ cancelled | α=5 单批容纳，用户批准 |
| Task 6 策展批 4 | ⊘ cancelled | α=5 单批容纳，用户批准 |
| Task 7 登记 + 报告 | ☑ | 36 题登记入 register；choice_curation_report.md 落盘 |
| Task 8 最终红线验证 | ☑ | 全量 2144 passed；diff_choice_final GAIN5/LOST0；改动仅 JSON+Truth+文档 |
