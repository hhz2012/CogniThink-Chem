namespace CogniThink.Chem.Core.Benchmark;

/// <summary>
/// 题目类别。推断动作（猜产物/猜成分/猜过程）属于 LLM 层；
/// 引擎只校验候选答案（方程式/理由方程式/数值），本枚举用于测试集分类与路由。
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
    StoichiometryCalc
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
    Calculate
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

    /// <summary>反应过程/实验步骤序列（成分推断/过程推断）。</summary>
    public List<StepSpec> Steps { get; set; } = new();

    /// <summary>定量事实（计算题）。</summary>
    public List<NumericFact> NumericFacts { get; set; } = new();
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

    /// <summary>对 <see cref="Equations"/> 中每条方程式的引擎预期。</summary>
    public EngineExpectation EngineExpectation { get; set; } = new();
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
/// 高中适用边界说明：引擎只做形式/守恒/白名单校验，部分"化学上错误但形式守恒"的错解
/// （如 Fe 与稀硫酸误写为生成 Fe^3+）引擎无法捕获——此类边界样例 <see cref="ExpectValid"/> 置 true，
/// 并在 <see cref="Description"/> 标注"边界样例：引擎职责外，由 LLM 层判断"。
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

    /// <summary>n = c·V。</summary>
    MolesFromConcentration,

    /// <summary>产率 = 实际 / 理论（有理比值）。</summary>
    YieldRatio,

    /// <summary>n = N / NA。</summary>
    MolesFromParticles,

    /// <summary>n(离子) = c·V·角标数。</summary>
    IonMolesFromSolute
}
