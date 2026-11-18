namespace CogniThink.TutorBrain.Application.Chemistry;

using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>
/// 题目编译器：把题干翻译成内部结构化查询（物质、离子、条件、问法）。
/// 只做解析与归一化，不在这里下结论；结论由 ChemicalInferenceService 查知识库得出。
/// </summary>
public sealed class ProblemQueryCompiler
{
    private readonly ChemicalKnowledgeBase _kb;

    public ProblemQueryCompiler(ChemicalKnowledgeBase kb) => _kb = kb;

    public ChemicalQuery Compile(string stem)
    {
        var normalized = ChemicalKnowledgeBase.NormalizeKey(stem);
        var ions = MatchIons(stem, normalized);

        // 先从题干中抹掉已识别的离子（化学式与中文名），避免 "Fe2+" 里的 "Fe" 再被当成物质
        var residueStem = stem;
        var residue = normalized;
        foreach (var ionFormula in ions)
        {
            var ion = _kb.FindIon(ionFormula);
            if (ion is null) continue;

            residue = residue.Replace(ChemicalKnowledgeBase.NormalizeKey(ionFormula), string.Empty, StringComparison.Ordinal);
            residueStem = residueStem.Replace(ion.Formula, string.Empty, StringComparison.OrdinalIgnoreCase)
                                     .Replace(ion.Name, string.Empty);
            foreach (var alias in ion.Aliases)
                residueStem = residueStem.Replace(alias, string.Empty);
        }

        // “水浴”是反应条件，不把其中的“水”误识别为反应物 H2O。
        var substanceStem = residueStem.Replace("水浴", string.Empty);
        var substanceNormalized = ChemicalKnowledgeBase.NormalizeKey(substanceStem);

        return new ChemicalQuery
        {
            Type = DetectType(stem),
            Substances = MatchSubstances(substanceStem, substanceNormalized),
            Ions = ions,
            Acidic = stem.Contains("酸性"),
            Alkaline = stem.Contains("碱性"),
            Heated = stem.Contains("加热") || stem.Contains("高温") || stem.Contains("灼烧"),
            Excess = stem.Contains("过量") || stem.Contains("足量"),
            RawStem = stem
        };
    }

    private List<string> MatchSubstances(string stem, string normalized)
    {
        var hits = new List<(string Id, string Matched, int Length)>();

        foreach (var substance in _kb.Substances)
        {
            var matched = FindMatch(stem, normalized, substance.Name, substance.Formula, substance.Aliases);
            if (matched is not null)
                hits.Add((substance.Id, matched, matched.Length));
        }

        // 去掉被更长匹配包含的短化学式，避免 "NaCl" 同时命中 "Na" 和 "Cl"
        return hits
            .Where(h => !hits.Any(other =>
                other.Length > h.Length && other.Matched.Contains(h.Matched, StringComparison.OrdinalIgnoreCase)))
            .Select(h => h.Id)
            .Distinct()
            .ToList();
    }

    private List<string> MatchIons(string stem, string normalized)
    {
        var hits = new List<(string Formula, string Matched, int Length)>();

        foreach (var ion in _kb.Ions)
        {
            var matched = FindMatch(stem, normalized, ion.Name, ion.Formula, ion.Aliases);
            if (matched is not null)
                hits.Add((ion.Formula, matched, matched.Length));
        }

        return hits
            .Where(h => !hits.Any(other =>
                other.Length > h.Length && other.Matched.Contains(h.Matched, StringComparison.OrdinalIgnoreCase)))
            .Select(h => h.Formula)
            .Distinct()
            .ToList();
    }

    /// <summary>中文按原文匹配，化学式按规范化文本匹配</summary>
    private static string? FindMatch(string stem, string normalizedStem, string name, string formula, List<string> aliases)
    {
        if (!string.IsNullOrWhiteSpace(name) && stem.Contains(name)) return name;

        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias) && stem.Contains(alias)) return alias;
        }

        if (string.IsNullOrWhiteSpace(formula)) return null;

        var normalizedFormula = ChemicalKnowledgeBase.NormalizeKey(formula);
        if (normalizedFormula.Length < 2) return null;

        // 纯字母且过短的化学式（如 Fe、Na）容易误匹配，要求题干中作为独立片段出现
        if (normalizedFormula.Length == 2 && normalizedFormula.All(char.IsLetter))
        {
            var index = normalizedStem.IndexOf(normalizedFormula, StringComparison.Ordinal);
            while (index >= 0)
            {
                var before = index == 0 ? ' ' : normalizedStem[index - 1];
                var afterIndex = index + normalizedFormula.Length;
                var after = afterIndex >= normalizedStem.Length ? ' ' : normalizedStem[afterIndex];
                // 前后都不能紧邻字母，避免 "Na" 命中 "NaCl"；数字与符号允许（如 2Fe、Fe3+）
                if (!char.IsLetter(before) && !char.IsLetter(after)) return formula;
                index = normalizedStem.IndexOf(normalizedFormula, index + 1, StringComparison.Ordinal);
            }
            return null;
        }

        return normalizedStem.Contains(normalizedFormula) ? formula : null;
    }

    private static ChemicalQueryType DetectType(string stem)
    {
        if (stem.Contains("共存")) return ChemicalQueryType.Coexistence;
        if (stem.Contains("鉴别") || stem.Contains("区分") || stem.Contains("检验")) return ChemicalQueryType.Identification;
        if (stem.Contains("颜色") || stem.Contains("色")) return ChemicalQueryType.Color;
        if (stem.Contains("溶解度") || stem.Contains("难溶") || stem.Contains("易溶") || stem.Contains("微溶")) return ChemicalQueryType.Solubility;
        if (stem.Contains("方程式") || stem.Contains("配平")) return ChemicalQueryType.Equation;
        if (stem.Contains("现象")) return ChemicalQueryType.Inference;
        if (stem.Contains("推断") || stem.Contains("可能含") || stem.Contains("一定含")) return ChemicalQueryType.Inference;
        return ChemicalQueryType.Unknown;
    }
}
