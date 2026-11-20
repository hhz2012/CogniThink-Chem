using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Molecules;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Task 40：有机结构图像选择题逐选项校验——用 molecule 资产的 RDKit 真值判定选项。
/// <para>真值源：<see cref="MoleculeAnalyzer.Analyze"/>（分子式/不饱和度/官能团/手性碳/等效氢/杂化碳/加成 H₂/极性）。</para>
/// <para>红线（不蒙数、零假阳性）：仅当四个选项<b>全部</b>能干净归入支持的断言类型且候选唯一 → 返回答案字母；
/// 任一选项断言无法归入支持类型 → 整题返回 null。共平面/共线几何、红外光谱、同分异构推理等不覆盖（诚实 null）。</para>
/// </summary>
public static class MoleculeChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】有机结构判定基于 molecule 资产的 RDKit 真值（分子式/官能团/手性碳/等效氢/杂化/加成/不饱和度/极性）；" +
        "四个选项必须全部可解析为上述断言；共平面/共线几何、红外波数、同分异构推理不臆断。";

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        // 守卫1：存在 valid 的 molecule 资产，能解析出 smiles 或 molblock
        var asset = imageAssets?.FirstOrDefault(a => a.Type == "molecule");
        if (asset is null) return null;
        var payload = asset.Payload;
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object) return null;
        var p = payload.Value;
        if (p.TryGetProperty("valid", out var vp) && vp.ValueKind == JsonValueKind.False) return null;
        string? smiles = null, molblock = null;
        if (p.TryGetProperty("smiles", out var sp) && sp.ValueKind == JsonValueKind.String) smiles = sp.GetString();
        if (p.TryGetProperty("molblock", out var mp) && mp.ValueKind == JsonValueKind.String) molblock = mp.GetString();
        if (string.IsNullOrWhiteSpace(smiles) && string.IsNullOrWhiteSpace(molblock)) return null;

        var analysis = MoleculeAnalyzer.Analyze(smiles, molblock);
        if (!analysis.Success) return null;

        // 守卫2：题干指向该图（含「如图」「结构简式」「所示」等），避免误伤无图的纯文字有机题
        var raw = req.RawText ?? "";
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count != 4 || !"ABCD".All(c => opts.ContainsKey(c.ToString()))) return null;
        if (!Regex.IsMatch(stem, @"如图|下图|结构简式|所示|分子结构|结构模型")) return null;

        var askDir = AskDir(stem);
        if (askDir == "?") return null;

        // 逐选项判定：四选项必须全部可判
        var verdicts = new List<(string letter, bool ok)>();
        var reasons = new List<string>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            var v = JudgeOption(kv.Value, analysis);
            if (v is null) return null; // 任一不可判 → 整题 null（不蒙数）
            verdicts.Add((kv.Key, v.Value));
            reasons.Add($"{kv.Key}：{kv.Value}（真值 {(v.Value ? "真" : "假")}）");
        }
        return ChooseUnique(verdicts, askDir, reasons);
    }

    /// <summary>判定一个选项的真伪；不可判（无法归入支持断言类型 / 断言歧义）返回 null。</summary>
    private static bool? JudgeOption(string text, MoleculeAnalysisResult r)
    {
        var hits = new List<bool>();

        // 1. 分子式
        var f = MatchFormula(text);
        if (f != null) hits.Add(FormulaEqual(f, r.MolecularFormula));

        // 2. 手性碳数
        var chiral = MatchInt(text, @"(\d+)\s*个手性碳(?:原子)?|手性碳(?:原子)?(?:有|为|是|共)?\s*(\d+)\s*个");
        if (chiral != null) hits.Add(chiral == r.ChiralCenterCount);

        // 3. 等效氢 / 一氯代物 / 氢谱组数
        var eqh = MatchInt(text, @"(?:一氯代物|等效氢|(?:核磁共振)?氢谱)[^。；]{0,10}?(\d+)\s*(?:种|组)|(\d+)\s*(?:种|组)(?:一氯代物|等效氢|氢谱峰)");
        if (eqh != null) hits.Add(eqh == r.EquivalentHydrogenGroups);

        // 4. sp² 杂化碳数
        var sp2 = MatchInt(text, @"sp[²2]\s*杂化(?:的)?碳(?:原子)?(?:有|为|是|共)?\s*(\d+)\s*个");
        if (sp2 != null) hits.Add(sp2 == r.Sp2CarbonCount);

        // 5. 可加成 H₂ 数
        var add = MatchInt(text, @"(?:与|加成)\s*(\d+)\s*(?:mol\s*)?H[₂2]|(\d+)\s*mol\s*H[₂2](?:发生)?(?:加成)?");
        if (add != null) hits.Add(add == r.AddableH2Count);

        // 6. 不饱和度
        var dou = MatchInt(text, @"不饱和度(?:为|是|Ω|Ω为)?\s*(\d+)|Ω\s*[=＝]\s*(\d+)");
        if (dou != null) hits.Add(dou == r.DegreeOfUnsaturation);

        // 7. 极性
        var polar = MatchPolar(text);
        if (polar != null) hits.Add(polar == r.IsPolar);

        // 8. 官能团存在
        var fg = MatchFunctionalGroup(text, r.FunctionalGroups);
        if (fg != null) hits.Add(fg.Value);

        // 恰好一个断言命中才判定；零命中或多命中（歧义）→ null
        return hits.Count == 1 ? hits[0] : null;
    }

    // ===================== 断言匹配器 =====================

    private static readonly Regex FormulaTokenRe = new(@"([A-Z][a-z]?)([₀-₉0-9]*)", RegexOptions.Compiled);

    private static string NormalizeSubscripts(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c >= '₀' && c <= '₉') sb.Append((char)('0' + (c - '₀')));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static Dictionary<string, int>? ParseFormula(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = NormalizeSubscripts(s).Replace(" ", "");
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in FormulaTokenRe.Matches(s))
        {
            var el = m.Groups[1].Value;
            var cnt = m.Groups[2].Value;
            var n = string.IsNullOrEmpty(cnt) ? 1 : int.Parse(cnt);
            if (!dict.ContainsKey(el)) dict[el] = 0;
            dict[el] += n;
        }
        if (dict.Count == 0) return null;
        return dict;
    }

    /// <summary>从选项中提取声称的分子式（须含「分子式」关键词，避免误读其他数字）。</summary>
    private static string? MatchFormula(string text)
    {
        var m = Regex.Match(text, @"分子式(?:为|是|：|:)?\s*([A-Z][a-z]?[₀-₉0-9]*)+");
        if (!m.Success) return null;
        // 取关键词后的完整分子式串
        var start = m.Index + m.Value.Length;
        // 简化：用正则再抓一次紧跟的元素+数字序列
        var m2 = Regex.Match(text.Substring(m.Index), @"([A-Z][a-z]?[₀-₉0-9]*)+");
        return m2.Success ? m2.Value : null;
    }

    private static bool FormulaEqual(string? claimed, string rdkit)
    {
        var a = ParseFormula(claimed ?? "");
        var b = ParseFormula(rdkit);
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
            if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
        return true;
    }

    /// <summary>从文本提取第一个整数；支持两个捕获组（取非空者）。</summary>
    private static int? MatchInt(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        if (!m.Success) return null;
        foreach (Group g in m.Groups)
        {
            if (g.Success && int.TryParse(g.Value, out var n)) return n;
        }
        return null;
    }

    private static bool? MatchPolar(string text)
    {
        if (Regex.IsMatch(text, @"非极性分子")) return false;
        if (Regex.IsMatch(text, @"(?<!非)极性分子")) return true;
        return null;
    }

    private static bool? MatchFunctionalGroup(string text, IReadOnlyList<string> groups)
    {
        // 声称「含有/有 X」→ X 必须在官能团列表；声称「不含/没有 X」→ 不在列表
        var names = new[] { "羧基", "羟基", "醛基", "酮羰基", "酯基", "醚键", "氨基", "硝基", "卤原子", "碳碳双键", "碳碳三键", "苯环" };
        foreach (var name in names)
        {
            // 先判否定（不含有/不含/没有/无），避免「不含有 X」被「含有」子串误命中
            var mNeg = Regex.Match(text, $@"(?:不含有|不含|没有|无)(\d{{0,2}}个)?{Regex.Escape(name)}");
            if (mNeg.Success) return !groups.Contains(name);
            // 再判肯定（含/含有/有）
            var mPos = Regex.Match(text, $@"(?:含有|含|有)(\d{{0,2}}个)?{Regex.Escape(name)}");
            if (mPos.Success) return groups.Contains(name);
        }
        return null;
    }

    // ===================== 公共工具（与其它校验器同口径） =====================

    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确|不相符|有误")) return "wrong";
        if (Regex.IsMatch(stem, @"正确|符合|准确")) return "correct";
        return "?";
    }

    private static ChoiceVerifier.Outcome? ChooseUnique(List<(string letter, bool ok)> verdicts, string askDir, List<string> reasons)
    {
        var wantTrue = askDir == "correct";
        var candidates = verdicts.Where(v => v.ok == wantTrue).Select(v => v.letter).ToList();
        if (candidates.Count != 1) return null;
        var letter = candidates[0];
        var dirWord = wantTrue ? "正确选项" : "错误选项";
        return new ChoiceVerifier.Outcome(letter, $"{dirWord}为 {letter}", reasons, ConstraintNote);
    }
}
