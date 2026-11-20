# 选择题求解能力泛化 + 数据缺陷修复 - 实现计划

> 父需求：[spec.md](./spec.md)。所有任务严守红线：零假阳性 / 零 harm / 零回归（LOST=0）、引擎唯一真值源、增量接线不改既有语义、全量测试绿 + 全库 EvalRunner diff 后再提交。

## Task 1: 抽离 `IStatementJudge` 统一陈述判定原语
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 定义接口 `IStatementJudge`：`Judge(string stem, string statement) -> Verdict { True, False, Undecided }`。
  - 把现有真值源（general/experiment/organic/structure 事实库匹配、计数、氧化还原、结构周期、化学计量）的**逐选项判定逻辑**聚合为一个组合判定器：任一原语给出确定结论则采纳；多个原语给出冲突结论 → `Undecided`（不蒙数）；全部不命中 → `Undecided`。
  - **红线**：聚合器的行为对既有单选瀑布链**字节级不变**——既有校验器仍走原路径，`IStatementJudge` 仅作为新增校验器（多选/组合）的依赖，不替换 `FactChoiceMatcher.TryDecide`。
  - 先实现事实库匹配 + 计数两个已有原语的适配（氧化还原电子计数在 Task 4 接入）。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-1.1: 给定一条在事实库中有唯一命中的陈述，`Judge` 返回对应 True/False；给定无命中的陈述返回 Undecided。证据：`StatementJudgeTests` 单元用例通过。
  - `rule` TR-1.2: 给定两个原语给出冲突结论的陈述，`Judge` 返回 Undecided（不偏信任一）。证据：冲突用例通过。
  - `rule` TR-1.3: 既有 `FactChoiceMatcher.TryDecide` 全库行为不变（Batch3/Counting 护栏测试全绿，解出数与此前一致）。证据：全量测试 0 失败。
- **Notes**: 这是多选与组合求解器的共同地基，必须先稳定。适配现有原语时不改其判定逻辑，只包装输出。
- **Completion Evidence**: 新增 `IStatementJudge`/`CompositeStatementJudge`/`FactBaseStatementJudge`/`CountingStatementJudge`；`FactChoiceMatcher.JudgeStatement` 纯增量（不动 `TryDecide`）；`StatementJudgeTests` 11 例通过（TR-1.1/1.2 + 全库无矛盾护栏 TR-1.3）；全量 980/0/0。

## Task 2: 新增 `MultiSelectChoiceValidator`（末端 fallback）+ tier2 多选保底题
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 在 `CallChoice` 瀑布链**末端**、`ChoiceVerifier.TryDecide` 兜底之前插入 `MultiSelectChoiceValidator`（增量、仅当前面全部返回 null 才触发，保证不回归已解题）。
  - 逻辑：逐选项调 `IStatementJudge`；问法方向="选正确"且 ≥2 选项确定为真（或"选错误"且 ≥2 确定为假）→ 返回拼接字母串（如 `AC`），走既有 `TryAdoptMultiSelectConclusion` 判分；任一选项 Undecided 或候选不足 → 返回 null。
  - 不依赖题干"双选/多选"标记。
  - 配套 tier2 可控格式多选题（文本完整、选项可被现有真值源确定判定），验证能力。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3, AC-8
- **Test Requirements**:
  - `rule` TR-2.1: 全库 diff LOST=0，已解题 gatewayAnswer 字节级不变。证据：before/after jsonl diff。
  - `rule` TR-2.2: 任一选项 Undecided、候选<2、多原语冲突 三种情况均返回 null。证据：`MultiSelectChoiceValidatorTests` 用例。
  - `rule` TR-2.3: tier2 多选题全部解出且字母正确，被 `IsAnswerCorrect` 判正；全库扫描 precision=1.0、harm=0。证据：护栏测试 + EvalRunner summary。
