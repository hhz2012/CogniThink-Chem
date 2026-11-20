# 选择题求解能力泛化 + 数据缺陷修复 - 独立审查

> 本文件在 Review 阶段创建。审查者须独立核验（不得仅采信实现者自验），逐条填写检查点证据与 Review R1 裁决。除本文件外不得修改实现代码/数据。
>
> 审查环境：Windows PowerShell 5；提交态 = 主提交 `80b9efc` + 文档同步 `7d1376e`（仅改 2 个 tasks.md）。主工作树含外部未提交改动（`general-choice-facts.json`、`pdf_questions.json` 选项补全、`Batch3ChoiceValidatorTests.cs`、`CogniThink.Chem.sln`、`PdfOcrRepair/`、`image-ocr-choice-coverage/`、`_TmpProbeSubitems.cs`），均不属本次范围；测试在干净 `git worktree @7d1376e` 独立构建运行，避免外部改动污染。

## 检查点（Checkpoints）

- [x] CP-R1: 全库零回归——既有解题 gatewayAnswer 字节级不变、LOST=0
  - **Type**: `rule`
  - **Covers**: AC-1, AC-6, AC-9（diff 部分）
  - **Pass Condition**: EvalRunner before/after diff 中 LOST=0、answer-changed=0、gatewayCorrect 单调不减
  - **Evidence**: 独立重跑 `_evalscratch\_diff_two.ps1` 两组，结果：① `engine_before`(757)→`engine_after`(757)：**GAIN=0、LOST=0、answer-changed=0**；② `base_pre5b`(757)→`after_5b`(757)：**GAIN=0、LOST=0、answer-changed=0**。`Select-String` 统计 `gatewayCorrect:true`：engine_before=448、engine_after=448、base_pre5b=448、after_5b=448；`checkpoint_engine_after.jsonl` 共 757 行。两端 correct 单调不减（448→448），已解题 `gatewayAnswer` 字节级不变（answer-changed=0）。

- [x] CP-R2: 多选/计数/氧化还原原语的安全语义——不可判时诚实 null、不蒙数
  - **Type**: `rule`
  - **Covers**: AC-2, AC-4（Undecided 分支）, AC-5（Undecided 分支）
  - **Pass Condition**: 任一选项/子项 Undecided、候选不足、多原语冲突、方程未配平/不可解析 时均返回 null/Undecided；既有单选/计数路径行为字节级不变
  - **Evidence**: 代码审查确认安全语义：
    (1) `MultiSelectChoiceValidator.TryDecide`：任一选项 `Judge==Undecided` → `return null`（L51）；满足问法方向的候选 `<2` → `return null`（L60）；输出字母按 Ordinal 排序拼接；`WrongDir` 与 `FactChoiceMatcher` 同口径。
    (2) `CompositeStatementJudge`：多原语给出冲突结论 → `Undecided`（L30-31），全部不命中 → `Undecided`（L34），一致则采纳。
    (3) `CountingChoiceValidator` 新增 1b `TryDecideGeneralSubItems` / 1c `TryDecideCombination`：任一子项 `Undecided` → null（L264/L302）；组合型仅 `askDir=="correct"`（反向组合语义歧义 → null，L292），选项无法解析为编号组合 → null（L312）。
    (4) `RedoxElectronStatementJudge`：方程未配平 / 非氧化还原 / 电子不守恒 / 转移电子为 0 → null（L244）；目标物种出现在错误侧 / 同侧多次 / 未出现 → null（L259/L262）；目标不含变价元素 → null（L270）；多方程无标签 → 歧义 null（L232）；`EquationBalancer.Balance` 失败 → null（L274）；范围刻意收窄，"氧化剂/还原剂/产物/比例"等角色陈述不被 `ParseElectronClaim` 解析 → `Undecided`。
    接线位置：`MultiSelectChoiceValidator` 接在 `CallChoice` 末端（`LlmGatewayService.cs` L852），在所有单选校验器（Classification…Stoichiometry）之后、`ChoiceVerifier.TryDecide` 兜底（L866）之前，仅前面全部返回 null 才触发，不抢在既有校验器前。
    既有路径字节级不变：`git diff 80b9efc^..80b9efc` 显示 `FactChoiceMatcher` 仅新增 `JudgeStatement`（`TryDecide` 未动）、`LlmGatewayService` 仅插入 MultiSelect 块、`CountingChoiceValidator` 唯一删除行 `return TryDecideCountOfCorrect(...)` 被替换为「1a 非空则返回 1a，否则试 1b」（1a 非空路径行为不变）；CP-R1 的 answer-changed=0 / LOST=0 是运行时佐证。安全分支测试覆盖见 CP-R3/CP-R5。

