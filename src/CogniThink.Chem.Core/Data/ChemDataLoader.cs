using System.Text.Json;
using System.Text.Json.Serialization;
using CogniThink.Chem.Core.Evaluation;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Data;

/// <summary>声明式数据文件的外层结构，带版本号便于后续比对。</summary>
public sealed class DataFile<T>
{
    public string Version { get; set; } = "0.1";

    public List<T> Items { get; set; } = new();
}

/// <summary>加载后的数据集（当前阶段：知识、规则、规则回归用例）。</summary>
public sealed class ChemData
{
    public ChemData(KnowledgeBase knowledge, IReadOnlyList<Rule> rules, IReadOnlyList<RuleTestCase> ruleTests)
    {
        Knowledge = knowledge;
        Rules = rules;
        RuleTests = ruleTests;
    }

    public KnowledgeBase Knowledge { get; }

    public IReadOnlyList<Rule> Rules { get; }

    public IReadOnlyList<RuleTestCase> RuleTests { get; }
}

/// <summary>JSON 数据加载器。仅依赖 BCL（D1 约束：不引入第三方包）。</summary>
public static class ChemDataLoader
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static KnowledgeBase LoadKnowledge(string dataRoot)
    {
        var items = ReadItems<KnowledgeItem>(Path.Combine(dataRoot, "knowledge", "knowledge.json"));
        return new KnowledgeBase(items);
    }

    public static IReadOnlyList<Rule> LoadRules(string dataRoot) =>
        ReadItems<Rule>(Path.Combine(dataRoot, "rules", "rules.json"));

    public static IReadOnlyList<RuleTestCase> LoadRuleTests(string dataRoot) =>
        ReadItems<RuleTestCase>(Path.Combine(dataRoot, "rules", "rule-tests.json"));

    public static ChemData LoadAll(string? dataRoot = null)
    {
        var root = DataRoot.Resolve(dataRoot);
        return new ChemData(LoadKnowledge(root), LoadRules(root), LoadRuleTests(root));
    }

    public static List<T> ReadItems<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"数据文件不存在：{path}", path);
        }

        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<DataFile<T>>(json, Options);
        return file?.Items ?? new List<T>();
    }
}