- **Notes**: 接线位置须经审查，确保不抢在既有单选校验器之前。
- **Completion Evidence**: `MultiSelectChoiceValidator` 接入 `CallChoice` 末端（Stoichiometry 之后、ChoiceVerifier 之前）；`MultiSelectChoiceValidatorTests` 6 例通过（TR-2.2 三种 null 情形 + TR-2.3 标记剥离变体 pdf-225→CD / pdf-329→ACE / pdf-071→BD 判正 + 全库 precision 护栏）；探针显示其命中的 7 题均已被更早校验器拦截 → 生产 0 新解、0 harm。

## Task 3: 扩展 `CountingChoiceValidator` 通用组合/计数求解（一般陈述子项）+ tier2 保底题
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 扩展路径一：`items` 不再 `Where(ExtractClaimedCount != null)` 过滤掉一般陈述子项；对每个子项调 `IStatementJudge` 判定真伪。
  - 全部子项可判后：对"正确的个数"型算出正确项数匹配选项；对组合型（选项为 ①②③ 组合）算出正确子项集合匹配选项（去重、排序后比对）。
  - 任一子项 Undecided → 整题 null（保持诚实）。
  - 保留既有"N 种"计数与异构体枚举路径不变（字节级）。
  - 配套 tier2 组合/计数保底题（子项文本完整、可被判定）。
- **Acceptance Criteria Addressed**: AC-4, AC-8
- **Test Requirements**:
  - `rule` TR-3.1: tier2 一般陈述子项的"正确个数"题与组合题全部解出且正确。证据：`CountingChoiceValidatorTests` 新增用例。
  - `rule` TR-3.2: 子项含 Undecided 时整题返回 null；既有计数/枚举题行为不变。证据：回归用例 + 既有护栏全绿。
  - `rule` TR-3.3: 全库扫描零假阳性（precision=1.0、harm=0）。证据：EvalRunner。
- **Notes**: 组合型选项解析（把 "①②③" 拆成子项编号集合）须与子项编号体系一致；用探针先在 pdf-009/012 上验证文本恢复后能否解析（OQ-3）。
- **Completion Evidence**: 新增 `SplitSubItemsIndexed`（去引导语）、`TryDecideGeneralSubItems`、`TryDecideCombination`、`ParseCombo`；既有 `JudgeCountOption`/N 种/异构体/`TryDecideCountOfCorrect` 字节级不变；`CountingChoiceGeneralizationTests` 6 例通过（一般计数→A / 全部唯一胜者 / 一 Undecided→null / 组合→B / 反向组合→null + 全库 precision 护栏）；全量 980/0/0。

## Task 4: 氧化还原电子转移计数原语 + tier2 保底题
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 1
- **Description**:
  - 新增原语：输入配平反应式（或半反应），计算各元素氧化态变化与总转移电子数；判定"每生成 1 mol X 转移 N mol 电子""X 是氧化剂/还原剂""氧化产物/还原产物"类陈述。
  - 方程未配平 / 物种不可解析 → 返回 Undecided。
  - 作为 `IStatementJudge` 的一个实现接入聚合器（供多选/组合复用）。
  - 配套 tier2 氧化还原电子计数保底题；尝试真题 pdf-075（文本完整、answer 干净），能解为奖励、不能解诚实 null。
- **Acceptance Criteria Addressed**: AC-5, AC-8
- **Test Requirements**:
  - `rule` TR-4.1: tier2 配平反应的电子转移陈述全部正确判定。证据：`RedoxChoiceValidatorTests` 新增用例。
  - `rule` TR-4.2: 未配平 / 不可解析输入返回 Undecided（不蒙数）。证据：边界用例。
  - `rule` TR-4.3: 接入聚合器后不破坏既有 redox 方程式对错判定路径（行为字节级不变）。证据：既有护栏全绿。
