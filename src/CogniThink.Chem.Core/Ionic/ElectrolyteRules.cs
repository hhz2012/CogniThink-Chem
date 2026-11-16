using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Ionic;

/// <summary>
/// 溶解性分级（高中溶解性表口径）。
/// </summary>
public enum Solubility
{
    /// <summary>可溶：离子方程式中应拆写为离子；作为生成物时不应标 ↓。</summary>
    Soluble,

    /// <summary>微溶：拆写与 ↓ 标注视澄清/浑浊语境而定，引擎只提示、不判错。</summary>
    SlightlySoluble,

    /// <summary>难溶：保留化学式；在溶液中生成时应标 ↓。</summary>
    Insoluble
}

/// <summary>
/// 高中电解质与溶解性规则门面。数据全部来自 data/highschool/ JSON 规则库
/// （electrolytes.json / solubility.json / ions.json），经 <see cref="RuleStore"/> 加载并缓存。
/// 教材来源标注见各 JSON 文件的 source 字段。
/// 铁律：表外物质一律返回 null / 不命中，由上层标记风险，引擎绝不猜测其溶解性或拆写方式。
/// 键约定：均使用 <see cref="Chemistry.Species.Body"/>（去电荷主体）形式，
/// 离子则用 <see cref="Chemistry.Species.ToString()"/> 的 caret 形式（如 "CO3^2-"）。
/// </summary>
public static class ElectrolyteRules
{
    private static readonly Lazy<ElectrolyteData> Data =
        new(() => RuleStore.Load().Query<ElectrolyteData>("electrolytes"));

    private static readonly Lazy<IonInventoryData> Ions =
        new(() => RuleStore.Load().Query<IonInventoryData>("ions"));

    private static readonly Lazy<IReadOnlySet<string>> StrongAcidSet =
        new(() => ToSet(Data.Value.StrongAcids));

    private static readonly Lazy<IReadOnlySet<string>> StrongBaseSet =
        new(() => ToSet(Data.Value.StrongBases));

    private static readonly Lazy<IReadOnlySet<string>> WeakElectrolyteSet =
        new(() => ToSet(Data.Value.WeakElectrolytes));

    private static readonly Lazy<IReadOnlySet<string>> CommonOxideSet =
        new(() => ToSet(Data.Value.CommonOxides));

    private static readonly Lazy<IReadOnlySet<string>> GasSet =
        new(() => ToSet(Ions.Value.Gases));

    private static readonly Lazy<IReadOnlySet<string>> WeakAcidRadicalSet =
        new(() => new HashSet<string>(Data.Value.WeakAcidRadicalConjugates.Keys, StringComparer.Ordinal));

    /// <summary>
    /// 六大强酸（离子方程式中必须拆写）。
    /// 来源：人教版必修第一册·电解质的电离。
    /// </summary>
    public static IReadOnlySet<string> StrongAcids => StrongAcidSet.Value;

    /// <summary>
    /// 强碱。注意 Ca(OH)2 虽是强碱但微溶，拆写视语境（澄清石灰水拆、石灰乳不拆），
    /// 由 <see cref="IonicEquationChecker"/> 结合 <see cref="LookupSolubility"/> 处理。
    /// 来源：人教版必修第一册·电解质的电离。
    /// </summary>
    public static IReadOnlySet<string> StrongBases => StrongBaseSet.Value;

    /// <summary>
    /// 弱电解质：离子方程式中必须保留化学式，不得拆成离子。
    /// 来源：人教版必修第一册·弱电解质；选择性必修第一册·电离平衡。
    /// 易错点：NH3·H2O 用 "·" 书写（一水合氨），H2O 既是溶剂也是弱电解质。
    /// </summary>
    public static IReadOnlySet<string> WeakElectrolytes => WeakElectrolyteSet.Value;

    /// <summary>
    /// 不能与 H^+ 同侧共存的弱酸根（含酸式根）→ 其结合产物（共轭弱电解质）候选。
    /// 反应物侧同时出现 H^+ 与该根时，生成物必须出现候选之一；
    /// 生成物侧同时出现 H^+ 与该根（不可逆方程）视为错误。
    /// 来源：人教版必修第一册·离子反应（离子共存判断）。
    /// 说明：CO3^2-/SO3^2- 允许以"H2O + CO2/SO2"形式体现（H2CO3、H2SO3 分解），
    /// 故候选中含 CO2/SO2；CO3^2-、S^2-、SO3^2- 少量酸时生成酸式根，一并列入候选。
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> WeakAcidRadicalConjugates =>
        Data.Value.WeakAcidRadicalConjugates;

    /// <summary>不能与 H^+ 同侧共存的弱酸根集合（= <see cref="WeakAcidRadicalConjugates"/> 的键）。</summary>
    public static IReadOnlySet<string> WeakAcidRadicals => WeakAcidRadicalSet.Value;

    /// <summary>
    /// 离子方程式中通常以化学式保留、不拆写的常见氧化物（含过氧化物）。
    /// 来源：人教版必修第一册·离子方程式书写步骤（氧化物不拆）。
    /// </summary>
    public static IReadOnlySet<string> CommonOxides => CommonOxideSet.Value;

    /// <summary>
    /// 溶液中生成时需要标 ↑ 的常见气体。
    /// 来源：人教版必修第一册/第二册常见气体性质。
    /// 易错点：HCl 不在表内——在水溶液中 HCl 完全电离，若以分子形式出现按"强酸应拆"处理。
    /// </summary>
    public static IReadOnlySet<string> Gases => GasSet.Value;

    /// <summary>
    /// 查询物质溶解性（委托 <see cref="SolubilityTable"/>，数据源 solubility.json）。
    /// </summary>
    /// <param name="formula">去电荷化学式主体（<see cref="Chemistry.Species.Body"/>），如 "BaSO4"、"Ca(OH)2"。</param>
    /// <returns>溶解性；表外物质返回 null（引擎不判断，绝不猜测）。</returns>
    public static Solubility? LookupSolubility(string formula) => SolubilityTable.Lookup(formula);

    private static IReadOnlySet<string> ToSet(List<string> items) =>
        new HashSet<string>(items, StringComparer.Ordinal);
}
