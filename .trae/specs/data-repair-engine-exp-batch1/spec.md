# 数据修复（答案键）+ 引擎扩展 并行批次 1 — Product Requirements Document

## Overview
- **Summary**: 在当前 `bothWrong=53` 题集中，分两轨并行：(Stream A) 对 `answer`/`answerKeywords` 错误/污染/垃圾、且能经官方答案册唯一确认真值的题，修正 `pdf_questions.json` 的答案键；(Stream B) 对其中「答案键正确、题干与选项文本完整、无图像依赖、可由现有事实库覆盖」的题，追加事实/必要时求解器，使其由 bothWrong 翻正，在严守红线前提下争取 `gwCorr` 增益。
- **Purpose**: 清理退化/污染答案键造成的真值失真与 pureLlm 假阳性（Stream A，本身 `gwCorr +0`，价值在真值完整与清障），同时解锁文本完整的引擎可解题（Stream B，贡献 `gwCorr` 增益）。延续全批次红线：`precision=1`、`harm=0`、`LOST=[]`、不蒙数。
- **Target Users**: 题库维护者、评估管线、下游化学推理引擎。

## Goals
- **G1（Stream A 修键）**: 修正一批高置信、官方源可核的错误/污染答案键，消除污染键对判分真值的腐蚀。
- **G2（Stream B 引擎扩展）**: 为至少一道经审计确认安全的可解题扩展引擎能力，使其 `gatewayCorrect` 翻正并贡献 `gwCorr` 增益；若审计判定无安全候选，则记录原因且不强行扩展（安全降级，review 确认审计充分即可）。
- **G3（红线与可追溯）**: 全程零回归、零假阳性、零蒙数；每条改动在登记册留有可复核的官方源证据。

## Non-Goals
- 不复原/改写题面或选项文本（题面截断、题干拼接、缺失子问题等属于「修键+复原题干」范围，本批不做）。
- 不处理图像依赖题（装置图、曲线图、结构图、晶胞、流程图等必须看图的题）。
- 不做自由文本填空/复杂计算的引擎求解器扩展；Stream B 优先选择/判断题，且优先复用现有事实库模式。
- 不修改任何**已判正**题的字段；`pdf_questions.json` 仅允许改 `answer` / `answerKeywords`（Stream A），不动 `question`/`options`/`type`/`note`。
- 不重跑 `pdfs/_extract/build_final.py`（会冲掉既有人工订正，且其题号对齐已漂移）。
- 不做单题策展：Stream B 新增事实的 `match` 必须是通用化学原理词汇，可跨题复用。

## Background & Context
- 上一批（experiment-op，已评审 pass）：`gwCorrect 702→704`，`bothWrong 55→53`，`harm=0`，`precision=1`。基线快照 `_evalscratch/snapshot_after_expop.json`。
- W 类数据修复批次（`data-defect-repair-plan.md`）已确立方法：直接编辑运行时 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 的 `answer`/`answerKeywords`；高置信、有证据、不臆造；并证明**纯修键 `gwCorr +0`**（这些题引擎本就空结论）。
- 本批调研发现（探针 `_evalscratch/audit_datarepair2.py`、`_evalscratch/probe_search_answer.py`、`_evalscratch/dump_answer_pages.py`）：
  - `questions_parsed.json` 的 `(ans_page, num)` 对齐**已漂移**，把许多题映射到错误的答案册条目；运行时 `pdf_questions.json` 已有部分被人工修对（note 带 `[官方源订正]`/`[千问求解]` 标记）。
  - 官方真值源齐备：`pdfs/_extract/answer/page_*.json`（154 页答案册 OCR）+ `pdfs/extracted/*.txt`；习题册 OCR 仅 `pdfs/_extract/exercise/` 前 ~23 页（故本批不做题干复原）。
  - 已验证一个**内容检索解析器**：不依赖漂移的 `(ans_page,num)`，按题干化学关键词在答案册全文检索定位真实条目，并用解析与题干化学一致性佐证。已确认样本：**pdf-162 真实答案为 `c`**（答案册 page 57 num 5 第(3)子问「酸化应选盐酸，c 正确」，其运行时键当前被另一道 Pb 大题的 7 子问串污染）。
  - Stream B 初始线索：`pdf-075`（Xe 化合物反应选择，文本完整、键 `B` 与官方一致、无图）等需审计确认是否可由事实覆盖。
