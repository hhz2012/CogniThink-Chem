namespace CogniThink.Chem.Core.Benchmark;

/// <summary>
/// 题目类别。引擎按类别路由到对应的知识驱动推断引擎（见 Solving.ProblemSolver）：
/// 产物/成分/过程推断与离子共存由反应知识库推导，定量计算由有理数引擎精确复算；
/// 本枚举用于测试集分类与路由。
/// </summary>
public enum ProblemCategory
{
    /// <summary>产物推断：给定反应物与条件，写出产物与方程式。</summary>
    ProductInference,

    /// <summary>成分推断：给定可能物质列表与实验过程，判断一定含/一定不含/可能含。</summary>
    SubstanceIdentification,

    /// <summary>过程推断：给定物质候选与转化步骤（A→B→C），确定各物质并写出各方程式。</summary>
    ProcessInference,

    /// <summary>离子共存：给定离子组与介质，判断能否大量共存并给出理由方程式。</summary>
    IonCoexistence,

    /// <summary>定量计算：摩尔/浓度/气体体积/产率等，引擎用有理数精确复算。</summary>
    StoichiometryCalc,

    /// <summary>电化学：电极反应式、电解产物、离子移动方向。</summary>
    Electrochemistry,

    /// <summary>化学平衡综合：三段式、转化率、Kp、平衡移动方向。</summary>
    ChemicalEquilibrium,

    /// <summary>水溶液中的离子平衡：粒子浓度大小比较、三大守恒。</summary>
    AqueousIonEquilibrium,

    /// <summary>工业流程综合推断：多步转化 + 操作目的。</summary>
    IndustrialProcess
}

/// <summary>难度（对应课内层次：基础/中档/难题）。</summary>
public enum Difficulty
{
    Basic,
    Medium,
    Hard
}

/// <summary>设问类型（与 <see cref="ProblemCategory"/> 一一对应，冗余写出便于自校验）。</summary>
public enum AskType
{
    /// <summary>写产物/方程式。</summary>
    WriteProducts,

    /// <summary>判断物质成分。</summary>
    IdentifySubstances,

    /// <summary>写转化链各步方程式并确定各物质。</summary>
    WriteStepEquations,

    /// <summary>判断离子能否共存。</summary>
    JudgeCoexistence,

    /// <summary>数值计算。</summary>
    Calculate,

    /// <summary>写电极反应式（正负极/阴阳极）。</summary>
    WriteElectrodeEquations,

    /// <summary>平衡三段式、转化率、Kp 与移动方向计算。</summary>
    EquilibriumCalculation,

    /// <summary>粒子浓度大小比较与守恒等式。</summary>
    CompareConcentrations,

    /// <summary>流程各步物质与操作目的分析。</summary>
    AnalyzeProcess
}

/// <summary>成分推断结论：一定含 / 一定不含 / 可能含（无法确定）。</summary>
public enum Presence
{
    Must,
    MustNot,
    Maybe
}

/// <summary>离子共存结论。</summary>
public enum CoexistVerdict
{
    Coexist,
    NotCoexist
}

/// <summary>
/// 统一题目规范（ProblemSpec）。全部五类题目共用该结构，按类别启用不同字段：
/// <list type="bullet">
/// <item>ProductInference：given.species + expected.equations。</item>
/// <item>SubstanceIdentification：given.candidateSpecies + given.steps + expected.composition。</item>
/// <item>ProcessInference：given.steps + expected.substanceMap + expected.equations。</item>
/// <item>IonCoexistence：given.species（离子组）+ given.medium + expected.verdict（+理由方程式）。</item>
/// <item>StoichiometryCalc：given.numericFacts + expected.calc + expected.numericAnswer。</item>
/// </list>
/// JSON 序列化走 <see cref="Data.ChemDataLoader.Options"/>（camelCase + 字符串枚举）。
/// </summary>
public sealed class ProblemSpec
{
    /// <summary>唯一编号：类别前缀-序号，如 "PROD-001"、"IDENT-003"。</summary>
    public string Id { get; set; } = "";

