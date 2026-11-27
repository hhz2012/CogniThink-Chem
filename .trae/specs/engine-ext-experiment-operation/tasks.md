# 引擎能力扩展·实验操作规范判定事实（pdf-416 / pdf-531）- Implementation Plan

## Task 1: 追加 8 条实验操作事实到 experiment-facts.json
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 用增量 Edit 向 `data/highschool/experiment-facts.json` 末尾追加 `EF-416-A/B/C/D`（结晶方法选择）与 `EF-531-A/B/C/D`（滴定操作正误），共 8 条。
  - 插入前先 Read 文件尾部确认真实末条并给原末条补尾逗号；保持 4 空格缩进单行格式；末条无尾逗号。
  - `match`/`stemMatch` 关键词与 `_evalscratch/probe_exp2.py` 的 drafts 完全一致（已验证零 stray-hit）；`verdict` 经独立化学核对；`reason` 写清通用原理。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-5
- **Test Requirements**:
  - `rule` TR-1.1: JSON 解析通过且 statements 总数 = 原数 + 8；证据：`probe_exp3.py` 输出 `total statements: 108 (expect 108)`、`new facts: 8`。PASS。
  - `rule` TR-1.2: blast-radius 8 条各仅命中目标 (qid, option)，pdf-416 单库→C、pdf-531 多路径→BC；证据：`probe_exp3.py` 输出 8 条均 OK、`TOTAL PROBLEMS=0`。PASS。
- **Completion Evidence**: `experiment-facts.json` 100→108 statements；`_evalscratch/probe_exp3.py` 全绿（TOTAL PROBLEMS=0）。

## Task 2: 同步测试真值表（ExperimentTruth + 多选回归行）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - `tests/CogniThink.TutorBrain.Tests/Batch2ChoiceValidatorTests.cs`：在 `ExperimentTruth` 字典追加 `["pdf-416"] = "C"`（带注释说明实验操作事实 EF-416）。
  - `tests/CogniThink.TutorBrain.Tests/MultiSelectChoiceValidatorTests.cs`：在 `NoMarkerVariant_SolvesMultiSelect_AndGradedCorrect` 增加 `[InlineData("pdf-531", "BC")]`。
- **Acceptance Criteria Addressed**: AC-4
- **Test Requirements**:
  - `rule` TR-2.1: `dotnet build` 通过（0 error）。PASS（含一次因无关 WIP 破坏构建的插曲，已隔离，见 review.md）。
  - `rule` TR-2.2: `Experiment_Solves_KnownQuestion(pdf-416)` 与 `NoMarkerVariant(pdf-531)` 通过；证据：364 测试全绿。PASS。
- **Completion Evidence**: 两测试文件已编辑；`dotnet test` 目标类 364/0 通过，含新增两条用例。

## Task 3: 全量单测绿（护栏门）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2
- **Description**:
  - 跑 `Batch2ChoiceValidatorTests`、`Batch3ChoiceValidatorTests`、`MultiSelectChoiceMatcherTests`、`MultiSelectChoiceValidatorTests`、全量 TutorBrain。
- **Acceptance Criteria Addressed**: AC-4
- **Test Requirements**:
  - `rule` TR-3.1: 目标测试类 + 全量 TutorBrain 全部通过；证据：364/0 与 1760/0。PASS。
  - `rule` TR-3.2: `FullBank_MultiSelect_Assertions_AreExactlySeven` 仍为 7、`FullBank_SingleSelect_Assertions_StaySingleLetter` 通过；证据：364 全绿（含这两门）。PASS。
- **Completion Evidence**: 4 护栏类 364/0；全量 TutorBrain 1760/0（playbook 所述旧 MixedAnswer 红测现已不存在/已修复，0 失败）。

## Task 4: 评估 + 基线 diff（红线验证）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 3
- **Description**:
  - 跑 eval tag `after_expop` 生成新快照；与 `snapshot_after_structperiodic` diff。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-4.1: pdf-416/531 ∈ GAINED 且判正（C/BC）；证据：`expop_diff.py` 输出。PASS。
  - `rule` TR-4.2: LOST=[]、harm=0、precision=1、已判正题 gatewayAnswer 字节级不变；证据：diff 输出。PASS。
- **Completion Evidence**: `_evalscratch/snapshot_after_expop.json`；`_evalscratch/expop_diff.py` 输出：gwCorrect 702→704、bothWrong 55→53、LOST=[]、harm=0、precision=1、GAINED=[pdf-416,pdf-531]、extra=[]、已判正变动=0，`ALL RED LINES PASS`。

## Task 5: 维护 data-defect-register.md
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 4
- **Description**:
  - 更正 pdf-416（L364 NO-GO、L529 跳过表）、pdf-531（L131/L192/L531 跳过/缺陷表）的过时登记为「已解」。
  - 补登本批新发现未登记缺陷：pdf-180（张冠李戴）、pdf-198/528（非引擎域填空）、pdf-333/377（题面截断/图缺失）。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-5.1: pdf-416/531 状态更新；新增条目覆盖本批未登记缺陷；证据：`data-defect-register.md` diff。PASS。
- **Completion Evidence**: 登记册 6 处编辑落地（4 处更正 + 5 条新增于 H.2 表）。

## Task 6: 独立评审（review.md）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 5
- **Description**:
  - 生成 `review.md`；委派一次只读独立评审（fresh context），核对全部 AC/TR。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6
- **Test Requirements**:
  - `rule` TR-6.1: 独立评审结果为 `pass`。证据：`review.md` Review History。PASS。
- **Completion Evidence**: [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/engine-ext-experiment-operation/review.md) Cycle 1 verdict=pass；6/6 checkpoint 通过，AC-5 rubric 5/5，无 actionable finding。独立重测 364/0、`expop_diff` 全绿、`probe_exp3` TOTAL PROBLEMS=0。
