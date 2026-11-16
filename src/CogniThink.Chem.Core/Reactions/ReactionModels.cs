using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Reactions;

/// <summary>
/// 反应主类型。一个反应只标一个"主类型"用于路由与讲解归类；
/// 更细的标签（如"氧化还原""放热"）放 <see cref="ReactionEntry.Tags"/>。
/// </summary>
public enum ReactionType
{
    /// <summary>化合反应：多变一。</summary>
    Combination,

    /// <summary>分解反应：一变多。</summary>
    Decomposition,

    /// <summary>置换反应：单质 + 化合物 → 新单质 + 新化合物。</summary>
    Displacement,

    /// <summary>复分解反应：两种化合物互相交换成分。</summary>
    DoubleDisplacement,

    /// <summary>燃烧（通常也是氧化还原，主类型按燃烧归类）。</summary>
    Combustion,

    /// <summary>其它氧化还原（不属于上述四类的氧化还原，如归中、歧化、复杂氧化还原）。</summary>
    Redox,

    /// <summary>有机反应（取代/加成/氧化/酯化/水解/聚合等，细分入 Tags）。</summary>
    Organic,

    /// <summary>电极反应（原电池/电解池半反应）。</summary>
    Electrode,

    /// <summary>电离/水解等离子平衡（多为可逆）。</summary>
    IonEquilibrium,

    /// <summary>热化学方程式（含反应热）。</summary>
    Thermochemical
}

/// <summary>
/// 反应知识库条目（知识驱动推断的地基）。每一条都是可溯源到教材的确定反应：
/// 给定 <see cref="Reactants"/> 与 <see cref="Conditions"/>，产物唯一确定为 <see cref="Products"/>。
/// 推断引擎只在这些条目之上做检索与组合，绝不凭空生成产物。
/// </summary>
public sealed class ReactionEntry
{
    /// <summary>唯一编号：RX-模块-族-序号，如 "RX-IN-NA-001"、"RX-OR-ALD-003"。</summary>
    public string Id { get; set; } = "";

    /// <summary>反应名称（教材表述），如"钠在氯气中燃烧"。</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 反应物化学式列表（L2 物种写法：分子用主体如 "Na"、"H2SO4"；离子用 caret 形式如 "Fe^3+"）。
    /// 顺序无关，匹配时按集合比较。
    /// </summary>
    public List<string> Reactants { get; set; } = new();

    /// <summary>
    /// 反应条件（原子条件列表，如 ["点燃"]、["△","浓"]、["催化剂","加热"]）。
    /// 无条件（常温即可进行）为空列表。
    /// </summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>产物化学式列表（L2 物种写法）。</summary>
    public List<string> Products { get; set; } = new();

    /// <summary>
    /// 完整化学方程式（L2 引擎格式，已配平，含条件与状态符号），
    /// 如 "2Na + Cl2 =点燃= 2NaCl"、"Cu + 2H2SO4 =△,浓= CuSO4 + SO2↑ + 2H2O"。
    /// 该字段必须通过 <c>ChemicalCheckerService.Check</c>（白名单+守恒+配平），由自洽测试保证。
    /// </summary>
    public string Equation { get; set; } = "";

    /// <summary>主反应类型。</summary>
    public ReactionType ReactionType { get; set; }

    /// <summary>实验现象（教材描述），供成分推断与讲解引用；无现象观察可为空。</summary>
    public string Phenomenon { get; set; } = "";

    /// <summary>归属知识点 id（必须存在于 knowledge.json 覆盖清单）。</summary>
    public string KnowledgeId { get; set; } = "";

    /// <summary>教材/课标出处，可溯源。</summary>
    public string Source { get; set; } = "";

    /// <summary>学段范围：必修 / 选择性必修 / 延伸。</summary>
    public GradeScope GradeScope { get; set; } = GradeScope.Required;

    /// <summary>细分标签（如 "氧化还原"、"放热"、"取代"、"加成"），供检索与讲解。</summary>
    public List<string> Tags { get; set; } = new();

    public override string ToString() => $"{Id} {Name}";
}
