using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 选择项显式键角数值真伪判定（FR-2）。复用 VseprSolver 的构型/孤对真值表。
/// 仅判「某微粒 键角 为 X°」类显式数值断言：查表得到标准键角，与声称值在容差内比较。
/// 红线（不蒙数）：比较型键角（含 &gt;/&lt;/比）、未知微粒、无显式数值 → Undecided。
/// 含孤对的微粒仅在有教材标准值覆盖表时才判；其余回退 Undecided。
/// </summary>
internal sealed class VseprBondAngleStatementJudge : IStatementJudge
{
    private static readonly Regex Reject = new(@"[<>＜＞]|比|大于|小于|比较|大小", RegexOptions.Compiled);

    // 含孤对微粒的教材标准键角（高中常考），键名与 VseprSolver 对照表一致（CanonSpecies 归一化后）
    private static readonly Dictionary<string, double> SpeciesAngle = new(StringComparer.Ordinal)
    {
        ["NH3"] = 107, ["PH3"] = 93, ["AsH3"] = 92,
        ["H2O"] = 104.5, ["H2S"] = 92, ["OF2"] = 103, ["SCl2"] = 103,
        ["SO2"] = 119, ["O3"] = 117, ["NO2-"] = 115, ["SnCl2"] = 95,
        ["PCl3"] = 100, ["NCl3"] = 107,
    };

    // 无孤对时由构型推出的标准键角（构型同义词串内包含关键词即命中）
    private static readonly (string Keyword, double Angle)[] ShapeAngleNoLone =
    {
        ("直线", 180),
        ("平面三角", 120),
        ("正四面体", 109.5), ("四面体", 109.5),
        ("平面正方形", 90),
        ("正八面体", 90),
    };

    private const double Tol = 2.5;

    public Verdict Judge(string stem, string statement)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(statement)) return Verdict.Undecided;
            var n = StatementNorm.Norm(statement);
            if (!n.Contains("键角")) return Verdict.Undecided;
            if (Reject.IsMatch(n)) return Verdict.Undecided;

            var claimed = ParseAngle(n);
            if (claimed is null) return Verdict.Undecided;

            var species = VseprSolver.FindKnownSpeciesInText(n);
            if (string.IsNullOrEmpty(species)) return Verdict.Undecided;

            if (!TryGetCanonicalAngle(species, out var canonical)) return Verdict.Undecided;

            return Math.Abs(claimed.Value - canonical) <= Tol ? Verdict.True : Verdict.False;
        }
        catch
        {
            return Verdict.Undecided;
        }
    }

    private static bool TryGetCanonicalAngle(string species, out double angle)
    {
        angle = 0;
        if (SpeciesAngle.TryGetValue(species, out var a)) { angle = a; return true; }
        if (!VseprSolver.TryGetShapeInfo(species, out var shape, out var lp)) return false;
        if (lp != 0) return false; // 含孤对且无覆盖表 → 不蒙数
        foreach (var (kw, ang) in ShapeAngleNoLone)
        {
            if (shape.Contains(kw, StringComparison.Ordinal)) { angle = ang; return true; }
        }
        return false; // 构型无标准角映射（如三角双锥）→ 不蒙数
    }

    private static double? ParseAngle(string n)
    {
        // 优先 120° / 109°28′ / 104.5°；其次 120度
        var m = Regex.Match(n, @"(\d+(?:\.\d+)?)\s*°(?:\s*(\d+)\s*[′'])?");
        if (!m.Success) m = Regex.Match(n, @"(\d+(?:\.\d+)?)\s*度");
        if (!m.Success) return null;
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var deg)) return null;
        if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var min)) deg += min / 60.0;
        return deg;
    }
}
