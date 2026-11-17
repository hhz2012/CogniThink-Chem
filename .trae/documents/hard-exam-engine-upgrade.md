# 高考最难题型题库 + 四类新推理引擎

## Context（为什么做）

现有引擎覆盖 5 类题型（产物推断、成分推断、过程推断、离子共存、定量计算），基准题库 42 题中仅 10 题 Hard，且全部集中在"离子反应/元素化合物"层面。高考化学区分度最高的四类题——**电化学（电极反应式）、化学平衡综合（三段式/Kp/移动方向）、水溶液离子平衡（三大守恒/粒子浓度比较）、工业流程综合推断（多步转化+操作目的）**——目前引擎完全没有能力，前端也没有对应分类。

目标：为这四类各建 8 道 Hard 题（共 32 题，满足 `BenchmarkTests.cs#L26-L29` 每类 ≥8 的不变式），并新增 4 个独立推理引擎，使每道题都能输出**完整可溯源的推理链**（`InferenceStep`：结论 + 证据方程式 + KnowledgeId + 教材出处），同时接入基准验证、`--verify-real` 真题验证与 `--export-web` 前端。

铁律不变：所有结论落到知识库条目/规则条目；推导不出来就如实上抛 `Underdetermined`/`NoSolution`，禁止硬解。

## 现状可复用设施（已核实）

