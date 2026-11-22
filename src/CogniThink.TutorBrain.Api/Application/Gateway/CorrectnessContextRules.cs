using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Data;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 方程式"书写正误"判定的语境规则表（阶段3D / AC-14）。
/// <para>规则数据在 <c>data/knowledge/correctness-context-rules.json</c>（化学事实只存 JSON；本文件仅含
/// 通用文本归一化与匹配循环，无硬编码化学真值）。</para>
/// <para>每条规则：仅当条件全部命中才返回一条 × 理由 + 稳定 id；否则返回 null（不影响引擎判定）。
/// 规则未命中时，外层 <see cref="EquationCorrectnessJudge"/> 按引擎结果判 √/null，绝不臆断。</para>
/// <para>新增规则铁律：必须能写出不依赖具体题号的化学条件、配单测、且全量评估 harm=0；否则不得加入。</para>
/// </summary>
internal static class CorrectnessContextRules
{
    private sealed class CtxRule
    {
        public string Id { get; set; } = "";
        public List<string> EqContainsAll { get; set; } = new();
        public List<string> EqContainsAny { get; set; } = new();
        public List<string> HayContainsAny { get; set; } = new();
        public List<string> CtxContainsAll { get; set; } = new();
        public string? Regex { get; set; }
        public string Reason { get; set; } = "";
    }

    private sealed class CtxRulesData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<CtxRule> Rules { get; set; } = new();
    }

    private static readonly Lazy<List<CtxRule>> Rules = new(LoadRules);

    private static List<CtxRule> LoadRules()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "knowledge", "correctness-context-rules.json");
            if (!File.Exists(path)) return new List<CtxRule>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var data = JsonSerializer.Deserialize<CtxRulesData>(json, opts);
            return data?.Rules ?? new List<CtxRule>();
        }
        catch
        {
            return new List<CtxRule>();
        }
    }

    /// <summary>命中确定性语境错误时返回 (规则 id, 理由)；否则 (null, null)。</summary>
    public static (string? Id, string? Reason) EvaluateWithId(string normalizedEquation, string? context)
    {
        var eq = normalizedEquation ?? string.Empty;
        // 语境也归一化 Unicode 上下标，便于检索
        var ctx = NormalizeForSearch(context);
        var hay = ctx + " " + eq;

        foreach (var rule in Rules.Value)
        {
            if (rule.EqContainsAll.Count > 0 && !rule.EqContainsAll.All(t => Contains(eq, t))) continue;
            if (rule.EqContainsAny.Count > 0 && !rule.EqContainsAny.Any(t => Contains(eq, t))) continue;
            if (rule.HayContainsAny.Count > 0 && !rule.HayContainsAny.Any(t => Contains(hay, t))) continue;
            if (rule.CtxContainsAll.Count > 0 && !rule.CtxContainsAll.All(t => Contains(ctx, t))) continue;
            if (!string.IsNullOrEmpty(rule.Regex) && !Regex.IsMatch(eq, rule.Regex)) continue;
            return (rule.Id, rule.Reason);
        }
        return (null, null);
    }

    /// <summary>
    /// 命中确定性语境错误时返回理由；否则 null。
    /// 行为字节级不变（内部委托 <see cref="EvaluateWithId"/> 丢弃 id，既有消费者零改动）。
    /// </summary>
    public static string? Evaluate(string normalizedEquation, string? context)
        => EvaluateWithId(normalizedEquation, context).Reason;

    private static bool Contains(string s, string needle) =>
        s.Contains(needle, StringComparison.Ordinal);

    /// <summary>把 Unicode 上下标转为普通 ASCII（仅用于文本检索，不加 caret）。</summary>
    private static string NormalizeForSearch(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c >= '₀' && c <= '₉') sb.Append((char)('0' + (c - '₀')));
            else if (c >= '⁰' && c <= '⁹') sb.Append((char)('0' + (c - '⁰')));
            else if (c == '⁺') sb.Append('+');
            else if (c == '⁻') sb.Append('-');
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
