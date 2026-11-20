# 选择题求解能力泛化 + 数据缺陷修复 - 需求规格说明书（PRD）

## Overview
- **Summary**: 停止"逐题硬编码事实"的路线，通过两条轨道提升化学辅导引擎的选择题求解能力：(1) **引擎能力泛化**——新增/扩展多选判定、通用组合/计数求解、氧化还原电子转移计数三类确定性推理；(2) **数据缺陷修复**——纠正错题键、回填缺失题面，解除阻塞引擎求解的数据硬伤。所有改动严守"零假阳性 / 零 harm / 零回归（LOST=0）"红线，引擎始终是唯一真值源，不偷看备注。
- **Purpose**: batch1–batch3 的逐题策展已逼近纯文本事实库的安全上限（剩余 34 道中仅 4 道是纯引擎缺口，其余为数据缺陷）。继续逐题硬编码会触发零假阳性红线且不可持续。本 spec 用**泛化的引擎推理 + 数据修复**替代逐题硬编码，建立可扩展的求解能力。
- **Target Users**: 离线评估（EvalRunner 全库评测）的维护者；引擎架构的后续迭代者。

## Goals
- **G1 多选判定泛化**：引擎能在不依赖题干"双选/多选"显式标记、不逐题策展事实的前提下，对多选选择题输出正确字母集合（如 `AC`），并被既有判分路径采纳判正。
- **G2 通用组合/计数求解**：引擎能拆解 ①②③④ / (1)(2) 形式的子项，对**一般化学陈述**子项逐子项判定真伪（而非仅限"N 种"计数断言），再组合匹配选项；任一子项不可判则整题 null。
- **G3 氧化还原电子转移计数**：引擎能对已配平反应计算氧化态变化与电子转移数，判定"每生成 1 mol X 转移 N mol 电子"类陈述。
- **G4 数据缺陷修复**：纠正 9 道存储 answer 与化学真值/备注冲突的错题键；审计并回填可恢复的缺失子项/选项文本，解除组合/计数题的输入缺失硬伤。
- **G5 零回归**：所有改动在全库 757 题上 LOST=0、harm=0、precision=1.0；全量测试套件 0 失败。