    public ProblemCategory Category { get; set; }

    public Difficulty Difficulty { get; set; } = Difficulty.Basic;

    /// <summary>
    /// L1 文本输入：自然语言题干（原题），供 LLM 读题，引擎不解析。
    /// 书写规范：物质用化学式，离子用 Fe^3+ 形式，现象描述与教材一致。
    /// </summary>
    public string ProblemText { get; set; } = "";

    public GivenSpec Given { get; set; } = new();

    public QuestionSpec Question { get; set; } = new();

    /// <summary>标准答案（离线测试中代替 LLM 输出，供引擎校验）。</summary>
    public ExpectedSpec Expected { get; set; } = new();

    /// <summary>干扰项：学生典型错解，用于测试引擎的纠错捕获能力。</summary>
    public List<DistractorSpec> Distractors { get; set; } = new();

    /// <summary>教材/课标出处（必填，规则库铁律：全部知识可溯源）。</summary>
    public string Source { get; set; } = "";

    public List<string> Tags { get; set; } = new();
}

/// <summary>已知条件。</summary>
public sealed class GivenSpec
{
    /// <summary>反应物/起始物种（产物推断），或待判定的离子组（离子共存）。</summary>
    public List<string> Species { get; set; } = new();

    /// <summary>反应条件（点燃/△/高温/通电/催化剂/浓 等，可组合如 "△,浓"）。</summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>介质："acidic" / "basic" / null（氧化还原半反应与共存语境用）。</summary>
    public string? Medium { get; set; }

    /// <summary>可能物质列表（成分推断/过程推断的候选集）。</summary>
    public List<string> CandidateSpecies { get; set; } = new();

    /// <summary>
    /// 成分推断模式标记：true = 待测对象是候选中的"某一种"（单一实体，须唯一解释全部证据）；
    /// false（缺省）= 混合成分（可能含多种）。由题面语义显式标注，求解器不做猜测。
    /// </summary>
    public bool SingleEntity { get; set; }

    /// <summary>反应过程/实验步骤序列（成分推断/过程推断）。</summary>
    public List<StepSpec> Steps { get; set; } = new();

    /// <summary>定量事实（计算题）。</summary>
    public List<NumericFact> NumericFacts { get; set; } = new();

    /// <summary>电化学装置（电化学题）。</summary>
    public CellSpec? Cell { get; set; }

    /// <summary>平衡体系（化学平衡综合题）。</summary>
    public EquilibriumGivenSpec? Equilibrium { get; set; }

    /// <summary>溶液组成（水溶液离子平衡题）。</summary>
    public AqueousGivenSpec? Solution { get; set; }
}

/// <summary>电化学装置描述：原电池（正负极）或电解池（阴阳极）。</summary>
public sealed class CellSpec
{
    /// <summary>"galvanic"（原电池）/ "electrolytic"（电解池）。</summary>
    public string Type { get; set; } = "galvanic";

    /// <summary>
    /// 电极材料（L2 写法）。原电池按题给顺序 [较活泼, 较不活泼]；
    /// 电解池按 [阳极, 阴极]。惰性电极写 "C" 或 "Pt"。
    /// </summary>
    public List<string> Electrodes { get; set; } = new();

    /// <summary>电解质溶液/熔融物（L2 写法），如 "H2SO4"、"CuCl2"、"NaCl"。</summary>
    public string Electrolyte { get; set; } = "";

    /// <summary>介质："acidic" / "basic"（决定电极式的 H^+/OH^- 配平方式）。</summary>
    public string Medium { get; set; } = "";

    /// <summary>
    /// 总反应（可空）。给出时引擎以此拆解半反应；
    /// 为空时按电极+电解质检索知识库 ELC 条目，检索不到则如实上抛欠定。
    /// </summary>
    public string Overall { get; set; } = "";

    /// <summary>通入气体燃料/氧化剂（燃料电池用），如 ["H2"]、["O2"]。</summary>
    public List<string> GasFeeds { get; set; } = new();
}

