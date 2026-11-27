using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 选择项晶体类型真伪判定（FR-3）。查 structure-property-facts.json 晶体类型真值表，
/// 判定「X 属于/是/为 Y 晶体」类陈述。
/// 红线（不蒙数）：表外物质 / 无明确晶体类型断言 / 多物质或多类型歧义 → Undecided。
/// 共价晶体 ≡ 原子晶体（高中教材同义，归一化）。
/// </summary>
internal sealed class CrystalTypeStatementJudge : IStatementJudge
{
    private static readonly Regex TypeToken = new(
        @"原子晶体|共价晶体|分子晶体|离子晶体|金属晶体|混合型晶体|过渡晶体",
        RegexOptions.Compiled);

    private static readonly Lazy<StructurePropertyData?> Data = new(() =>
    {
        try { return RuleStore.Load().Query<StructurePropertyData>("structure-property-facts"); }
        catch { return null; }
    });

    public Verdict Judge(string stem, string statement)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(statement)) return Verdict.Undecided;
            var n = StatementNorm.Norm(statement);

            var types = TypeToken.Matches(n);
            if (types.Count != 1) return Verdict.Undecided; // 无断言或多类型歧义 → 不蒙数
            var claimed = StatementNorm.NormCrystalType(types[0].Value);

            var data = Data.Value;
            if (data is null || data.CrystalTypes.Count == 0) return Verdict.Undecided;

            CrystalTypeEntry? hit = null;
            var hits = 0;
            foreach (var e in data.CrystalTypes)
            {
                if (MatchesAny(n, e.Formula, e.Names))
                {
                    hit = e;
                    hits++;
                    if (hits > 1) return Verdict.Undecided; // 多物质歧义 → 不蒙数
                }
            }
            if (hit is null || hits != 1) return Verdict.Undecided;

            var trueType = StatementNorm.NormCrystalType(hit.Type);
            return string.Equals(trueType, claimed, StringComparison.Ordinal) ? Verdict.True : Verdict.False;
        }
        catch
        {
            return Verdict.Undecided;
        }
    }

    private static bool MatchesAny(string n, string formula, List<string> names)
    {
        if (ContainsToken(n, StatementNorm.Norm(formula))) return true;
        foreach (var nm in names)
            if (ContainsToken(n, StatementNorm.Norm(nm))) return true;
        return false;
    }

    // 化学式要求字母数字边界（避免 "C" 命中一切）；中文名直接子串匹配
    private static bool ContainsToken(string n, string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (Regex.IsMatch(key, @"[\u4e00-\u9fff]"))
            return n.Contains(key, StringComparison.Ordinal);
        var idx = n.IndexOf(key, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var beforeOk = idx == 0 || !StatementNorm.IsAsciiAlnum(n[idx - 1]);
            var after = idx + key.Length;
            var afterOk = after >= n.Length || !StatementNorm.IsAsciiAlnum(n[after]);
            if (beforeOk && afterOk) return true;
            idx = n.IndexOf(key, idx + 1, StringComparison.Ordinal);
        }
        return false;
    }
}