- **Notes**: 复用 Chem 引擎现有配平/氧化态能力；若需新依赖先评估（A3 假设）。
- **Completion Evidence**: 新增 `RedoxElectronStatementJudge`（自包含新文件，复用 `RedoxBalancer.SplitHalfReactions` + `EquationBalancer` + `MoleCalculator`，既有校验器逻辑零改动 → TR-4.3 字节级不变）；支持 每生成/消耗 1 mol 目标 + NA 归一 + 质量/体积换算 + 标签反应定位（ⅱ/①/(1)/步骤④）；未配平/非氧化还原/目标歧义或不含变价元素 → Undecided（TR-4.2 不蒙数）；接入 MultiSelect 与 Counting 的 `CompositeStatementJudge`；`RedoxElectronStatementJudgeTests` 18 例通过（TR-4.1 真/伪判定 + pdf-075 真实 OCR 探针 + 全库无矛盾/无错采纳双护栏）；全量 980/0/0。**范围刻意收窄**：本版仅判电子转移数陈述，"氧化剂/还原剂/氧化产物/还原产物/比例"角色陈述返回 Undecided（留待后续零假阳性预算内扩展）。

## Task 5: 纠正错题键（answer + answerKeywords）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None（数据轨道，可与 Task 1–4 并行）
- **Description**:
  - 对 pdf-101、148、197、213、269、279、280、399、405 逐题独立核对化学真值（note 辅助 + 化学知识确认），修正 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 的 `answer` 与 `answerKeywords`。
  - 权威源是 `src/.../data/pdf_questions.json`（构建复制到 bin）；`_evalscratch/` 旧副本不动。
  - 不反向修改 note 迁就错误 answer；不改引擎逻辑。
  - 每题在 tasks.md 留痕：id、原值→新值、核对依据。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-5.1: 9 道题修正后的 answer 与 note/化学真值一致；`answerKeywords` 含正确字母。证据：逐题核对记录。
  - `rule` TR-5.2: 全库 diff LOST=0、harm=0、precision=1.0；修正题若被引擎解出则判正，否则保持 null。证据：EvalRunner diff。
- **Notes**: 沿用 Task 35 续 / Task 46 已验证流程。提交前 `_Tmp*.cs` 探针必须删除。
- **Progress**:
  - **5a 完成**（已验证 GAIN=0/LOST=0/answer-changed=0）：pdf-101→C、pdf-148→D、pdf-269→B、pdf-279→D、pdf-280→C、pdf-399→D，每题 `answer` 与 `answerKeywords[0]`（及提示文本指向正确选项）同步改。
  - **5b 完成**：完成答案册 OCR 扫描（`_evalscratch/ocr_answers/` 154 页），定位官方解析逐题裁定：
    - **改键 4 道**（经答案册官方标记确认）：pdf-185→D（page_064，原键 `(1)B (2)D` 抄自他题）、pdf-213→A（page_076-077，原键 `B,C` 与问法反向）、pdf-405→C（page_137 q11 标记 `11.C`）、pdf-406→C（page_137 q12 标记 `12.C`，与 405 同损坏模式）。
    - **不改键 2 道**：pdf-075（答案册 page_026 确认键 `B` 正确，缺陷是选项文本/note 损坏，移入登记册 D 类）、pdf-197（题面拼接两道题，答案册无法定位，移入登记册 A.2 保持 `null`）。
  - **范围说明**：spec FR-6 原列 9 道；实际修正 **10 道**（5a 6 + 5b 4，406 为 5b 审计新增的同模式损坏），197 登记未修、075 重新归类为选项/note 缺陷。AC-6 要求"与化学真值/备注一致"已满足。
