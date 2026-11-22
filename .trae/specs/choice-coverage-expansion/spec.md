# 需求规格：选择题事实覆盖扩展（41 题最大头）

**功能域：** 高中化学题库 / 确定性化学引擎 — 选择题事实策展与覆盖扩展
**Spec 唯一标识：** `choice-coverage-expansion`
**状态：** 已完成（Review R1 pass，2026-11-21）

---

## 1. 问题与目标

### 1.1 背景
上一批次（[multi-part-compound-answer-fix](file:///D:/github/CogniThink-Chem/.trae/specs/multi-part-compound-answer-fix/spec.md)）对 104 道多段未解逐题审计后，剩余 103 题中 **(b) 能力缺口 95 题**，其中最大桶为 **choice 知识/核验覆盖缺口 41 题**（证据：[_evalscratch/multi_remaining_audit.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/multi_remaining_audit.tsv)）。

### 1.2 现状取证（已落盘，可复核）
- 当前绿色基线：`checkpoint_pdf174fix`（gatewayCorrect=**618**，precision=1，harm=0，bothWrong=139）。
- 选择题基线：评估库中 `taskType=choice` 共 **504 题**，已判正 **457 题**，未解 **47 题**；其中 41 题为「能力缺口」（本 spec 目标），6 题为数据缺陷（pdf-162/522 等，不在本 spec 范围）。
- 41 题逐题分析已落盘：[_evalscratch/choice41_analysis.md](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_analysis.md)。关键事实：**41 题全部 `taskType=choice`、`type=choice`、`usedEngine=False`、`gatewayAnswer=null`** —— 路由正常（已被 `ClassifyTaskDeterministic` 判为 choice 并进入 `CallChoice`），但**没有任何选择校验器产出可信结论**。
- 41 题题型一致：均为「下列说法正确/错误的是」多陈述判断选择题，每选项是一条独立化学陈述需判真伪。

### 1.3 根因（机制级）
[FactChoiceMatcher.TryDecide](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs#L169-L246) 的硬红线：**每个选项必须恰好命中一条事实**（`match` 全部出现在选项 + `stemMatch` 全部出现在题干），任一选项 0 条或 ≥2 条命中 → 整题返回 null，不蒙数。41 题在对应事实库中无匹配策展事实 → 全部选项 0 命中 → 返回 null → `usedEngine=False`。

### 1.4 目标
- 对 41 题先做**可行性分诊**（α 可安全策展 / β 需新求解器 / γ 数据或图像缺陷不在范围）。
- 对 **α 子集**按既有安全策展协议（事实 JSON + `stemMatch` 全库唯一锁 + 护栏 Truth 表 + 全量测试 + eval diff）逐批策展，**每批 GAIN≥1 且 LOST=0 / HARM=0 / precision=1**。
- 对 **β / γ** 如实登记到 [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md)，不伪装为「记号问题」。

### 1.5 非目标（明确不做）
- **不**为 β 类新增/扩写选择校验器逻辑（那是独立 solver spec，需各自风险评估）。
- **不**修复 γ 类数据缺陷（题干截断 / 图像依赖 / answer 冲突）—— 那是数据重抽取 spec，需官方源。
- **不**放宽 `FactChoiceMatcher` 的「每选项恰好 1 条」红线或 `WrongDir` 问法方向判定。
- **不**硬编码任何化学事实到 `.cs` 逻辑；事实只进 JSON。
- **不**在无官方源核对的情况下修改题库 answer 键。

---

## 2. 功能需求

### FR-1 可行性分诊（前置，必做）
对 41 题逐题判定类别，落盘 `_evalscratch/choice41_audit.tsv`，字段：`id / askDir(correct|wrong) / optionCount / αβγ / reason / 目标校验器`。
判据：
- **α（可安全策展）**：① 所有选项文本完整、纯文本可匹配（无仅图像能定的关键信息）；② 存在全库唯一的 `stemMatch` 锚点（题干中独有的短语组合，锁定到本题，防跨题命中）；③ 每个选项的 verdict 可由**官方 answer 键唯一反推**并经化学核对一致；④ 无未解决的 answer 冲突。
- **β（需新求解器）**：选项真伪依赖多步计算 / 晶胞计数 / 从图像读取数值 / 结构推理，无法归约为一条可匹配的事实陈述。
- **γ（不在本 spec 范围）**：题干或选项文本截断缺失、关键信息仅在图像中、answer 与化学真值冲突且无官方源核对、数据损坏（note 自相矛盾等）。

### FR-2 策展协议（α 类，分批）
每个 α 题按既有协议（见 [Batch3ChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs) 中 ConceptTruth 各批次注释与 [choice-curated-facts.json](file:///D:/github/CogniThink-Chem/data/highschool/choice-curated-facts.json) 先例）策展：
1. 为每个选项写一条事实：`id`、`match`（选项中必现子串列表，全部命中才算匹配）、`stemMatch`（题干独有锚点列表，非空且全库唯一）、`verdict`（bool）、`reason`（化学推理，供教学展示）。
2. 事实写入**对应校验器的隔离事实库**：Concept 路径 → `choice-curated-facts.json`（红线 #6 隔离，不污染通用库）；其它域（有机/结构/实验）→ 对应 `*-facts.json`，由分诊确认目标校验器。
3. 将该题 ID 与期望答案加入对应护栏 Truth 字典（Concept → `Batch3ChoiceValidatorTests.ConceptTruth`；其它 → 对应 `*ValidatorTests` 的 Truth 表）。
4. **整题契约**：`TryDecide` 对该题每选项恰好命中 1 条事实、候选唯一（单选）或 ≥2（双选/多选且题干标注），返回字母与官方 answer 一致。

### FR-3 分批与回归兜底
α 题按**域分批**（如：有机命名/用语一批、氧化还原/电化学一批、实验/安全一批、结构/周期一批、平衡/电离一批），每批独立闭环：
- 策展事实 → 更新护栏 Truth → `dotnet test` 全绿 → 打 eval tag → `diff_eval` 相对上一绿色 tag 验证 **GAIN = 本批策展数、LOST=0、HARM=0** → 若任一红线破则**回退本批**。
- 批大小建议 ≤ 10 题，便于定位回归。

### FR-4 β / γ 登记
对分诊为 β / γ 的题，在 [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md) 新增/更新条目，给出**真实理由**（能力缺口类型 / 数据缺陷类型），并在本 spec 报告中列出清单。

### FR-5 既往舍弃题的重核
`Batch3ChoiceValidatorTests` 注释已记录曾舍弃的相关题（pdf-075「存储 answer 与化学真值冲突，待官方题源核对」、pdf-386「note 自相矛盾、数据损坏」）。分诊时**必须重核**：若有官方源可消除冲突/数据已修复则可升 α，否则维持 γ（登记理由），不得盲目重策展。

---

## 3. 非功能需求

### NFR-1 零假阳性（precision=1）
全库任何**未列入**护栏 Truth 表的题，不得因本批新增事实产生非 null 结论。由 `Batch3ChoiceValidatorTests`（及相关 validator 测试）全量扫描 `BuildTestCases()` 兜底。

### NFR-2 零伤害（harm=0）
凡被采纳门采纳的结论（非 null 且通过方向/选项字母判定），必须被 `IsAnswerCorrect` 判为正确。由 eval `harmCount` + 测试护栏兜底。

### NFR-3 零回退（LOST=0）
相对绿色基线 `checkpoint_pdf174fix`（618），每批 eval diff 的 LOST 必须为 0。已判正的题不得变错。

### NFR-4 `stemMatch` 隔离
每条新增事实的 `stemMatch` 非空，且其归一化后的子串组合在**全库题干**中仅出现在目标题（脚本可证唯一性）。`match` 不得过于宽泛导致跨选项/跨题串命中。

### NFR-5 真值可追溯
每条 `verdict` 必须可由官方 answer 键反推并经独立化学核对；若 answer 与化学真值冲突，**先**走官方源核对修正 answer（登记 A 类），**再**策展，不得用冲突 answer 反推 verdict。

### NFR-6 无逻辑硬编码
化学事实只进 `data/highschool/*.json`；`.cs` 仅在确有可证安全且经测试覆盖的情况下才改动（本 spec 默认不动校验器逻辑）。

---

## 4. 约束与依赖

### 4.1 约束（冻结红线，每批必过）
- precision=1、harmCount=0、LOST=0（NFR-1/2/3）。
- 题干/answer 改动必须附官方源溯源（NFR-5）。
- 化学事实只在 JSON，不硬编码（NFR-6）。
- 不放宽采纳门 / 不放宽 `TryDecide` 每选项 1 条红线 / 不放宽 `WrongDir`。

### 4.2 依赖
- 绿色基线 tag：`checkpoint_pdf174fix`（618）。每批新 tag 如 `choice_b1`、`choice_b2`…
- 评估工具：`_evalscratch/run_eval_resume.ps1 -Tag <tag> -Env Production`、`_evalscratch/diff_eval.ps1 -OldTag <prev> -NewTag <tag>`。
- 全量测试：`dotnet test CogniThink.Chem.sln -c Release --no-restore`。
- 护栏测试：[Batch3ChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs) 及其它 `*ChoiceValidatorTests`。
- 事实库加载：[FactChoiceMatcher](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs)（`choice-curated-facts` 仅在 Concept 路径合并加载，红线 #6 隔离）。
- 判分：[IsAnswerCorrect](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L308-L370) + 采纳门 [BuildGatewayAnswerAsync](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L626-L696)。

### 4.3 假设
- 41 题的 `answerKeywords` 即官方答案键（与题库 `answer` 一致），可作为 verdict 反推真值源；冲突题以官方源为准（NFR-5）。
- `choice-curated-facts.json` 是 Concept 路径单题策展的安全隔离位置（已有 GC-742 等先例）。
- 分诊可在不修改任何代码的前提下完成（仅读题库 + 分析）。

### 4.4 开放问题
- **OQ-1（已由 Task 1 分诊落实）**：α 子集的最终规模需分诊后确定（原估 15–30 题，取决于多少题存在全库唯一 stemMatch 且选项文本完整）。**分诊实测 α=5 / β=16 / γ=20**（证据：[_evalscratch/choice41_audit.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_audit.tsv)）。α 远低于预估的主因：41 题中大量存在 answer 与化学真值/备注冲突（20 题归 γ），或多字母答案但题干无「双选/多选」标注（单选契约候选不唯一）、选项文本被图像截断。本 spec 承诺「策展所有安全可策展的 α」，不承诺固定数字——5 题即当前可安全策展的全集。
- **OQ-2（已由 Task 1 分诊落实）**：个别域（有机结构/晶胞）若分诊发现大量 α 但需扩 `OrganicChoiceValidator`/`StructurePeriodicValidator` 的 Truth 表，是否在本 spec 一并处理？**实测**：所有 5 个 α 均可落入既有 Concept/MultiSelect 事实链（`choice-curated-facts.json`），无需扩 Organic/Structure 校验器 Truth；晶胞/有机结构相关题因图像依赖或 answer 冲突全部归 β/γ，不在本 spec 策展。
- **OQ-3（已由 Task 1 分诊决议）**：双选/多选题（如 pdf-263 有 6 选项、pdf-115 答案 AC、pdf-475 答案 ACD）的策展需 `TryDecide` 多选分支（题干标注「双选/多选」或候选 ≥2）。**决议**：核实 `FactChoiceMatcher.JudgeStatement` 遍历全部 `ChoiceFactKeys`（含 `choice-curated-facts`），既有 `MultiSelectChoiceValidator`（瀑布链末端 fallback）**不依赖题干标注**且仅靠新增 JSON 事实即可驱动；其护栏 `FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm` 对全库任一非 null 结论强制判分正确。故「题干无标注但答案多字母」的题无需改 .cs、无需新求解器即可策展。据此 pdf-115（AC）、pdf-475（ACD）升入 α（α2）；其余多选无标记题因选项被图像截断或 answer 冲突仍归 β/γ。**范围决策（待用户确认）**：α2 策展（+2 题）虽不动 .cs、有护栏兜底，但是否纳入本 spec 批次需用户批准（原 FR-2 协议以 Concept 单选路径为主）。

---

## 5. 验收标准

### AC-1（rule）：41 题分诊落盘
- 可观测：`_evalscratch/choice41_audit.tsv` 存在，恰好 41 数据行，每行含 `id / askDir / optionCount / αβγ / reason / 目标校验器`；α+β+γ 计数 = 41。
- 证据：TSV 文件 + 程序化计数复核输出（α=? / β=? / γ=?）。

### AC-2（rule）：α 类策展后判正且零假阳
- 可观测：每个 α 题在护栏 Truth 表中且 `dotnet test` 全绿；`Batch3ChoiceValidatorTests`（及相关测试）无未列入 Truth 的题产生非 null；α 题 `TryDecide` 返回字母与官方 answer 一致。
- 证据：测试通过汇总（0 failed）+ 护栏测试无假阳断言。

### AC-3（rule）：eval diff 红线
- 可观测：相对 `checkpoint_pdf174fix`（618），最终 tag 的 diff 显示 **GAIN = α 策展数、LOST=0、HARM=0、precision=1**。
- 证据：`_evalscratch/diff_choice_final.txt` + 最终 summary（precision=1/harm=0）。

### AC-4（rule）：β / γ 如实登记
- 可观测：`data-defect-register.md` 为每个 β / γ 题新增/更新条目，理由为真实能力缺口或数据缺陷；无 β / γ 题被伪装为「记号问题」或遗漏。
- 证据：登记册 diff + 本 spec 报告中的 β/γ 清单与登记册条目一一对应。

### AC-5（rule）：策展事实合规
- 可观测：每条新增事实 `stemMatch` 非空；脚本验证其归一化子串组合在全库题干中唯一（无跨题命中）；`verdict` 与官方 answer 反推一致；`git diff` 无 `.cs` 化学逻辑硬编码（仅 JSON + 测试 Truth 表）。
- 证据：唯一性检查脚本输出 + facts JSON diff。

### AC-6（rubric）：分诊与策展的质量
- 维度：**风险控制与覆盖面的平衡**（0–2）。
  - 2：α 判定严格（每题 stemMatch 唯一性有脚本证据、verdict 有化学核对记录），不冒进；β/γ 理由具体可执行；每批有独立 eval 兜底且无回退。
  - 1：基本合规，但个别 α 题的 stemMatch 唯一性或 verdict 追溯记录不够完整，或批过大增加定位难度。
  - 0：存在未核真值即策展、stemMatch 可跨题、或用冲突 answer 反推等冒险行为。
- 及格阈值：≥ 2（本 spec 安全-critical，要求满分）。
- 证据：`_evalscratch/choice_curation_report.md` + 分诊/策展落盘文件。

---

## 6. 开放问题追踪

（见 §4.4，OQ-1/OQ-2/OQ-3 将在 Plan 阶段的分诊任务中落实为具体批次划分。）