## Non-Goals
- **不做图像/结构图解析**：132 道 FIGURE 题与 7 道被误标为纯文本的结构图题（pdf-233/237/331/349/376/382/384）归 Task 43 图像解析轨道，本轮不动。
- **不用备注（note）作运行时真值源**：`EngineRequest` 不含 note（见 [EngineRequest](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs#L26-L51)、[GenPdfQuestions](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1670-L1713)）。note 仅可作为数据修复时核对答案键的辅助参考，绝不注入引擎判定。
- **不逐题硬编码事实**：不为单题新增 `general-choice-facts` 条目来硬解；新增真值必须是**可泛化的推理原语**或经全库零假阳性护栏验证的通用事实。
- **不改评分红线**：不修改 `IsAnswerCorrect`、`TryAdoptMultiSelectConclusion`、`FactChoiceMatcher` 既有单选契约（Task 36）的语义；新增能力为**增量接线**。
- **不追求覆盖全部 34 道**：不可恢复的数据缺陷（源 PDF 不可得、子项无法还原）诚实登记为数据缺陷清单，保持 null，不蒙数。

## Background & Context

### 34 道"纯文本未判正"逐题归因（checkpoint p3_b3 + 原始 pdf_questions.json 核实）
| 归因桶 | 数量 | 题目 | 说明 |
|---|---|---|---|
| 引擎能力缺口（文本完整+答案键干净） | 4 | pdf-075、115、252、491 | 075 需氧化还原电子计数；115/252/491 为多选（AC/BFH/AC） |
| 答案键错误/与备注矛盾 | 9 | pdf-101、148、197、213、269、279、280、399、405 | answer 与 note 结论冲突或被填成 `[A,B,C,D]` 占位 |
| 子项陈述缺失（仅 note 有，运行时文本无） | 5 | pdf-009、012、020、065、337 | 组合/计数题的 ①②③④ 在 question 字段丢失（OCR 截断） |
| 选项截断/缺失 | 4 | pdf-158、291、437、381 | 仅 1–3 个选项，D 项等丢失；381 仅剩 A |
| 选项内容为空（图未挂进选项） | 2 | pdf-319、436 | C/D 选项文本为空 |
| 题面格式/答案不一致 | 1 | pdf-263 | 问"正确的有几项"但选项是陈述句本身 |
| 一题多问配对 | 1 | pdf-185 | `(1)B (2)D` 两问合一 |
| 实为图依赖（误标纯文本） | 7 | pdf-233、237、331、349、376、382、384 | 引用结构图/流程图，资产未挂载 |
| 已安全处理（诚实 null） | 1 | pdf-394 | CountingChoiceValidator 正确返回 null |

合计 34 ✓。证据见 [unsolved_puretext_choice.md](file:///d:/github/CogniThink-Chem/_evalscratch/unsolved_puretext_choice.md)、[raw_ids_dump.txt](file:///d:/github/CogniThink-Chem/_evalscratch/raw_ids_dump.txt)。

### 既有能力与缺口
- **多选输出契约已存在**（Task 36）：[FactChoiceMatcher](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs#L123-L141) 支持题干含「双选/多选」时返回多字母；[判分路径](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L637-L644) 已有 `TryAdoptMultiSelectConclusion`。Task 41/42 已策展 6 道标记型双选/多选。**缺口**：pdf-115/252/491 题干无显式标记、选项无对应真值源 → 被当单选处理返回 null。
- **计数/组合校验器已存在但能力受限**：[CountingChoiceValidator](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CountingChoiceValidator.cs) 路径一（L127-147）的 `items = items.Where(it => ExtractClaimedCount(it) is not null)` **只保留含"N 种"的子项**，一般化学陈述子项被丢弃；组合型（选项为 ①②③ 组合）不被 `LooksLikeCountOfCorrect` 识别。
- **氧化还原校验器只判方程式对错**：[RedoxChoiceValidator](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/RedoxChoiceValidator.cs) 不计算电子转移数，无法判 pdf-075 的"每生成 1 mol O₂ 转移 4 mol 电子"。
- **运行时输入不含 note**：引擎只能从 `RawText`（题干+选项）求解。
- **数据键修正有成熟先例**：Task 35 续（7 道）、Task 46（pdf-108/173/507）已建立"核对化学真值→改 answer+answerKeywords→全库护栏验证"的流程。

### 红线（冻结，必须遵守）
- LLM 永不为化学真值源；引擎（事实库 + 确定性推理原语）是唯一真值源。
- 凡采纳必判正（`IsAnswerCorrect` 通过）；无唯一解 → `null`，绝不蒙数。
- 零假阳性、零 harm、零回归（LOST=0）。
- 引擎逻辑只增量接线，不改既有校验器判定语义；既有单选行为字节级不变。
- 每次改动：全量测试绿 + 全库 EvalRunner before/after diff 后再提交。

## Functional Requirements

### 引擎能力泛化
- **FR-1（多选判定器，MultiSelectChoiceValidator）**：在 `CallChoice` 瀑布链**末端**（`ChoiceVerifier.TryDecide` 兜底之前）新增一个增量校验器。它逐选项调用一个可复用的 `IStatementJudge` 获得确定真/假；当问法方向为"选正确"且 ≥2 个选项被确定为真、或"选错误"且 ≥2 个被确定为假时，返回拼接字母串；任一选项不可判（判定器返回"未定"）→ 整题返回 null。不依赖题干"双选/多选"标记。
- **FR-2（可复用陈述判定原语 IStatementJudge）**：抽离/聚合现有真值源（general/experiment/organic/structure 事实库匹配、计数、氧化还原、结构周期、化学计量）为统一接口 `Judge(stem, statement) -> True | False | Undecided`。组合求解器与多选判定器共用。**仅当某原语给出确定结论时采纳；多个原语冲突时以 Undecided 论**（不蒙数）。
- **FR-3（通用组合/计数求解器）**：扩展 `CountingChoiceValidator`，使其对 ①②③④ / (1)(2) / 1.2. 标记拆出的**一般化学陈述子项**也逐子项调用 `IStatementJudge` 判定（不再仅保留"N 种"断言）；全部子项可判后，对"正确的个数"型算出正确项数匹配选项，对组合型（选项为 ①②③ 组合）算出正确子项集合匹配选项；任一子项不可判 → null。
- **FR-4（氧化还原电子转移计数原语）**：在 `RedoxChoiceValidator` 内新增或独立出一个原语：输入配平的反应式（或半反应），计算各元素氧化态变化与转移电子数，能判定"每生成 1 mol X 转移 N mol 电子""X 是氧化剂/还原剂""氧化产物/还原产物"类陈述；不可判（方程未配平、物种无解析）→ Undecided。
- **FR-5（可控格式新题验证）**：为每个新增原语配套 tier2 可控格式合成题（文本完整、答案已知），保证原语在干净输入上 100% 正确；现有真题能解为奖励、不能解诚实 null（沿用 [counting-stoichiometry 计划](file:///d:/github/CogniThink-Chem/.trae/documents/chem-engine-counting-stoichiometry-plan.md) 的"保底+奖励"模式）。

### 数据缺陷修复
- **FR-6（错题键纠正）**：对 9 道 answer 与化学真值/note 冲突的题（pdf-101、148、197、213、269、279、280、399、405），逐题独立核对化学真值后修正 `answer` 与 `answerKeywords`（权威源 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`，构建复制到 bin；`_evalscratch/` 旧副本不动）。note 仅作辅助核对，不反向修改 note 来迁就错误 answer。
- **FR-7（缺失题面回填审计）**：对 12 道子项/选项缺失题（pdf-009、012、020、065、337、158、291、437、381、319、436、263、185），优先从原始 PDF 重新抽取恢复文本；源不可得时用 note 还原**子项文本（不含 verdict）**作为次选。回填仅写入 `question`/`options` 字段，不注入任何判定结论。无法可靠恢复的登记入数据缺陷清单，保持 null。
- **FR-8（数据缺陷清单）**：产出一份 `data-defect-register.md`，列出所有未修复的数据缺陷（不可恢复的缺失文本、一题多问、图未挂载）及原因，供后续数据管道/图像轨道接续。

## Non-Functional Requirements
- **NFR-1 零假阳性**：新增校验器在全库（含已解题）上不得产生任何错误采纳；全库扫描 precision=1.0、harm=0。
- **NFR-2 零回归**：全库 EvalRunner before/after diff 中 LOST=0，gatewayCorrect 单调不减。
- **NFR-3 增量安全**：新增校验器置于瀑布链末端 fallback 位，仅在所有既有校验器返回 null 时才触发，确保已解题的结论字节级不变。
- **NFR-4 可测试**：每个原语有单元测试（正例+反例+边界）；每个校验器有全库零假阳性护栏测试。
- **NFR-5 可追溯**：每处数据修复在 tasks.md 留痕（题 id、原值、新值、核对依据）。

## Constraints
- **Technical**: Windows / PowerShell 5；`dotnet vstest` 运行测试（避免 `dotnet test` 节点复用崩溃）；构建前杀残留 dotnet/testhost 进程；删 `_Tmp*.cs` 探针；提交用 `git commit -F <UTF-8 txt>`（PS5 中文 heredoc 不可用）。
- **Business**: 严守项目冻结红线（见上）；不引入 LLM 推理依赖。
- **Dependencies**: 依赖 Task 36 多选契约、Task 41/42 多选策展先例、Task 35/46 数据键修正先例；图像题依赖 Task 43（本轮不做）。

## Assumptions
- **A1**: 原始 PDF 源对缺失文本的题可重新获取（用于 FR-7 回填）。若不可得，FR-7 退化为"登记入缺陷清单"，不猜填。
- **A2**: 9 道错题键的化学真值可由 note + 化学知识独立确认（沿用 Task 35/46 已验证可行的方法）。
- **A3**: 新增原语在当前 Chem 引擎能力范围内（配平、氧化态、电子计数已有/可扩展），不需新外部依赖。

## Acceptance Criteria

### AC-1: 多选判定器为末端 fallback 且不回归
- **Type**: `rule`
- **Given**: 新增 `MultiSelectChoiceValidator` 接入 `CallChoice` 末端
- **When**: 对全库 757 题运行 EvalRunner，对比改动前 checkpoint
- **Then**: 所有改动前 gatewayCorrect=true 的题仍为 true（LOST=0），且其 gatewayAnswer 字节级不变
- **Pass Condition**: diff 中 LOST=0；已解题 gatewayAnswer 无变化
- **Evidence**: `_evalscratch` 下 before/after jsonl 的 diff 脚本输出 LOST=0

### AC-2: 多选判定器的安全语义
- **Type**: `rule`
- **Given**: 一道多选题，其选项经 `IStatementJudge` 判定
- **When**: 存在任一选项被判定为 Undecided，或满足方向的确定候选 <2
- **Then**: 校验器返回 null（不臆断）
- **Pass Condition**: 单元测试覆盖 Undecided / 候选不足 / 多原语冲突三种情况均返回 null
- **Evidence**: `MultiSelectChoiceValidatorTests` 相关用例通过

### AC-3: 多选判定器在可控格式题上正确输出
- **Type**: `rule`
- **Given**: tier2 可控格式多选题（文本完整、选项可被现有真值源确定判定）
- **When**: 运行校验器
- **Then**: 返回正确字母集合，且被 `TryAdoptMultiSelectConclusion` + `IsAnswerCorrect` 判正
- **Pass Condition**: tier2 多选题全部解出且字母正确；全库扫描零假阳性
- **Evidence**: 新增护栏测试 + 全库 precision=1.0

### AC-4: 通用组合/计数求解器对一般陈述子项生效
- **Type**: `rule`
- **Given**: 一道"正确的个数"或组合型题，子项为一般化学陈述且全部可被 `IStatementJudge` 确定判定
- **When**: 运行求解器
- **Then**: 返回正确选项；任一子项 Undecided 时返回 null
- **Pass Condition**: tier2 组合/计数题（子项可判）全部解出；子项含不可判项时返回 null
- **Evidence**: 扩展 `CountingChoiceValidatorTests` 新增用例通过

### AC-5: 氧化还原电子转移计数原语
- **Type**: `rule`
- **Given**: 配平的氧化还原反应与"每生成 1 mol X 转移 N mol 电子"类陈述
- **When**: 运行原语
- **Then**: 正确判定陈述真伪；方程未配平或物种不可解析时返回 Undecided
- **Pass Condition**: tier2 氧化还原电子计数题全部正确；不可判输入返回 Undecided
- **Evidence**: `RedoxChoiceValidatorTests` 新增用例通过

### AC-6: 错题键纠正零回归
- **Type**: `rule`
- **Given**: 9 道错题键已按化学真值修正 answer+answerKeywords
- **When**: 全库 EvalRunner before/after
- **Then**: 修正题不引入新的假阳性或 harm；LOST=0；修正后该题若被引擎解出则判正，若仍不可解则保持 null
- **Pass Condition**: diff LOST=0、harm=0、precision=1.0；9 道题的 answer 字段与 note/化学真值一致
- **Evidence**: tasks.md 留痕 + diff 输出 + `Batch3ChoiceValidatorTests` 全绿

### AC-7: 缺失题面回填不注入判定
- **Type**: `rule`
- **Given**: 回填恢复了子项/选项文本的题
- **When**: 引擎求解
- **Then**: 回填内容仅为题面文本，引擎仍独立推理；回填不改变任何题的存储 answer
- **Pass Condition**: 回填题的 `question`/`options` 字段文本完整且无 verdict 字眼注入；存储 answer 未被回填改动
- **Evidence**: 回填前后 question/options diff 审查；数据缺陷清单登记不可恢复项

### AC-8: 引擎能力泛化度（不逐题硬编码）
- **Type**: `rubric`
- **Dimension**: 新增能力对未见过的题的泛化程度，及对逐题硬编码的替代程度
- **Scale**: 1-5
- **Anchors**: 1 = 仍主要靠新增单题事实；3 = 有泛化原语但仅覆盖少量固定模式；5 = 原语可泛化到一类题，新增题无需改代码/加单题事实即可解出
- **Pass Threshold**: >= 4
- **Evidence**: tier2 题未在事实库中单题策展即可被原语解出的比例；代码审查无新增单题 `general-choice-facts` 条目

### AC-9: 全量验证绿
- **Type**: `rule`
- **Given**: 全部改动完成
- **When**: 运行全量测试套件（TutorBrain + Chem）+ 全库 EvalRunner
- **Then**: TutorBrain 0 失败、Chem 0 失败；EvalRunner 无 failed 记录；diff LOST=0
- **Pass Condition**: 测试 0 失败；EvalRunner failed=0；LOST=0
- **Evidence**: 测试输出 + checkpoint summary

## Open Questions
- [ ] **OQ-1**: 原始 PDF 源是否可得、能否重新抽取缺失子项？（决定 FR-7 是回填还是仅登记）——实现前需确认。
- [ ] **OQ-2**: pdf-115/252/491 的选项能否被现有真值源（不加单题事实）确定判定？若不能，G1 在当前数据集上即时收益可能为 0，需以 tier2 保底题证明能力。——实现阶段用探针验证。
- [ ] **OQ-3**: 组合型题（选项为 ①②③ 组合）的子项恢复后，`IStatementJudge` 能否覆盖其化学陈述类别？若覆盖不足，组合求解器在真题上收益有限。——实现阶段验证。