- 半反应拆解：[RedoxBalancer.SplitHalfReactions](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Balancing/RedoxBalancer.cs#L89-L241) 已能产出 `OxidationHalf/ReductionHalf.Expression`（含 `n e^-`，见 L462），介质仅 acidic/basic。
- 精确有理数：[Rational](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Math/Rational.cs)（无开方/幂，二次方程须判别式为完全平方）、`RationalMatrix`（零空间法）、`EquationStoichiometry`、`MoleCalculator`。
- 规则库：[rules.json](file:///d:/github/CogniThink-Chem/data/rules/rules.json) 已有 EQ-001 勒夏特列、EQ-002、SOL-003 溶度积、SOL-004 沉淀转化、SOL-006 Kh=Kw/Ka、SOL-007 三大守恒、RED-004 电极电势、CONS-ELEC/CONS-CHARGE。
- 反应库：`reactions-principles.json` 有 RX-PL-ELC-001..005、RX-PL-THM-*，但**电极式与 ΔH 只写在 phenomenon 文本里**（如 `"负极：Zn - 2e^- = Zn^2+；正极：Cu^2+ + 2e^- = Cu"`）→ 需结构化为字段，不解析文本。
- 推理链模型：[InferenceStep](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/InferenceModels.cs#L23-L39) → [SolutionStep](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Solving/SolutionModels.cs#L52-L72) → [Explainer](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Explanation/Explainer.cs#L75-L119)。
- 化学式→离子唯一解：`IonizationMap.IonizesTo`；校验：`ChemicalCheckerService.Check(CheckRequest{Equation,Mode,Medium})`；白名单：`WhitelistGuard`。

## 关键阻塞点（必须先解决）

`Species.Parse("e^-")` 失败（元素表无 "e"），导致任何含电子的电极反应式过不了 `EquationParser` → `BenchmarkVerifier.VerifyExpectedEquations` 的"方程式解析"检查。

修复：[Species.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Chemistry/Species.cs#L146-L160) 内特例化电子（body `e` → `Elements` 空集、`Charge = -1`）；`data/highschool/ions.json` 增加 `"e^-"` 使白名单放行；`CheckRequest` 增加 `IsHalfReaction`（或 `EquationMode.HalfReaction`），半反应跳过离子拆写检查、守恒校验中把电子计为 -1 电荷项。

## 实施步骤

### 阶段 1：电子伪物种 + 校验管线（可独立提交）
改 `Species.cs`、`EquationParser`（如需要）、`CheckRequest`/`ChemicalCheckerService`、`ions.json`。
判据：`dotnet test` 仍 523/523 绿；新增单测断言 `Species.Parse("e^-").Charge == -1`、`Check("Zn - 2e^- = Zn^2+")` 通过元素/电荷守恒。

### 阶段 2：模型与路由骨架
[ProblemModels.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Benchmark/ProblemModels.cs#L8-L24)：
- `ProblemCategory` 追加 `Electrochemistry / ChemicalEquilibrium / AqueousIonEquilibrium / IndustrialProcess`；`AskType` 追加 `WriteElectrodeEquations / EquilibriumCalculation / CompareConcentrations / AnalyzeProcess`。
- `GivenSpec` 追加可空字段（对现有 5 类零影响）：`Cell{Type,Electrodes[],Electrolyte,Medium}`、`Equilibrium{Equation,Initial{},Disturbance{Kind,Value},Given{...}}`、`SolutionSpec{Solutes[],Concentration,VolumeLiters,MixWith[],Ph}`；`StepSpec` 追加 `Purpose`。
- `ExpectedSpec` 追加：`ElectrodeEquations{Negative,Positive,Anode,Cathode}`、`EquilibriumTable{Start[],Change[],End[]}`、`EquilibriumAnswer{Conversion,AmountFraction,Kp,Direction}`、`ConcentrationOrder[]`、`ConservationEquations[]`。
- [ProblemSolver.Solve](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Solving/ProblemSolver.cs#L25-L42) 加 4 个路由分支 + 4 个 `Solve*` 私有方法；`SolutionModels` 加对应输出字段。
- [BenchmarkLoader.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Benchmark/BenchmarkLoader.cs#L16-L24) 的 `CategoryFiles` 加 4 条映射（`hard-electrochemistry.json` / `hard-equilibrium.json` / `hard-aqueous-equilibrium.json` / `hard-process-flow.json`），L85-L170 的按类自校验加 4 个 case。
判据：新题库文件尚未创建时 `LoadAll` 不报错（缺文件时按现有实现处理，若会抛异常则先建空 items 文件）；523 测试绿。

### 阶段 3：电化学引擎 + 题库（8 题）
新文件 `Inference/ElectrochemEngine.cs`：`Infer(ElectrochemRequest, ReactionLibrary?)`。推理链：
1. 判装置类型（原电池→正/负极；电解池→阴/阳极）与介质（acidic/basic/熔融，熔融不支持则如实 `Underdetermined`）；
2. 总反应来源：`given.cell.overall` 或检索 `ReactionLibrary` 的 ELC 条目（新增结构化 `halfReactions{negative,positive}` 字段，见阶段 6 数据）；
3. `RedoxBalancer.SplitHalfReactions` 机器拆解氧化/还原半反应；
4. 半反应→电极映射（氧化=负极/阳极，还原=正极/阴极），依据 RED-004 电极电势规则核对活泼性；
5. 每条电极式过 `Check(Mode=HalfReaction)`：元素守恒、电荷守恒、电子项存在；
6. 电子守恒：两极式乘系数使 `e^-` 相等（`CONS-ELEC-001`），相加还原总反应并与来源条目比对。
每步一条 `InferenceStep`（Evidence=该步方程式，KnowledgeId=`RX-PL-ELC-*`/`RED-004`）。
题库 `hard-electrochemistry.json`（ELEC-H01..H08）：Cu-Zn 稀硫酸原电池、氢氧燃料电池（酸/碱介质）、铅蓄电池、LiFePO4 锂离子电池、电解 CuCl2/饱和食盐水/Al2O3 熔融（最后一题期望 `Underdetermined`，演示边界）、电镀与腐蚀。

### 阶段 4：化学平衡引擎 + 题库（8 题）
新文件 `Inference/EquilibriumEngine.cs`：`Infer(EquilibriumRequest)`。推理链：
1. `EquationBalancer`/`EquationStoichiometry` 解析可逆反应系数（来源 `RX-PL-EQ-*`）；
2. 建三段式（`Rational` 精确）：起始 → 转化 x → 平衡；
3. 列 Kc/Kp 表达式（分压 = 摩尔分数 × 总压），按已知量选择求解路径：给定转化率直接算、或给定 K 解一元二次（判别式为完全平方才求解，否则 `Underdetermined` 并说明"非精确有理解"）；
4. 扰动分析：调 `RuleStore` 的 EQ-001（浓度/压强/温度方向）与 EQ-002/EQ-003（K 只随温度、催化剂不改转化率），生成"判据→方向→新平衡量"三步；
5. 输出转化率、体积分数/分压、Kp、移动方向，均带公式与出处。
题库 `hard-equilibrium.json`（EQ-H01..H08）：合成氨、SO2 催化氧化、N2O4/NO2、恒压与恒容对比、等效平衡、Kp 计算、图像类只出 1 题且期望 `Underdetermined`。

### 阶段 5：水溶液离子平衡引擎 + 题库（8 题）
新文件 `Inference/AqueousEquilibriumEngine.cs`：`Infer(AqueousRequest)`。推理链：
1. `IonizationMap` + 新常数文件判强弱电解质、写出电离/水解方程（`RX-PL-ION-*`/`RX-PL-HYD-*`）；
2. 由溶质与体积/浓度做混合后初始浓度计算（`MoleCalculator`）；
3. 三大守恒等式生成（`SOL-007`）：电荷守恒（Σc·z⁺ = Σc·z⁻）、物料守恒、质子守恒；
4. 粒子浓度排序：能由"强电解质完全电离 > 弱电离/水解程度小 + 守恒式"确定者给 `Solved`，需要解 Ka 二次方程才能定序者，若题目已给 pH 或给 Ka 且判别式完全平方则精确算，否则 `Underdetermined`；
5. 滴定关键点（恰好中和/等体积混合/中性点）用守恒式比较，避免数值猜测。
题库 `hard-aqueous-equilibrium.json`（AQ-H01..H08）：CH3COONa、NH4Cl、Na2CO3、NaHCO3（电离>水解）、等浓度 CH3COOH/CH3COONa 缓冲、NaOH 滴定 CH3COOH 至中性/pH=7 判据、二元弱酸 Na2C2O4、Ksp 与沉淀转化（`SOL-003/SOL-004`）。

### 阶段 6：工业流程引擎 + 知识库数据结构化
- 新文件 `Inference/ProcessFlowEngine.cs`：复用 [ProcessInferenceEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessInferenceEngine.cs) 的链回溯赋值，为每步追加"目的/原理"节点：新规则 `PROC-001`（除杂不引入新杂质）、`PROC-002`（沉淀转化方向，引用 SOL-004）、`PROC-003`（控制 pH 使 Fe3+ 沉淀完全而主体离子不沉淀，引用 SOL-003/Ksp）、`PROC-004`（蒸发浓缩冷却结晶 vs 灼烧、洗涤、循环利用），每条带 `evidence`+`source`（人教版必修/选必出处）。
- `data/knowledge/reactions/reactions-principles.json`：ELC 条目补 `halfReactions{negative,positive}`、THM 条目补 `deltaH`；`ReactionEntry` 模型加对应可空字段（旧数据反序列化不受影响）。
- 新增 `data/highschool/acid-constants.json`（Ka/Kb/Kw，模型 `EquilibriumConstantsData`）、`data/highschool/electrode-potentials.json`（标准电极电势，供 RED-004 使用），经 `RuleStore.Query<T>` 直接可用。
- 题库 `hard-process-flow.json`（PROCF-H01..H08）：粗盐提纯、从海水中提取溴/镁、铝土矿制 Al、黄铁矿制硫酸、废旧锂电池回收、SiO2 基流程、除杂 pH 调控、母液循环。

### 阶段 7：验证、真题与前端接线
- `BenchmarkVerifier`：新类别在 `VerifyExpectedEquations` 之后追加结论比对分支（电极式集合、三段式表、浓度顺序、物质链+目的）。
- `Program.cs` 的 `RunVerifyReal`（L305-L366）加 4 个 case；把可溯源的高考真题（如 2023/2024 全国卷电极式题、浙江选考平衡题）追加进 `data/real-exam/real-exam.json`。
- `Explainer.CategoryName`/`SummarizeAnswer` 与 `GuideGenerator.BuildLevel3` 加 4 个 case（否则 `ExplanationGuideTests` 的"每题四级引导非空"断言失败）。
- `web/app.js`：分类 tab（L90-L100）加 4 项、`answerHtml`（L293-L341）加 4 个渲染分支（电极式双栏、三段式表格、浓度顺序链、流程步骤+目的）；`style.css` 加 `cat-*` 类；重跑 `--export-web` 生成 `web/data/problems.json`。
- 测试：新增 `ElectrochemEngineTests.cs`/`EquilibriumEngineTests.cs`/`AqueousEquilibriumEngineTests.cs`/`ProcessFlowEngineTests.cs`（每引擎含"完整推理链"断言：步骤数 ≥4、每步 Description 非空、含 KnowledgeId 的步骤可溯源）；把 [ProblemSolverTests.cs#L86-L95](file:///d:/github/CogniThink-Chem/tests/CogniThink.Chem.Tests/ProblemSolverTests.cs#L86-L95) 的 42/41/PROD-004 常量改为新总数与新拒答集合。

## 题库设计约定（四类共用）

- `difficulty` 一律 `Hard`；`expected.equations` 非空的题必须配 ≥1 个 `distractors`（`BenchmarkTests.cs#L43-L51` 强制），错解取高考典型失误：正负极写反、漏写 `e^-` 项、守恒式漏系数、三段式转化量与平衡量混淆、忽略多元弱酸分步电离、流程除杂引入新杂质。
- `engineExpectation.mustPass` 用现有检查名：`白名单校验`、`元素与电荷守恒`、`配平`、`状态符号`、`氧化还原分析`；新类别可加 `电子守恒`（若 `ChemicalCheckerService` 无此名则复用"氧化还原分析"）。
- 边界题（熔融介质、图像读取、开放性实验表述）明确写 `expected.engineExpectation.isValid=true` + 结论为 `Underdetermined`，不伪装成可解。

## Verification

每阶段结束依次执行（PowerShell，中文输出需 UTF8）：

1. `dotnet build CogniThink.Chem.sln --nologo -v minimal`
2. `dotnet test CogniThink.Chem.sln --nologo -v minimal`（阶段 1-2 必须保持 523 全绿；阶段 3-7 允许总数增加，禁止任何既有测试变红）
3. `dotnet run --project src/CogniThink.Chem.Cli --no-build -- --verify-real`（真题 FAIL 只允许已知的 CN⁻ 白名单边界与新题未入库阶段）
4. `dotnet run --project src/CogniThink.Chem.Cli --no-build -- --export-web` 后用浏览器子代理抽查 4 个新分类的详情页推理链渲染
5. 抽查推理链完整性：`--solve ELEC-H01`/`EQ-H01`/`AQ-H01`/`PROCF-H01` 的 Steps 输出须 ≥4 步且每步带出处

## 风险与取舍

- **原理上不可严格推导的**：pH/速率图像读图、有机结构推断（需 SMILES 与同分异构计数）、实验方案开放性表述。本次不建这类期望 `Solved` 的题；每类最多 1 道边界题且期望 `Underdetermined`。
- **Rational 无开方**：平衡/电离二次方程只在判别式为完全平方时求解，题库数据据此设计（转化率取 1/2、1/3、2/5 等），其余如实上抛。
- **改动面大**：`Species`/`CheckRequest` 属底层，阶段 1 单独提交并全量回归，降低后续阶段的排查成本。
- 题量 32 题 × 每题 distractors + 引擎支持，工作量集中在阶段 3-6；每阶段可独立提交，任一阶段结束仓库都处于全绿可交付状态。