- 既有瀑布链与事实库（`data/highschool/*.json`）及 `LlmGatewayService` 路由不变；本批只增量追加、不改动既有事实与 C# 判定逻辑（Stream B 若确需 `.cs` 求解器，须单独论证并在 review 中重点核查）。

## Functional Requirements
- **FR-A1（Stream A 证据化修键）**: 每条修键必须有官方答案册定位证据：用内容检索解析器定位条目，且该条目的解析与题干化学一致；OCR 歧义/标签污染项（如 pdf-100/263/197）须渲染答案册对应页图、用 VL 读取确认后方可修，否则跳过。
- **FR-A2（Stream A 改后真值一致）**: 修后 `answer`/`answerKeywords` 与官方源一致；`answerKeywords[0]` 为规范化答案（选择题字母，填空为官方答案文本首段），与既有 `NormOpt`/评估判分兼容。
- **FR-A3（Stream A 零副作用）**: 修键不改变任何已判正题的 `gatewayAnswer`；不引入新的退化键或假阳性。
- **FR-B1（Stream B 候选准入）**: 仅对审计通过的候选题扩展：(i) 答案键正确或已在 Stream A 校正；(ii) 题干与选项文本完整、解题所需信息不依赖图像；(iii) 可由现有事实库模式覆盖（优先）；(iv) 新增事实经全库 blast-radius 扫描零 stray-hit。
- **FR-B2（Stream B 翻正）**: 候选题扩展后 `gatewayCorrect=True`，引擎结论与正确键一致；若候选需 `.cs` 求解器而非纯事实，须论证必要性并保持既有行为不变。
- **FR-B3（Stream B 零假阳性）**: 新增事实不得命中任何非目标题；既有事实与瀑布链不变。
- **FR-R1（红线）**: 全量评估 `LOST=[]`、`harm=0`、`enginePrecision=1`；已判正题 `gatewayAnswer` 字节级不变。
- **FR-R2（登记册）**: 更正 Stream A 已解题、Stream B 已解题在 `data-defect-register.md` 的过时登记；补登本批新发现缺陷；每条修键/扩展记录官方源证据。

## Non-Functional Requirements
- **NFR-1（可追溯）**: 每条改动（修键或加事实）在 `_evalscratch/` 留有探针输出/快照片段作为证据，登记册可复核。
- **NFR-2（最小改动）**: 仅改动必要文件（`pdf_questions.json`、事实库 json、测试真值表、登记册、spec 三件套、`_evalscratch/` 探针），不夹带无关重构。
- **NFR-3（可复现）**: 评估命令、探针参数、基线对比脚本均记录在 `tasks.md`/`review.md`，第三方可重跑。

## Constraints
- **Technical**: 数据修键只动 `answer`/`answerKeywords`；引擎扩展优先事实，`.cs` 改动须最小且单独评审；不重跑提取管线。
- **Business**: 宁可不修也不引入错误键或错误事实；零蒙数。
- **Dependencies**: 官方源 `pdfs/`；既有评估管线 `run_eval_resume.ps1`；既有事实库与 `LlmGatewayService` 瀑布链；既有护栏测试（`Batch2`/`Batch3`/`MultiSelectChoice*` 等）。

## Assumptions
- 答案册 OCR JSON 对大多数题的答案字母/文本可读；少数歧义项可用 `pdfs/_extract/peek_answer.py` 的 VL 读页图（该脚本内嵌 API key，仅读不写）。
- `bothWrong=53` 中至少存在一道「键正确、文本完整、无图、可由事实覆盖」的选择题作为 Stream B 候选；若审计否决全部候选，G2 按安全降级处理（不强行扩展，review 确认审计充分）。

## Acceptance Criteria