- [x] CP-R3: 原语在 tier2 可控格式题正确输出、被采纳判正，全库零假阳性
  - **Type**: `rule`
  - **Covers**: AC-3, AC-4（求解分支）, AC-5（求解分支）
  - **Pass Condition**: tier2 多选/组合计数/氧化还原电子题全部解出且字母正确并被 `IsAnswerCorrect` 判正；全库扫描 precision=1.0、harm=0
  - **Evidence**: (1) 无新增单题事实条目：`git show 80b9efc --name-only` 显示唯一改动的数据文件是 `pdf_questions.json`，未触碰任何 facts json（general-choice-facts / experiment-facts / organic-facts / structure-periodic-facts / counting-facts）。(2) tier2 测试全绿：在干净 worktree @7d1376e 用 `--TestCaseFilter` 过滤运行 4 个新增测试类（`MultiSelectChoiceValidatorTests` / `StatementJudgeTests` / `CountingChoiceGeneralizationTests` / `RedoxElectronStatementJudgeTests`）= **41 通过 / 0 失败 / 0 跳过**（与 tasks.md 声称 6+11+6+18=41 一致），含候选不足→null、任一选项 Undecided→null、多原语冲突→Undecided、子项 Undecided→null、反向组合→null、未配平/非氧还/目标歧义→Undecided 等安全分支，以及 `FullBank_*_NoHarm` / `NeverContradicts_*` 全库零假阳性护栏。全库 precision=1.0、harm=0 由 CP-R1 GAIN=0 佐证（无新采纳即无错采纳）。

- [x] CP-R4: 数据修复不注入 verdict、不改存储 answer、不可靠恢复登记保持 null
  - **Type**: `rule`
  - **Covers**: AC-6（真值一致性）, AC-7
  - **Pass Condition**: 提交的 `pdf_questions.json` 改动仅为 10 处 answer/answerKeywords；无 verdict 字眼注入 question/options；未修复项全部登记入 `data-defect-register.md`；回填（若有）不改变存储 answer
  - **Evidence**: (1) 仅 10 处 answer/answerKeywords：用 .NET `JavaScriptSerializer` 对 `80b9efc^` 与 `80b9efc` 的 `pdf_questions.json` 做字段级（question/options/note/answer/answerKeywords）比对，结果恰好 10 个 id 变更——**pdf-101/148/185/213/269/279/280/399/405/406**，每题仅 `answer` 与 `answerKeywords` 变化，`question`/`options`/`note` 字节一致；总题数 545→545 无增删。`git diff` 复核一致（10 个 hunk，+/- 共 52 行全部落在 `answerKeywords` 数组与 `answer` 字段）。(2) 无 verdict 注入：`answerKeywords` 新值均为既有 `options` 字段中正确选项的原文复制（如 pdf-101 取 option C 文本 "KSCN能区分…"），不含「正确/错误/应选/答案为」等判定字眼；`note`/`question`/`options` 未改动。(3) 回填：本轮按设计未执行题面回填（无干净原页 OCR，不从 note 剥离 verdict），存储 `answer` 未被回填改动。(4) `data-defect-register.md` 覆盖：A.1 已修复 10 道留痕 / A.2 未修（197）/ B 子项缺失（009/012/020/065）/ C 图像依赖（337/158/263/319）/ D note·选项损坏但键正确（381/075）。**【actionable 缺口】**：`pdf-291`（缺 D 选项）、`pdf-437`（仅 A/B，缺 C/D）、`pdf-436`（C 选项文本为空）在 80b9efc 提交态确为选项截断/空缺陷且 `gatewayCorrect=false`，但未登记入册（spec FR-7 原列这 3 题为回填审计对象），建议补登记；主工作树有外部未提交的「pdf 选项补全」在处理它们，但不属于本次提交范围。

