namespace CogniThink.TutorBrain.Application.Chemistry;

using System.Text.Json;
using System.Text.Json.Serialization;
using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>
/// 化学知识库：启动时加载 JSON 本体，建立别名索引，供推理服务内部查询。
/// 数据与代码分离，新增知识只需改 data/chemistry/*.json。
/// </summary>
public sealed class ChemicalKnowledgeBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, Substance> _substanceIndex = new();
    private readonly Dictionary<string, ChemicalIon> _ionIndex = new();
    private readonly List<Substance> _substances = new();
    private readonly List<ChemicalIon> _ions = new();
    private readonly List<Reaction> _reactions = new();
    private readonly List<IonInteraction> _interactions = new();
    private readonly List<KnowledgeSubstanceLink> _knowledgeLinks = new();
    private readonly SolubilityTable _solubility = new();
    private readonly Dictionary<string, List<Reaction>> _reactionsBySubstance = new();
    private readonly Dictionary<string, List<Reaction>> _reactionsByProduct = new();

    private static readonly HashSet<string> KnownKnowledgeIds = new()
    {
        "C-ELECTROLYTE", "C-ION-REACTION", "C-REDOX", "C-COEXIST", "C-FE", "C-NA"
    };

    public IReadOnlyList<Substance> Substances => _substances;
    public IReadOnlyList<ChemicalIon> Ions => _ions;
    public IReadOnlyList<Reaction> Reactions => _reactions;
    public IReadOnlyList<IonInteraction> Interactions => _interactions;
    public SolubilityTable Solubility => _solubility;

    /// <summary>加载与自检错误（为空表示知识库完整）</summary>
    public List<string> LoadErrors { get; } = new();

    public static ChemicalKnowledgeBase Load(string basePath)
    {
        var kb = new ChemicalKnowledgeBase();
        var dir = Path.Combine(basePath, "data", "chemistry");

        kb._substances.AddRange(ReadList<Substance>(Path.Combine(dir, "substances.json"), kb.LoadErrors));
        kb._ions.AddRange(ReadList<ChemicalIon>(Path.Combine(dir, "ions.json"), kb.LoadErrors));
        kb._reactions.AddRange(ReadList<Reaction>(Path.Combine(dir, "reactions.json"), kb.LoadErrors));
        kb._interactions.AddRange(ReadList<IonInteraction>(Path.Combine(dir, "ion-interactions.json"), kb.LoadErrors));
        kb._knowledgeLinks.AddRange(ReadList<KnowledgeSubstanceLink>(Path.Combine(dir, "knowledge-links.json"), kb.LoadErrors));

        var solubilityPath = Path.Combine(dir, "solubility.json");
        if (File.Exists(solubilityPath))
        {
            var table = ReadObject<SolubilityTable>(solubilityPath, kb.LoadErrors);
            if (table is not null)
            {
                kb._solubility.Rules.AddRange(table.Rules);
                kb._solubility.Exceptions.AddRange(table.Exceptions);
            }
        }

        kb.BuildIndexes();
        kb.Validate();
        return kb;
    }

    private static List<T> ReadList<T>(string path, List<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add($"缺少知识文件：{path}");
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), JsonOptions) ?? new List<T>();
        }
        catch (Exception ex)
        {
            errors.Add($"解析失败 {Path.GetFileName(path)}：{ex.Message}");
            return new List<T>();
        }
    }

    private static T? ReadObject<T>(string path, List<string> errors)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            errors.Add($"解析失败 {Path.GetFileName(path)}：{ex.Message}");
            return default;
        }
    }

    private void BuildIndexes()
    {
        foreach (var link in _knowledgeLinks)
        {
            foreach (var substanceId in link.SubstanceIds)
            {
                var substance = _substances.FirstOrDefault(s => s.Id == substanceId);
                if (substance is not null && !substance.KnowledgeIds.Contains(link.KnowledgeId))
                    substance.KnowledgeIds.Add(link.KnowledgeId);
            }
        }

        foreach (var s in _substances)
        {
            AddIndex(_substanceIndex, s.Id, s);
            AddIndex(_substanceIndex, s.Name, s);
            AddIndex(_substanceIndex, s.Formula, s);
            foreach (var alias in s.Aliases) AddIndex(_substanceIndex, alias, s);
        }

        foreach (var ion in _ions)
        {
            AddIndex(_ionIndex, ion.Id, ion);
            AddIndex(_ionIndex, ion.Name, ion);
            AddIndex(_ionIndex, ion.Formula, ion);
            foreach (var alias in ion.Aliases) AddIndex(_ionIndex, alias, ion);
        }

        foreach (var reaction in _reactions)
        {
            foreach (var reactant in reaction.Reactants)
            {
                if (!_reactionsBySubstance.TryGetValue(reactant, out var list))
                {
                    list = new List<Reaction>();
                    _reactionsBySubstance[reactant] = list;
                }
                list.Add(reaction);
            }

            foreach (var product in reaction.Products)
            {
                if (!_reactionsByProduct.TryGetValue(product, out var list))
                {
                    list = new List<Reaction>();
                    _reactionsByProduct[product] = list;
                }
                list.Add(reaction);
            }
        }
    }

    private void AddIndex<T>(Dictionary<string, T> index, string key, T value)
    {
        var normalized = NormalizeKey(key);
        if (normalized.Length == 0) return;
        index.TryAdd(normalized, value);
    }

    private void Validate()
    {
        foreach (var link in _knowledgeLinks)
        {
            if (!KnownKnowledgeIds.Contains(link.KnowledgeId))
                LoadErrors.Add($"物质绑定引用了不存在的知识点 {link.KnowledgeId}");

            foreach (var substanceId in link.SubstanceIds)
            {
                if (_substances.All(s => s.Id != substanceId))
                    LoadErrors.Add($"知识点 {link.KnowledgeId} 绑定了不存在的物质 {substanceId}");
            }
        }

        foreach (var reaction in _reactions)
        {
            foreach (var id in reaction.Reactants.Concat(reaction.Products))
            {
                if (!_substances.Any(s => s.Id == id))
                    LoadErrors.Add($"反应 {reaction.Id} 引用了不存在的物质 {id}");
            }
        }

        foreach (var interaction in _interactions)
        {
            foreach (var ion in interaction.Ions)
            {
                if (!_ions.Any(i => NormalizeKey(i.Formula) == NormalizeKey(ion) || NormalizeKey(i.Id) == NormalizeKey(ion)))
                    LoadErrors.Add($"离子规则 {interaction.Id} 引用了不存在的离子 {ion}");
            }
        }

        foreach (var ex in _solubility.Exceptions)
        {
            if (!_ions.Any(i => NormalizeKey(i.Formula) == NormalizeKey(ex.Cation)))
                LoadErrors.Add($"溶解度例外引用了不存在的阳离子 {ex.Cation}");
            if (!_ions.Any(i => NormalizeKey(i.Formula) == NormalizeKey(ex.Anion)))
                LoadErrors.Add($"溶解度例外引用了不存在的阴离子 {ex.Anion}");
        }
    }

    public Substance? FindSubstance(string key) =>
        _substanceIndex.TryGetValue(NormalizeKey(key), out var s) ? s : null;

    public ChemicalIon? FindIon(string key) =>
        _ionIndex.TryGetValue(NormalizeKey(key), out var i) ? i : null;

    public IReadOnlyList<Reaction> GetReactionsFor(string substanceId) =>
        _reactionsBySubstance.TryGetValue(substanceId, out var list) ? list : Array.Empty<Reaction>();

    /// <summary>以该物质为生成物的反应（逆向推理使用）</summary>
    public IReadOnlyList<Reaction> GetReactionsProducing(string substanceId) =>
        _reactionsByProduct.TryGetValue(substanceId, out var list) ? list : Array.Empty<Reaction>();

    public Substance? FindSubstanceById(string id) =>
        _substances.FirstOrDefault(s => s.Id == id);

    public IReadOnlyList<Reaction> GetReactionsForKnowledge(string knowledgeId) =>
        _reactions.Where(r => r.KnowledgeIds.Contains(knowledgeId)).ToList();

    /// <summary>汇总直接绑定物质，以及该知识点反应涉及的全部反应物和产物。</summary>
    public IReadOnlyList<Substance> GetSubstancesForKnowledge(string knowledgeId)
    {
        var ids = _substances
            .Where(s => s.KnowledgeIds.Contains(knowledgeId))
            .Select(s => s.Id)
            .ToHashSet();
        ids.UnionWith(GetReactionsForKnowledge(knowledgeId)
            .SelectMany(r => r.Reactants.Concat(r.Products)));
        return _substances.Where(s => ids.Contains(s.Id)).ToList();
    }

    public IReadOnlyList<IonInteraction> GetInteractionsFor(string ionFormula) =>
        _interactions.Where(i => i.Ions.Any(x => NormalizeKey(x) == NormalizeKey(ionFormula))).ToList();

    /// <summary>查溶解度：先查例外，再按阴离子规则，默认可溶</summary>
    public SolubilityLevel GetSolubility(string cation, string anion)
    {
        var ex = _solubility.Exceptions.FirstOrDefault(e =>
            NormalizeKey(e.Cation) == NormalizeKey(cation) &&
            NormalizeKey(e.Anion) == NormalizeKey(anion));
        if (ex is not null) return ex.Level;

        var rule = _solubility.Rules.FirstOrDefault(r => NormalizeKey(r.Anion) == NormalizeKey(anion));
        if (rule is null) return SolubilityLevel.Soluble;

        return rule.Cations.Any(c => NormalizeKey(c) == NormalizeKey(cation))
            ? SolubilityLevel.Insoluble
            : rule.Level;
    }

    /// <summary>规范化：小写、去空格、去 ^、常见上下标转数字</summary>
    public static string NormalizeKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var chars = input.Trim().Select(c => c switch
        {
            '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
            '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
            '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
            '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
            '⁺' => '+', '⁻' => '-',
            _ => c
        }).ToArray();

        return new string(chars)
            .ToLowerInvariant()
            .Replace("^", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("\u00a0", string.Empty);
    }
}
