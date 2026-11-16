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
