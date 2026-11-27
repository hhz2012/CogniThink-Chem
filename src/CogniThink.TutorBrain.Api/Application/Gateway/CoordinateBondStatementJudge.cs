using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 选择项配位键真伪判定（FR-4）。查 structure-property-facts.json 含配位键物种真值表，
/// 判定声称某物种含/形成配位键（或明确给体→受体）的陈述。
/// 红线（不蒙数）：表外物种 / 无明确配位键断言 / 多物种歧义 → Undecided。
/// </summary>
internal sealed class CoordinateBondStatementJudge : IStatementJudge
{
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
            if (!n.Contains("配位键")) return Verdict.Undecided;

            var data = Data.Value;
            if (data is null || data.CoordinateBonds.Count == 0) return Verdict.Undecided;

            CoordinateBondEntry? hit = null;
            var hits = 0;
            foreach (var e in data.CoordinateBonds)
            {
                if (MatchesEntry(n, e))
                {
                    hit = e;
                    hits++;
                    if (hits > 1) return Verdict.Undecided; // 多物种歧义 → 不蒙数
                }
            }
            if (hit is null || hits != 1) return Verdict.Undecided;

            // 断言方向：配位键前 4 字内出现 不/没/无/未 → 否定断言
            var negative = Regex.IsMatch(n, @"(不|没|无|未).{0,4}配位键");
            var trueHas = hit.HasCoordinateBond;
            var claimHas = !negative;
            return claimHas == trueHas ? Verdict.True : Verdict.False;
        }
        catch
        {
            return Verdict.Undecided;
        }
    }

    private static bool MatchesEntry(string n, CoordinateBondEntry e)
    {
        if (ContainsToken(n, StatementNorm.Norm(e.Match))) return true;
        if (ContainsToken(n, StatementNorm.Norm(e.Species))) return true;
        foreach (var nm in e.Names)
            if (ContainsToken(n, StatementNorm.Norm(nm))) return true;
        return false;
    }

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