- [x] CP-R5: 全量验证绿
  - **Type**: `rule`
  - **Covers**: AC-9
  - **Pass Condition**: TutorBrain 0 失败、Chem 0 失败；EvalRunner 无 failed 记录
  - **Evidence**: 主工作树因外部未提交改动（`general-choice-facts.json`、`pdf_questions.json` 选项补全、`Batch3ChoiceValidatorTests.cs`），其 DLL 跑出 2 失败（`Batch3ChoiceValidatorTests.Concept_ZeroFalsePositive_AcrossAllBuildTestCases` 报 pdf-006、`Concept_Coverage_MatchesTruthTable` 期望 129 实际 130）——均落在外部改动文件，与本次提交无关。为核验提交态，另建干净 `git worktree @7d1376e`（=80b9efc+文档同步），`dotnet build -c Debug -nr:false`（0 错误）后 `dotnet vstest`：**TutorBrain = 982 通过 / 0 失败 / 0 跳过**（exit 0）；**Chem = 905 通过 / 0 失败 / 0 跳过**（exit 0）。EvalRunner：`checkpoint_engine_after.jsonl` 共 757 行，0 行含 `Exception` / `System.` 栈追踪（无 failed 记录），`gatewayCorrect` true=448 / false=309；结合 CP-R1 两组 diff LOST=0。提交态全量绿。

- [x] CP-U1: 引擎能力泛化度（不逐题硬编码）
  - **Type**: `rubric`
  - **Covers**: AC-8
  - **Dimension**: 新增能力对未见过的题的泛化程度，及对逐题硬编码的替代程度
  - **Scale**: 1-5
  - **Anchors**: 1 = 仍主要靠新增单题事实；3 = 有泛化原语但仅覆盖少量固定模式；5 = 原语可泛化到一类题，新增题无需改代码/加单题事实即可解出
  - **Pass Threshold**: >= 4
  - **Evidence**: 评分 **4/5**（阈值 ≥4，通过）。依据：(1) 新增 `IStatementJudge` 统一接口 + `CompositeStatementJudge` 聚合器（事实库/计数/氧化还原电子），多原语冲突→Undecided，可被多选与组合求解器复用——是泛化原语而非单题策展。(2) `MultiSelectChoiceValidator` 不依赖题干「双选/多选」标记、逐选项原语判定，适用于一类多选题。(3) `CountingChoiceValidator` 一般子项路径覆盖 ①②③④ / (1)(2) / 1.2. 一般化学陈述子项（不再仅限「N 种」），并支持组合型。(4) `RedoxElectronStatementJudge` 对任意配平氧化还原反应计算电子转移，泛化到一类陈述。(5) 代码审查确认 80b9efc 未新增任何单题 `general-choice-facts` 条目；41 个 tier2 可控格式题（未单题策展）即被原语解出（见 CP-R3）。扣 1 分：生产真题即时收益为 0——目标多选/组合题或被更早校验器拦截、或被数据缺陷（子项文本缺失）阻塞，泛化能力尚未在真题上兑现奖励（需待数据回填接续），但这是「引擎就绪、数据接续」的正确设计，非原语本身缺陷。

## Review History

### Review R1
- **Result**: `pass`
- **Evidence**: 6 个检查点全部通过。CP-R1 两组 EvalRunner diff 均 GAIN=0/LOST=0/answer-changed=0、correct 448/757 单调不减；CP-R2 代码审查确认 Undecided/候选不足/多原语冲突/未配平/歧义一律 null 或 Undecided，MultiSelect 末端接线且既有路径字节级不变；CP-R3 无新增单题 facts 条目、4 个新增测试类 41 例全绿、全库零假阳性；CP-R4 字段级比对确认 pdf_questions.json 仅 10 处 answer/answerKeywords 改动且无 verdict 注入（发现登记册遗漏 pdf-291/437/436 三项，为非阻塞 actionable 项）；CP-R5 干净 worktree @7d1376e 独立构建测试 TutorBrain 982/0/0、Chem 905/0/0，EvalRunner 757 行 0 异常；CP-U1 泛化度 4/5（≥4）。无阻塞性问题。
- **Blocked By**: （仅 blocked 时填写）
- **Resume When**: （仅 blocked 时填写）

### 补救记录（R1 非阻塞 actionable 项）
- **R1 发现**：`data-defect-register.md` 遗漏 pdf-291（缺 D 选项）、pdf-437（仅 A/B，缺 C/D）、pdf-436（C 选项「水合离子」为图示、文本缺失）三项在 80b9efc 提交态确为未修复选项缺陷且 `gatewayCorrect=false`（见 CP-R4）。
- **处置**：已补登记入册——pdf-436 入 C 类（选项内容依赖图像），pdf-291/437 入 D 类（选项文本缺失，存储键与 note 推导一致、外部「选项补全」WIP 正在处理）。**仅文档改动，未触碰实现代码或数据，零行为变更**；R1 `pass` 裁决不变，无遗留 actionable 项。