/// <summary>化学平衡题的题给体系。</summary>
public sealed class EquilibriumGivenSpec
{
    /// <summary>可逆反应方程式（L2 写法，可用 = 或 ⇌，未配平亦可）。</summary>
    public string Equation { get; set; } = "";

    /// <summary>容器："constantVolume"（恒温恒容，缺省）/ "constantPressure"（恒温恒压）。</summary>
    public string Container { get; set; } = "constantVolume";

    /// <summary>起始投料：物种 → 物质的量（mol，字符串精确值）。</summary>
    public Dictionary<string, string> Initial { get; set; } = new();

    /// <summary>题给平衡信息（见 <see cref="EquilibriumKnownSpec.Kind"/>）。</summary>
    public EquilibriumKnownSpec? Known { get; set; }

    /// <summary>平衡后施加的扰动（可空）。</summary>
    public DisturbanceSpec? Disturbance { get; set; }

    /// <summary>体系总压（Kp 计算用，字符串精确值，单位 atm 或 kPa 由题目文字说明）。</summary>
    public string? TotalPressure { get; set; }

    /// <summary>要求摩尔分数的物种（题问物种）；缺省时取 known 物种。</summary>
    public string? FractionSpecies { get; set; }
}

/// <summary>题给平衡信息：kind = conversion（某反应物转化率）/ amountFraction（某组分平衡体积/物质的量分数）/ moles（某组分平衡物质的量）。</summary>
public sealed class EquilibriumKnownSpec
{
    public string Kind { get; set; } = "";

    /// <summary>相关物种。</summary>
    public string Species { get; set; } = "";

    /// <summary>精确值（"1/2"、"0.4"、"2"）。</summary>
    public string Value { get; set; } = "";
}

/// <summary>平衡扰动：kind = addSpecies（加料）/ compress（缩小体积加压）/ expand（减压）/ heatUp（升温）/ coolDown（降温）/ catalyst（加催化剂）。</summary>
public sealed class DisturbanceSpec
{
    public string Kind { get; set; } = "";

    /// <summary>addSpecies 时的物种。</summary>
    public string Species { get; set; } = "";

    /// <summary>正反应热符号（heatUp/coolDown 须给："exothermic" 放热 / "endothermic" 吸热）。</summary>
    public string Thermal { get; set; } = "";
}

/// <summary>水溶液题的组成：单一溶质，或两种溶质混合（等体积混合/恰好中和等）。</summary>
public sealed class AqueousGivenSpec
{
    /// <summary>溶质清单（L2 写法 + 浓度/体积）。</summary>
    public List<SoluteSpec> Solutes { get; set; } = new();

    /// <summary>题给 pH（可空，用于需要 Ka 才能定序的精确判定）。</summary>
    public string? Ph { get; set; }

    /// <summary>题给附加条件（如"恰好中和"、"pH=7"、"等体积混合"）。</summary>
    public string Note { get; set; } = "";

    /// <summary>要求比较浓度的粒子清单（L2 离子写法；空 = 不排序，如沉淀转化题）。</summary>
    public List<string> OrderSpecies { get; set; } = new();

    /// <summary>沉淀转化条件（可空；给出时走 Ksp 转化路径）。</summary>
    public AqueousConversionSpec? Conversion { get; set; }
}

/// <summary>沉淀转化题条件：原沉淀 + 加入离子 → 目标沉淀。</summary>
public sealed class AqueousConversionSpec
{
    /// <summary>原沉淀化学式主体（如 "AgCl"）。</summary>
    public string Original { get; set; } = "";

    /// <summary>加入的离子（如 "I^-"）。</summary>
    public string AddedIon { get; set; } = "";

    /// <summary>转化目标沉淀（如 "AgI"）。</summary>
    public string Target { get; set; } = "";
}

/// <summary>一种溶质及其用量。</summary>
public sealed class SoluteSpec
{
    public string Species { get; set; } = "";

    /// <summary>物质的量浓度（mol/L，字符串精确值）。</summary>
    public string Concentration { get; set; } = "";