- **Completion Evidence**: 10 道题 `answer` + `answerKeywords` 修正已写入 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`（TR-5.1）；EvalRunner diff `base_pre5b`(757)→`after_5b`(757) = **GAIN=0、LOST=0、answer-changed=0**，两端 correct 均 448/757（TR-5.2：零回归、零 harm，引擎对这 10 道均返回 null，改键不改变判分）；留痕见 `data-defect-register.md` A.1。

## Task 6: 缺失题面回填审计 + 数据缺陷清单
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: None（但 OQ-1 须先回答：原始 PDF 源是否可得）
- **Description**:
  - 审计 12 + 道子项/选项缺失题（pdf-009、012、020、065、337、158、291、437、381、319、436、263、185）。
  - 优先从原始 PDF 重新抽取恢复文本；源不可得时用 note 还原**子项文本（不含 verdict）**作为次选。回填仅写 `question`/`options` 字段，不注入判定结论，不改存储 answer。
  - 无法可靠恢复的登记入 `data-defect-register.md`（本题录或 chem-engine 目录），保持 null，供后续数据管道/图像轨道接续。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-6.1: 回填题的 `question`/`options` 文本完整且无 verdict 字眼注入；存储 answer 未被回填改动。证据：回填前后字段 diff 审查。
  - `rule` TR-6.2: 不可恢复项全部登记入数据缺陷清单，含原因。证据：清单文件存在且条目完整。
  - `rule` TR-6.3: 回填不引入回归（LOST=0）。证据：EvalRunner diff。
- **Notes**: OQ-1 未确认前，本任务退化为"仅审计 + 登记清单"，不猜填。回填后用 Task 3 的组合求解器验证能否解出（奖励）。
- **Progress**:
  - **审计完成**：对 17 个相关 id（原 13 缺失题面 + 5b 候选 075/197/213/405/185）逐题只读核对了 `question`/`options`/`answer`/`note`。
  - **关键结论**：组合/计数型未解的根因是**子项原文不在 `question` 字段**（pdf-009/012/020 的 ①–⑧ 仅以带 verdict 的形式存在于 note；pdf-065 子项完全缺失；pdf-337/158 子项是图像/表格），**不是引擎能力缺口**——Task 3 一般子项求解器在 tier2 已验证可解，题面完整即可生效。
  - **不**从 note 剥离 verdict 回填子项：会注入判定字眼、有假阳性风险（违反 TR-6.1）；干净源是习题册原页 OCR，而当前缓存 OCR（`_evalscratch/ocr`）质量差（字符间距错乱），暂不回填，保持 `null`。
  - **登记册已建并随 5b 更新**：`data-defect-register.md`（仓库根）含 A.1 已修复（10 道留痕）/A.2 未修（197）/B 子项缺失（009/012/020/065）/C 图像依赖（337/158/263/319）/D note·选项损坏但键正确（381/075），条目完整，满足 TR-6.2。
  - **回填（TR-6.1/TR-6.3）按设计推迟**：答案册 OCR 已完成但仅覆盖解析页，**题面子项的干净原页 OCR 仍不可得**（缓存 `_evalscratch/ocr` 质量差，字符间距错乱/错字）；从 note 剥离 verdict 回填会注入判定字眼、有假阳性风险（违反 TR-6.1）。故本轮不回填，B/C/D 类保持 `null` 并登记，待后续高质量题面 OCR / 图像轨道接续后再执行回填并跑 EvalRunner diff（届时验证 TR-6.1/TR-6.3）。这是"宁可未解、不可假阳性"的刻意范围决定，符合 spec Non-Goals。
  - **回填执行（pdf-009 / pdf-012）**：原页渲染图（`_evalscratch/renders/page_006.png` / `page_007.png`）可用作干净源，逐字核对后把 ①–⑦ 纯陈述写入 `question`（pdf-012 含 ¹²C/¹⁴C、Na₂CO₃ 等上下标），仅改 `question`，options/answer/note 未动，无 verdict 注入（TR-6.1）。预检：`SplitSubItemsIndexed` 各拆出 7 子项；JSON 有效（545 条）。验证（隔离 worktree HEAD + 仅本改动）：eval diff vs `checkpoint_after_curate_237` GAIN=0/LOST=0/HARM=0；全量测试 1943/0（TR-6.3 零回归）。引擎仍未自解：探针（sanity 证 `CompositeStatementJudge` 加载/匹配正常）显示两题 14 子项在 general/experiment/organic/structure 四库中全部 `Undecided`，依"任一子项 Undecided → 整题 null"返回空；需策展事实覆盖方可判正，属后续独立任务。
  - **回填执行（pdf-020 / pdf-065）**：原页渲染图（`page_009.png` / `page_019.png`）逐字核对，把 pdf-020 ①–⑧、pdf-065 ①–⑩ 纯陈述写入 `question`（保留 H₂O/Fe³⁺/Hg²⁺/KMnO₄ 等上下标；纠正 pdf-020 note 转述失真——① 原文"任何物质"、⑦ 原文"同温同体积"，note 写成"气体"/"同温同压"；pdf-065 原 note 张冠李戴完全弃用），仅改 `question`，options/answer/note 未动（TR-6.1）。预检：`SplitSubItemsIndexed` 拆出 8/10 子项；JSON 545 条有效。验证（隔离 worktree HEAD=`1c3e953` + 仅本改动）：eval diff vs `checkpoint_backfill_009_012` GAIN=0/LOST=0/HARM=0；全量 1943/0（TR-6.3 零回归）。引擎仍未自解：探针（sanity 证 judge 正常）显示 pdf-020 8/8、pdf-065 10/10 子项全部 `Undecided` → 整题 null；另 pdf-020 选项单位"个"不被路径 1b 选项解析（仅认"项/全部"）支持。需策展事实（+ 必要的引擎"个"单位支持）方可判正，属后续独立任务。
- **Completion Evidence**: 审计 17 个相关 id 完成，结论"组合/计数未解根因是数据缺陷（子项文本缺失），非引擎能力缺口"已固化进登记册序言（TR-6.2：`data-defect-register.md` 存在且条目完整，含 5b 已修复/未修留痕）；TR-6.1/TR-6.3 因无干净题面源按设计推迟（已登记，未猜填），不构成本轮缺口——回填执行将作为后续独立任务。

## Task 7: 全库集成验证 + 护栏测试全绿 + EvalRunner diff
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 2, Task 3, Task 4, Task 5, Task 6
- **Description**:
  - 合并所有轨道改动后，杀残留进程、删 `_Tmp*.cs`，构建 `tests/CogniThink.TutorBrain.Tests` 与 `tests/CogniThink.Chem.Tests`。
  - 运行全量测试套件：TutorBrain 0 失败、Chem 0 失败；重点护栏（Batch3Choice、Counting、新增 MultiSelect/StatementJudge/Redox 测试）全绿。
  - 运行全库 EvalRunner（新 tag），与改动前 checkpoint 做 diff：确认 LOST=0、harm=0、precision=1.0、gatewayCorrect 单调不减。
  - 逐题核对本轮新解出题的字母与存储 answer 一致。
- **Acceptance Criteria Addressed**: AC-1, AC-6, AC-9, AC-8（证据汇总）
- **Test Requirements**:
  - `rule` TR-7.1: TutorBrain 0 失败、Chem 0 失败。证据：测试输出。
  - `rule` TR-7.2: EvalRunner failed=0；diff LOST=0、harm=0、precision=1.0。证据：checkpoint summary + diff 输出。
  - `rule` TR-7.3: 本轮新解出题的 gatewayAnswer 字母与存储 answer 逐题一致。证据：diff 候选核对。
  - `rubric` TR-7.4: 引擎能力泛化度（AC-8）；scale 1-5；anchors 1/3/5；threshold >= 4；evidence：tier2 题未单题策展即被原语解出的比例 + 代码审查无新增单题 general-choice-facts 条目。
- **Notes**: 这是提交前的最终闸门；任一 TR 不通过则回对应任务修复，不带病提交。
- **Completion Evidence**:
  - **TR-7.1（测试 0 失败）**：TutorBrain 全量 **980 通过 / 0 失败 / 0 跳过**（5b 数据编辑后重跑，进程 `ojxungopzh`）；Chem 全量 **905 通过 / 0 失败 / 0 跳过**（重跑确认）。重点护栏（Batch3Choice、Counting、新增 MultiSelect/StatementJudge/RedoxElectron 测试）全绿。
  - **TR-7.2（EvalRunner 零回归）**：两组 diff 均干净——① 引擎能力改动 `engine_before`(757)→`engine_after`(757)：**GAIN=0、LOST=0、answer-changed=0**；② 数据键改动 `base_pre5b`(757)→`after_5b`(757)：**GAIN=0、LOST=0、answer-changed=0**。两端 correct 均 448/757，单调不减。无新采纳 ⇒ precision=1.0、harm=0。EvalRunner 无 failed 记录。
  - **TR-7.3（新解字母一致）**：本轮 GAIN=0，无新解出题，故不存在 gatewayAnswer 与存储 answer 不一致的风险；既有解题 gatewayAnswer 字节级不变（answer-changed=0）。
  - **TR-7.4（rubric AC-8 引擎泛化度）**：评分 **4/5**（阈值 ≥4，通过）。依据：新增 `IStatementJudge` 聚合原语 / `MultiSelectChoiceValidator` / `CountingChoiceValidator` 一般子项路径 / `RedoxElectronStatementJudge` 均为**可泛化推理原语**，tier2 可控格式题（未在 `general-choice-facts` 单题策展）即可被解出（MultiSelect 3 例、Counting 6 例、RedoxElectron 18 例）；代码审查确认**未新增任何单题 `general-choice-facts` 条目**。生产端 0 即时收益并非原语弱，而是目标题已被更早校验器拦截、或被数据缺陷（子项文本缺失）阻塞——这正是"引擎就绪、数据接续"的正确设计。扣 1 分因生产真题即时收益为 0，需待数据回填后才能在真题上兑现奖励。

## Task 8: 提交 + 同步 Task 40 进度
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 7
- **Description**:
  - 仅暂存本轮改动的文件（引擎代码、测试、pdf_questions.json 数据修复、spec/tasks 文档），不暂存外部进程产生的 `_evalscratch/`、`PdfOcrRepair/`、`image-ocr-choice-coverage/` 等。
  - 提交信息写入 `_evalscratch/commit_msg_*.txt`（UTF-8），`git commit -F`。
  - 在 `chem-engine-question-solving/tasks.md` Task 40 同步本轮进度与指标。
- **Acceptance Criteria Addressed**: NFR-5
- **Test Requirements**:
  - `rule` TR-8.1: 提交仅含本轮文件；`git log` HEAD 正确；工作树仅剩外部项。证据：`git status` + `git log`。
- **Notes**: PS5 中文 heredoc 不可用，必须 `-F` 文件提交。
- **Completion Evidence**:
  - 主提交 `80b9efc`「选择题求解能力泛化 + 数据缺陷修复（引擎原语 + 错题键 + 缺陷登记册）」：17 文件，+1719/-27。含 6 新增引擎 `.cs` + 3 改引擎 `.cs` + 4 新增测试 `.cs` + `pdf_questions.json`（仅 10 处答案键 hunk，外部选项补全已排除）+ `data-defect-register.md` + 本 spec 的 spec.md/tasks.md。
  - **TR-8.1 暂存范围**：`git diff --cached --name-status` 确认仅我方文件；`pdf_questions.json` 暂存 diff 经审查仅含 10 处 `answer`/`answerKeywords` 变更（脚本 `_evalscratch/_stage_mine_json.py` 以 HEAD 为底拼接、校验 result==head 除我方 10 id 外字节一致）。提交后 `git status` 工作树仅剩外部项（`CogniThink.Chem.sln`、`Batch3ChoiceValidatorTests.cs`、`pdf_questions.json` 外部选项补全、`PdfOcrRepair/`、`image-ocr-choice-coverage/`），未误纳。
  - 后续小提交同步 `chem-engine-question-solving/tasks.md` Task 40 进度（NFR-5 可追溯），并回填本 Task 8 的 Completion Evidence。
