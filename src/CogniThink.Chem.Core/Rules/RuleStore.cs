using System.Text.Json;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Ionic;

namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 高中规则库加载器：读取 data/highschool/*.json 并按 (键, 类型) 缓存。
/// 键 = JSON 文件名（不含扩展名）：solubility / electrolytes / ions /
/// activity-series / double-decomposition。
/// 仅依赖 System.Text.Json（复用 <see cref="ChemDataLoader.Options"/>：
/// camelCase、允许注释与尾逗号、枚举按字符串）。
/// </summary>
public sealed class RuleStore
{
    private readonly string _directory;
    private readonly Dictionary<(string Key, Type Type), object> _cache = new();

    private RuleStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// 加载规则库。
    /// </summary>
    /// <param name="dataRoot">
    /// 数据根目录；为 null 时由 <see cref="DataRoot.Resolve"/> 定位
    /// （环境变量 COGNITHINK_DATA 或从程序目录向上查找仓库根）。
    /// </param>
    /// <exception cref="DirectoryNotFoundException">highschool 目录缺失。</exception>
    public static RuleStore Load(string? dataRoot = null)
    {
        var root = DataRoot.Resolve(dataRoot);
        var directory = Path.Combine(root, "highschool");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"高中规则库目录不存在：{directory}");
        }

        return new RuleStore(directory);
    }

    /// <summary>
    /// 按键查询规则文件并整体反序列化为 <typeparamref name="T"/>（结果缓存）。
    /// </summary>
    /// <typeparam name="T">规则数据模型，如 <see cref="SolubilityData"/>。</typeparam>
    /// <param name="key">文件名（不含 .json），如 "solubility"。</param>
    /// <exception cref="FileNotFoundException">规则文件不存在。</exception>
    /// <exception cref="InvalidDataException">反序列化结果为空。</exception>
    public T Query<T>(string key) where T : class
    {
        var cacheKey = (key, typeof(T));
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return (T)cached;
        }

        var path = Path.Combine(_directory, key + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"规则文件不存在：{path}", path);
        }

        var data = JsonSerializer.Deserialize<T>(File.ReadAllText(path), ChemDataLoader.Options)
            ?? throw new InvalidDataException($"规则文件反序列化结果为空：{path}");
        _cache[cacheKey] = data;
        return data;
    }
}

/// <summary>溶解性表数据（solubility.json）。键为去电荷化学式主体，值为溶解性分级。</summary>
public sealed class SolubilityData
{
    public string Version { get; set; } = "";

    /// <summary>教材来源标注。</summary>
    public string Source { get; set; } = "";

    public Dictionary<string, Solubility> Table { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>电解质分类数据（electrolytes.json）。</summary>
public sealed class ElectrolyteData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    public List<string> StrongAcids { get; set; } = new();

    public List<string> StrongBases { get; set; } = new();

    public List<string> WeakElectrolytes { get; set; } = new();

    public List<string> CommonOxides { get; set; } = new();

    /// <summary>弱酸根（caret 形式）→ 其共轭弱电解质候选。</summary>
    public Dictionary<string, string[]> WeakAcidRadicalConjugates { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>离子与气体清单（ions.json）。离子用 caret 形式（Fe^3+、SO4^2-）。</summary>
public sealed class IonInventoryData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    public List<string> Cations { get; set; } = new();

    public List<string> Anions { get; set; } = new();

    public List<string> Gases { get; set; } = new();

    /// <summary>常见非金属单质（不含气体清单已覆盖的 H2/O2/N2/Cl2）。</summary>
    public List<string> ElementalNonmetals { get; set; } = new();
}

/// <summary>金属活动性顺序（activity-series.json）。"H" 为非金属参照位。</summary>
public sealed class ActivitySeriesData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    /// <summary>按活动性由强到弱排列，含参照位 H。</summary>
    public List<string> Series { get; set; } = new();
}

/// <summary>
/// 课内补充物质清单（substances.json）：收纳不落入溶解性表/电解质/氧化物/气体/单质
/// 其它分类的课内物质，主要是有机物与少数无机物（H2O2、酸式盐等）。
/// 仅用于扩充白名单与推断检索，不参与溶解性/电解质判断。
/// </summary>
public sealed class SubstanceInventoryData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    /// <summary>课内有机物（分子式或结构简式主体），如 "C2H5OH"、"CH3CHO"、"C6H6"。</summary>
    public List<string> Organics { get; set; } = new();

    /// <summary>其它课内无机物（不属于氧化物/酸/碱/盐溶解性表者），如 "H2O2"、"Ca(HCO3)2"。</summary>
    public List<string> OtherInorganics { get; set; } = new();
}

/// <summary>
/// 特征现象索引（phenomena.json）：现象关键词 → 指示物种。
/// 供成分推断（IDENT 类）把实验现象证据映射为候选物种，也供讲解引用。
/// 推断引擎只做关键词匹配与候选交集，绝不猜测索引之外的物种。
/// </summary>
public sealed class PhenomenaData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    public List<PhenomenonEntry> Items { get; set; } = new();
}

/// <summary>单条特征现象条目。</summary>
public sealed class PhenomenonEntry
{
    /// <summary>编号，如 "PH-001"。</summary>
    public string Id { get; set; } = "";

    /// <summary>现象关键词（对题面现象文本做子串匹配），如 "血红色"。</summary>
    public string Keyword { get; set; } = "";

    /// <summary>现象完整描述（教材表述），供讲解。</summary>
    public string Description { get; set; } = "";

    /// <summary>现象指示的物种（L2 写法）；多个表示并列候选，由推断引擎与题目候选取交集。</summary>
    public List<string> Species { get; set; } = new();

    /// <summary>产生该现象所需试剂/语境（可为空），如 "KSCN 溶液"。</summary>
    public string Reagent { get; set; } = "";

    /// <summary>归属知识点 id。</summary>
    public string KnowledgeId { get; set; } = "";

    /// <summary>教材出处。</summary>
    public string Source { get; set; } = "";
}

/// <summary>
/// 电离平衡常数与溶度积数据（acid-constants.json，25 ℃）。
/// 数值一律 mantissa×10^exponent 精确存储（<see cref="ConstantSpec"/>），
/// 引擎比较走 <see cref="Rational"/>，不引入浮点。
/// </summary>
public sealed class AcidConstantsData
{
    public string Version { get; set; } = "";

    /// <summary>教材来源标注。</summary>
    public string Source { get; set; } = "";

    /// <summary>水的离子积 Kw。</summary>
    public ConstantSpec Kw { get; set; } = new();

    /// <summary>酸的电离常数。键："CH3COOH"（一元）或 "H2CO3:Ka1"/"H2CO3:Ka2"（多元分步）。</summary>
    public Dictionary<string, ConstantSpec> Ka { get; set; } = new(StringComparer.Ordinal);

    /// <summary>碱的电离常数。键为碱分子式，如 "NH3·H2O"。</summary>
    public Dictionary<string, ConstantSpec> Kb { get; set; } = new(StringComparer.Ordinal);

    /// <summary>难溶电解质溶度积。键为化学式主体，如 "AgCl"。</summary>
    public Dictionary<string, ConstantSpec> Ksp { get; set; } = new(StringComparer.Ordinal);

    /// <summary>多元弱酸共轭阶梯：酸名 → 由多质子到少质子的物种序列（如 H2CO3 → HCO3^- → CO3^2-）。</summary>
    public Dictionary<string, List<string>> AcidLadders { get; set; } = new(StringComparer.Ordinal);

    /// <summary>弱碱共轭阶梯：碱名 → [碱分子, 共轭酸阳离子]（如 NH3·H2O → NH4^+）。</summary>
    public Dictionary<string, List<string>> BaseLadders { get; set; } = new(StringComparer.Ordinal);

    /// <summary>溶度积表达式（讲解/守恒栏用）：化学式主体 → "Ksp(AgCl) = c(Ag^+)·c(Cl^-)"。</summary>
    public Dictionary<string, string> KspExpressions { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>精确常数规格：mantissa × 10^exponent（如 1.75×10^-5）。</summary>
public sealed class ConstantSpec
{
    /// <summary>尾数（精确小数字符串，<see cref="CogniThink.Chem.Core.Math.Rational.Parse"/> 可解析）。</summary>
    public string Mantissa { get; set; } = "";

    /// <summary>10 的指数（可为负）。</summary>
    public int Exponent { get; set; }

    /// <summary>
    /// 转为精确有理数值（高中 Ka/Kb/Kw 量级安全；极端指数如 10^-50 会溢出 long，
    /// 此类比较请改用 <see cref="Ratio"/> 先约指数差）。
    /// </summary>
    public CogniThink.Chem.Core.Math.Rational ToRational()
    {
        var value = CogniThink.Chem.Core.Math.Rational.Parse(Mantissa);
        var ten = new CogniThink.Chem.Core.Math.Rational(10);
        for (var i = 0; i < System.Math.Abs(Exponent); i++)
        {
            value = Exponent > 0 ? checked(value * ten) : checked(value / ten);
        }

        return value;
    }

    /// <summary>
    /// 精确求商 a/b：先合并 10 的指数差再运算，避免中间量溢出
    /// （如 Ksp(AgCl)/Ksp(AgI)：指数差 7，直接相除会得到 10^34 量级分母）。
    /// </summary>
    public static CogniThink.Chem.Core.Math.Rational Ratio(ConstantSpec a, ConstantSpec b)
    {
        var value = CogniThink.Chem.Core.Math.Rational.Parse(a.Mantissa)
            / CogniThink.Chem.Core.Math.Rational.Parse(b.Mantissa);
        var ten = new CogniThink.Chem.Core.Math.Rational(10);
        var diff = a.Exponent - b.Exponent;
        for (var i = 0; i < System.Math.Abs(diff); i++)
        {
            value = diff > 0 ? checked(value * ten) : checked(value / ten);
        }

        return value;
    }
}

/// <summary>复分解反应发生条件（double-decomposition.json）。</summary>
public sealed class DoubleDecompositionData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    public List<Condition> Conditions { get; set; } = new();

    /// <summary>单个条件：沉淀/气体/弱电解质。</summary>
    public sealed class Condition
    {
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";
    }
}
