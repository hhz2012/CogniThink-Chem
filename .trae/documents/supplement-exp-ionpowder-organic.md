# 补充三类题型题库与引擎：实验探究 / 离子粉末推断 / 有机推断

## Context（为什么做）

用户要求覆盖五类：实验探究、离子粉末推断、化工流程、水溶液平衡、有机。现状核查：

| 用户类别 | 现状 |
|---|---|
| 化工流程 | ✅ `IndustrialProcess`（8 题 Hard，已上线） |
| 水溶液平衡 | ✅ `AqueousIonEquilibrium`（8 题 Hard，已上线） |
| 离子粉末推断 | ⚠️ 仅由泛化的 `SubstanceIdentification` 覆盖，题集以 Basic/Medium 为主，无 Hard 专项 |
| 实验探究 | ❌ 无类别、无引擎 |
| 有机 | ❌ 无类别、无引擎（上一计划明确搁置） |

补充方案：新增 3 个类别各 8 道 Hard 题（共 24 题，题库 74→98），最大化复用现有引擎与知识库，接线全链路（路由/校验/讲解/引导/CLI/真题/前端）。铁律不变：结论溯源知识库/规则条目，推不出如实 `Underdetermined`/`NoSolution`，禁止猜测；数值 Rational 精确。

## 已核实的可复用底座

