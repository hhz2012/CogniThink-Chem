# 数据修复（答案键）+ 引擎扩展 并行批次 1 — Implementation Plan

## Task 1: 审计 53 道 bothWrong → 产出两份候选清单（Stream A 修键 / Stream B 引擎扩展）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 对当前 `bothWrong=53`（见 `_evalscratch/audit_datarepair2.txt`）逐题审计，产出 `_evalscratch/datarepair_engine_audit.md`：
    - **Stream A 修键候选**：运行时 `answer`/`answerKeywords` 错误/污染/垃圾，且能用内容检索解析器（`probe_search_answer.py`）在官方答案册定位到真实条目、解析与题干化学一致的题；标注置信度（文本明确=高，需页图 VL=中）。
    - **Stream B 引擎扩展候选**：键正确（或 Stream A 可先校正）、题干+选项文本完整、解题不依赖图像、可由现有事实库模式覆盖的选择/判断题；附「拟加事实关键词」与「目标路由」预判。
    - **排除清单**：图像依赖、多子问/自由文本填空、题干截断拼接（本批非目标）、键已正确且引擎确属能力缺口（需求解器超范围）——逐项给排除理由。
  - 已确认种子：pdf-162（Stream A，真实答案 `c`，答案册 page 57 num 5）。
- **Acceptance Criteria Addressed**: AC-1, AC-3, AC-6, AC-7
- **Test Requirements**:
  - `rule` TR-1.1: 53 题每题在审计文档中有「Stream A 候选 / Stream B 候选 / 排除」三选一分类及理由；证据指向探针输出或答案册 OCR。PASS 当分类覆盖率 100% 且每条候选有官方源定位。
  - `rubric` TR-1.2: 审计证据充分度；scale 1-5；anchors 1=无证据凭印象 / 3=部分有据 / 5=每类判定都有探针/答案册可复核输出；threshold >=4；evidence = `_evalscratch/datarepair_engine_audit.md`。
- **Notes**: 本任务只读，不改任何数据/源码。

## Task 2: Stream A — 修正审计确认的答案键
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 对 Task 1 中高置信的 Stream A 候选，编辑 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`：仅改 `answer` 与 `answerKeywords`，使其与官方源一致；`answerKeywords[0]` 为规范化答案。
  - 中置信（OCR 歧义）项：用 `pdfs/_extract/peek_answer.py`（或同等 VL）渲染答案册页图读数确认后再修；无法确认则跳过并在审计文档标注「跳过-证据不足」。
  - 保持 JSON 格式与缩进不变；用增量 Edit。
- **Acceptance Criteria Addressed**: AC-1, AC-2
- **Test Requirements**:
  - `rule` TR-2.1: JSON 解析通过；每条修键在 `_evalscratch/datarepair_engine_audit.md` 记录改前/改后+官方定位。PASS。
  - `rule` TR-2.2: 修键后 `python` 快速校验：每条修键题的 `answer` 等于审计确认的官方真值。PASS。
- **Notes**: 不碰 `question`/`options`/`note`/`type`。

## Task 3: Stream B — 为审计准入候选追加事实（必要时求解器）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1, Task 2（共享候选需先校正键）
- **Description**:
  - 对 Task 1 准入的 Stream B 候选，按既有事实库模式追加事实到 `data/highschool/*.json`（优先复用现有库；若需新库须论证并保持瀑布链兼容）。
  - 每条事实 `match` 为通用化学原理词汇；先用 `_evalscratch/` 探针（复刻 C# `NormOpt`/`OptContains`/`WRONG`，如 `probe_exp3.py` 模式）做全库 blast-radius，确认零 stray-hit 再落库。
  - 同步测试真值表（`Batch2ChoiceValidatorTests.ExperimentTruth` 或对应库的 truth 字典 / `MultiSelect` 行）。
  - 若某候选确需 `.cs` 求解器（事实无法覆盖），单独最小改动并在本任务说明必要性；否则不碰 C#。
- **Acceptance Criteria Addressed**: AC-3, AC-4
- **Test Requirements**:
  - `rule` TR-3.1: 每条新增事实经全库扫描 `TOTAL PROBLEMS=0`（仅命中目标）。PASS。
  - `rule` TR-3.2: 既有事实内容 `git diff` 无删改（仅追加）。PASS。
  - `rule` TR-3.3: 候选单库/多路径决策与预期一致（探针验证）。PASS。
- **Notes**: 不做单题策展；事实 `reason` 写清通用原理。

## Task 4: 构建 + 全量单测绿
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 2, Task 3
- **Description**:
  - `dotnet build CogniThink.Chem.sln -c Debug` 0 错误。
  - 跑护栏类（`Batch2`/`Batch3`/`MultiSelectChoiceMatcher`/`MultiSelectChoiceValidator`）与全量 TutorBrain 单测。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-4.1: build 0 错误。PASS。
  - `rule` TR-4.2: 护栏类 + 全量 TutorBrain 单测全绿（含 `ExactlySeven`=7、`StaySingleLetter`、`Concept_NoOverlap`、各 `NoHarm`）。PASS。

## Task 5: 全量评估 + 基线 diff（红线 + 增益验证）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 4
- **Description**:
  - 跑 `run_eval_resume.ps1 -Tag after_datarepair_engine -Env Production` 生成 `_evalscratch/snapshot_after_datarepair_engine.json`。
  - 与基线 `snapshot_after_expop` diff：核对 `LOST=[]`、`harm=0`、`enginePrecision=1`、已判正题 `gatewayAnswer` 不变、`GAINED` 集合（Stream B 翻正题）、Stream A 修键题的 `pureLlm`/`gateway` 状态变化。
- **Acceptance Criteria Addressed**: AC-2, AC-3, AC-5
- **Test Requirements**:
  - `rule` TR-5.1: `LOST=[]`、`harm=0`、`enginePrecision=1`、已判正题 `gatewayAnswer` 变动=0。PASS。
  - `rule` TR-5.2: 每条 Stream B 扩展候选 ∈ `GAINED` 且判正；Stream A 修键题无新增 `gatewayWrong`。PASS。

## Task 6: 维护 data-defect-register.md
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 5
- **Description**:
  - 更正 Stream A 已解题、Stream B 已解题在登记册的过时「NO-GO/不可靠/缺陷」登记为已解，并附官方源证据。
  - 补登本批审计新发现、未登记的缺陷（排除清单中确属数据缺陷的项）。
  - 在登记册新增本批次小节（类似 W 类），记录 Stream A 修键清单与 Stream B 扩展清单。
- **Acceptance Criteria Addressed**: AC-6, AC-7
- **Test Requirements**:
  - `rule` TR-6.1: 已解题登记状态更正；新增缺陷补登；每条修键/扩展可在登记册追溯到官方源。PASS。

## Task 7: 独立评审（review.md）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 6
- **Description**:
  - 生成 `.trae/specs/data-repair-engine-exp-batch1/review.md`；委派一次只读独立评审（fresh context）核对全部 AC/TR。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7
- **Test Requirements**:
  - `rule` TR-7.1: 独立评审结果为 `pass`。PASS。