    /// <summary>体积（L，字符串精确值；缺省 "1"）。</summary>
    public string VolumeLiters { get; set; } = "1";
}

/// <summary>一步反应过程或实验操作。</summary>
public sealed class StepSpec
{
    public int Step { get; set; }

    /// <summary>起点：物质标签（"A"）或样品名（"X"）。</summary>
    public string From { get; set; } = "";

    /// <summary>终点：物质标签或现象描述（"白色沉淀"）。</summary>
    public string To { get; set; } = "";

    /// <summary>加入的试剂。</summary>
    public List<string> Reagents { get; set; } = new();

    public List<string> Conditions { get; set; } = new();

    /// <summary>实验现象。</summary>
    public string Phenomenon { get; set; } = "";

    /// <summary>
    /// 本步操作目的（工业流程题）：如 "除去 Fe^3+ 杂质"、"将 Br^- 氧化为 Br2"。
    /// 引擎据此匹配流程目的规则（PROC-*）并输出"目的—原理—方程式"推理节点。
    /// </summary>
    public string Purpose { get; set; } = "";
}

/// <summary>定量事实。数值一律用字符串精确表示（Rational.Parse 可解析："10.6"、"1/2"）。</summary>
public sealed class NumericFact
{
    public NumericFactKind Kind { get; set; }

    /// <summary>相关物种（可为空，如溶液体积）。</summary>
    public string? Species { get; set; }

    /// <summary>精确数值（"0.5"、"10.6"、"1/2"）。</summary>
    public string Value { get; set; } = "";

    public string Unit { get; set; } = "";
}

/// <summary>定量事实种类。</summary>
public enum NumericFactKind
{
    Mass,
    Moles,
    GasVolume,
    SolutionVolume,
    Concentration,
    ParticleCount,
    ActualYieldMass
}

/// <summary>设问。</summary>
public sealed class QuestionSpec
{
    public AskType Ask { get; set; }

    /// <summary>求解目标描述（如 ["产物"]、["A","B","C"]、["CO2 标准状况体积"]）。</summary>
    public List<string> Targets { get; set; } = new();
}

/// <summary>标准答案与引擎预期。</summary>
public sealed class ExpectedSpec
{
    /// <summary>
    /// 标准方程式（L2 引擎格式："2H2 + O2 =点燃= 2H2O"、"Ba^2+ + SO4^2- = BaSO4↓"）。
    /// 产物推断/过程推断：各反应方程式；成分推断：证据反应方程式；
    /// 离子共存：不共存理由方程式；计算题：相关反应方程式。
    /// </summary>
    public List<string> Equations { get; set; } = new();

    /// <summary>成分推断结论：候选物质 → Must/MustNot/Maybe。</summary>
    public Dictionary<string, Presence>? Composition { get; set; }

    /// <summary>过程推断结论：标签（A/B/C）→ 物质化学式。</summary>
    public Dictionary<string, string>? SubstanceMap { get; set; }

    /// <summary>计算题答案（精确有理值字符串 + 单位）。</summary>
    public NumericAnswer? NumericAnswer { get; set; }

    /// <summary>计算题复算规格：引擎按此调用 MoleCalculator 精确复算并与答案比对。</summary>
    public CalcSpec? Calc { get; set; }

    /// <summary>离子共存结论。</summary>
    public CoexistVerdict? Verdict { get; set; }

    /// <summary>电化学结论：电极反应式（按装置类型只填对应两项）。</summary>
    public ElectrodeEquationsSpec? Electrodes { get; set; }

    /// <summary>化学平衡结论：三段式表 + 转化率/分数/Kp/移动方向。</summary>
    public EquilibriumAnswerSpec? Equilibrium { get; set; }

    /// <summary>水溶液离子平衡结论：粒子浓度顺序 + 守恒等式。</summary>
    public AqueousAnswerSpec? Aqueous { get; set; }