- **离子/粉末推断不需要新引擎**：[IdentificationEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/IdentificationEngine.cs) 混合成分模式（L199-L386）已支持：粉末/溶液混合物、组分间相互反应、过量试剂、沉淀提供者降级、必要性判定（commit 0e5a6be）、Must/MustNot/Maybe 三态与欠定上抛；[ReactionMatcher.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ReactionMatcher.cs) 有试剂归一化（"H2SO4(稀)"/"过量"）与离子池（L84-L117，离子推断基础）。substance-identification.json L45-L90 已有粉末 Hard 样例可参照。
- **有机推断底座**：[reactions-organic.json](file:///d:/github/CogniThink-Chem/data/knowledge/reactions/reactions-organic.json) 约 49 条 RX-OR-* 条目（甲烷取代/乙烯加成水化/乙醇氧化/酯化/葡萄糖银镜/苯硝化等）；[ReactionLibrary.Load](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Reactions/ReactionLibrary.cs#L64-L80) 自动加载 `reactions/*.json`，新增/补条目零加载代码改动；[ProcessInferenceEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessInferenceEngine.cs) 字母标签转化链回溯赋值（L73-L190）+ [phenomena.json](file:///d:/github/CogniThink-Chem/data/highschool/phenomena.json) 现象→结论约束（L243-L260）可直接复用；[substances.json](file:///d:/github/CogniThink-Chem/data/highschool/substances.json) 已有有机物白名单。
- **实验探究底座**：phenomena.json 现象条目带 knowledgeId/source；rules.json 已有 PROC-004 操作目的条目形态可仿写 EXP-* 规则。
- **接线范式**：与上一计划（hard-exam-engine-upgrade.md）阶段2-7 完全相同——ProblemModels 枚举/AskType → ProblemSolver case → BenchmarkLoader.CategoryFiles（[BenchmarkLoader.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Benchmark/BenchmarkLoader.cs#L16-L28)）+ schema 自检 → BenchmarkVerifier 结论分支 → Explainer.CategoryName/SummarizeAnswer + GuideGenerator.BuildLevel3（每题四级引导非空）→ ProblemSolverTests 计数（当前 74/69/未解5）→ Program.cs RunVerifyReal → web/app.js answerHtml 渲染分支（tab 数据驱动自动出现）→ --export-web → 浏览器抽查。

## 实施步骤

### 阶段 1：离子粉末推断（新类别 `IonPowderIdentification`，复用 IdentificationEngine）
- ProblemModels：枚举加 `IonPowderIdentification`，AskType 加 `IdentifyIonPowder`；given/expected 完全复用现有 `candidateSpecies + steps + composition` schema（零新字段）。
- ProblemSolver：case 直接转调 `SolveIdentification`（[ProblemSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Solving/ProblemSolver.cs#L38-L39)）。
- BenchmarkLoader/Verifier/Explainer（类别名"离子粉末推断"）/GuideGenerator 按范式加 case。
- 题库 `data/benchmark/problems/hard-ion-powder.json`（IONP-H01..H08）：
  - 4 题溶液连续操作离子推断（加酸产气/加 BaCl2 产不溶酸沉淀/加 NaOH 产使湿润红色石蕊变蓝气体/焰色等 → 对 K+、NH4+、Fe3+、Al3+、Ba2+、SO4^2-、CO3^2-、Cl-、NO3- 判 must/mustNot/maybe）；
  - 4 题固体粉末混合物（加水看颜色/溶解性/沉淀，再加酸看气体，含组分间互反应，如 CuSO4、Na2CO3、BaCl2、NaCl、CaCO3 体系）；
  - 全部按引擎实证设计：先探针跑通再回填 expected；"不能确定（Maybe）"作为结论组成部分不算边界题；最多 1 道 tags 含"边界"（引擎如实 Underdetermined）。
- 测试：ProblemSolverTests 计数 74→82；新建 `IonPowderEngineTests.cs`（或并入现有 Identification 测试风格，断言三态结论与可溯源步骤）。

### 阶段 2：有机推断（新类别 `OrganicInference` + 新引擎 `OrganicInferenceEngine`）
- 知识库补全：核查 RX-OR 链覆盖，补齐高频转化所需条目（乙烯→乙醇→乙醛→乙酸→乙酸乙酯全链、乙醇催化氧化、乙醛氧化/银镜、葡萄糖发酵产乙醇、苯硝化、酯水解等，人教版必修二/选择性必修3 出处）；只补教材内反应。
- 新引擎 `src/CogniThink.Chem.Core/Inference/OrganicInferenceEngine.cs`：Request/Result 同文件；核心复用 ProcessInferenceEngine 的回溯赋值与现象约束，面向有机特化——候选限定有机物+特征反应（RX-OR-*）匹配、Evidence/KnowledgeId 溯源；聚合物写法 `(C2H4)n` 若 Species/校验管线不支持则题面回避或如实边界。
- 题库 `hard-organic.json`（ORG-H01..H08）：转化链推断（A→B→C→D + 特征现象定物质）+ 写各步方程式（分子式风格 L2 写法）；**同分异构计数、由分子式+不饱和度反推结构**这类机械不可推的题不建 Solved 期望，最多 1 道边界题。
- 接线同范式（类别名"有机推断"）；ProblemSolverTests 82→90；新建 `OrganicInferenceEngineTests.cs`（完整推理链断言：≥4 步、每步 Description 非空、KnowledgeId 可溯源）。

### 阶段 3：实验探究（新类别 `ExperimentalInquiry` + 新引擎 `ExperimentEngine`）
- 新规则 `data/rules/rules.json` 加 EXP-001..00N（每条带 evidence+人教版出处）：气体验满与检验（O2/CO2/H2/NH3/Cl2）、气密性检查原理、焰色反应操作与结论、沉淀洗涤合格判据（取最后洗涤液检验）、分离方法选择判据（过滤/蒸发/蒸馏/萃取分液/重结晶适用条件）、滴定误差分析（仰视/俯视/润洗——凡可机械判定的方向结论才入库）。
- 新引擎 `ExperimentEngine.cs`：输入=操作/现象/待检对象规格，输出=结论（是何种气体/操作目的/现象解释/方法选择），逐条溯源 phenomena.json + EXP-* 规则；开放性表述题（方案设计、评价语言组织）不建 Solved。
- 题库 `hard-experiment.json`（EXP-H01..H08）：连续现象推断气体/离子组合检验方案、操作→目的、异常现象→原因；最多 1 道边界题。
- 接线同范式（类别名"实验探究"）；ProblemSolverTests 90→98；新建 `ExperimentEngineTests.cs`。

### 阶段 4：全链路收尾（真题/CLI/前端/提交）
- Program.cs RunVerifyReal 加 3 个 case；real-exam.json 追加 3-6 道可溯源真题（改编标注，先实证后回填）。
- web/app.js answerHtml 加 3 个渲染分支（离子粉末三态结论表、有机转化链+方程式、实验操作/现象/结论对照）；style.css 对应 cat-*；重跑 --export-web；浏览器子代理抽查 3 新 tab 与详情页。
- `dotnet test` 全绿后分逻辑提交（中文信息，不含 .trae/）并推送（push 需 dangerouslyDisableSandbox）。

## 题库设计约定（沿用上一计划）

difficulty 全 Hard；每类 ≥8 题（BenchmarkTests 不变式）；expected.equations 非空须配 ≥1 distractors（典型失误：漏"不能确定"、离子共存误判、有机物写错产物、检验试剂选错）；engineExpectation.mustPass 用现有检查名；边界题 isValid=true + 结论 Underdetermined。

## Verification

每阶段：`dotnet build CogniThink.Chem.sln --nologo -v minimal` → `dotnet test`（基线 627 全绿，允许增加禁止变红）→ 探针实证题库 expected 后删除探针。阶段4 后：`--verify-real`（FAIL 仅允许已知 CN⁻ 边界）、`--solve IONP-H01/ORG-H01/EXP-H01` 单题抽查（≥4 步带出处）、`--export-web` + 浏览器抽查 3 新分类。

## 风险与取舍

- **IdentificationEngine 可能解不出全部 8 题 IONP**：以探针结果定题面设计（候选集/操作序列落在其能力内），禁止为迁就引擎写错误化学；解不出的题调整数据而非硬扩引擎（引擎小修允许，须回归既有成分推断测试）。
- **有机方程式解析**：先探针验证 Species/EquationParser/守恒校验对分子式风格有机式（CH2=CH2、CH3COOH、C6H12O6 等）的通过情况，不通则优先修解析层（阶段2 内），仍不可靠的写法回避出镜。
- **实验探究天然开放**：只考"有唯一机械判据"的子题型（检验/目的/误差方向），方案评价类一律不做或边界。
- 预计测试基线 627 → 约 700+；题库 74 → 98 题。