### AC-1: Stream A 每条修键有官方源证据且改后真值一致
- **Type**: `rule`
- **Given**: 一条 `bothWrong` 题的 `answer`/`answerKeywords` 被判定错误/污染/垃圾
- **When**: 用内容检索解析器在官方答案册定位到真实条目，解析与题干化学一致（歧义项另附页图 VL 读数）
- **Then**: `pdf_questions.json` 中该题 `answer`/`answerKeywords` 被修正为与官方源一致，且在登记册记录定位证据（页+题号+改前/改后）
- **Pass Condition**: 每条修键均可在 `_evalscratch/` 探针输出或答案册 OCR 中复核到官方真值；改后答案与官方一致
- **Evidence**: 审计文档 + 登记册对应行 + `git diff`

### AC-2: Stream A 零回归
- **Type**: `rule`
- **Given**: 基线快照 `snapshot_after_expop`（gwCorrect=704, bothWrong=53, harm=0, precision=1）
- **When**: 仅执行 Stream A 修键后跑全量评估
- **Then**: `LOST=[]`、`harm=0`、`enginePrecision=1`、已判正题 `gatewayAnswer` 字节级不变；不出现新的 `gatewayWrong`
- **Pass Condition**: diff 脚本输出满足上述全部
- **Evidence**: `_evalscratch/` diff 输出

### AC-3: Stream B 翻正（或安全降级）
- **Type**: `rule`
- **Given**: Stream A 已确立正确键，审计产出 Stream B 候选清单
- **When**: 对准入候选追加事实（必要时求解器）并跑评估
- **Then**: 每道扩展的候选题 `gatewayCorrect=True` 且结论与正确键一致；若审计判定无安全候选，则不扩展且在 `review.md` 记录审计充分性
- **Pass Condition**: 有候选时全部翻正；无候选时安全降级有记录
- **Evidence**: 快照中候选题 `gatewayCorrect`/`gatewayAnswer`/`gatewayConclusion`

### AC-4: Stream B 零假阳性
- **Type**: `rule`
- **Given**: Stream B 新增事实
- **When**: 对全库所有题做 blast-radius 扫描
- **Then**: 每条新事实仅命中其目标 (qid, option)，零 stray-hit；既有事实未被删改；瀑布链顺序不变
- **Pass Condition**: 探针输出 `TOTAL PROBLEMS=0`，且既有事实内容 `git diff` 无变更
- **Evidence**: `_evalscratch/` 探针输出

### AC-5: 红线与全库护栏
- **Type**: `rule`
- **Given**: 本批全部改动完成
- **When**: 跑全量单测与全量评估
- **Then**: 全量单测通过；护栏断言通过（`FullBank_MultiSelect_Assertions_AreExactlySeven` 仍为 7、`FullBank_SingleSelect_Assertions_StaySingleLetter`、`Concept_NoOverlap`、各 `NoHarm`）；`enginePrecision=1`、`harm=0`、`LOST=[]`
- **Pass Condition**: 全部为绿/满足
- **Evidence**: `dotnet test` 输出 + 评估 diff 输出

### AC-6: 证据与可追溯性
- **Type**: `rubric`
- **Dimension**: 每条改动（修键/加事实）的官方源与探针证据可复核程度
- **Scale**: 1-5
- **Anchors**: 1 = 改动无证据；3 = 部分改动有证据、部分缺失；5 = 每条改动都有可复核的官方源定位 + 探针/快照证据 + 登记册记录
- **Pass Threshold**: >= 4
- **Evidence**: 审计文档、登记册、`_evalscratch/` 工件

### AC-7: 零蒙数纪律
- **Type**: `rubric`
- **Dimension**: 是否存在无官方源支撑的答案/事实写入；歧义项处理是否合规（页图核实或跳过）
- **Scale**: 1-5
- **Anchors**: 1 = 存在臆造答案/事实；3 = 少数项证据薄弱；5 = 全部答案/事实均有官方源或解析佐证，歧义项要么页图核实要么明确跳过并记录
- **Pass Threshold**: >= 4
- **Evidence**: 审计文档、修键/加事实清单、登记册

## Open Questions
- 无阻塞性开放问题：范围（修键 + 引擎扩展并行）已由用户确认；Stream B 候选的精确清单由 Task 1 审计产出。