    /// <summary>工业流程结论：各步操作目的关键词（步骤号 → 必须出现在该步推理中的关键词）。</summary>
    public Dictionary<string, string>? Purposes { get; set; }

    /// <summary>对 <see cref="Equations"/> 中每条方程式的引擎预期。</summary>
    public EngineExpectation EngineExpectation { get; set; } = new();
}

/// <summary>电极反应式答案：原电池填 Negative/Positive，电解池填 Anode/Cathode。</summary>
public sealed class ElectrodeEquationsSpec
{
    /// <summary>负极（氧化，失电子）。</summary>
    public string? Negative { get; set; }

    /// <summary>正极（还原，得电子）。</summary>
    public string? Positive { get; set; }

    /// <summary>阳极（氧化，失电子）。</summary>
    public string? Anode { get; set; }

    /// <summary>阴极（还原，得电子）。</summary>
    public string? Cathode { get; set; }
}

/// <summary>化学平衡答案。</summary>
public sealed class EquilibriumAnswerSpec
{
    /// <summary>三段式行：物种 + 起始/转化/平衡量（mol，精确字符串）。</summary>
    public List<EquilibriumRowSpec> Table { get; set; } = new();

    /// <summary>转化率对应的物种与其转化率（精确字符串，如 "1/3"）。</summary>
    public string? ConversionSpecies { get; set; }

    public string? Conversion { get; set; }

    /// <summary>某组分的平衡物质的量（分数）；Species 与 Value。</summary>
    public string? FractionSpecies { get; set; }

    public string? Fraction { get; set; }

    /// <summary>Kp 或 Kc 表达式值（精确字符串）。</summary>
    public string? KValue { get; set; }

    /// <summary>平衡移动方向："forward" / "reverse" / "noShift"（无扰动时为空）。</summary>
    public string? Direction { get; set; }
}

/// <summary>三段式的一行。</summary>
public sealed class EquilibriumRowSpec
{
    public string Species { get; set; } = "";

    public string Start { get; set; } = "";

    public string Change { get; set; } = "";

    public string End { get; set; } = "";
}

/// <summary>水溶液离子平衡答案。</summary>
public sealed class AqueousAnswerSpec
{
    /// <summary>
    /// 粒子浓度由大到小的顺序（如 ["c(Na^+)","c(CH3COO^-)","c(OH^-)","c(H^+)"]），
    /// 引擎按"完全电离 > 弱过程 + 守恒式"链推出同序结论。
    /// </summary>
    public List<string> Order { get; set; } = new();

    /// <summary>必须写出的守恒等式（电荷/物料/质子，L2 写法）。</summary>
    public List<string> ConservationEquations { get; set; } = new();
}

/// <summary>引擎对标准方程式的预期校验结果（逐条方程式适用）。</summary>
public sealed class EngineExpectation
{
    /// <summary>预期整体是否通过（超纲干扰例为 false）。</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>预期整体风险标签（空 = 无标签）。</summary>
    public string RiskTag { get; set; } = "";

    /// <summary>必须通过且存在的检查项名（如 "配平"、"元素与电荷守恒"）。</summary>
    public List<string> MustPass { get; set; } = new();

    /// <summary>必须失败且存在的检查项名（用于超纲干扰例，如 "白名单校验"）。</summary>
    public List<string> MustFail { get; set; } = new();
}

/// <summary>
/// 干扰项：学生典型错解。
/// 引擎捕获范围：形式/守恒/白名单/拆写校验 + 反应库一致性（产物与课内知识库矛盾）。
/// 仍有少数边界样例超出引擎判定范围（如知识库未收录该反应物组合时不判产物正误），
/// 此类样例 <see cref="ExpectValid"/> 置 true，并在 <see cref="Description"/> 标注"边界样例"。
/// </summary>
public sealed class DistractorSpec
{
    /// <summary>编号：主题编号-D序号，如 "PROD-001-D1"。</summary>
    public string Id { get; set; } = "";

    /// <summary>错解描述（学生思维误区）。</summary>
    public string Description { get; set; } = "";

    /// <summary>错误方程式（L2 引擎格式）。</summary>
    public string Equation { get; set; } = "";

    /// <summary>预期引擎整体结论：false = 应被捕获判错；true = 边界样例（引擎形式上放行）。</summary>
    public bool ExpectValid { get; set; }

    /// <summary>预期失败的检查项名（ExpectValid=false 时必填）。</summary>
    public List<string> ExpectFailChecks { get; set; } = new();

    /// <summary>预期风险标签（空 = 不校验标签）。</summary>
    public string ExpectRiskTag { get; set; } = "";
}

/// <summary>计算题答案。Value 为精确有理数字符串（Rational.Parse："2.24"、"9/10"）。</summary>
public sealed class NumericAnswer
{
    public string Value { get; set; } = "";

    public string Unit { get; set; } = "";

    public string? Species { get; set; }
}

/// <summary>
/// 计算题复算规格：声明式描述 MoleCalculator 的调用方式，
/// 验证器按 <see cref="Op"/> 分派，结果与 <see cref="ExpectedSpec.NumericAnswer"/> 精确比对。
/// 数值字段均为精确有理数字符串；按各 Op 启用对应字段。
/// </summary>
public sealed class CalcSpec
{
    public CalcOp Op { get; set; }

    /// <summary>主物种（摩尔质量查询用），如 "Na2CO3"、"CuSO4·5H2O"。</summary>
    public string? Species { get; set; }

    /// <summary>物质的量 n。</summary>
    public string? Moles { get; set; }

    /// <summary>质量（g）。</summary>
    public string? MassGrams { get; set; }

    /// <summary>溶液体积（L）。</summary>
    public string? SolutionVolumeLiters { get; set; }

    /// <summary>标准状况气体体积（L）。</summary>
    public string? GasVolumeLiters { get; set; }

    /// <summary>物质的量浓度（mol/L）。</summary>
    public string? Concentration { get; set; }

    /// <summary>稀释前浓度（mol/L）。</summary>
    public string? Concentration1 { get; set; }

    /// <summary>稀释前体积（L）。</summary>
    public string? Volume1Liters { get; set; }

    /// <summary>稀释后体积（L）。</summary>
    public string? Volume2Liters { get; set; }

    /// <summary>实际产量（g）。</summary>
    public string? ActualMassGrams { get; set; }

    /// <summary>理论产量（g）。</summary>
    public string? TheoreticalMassGrams { get; set; }

    /// <summary>粒子数尾数（如 "1.204"，与 <see cref="ParticleExponent"/> 组合）。</summary>
    public string? ParticleMantissa { get; set; }

    /// <summary>粒子数 10 的指数（如 24 → 1.204×10^24）。</summary>
    public int ParticleExponent { get; set; }

    /// <summary>1 个溶质化学式单元中的目标离子个数（如 Al2(SO4)3 中 SO4^2- 为 "3"）。</summary>
    public string? IonSubscript { get; set; }
}

/// <summary>计算类型（对应 MoleCalculator 的各方法）。</summary>
public enum CalcOp
{
    /// <summary>c = n / V。</summary>
    MolarConcentration,

    /// <summary>n = m / M。</summary>
    MolesFromMass,

    /// <summary>m = n × M。</summary>
    MassFromMoles,

    /// <summary>V(标况) = (m / M) × 22.4。</summary>
    GasVolumeFromMass,

    /// <summary>n = V(标况) / 22.4。</summary>
    MolesFromGasVolume,

    /// <summary>c2 = c1·V1 / V2。</summary>
    Dilution,

    /// <summary>V1 = c2·V2 / c1（由目标浓度与体积反求所需浓溶液体积）。</summary>
    DilutionVolume,

    /// <summary>n = c·V。</summary>
    MolesFromConcentration,

    /// <summary>产率 = 实际 / 理论（有理比值）。</summary>
    YieldRatio,

    /// <summary>n = N / NA。</summary>
    MolesFromParticles,

    /// <summary>n(离子) = c·V·角标数。</summary>
    IonMolesFromSolute
}
