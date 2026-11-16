using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.Chem.Core.Ionic;

/// <summary>
/// 离子方程式校验器。只做确定性校验，不预测产物、不推断反应。
/// 检查管线（按序写入 <see cref="CheckReport"/>）：
/// <list type="number">
/// <item>元素守恒 / 电荷守恒（复用 <see cref="EquationBalancer.ConservationViolations"/>，
/// 按学生书写系数检查）。</item>
/// <item>离子共存与弱电解质拆写：H^+/OH^-、NH4^+/OH^-、H^+/弱酸根 同侧组合检查；
/// 强酸、强碱、可溶盐以分子形式出现时标"应拆"。</item>
/// <item>状态符号：↓ 按溶解性表校验（可溶标 ↓ 错、难溶漏 ↓ 错、微溶只提示），
/// ↑ 按气体清单校验；↑↓ 只允许标在生成物后。</item>
/// </list>
/// 高中适用边界：水溶液中的离子方程式。非水体系、熔融态、胶体制备的语境差异引擎不裁决。
/// 易错点：
/// <list type="bullet">
/// <item>微溶物（Ca(OH)2、CaSO4、Ag2SO4、MgCO3、PbCl2）拆与不拆、标不标 ↓ 视语境，引擎只提示不判错。</item>
/// <item>可逆号（⇌）视为电离/水解平衡语境：生成物共存检查跳过，漏标 ↑↓ 降级为提示
/// （水解程度小，教材不标沉淀/气体符号）。</item>
/// <item>单质（如置换出的 Cu）高中通常不标 ↓，引擎只提示不判错。</item>
/// </list>
/// </summary>
public static class IonicEquationChecker
{
    /// <summary>
    /// 校验离子方程式字符串。
    /// </summary>
    /// <param name="equation">
    /// 方程式，如 "Ba^2+ + SO4^2- = BaSO4↓"、"CH3COOH + OH^- = CH3COO^- + H2O"。
    /// </param>
    /// <returns>
    /// 统一校验报告。解析失败时 <see cref="CheckReport.ErrorMessage"/> 非空；
    /// 输入不含离子时按化学方程式处理并给出提示项（不做拆写/状态符号检查）。
    /// </returns>
    public static CheckReport Check(string equation)
    {
        var report = new CheckReport();
        if (!EquationParser.TryParse(equation, out var parsed, out var error))
        {
            report.ErrorMessage = error!;
            report.AddFail("方程式解析", error!, RiskTags.ParseError);
            return report;
        }

        return Check(parsed!);
    }

    /// <summary>校验已解析的方程式（供统一入口复用解析结果）。</summary>
    public static CheckReport Check(ParsedEquation equation)
    {
        var report = new CheckReport();
        if (!equation.HasIons)
        {
            report.AddPass("离子方程式判定", "未检测到离子，按化学方程式处理，不适用离子拆写校验");
            return report;
        }

        CheckConservation(equation, report);
        CheckCoexistence(equation, report);
        CheckSplitting(equation, report);
        CheckStateSymbols(equation, report);
        return report;
    }

    /// <summary>元素守恒与电荷守恒：逐条违规单独成项，便于伴思层定位具体元素。</summary>
    private static void CheckConservation(ParsedEquation equation, CheckReport report)
    {
        var violations = EquationBalancer.ConservationViolations(equation);
        if (violations.Count == 0)
        {
            report.AddPass("元素与电荷守恒");
            return;
        }

        foreach (var violation in violations)
        {
            var name = violation.StartsWith("电荷", StringComparison.Ordinal) ? "电荷守恒" : "元素守恒";
            report.AddFail(name, violation, RiskTags.ConservationViolation);
        }
    }

    /// <summary>
    /// 离子共存检查。
    /// 反应物侧：不能共存的离子对同时出现时，生成物必须给出其结合产物
    /// （如 H^+ + OH^- → H2O；CO3^2- + H^+ → H2CO3/HCO3^-/CO2）。
    /// 生成物侧（仅不可逆方程）：不能共存的离子对不得同时作为生成物——
    /// 可逆号语境（弱电解质电离、水解平衡）允许共轭对同时存在，跳过。
    /// </summary>
    private static void CheckCoexistence(ParsedEquation equation, CheckReport report)
    {
        var problems = new List<string>();
        var left = equation.Left.Select(t => t.Species.ToString()).ToHashSet(StringComparer.Ordinal);
        var right = equation.Right.Select(t => t.Species.ToString()).ToHashSet(StringComparer.Ordinal);

        ReactantPair(left, right, "H^+", "OH^-", new[] { "H2O" }, problems);
        ReactantPair(left, right, "NH4^+", "OH^-", new[] { "NH3·H2O", "NH3" }, problems);
        foreach (var (radical, conjugates) in ElectrolyteRules.WeakAcidRadicalConjugates)
        {
            ReactantPair(left, right, "H^+", radical, conjugates, problems);
        }

        if (!equation.Reversible)
        {
            ProductPair(right, "H^+", "OH^-", problems);
            ProductPair(right, "NH4^+", "OH^-", problems);
            foreach (var radical in ElectrolyteRules.WeakAcidRadicals)
            {
                ProductPair(right, "H^+", radical, problems);
            }
        }

        if (problems.Count == 0)
        {
            report.AddPass("离子共存", "未发现同侧不能共存的离子组合");
            return;
        }

        foreach (var problem in problems)
        {
            report.AddFail("离子共存", problem, RiskTags.IonicNotationError);
        }
    }

    /// <summary>
    /// 强电解质拆写检查：离子方程式中强酸、强碱、可溶盐必须拆成离子。
    /// 判定顺序（关键，避免误判）：弱电解质 → 强酸 → 强碱 → 单质 → 氧化物 → 溶解性表。
    /// 微溶物与表外物质只提示、不判错。
    /// </summary>
    private static void CheckSplitting(ParsedEquation equation, CheckReport report)
    {
        var problems = new List<string>();
        var notes = new List<string>();

        foreach (var term in equation.AllTerms)
        {
            var species = term.Species;
            if (species.Charge != 0)
            {
                continue; // 已是离子形式
            }

            var body = species.Body;
            if (ElectrolyteRules.WeakElectrolytes.Contains(body))
            {
                continue; // 弱电解质保留化学式，正确
            }

            if (ElectrolyteRules.StrongAcids.Contains(body))
            {
                problems.Add($"{body} 是强酸，在离子方程式中应拆写为离子");
                continue;
            }

            if (ElectrolyteRules.StrongBases.Contains(body))
            {
                switch (ElectrolyteRules.LookupSolubility(body))
                {
                    case Solubility.Soluble:
                        problems.Add($"{body} 是强碱且可溶，在离子方程式中应拆写为离子");
                        break;
                    case Solubility.SlightlySoluble:
                        notes.Add($"{body} 微溶：澄清溶液应拆、浊液不拆，引擎不结合语境判断");
                        break;
                }

                continue;
            }

            if (species.Elements.Count == 1)
            {
                continue; // 单质不拆
            }

            if (ElectrolyteRules.CommonOxides.Contains(body))
            {
                continue; // 氧化物保留化学式
            }

            switch (ElectrolyteRules.LookupSolubility(body))
            {
                case Solubility.Soluble:
                    problems.Add($"{body} 是可溶性盐，在离子方程式中应拆写为离子");
                    break;
                case Solubility.SlightlySoluble:
                    notes.Add($"{body} 微溶，拆写视语境而定，引擎不判断");
                    break;
                case null:
                    notes.Add($"{body} 不在电解质规则表中，引擎不判断其拆写");
                    break;
                // Insoluble：难溶物保留化学式，正确
            }
        }

        foreach (var problem in problems)
        {
            report.AddFail("强电解质拆写", problem, RiskTags.IonicNotationError);
        }

        if (problems.Count == 0)
        {
            report.AddPass("强电解质拆写", notes.Count == 0 ? "未发现应拆未拆项" : string.Join("；", notes));
        }
    }

    /// <summary>
    /// 状态符号（↑↓）检查：
    /// 反应物不得标注；生成物中气体漏 ↑ / 非气体标 ↑、难溶物漏 ↓ / 可溶物标 ↓ 均判错；
    /// 微溶物、单质、表外物质只提示。可逆方程中漏标降级为提示（水解/电离平衡语境）。
    /// </summary>
    private static void CheckStateSymbols(ParsedEquation equation, CheckReport report)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        var missingIsError = !equation.Reversible;

        foreach (var term in equation.Left)
        {
            if (term.State != null)
            {
                problems.Add($"反应物 {term.Species} 不应标注 {term.State}（↑↓只标在生成物后）");
            }
        }

        foreach (var term in equation.Right)
        {
            var species = term.Species;
            var body = species.Body;
            var isGas = ElectrolyteRules.Gases.Contains(body);

            if (term.State == "↑")
            {
                if (!isGas)
                {
                    problems.Add($"{body} 不是气体，不应标 ↑");
                }

                continue;
            }

            if (isGas)
            {
                if (term.State == null)
                {
                    ReportOrNote(missingIsError, problems, notes, $"生成物 {body} 是气体，应标 ↑");
                }

                continue;
            }

            if (species.Elements.Count == 1)
            {
                if (term.State == "↓")
                {
                    notes.Add($"生成物 {body} 是单质，高中通常不对单质标 ↓，引擎不判错");
                }

                continue;
            }

            var solubility = ElectrolyteRules.LookupSolubility(body);
            if (term.State == "↓")
            {
                switch (solubility)
                {
                    case Solubility.Soluble:
                        problems.Add($"{body} 可溶于水，不应标 ↓");
                        break;
                    case Solubility.SlightlySoluble:
                        notes.Add($"{body} 微溶，是否标 ↓ 视语境而定，引擎不判断");
                        break;
                    case null:
                        notes.Add($"{body} 不在溶解性表中，引擎不判断 ↓ 是否正确");
                        break;
                    // Insoluble：难溶物标 ↓，正确
                }
            }
            else if (solubility == Solubility.Insoluble)
            {
                ReportOrNote(missingIsError, problems, notes, $"生成物 {body} 难溶于水，应标 ↓");
            }
        }

        foreach (var problem in problems)
        {
            report.AddFail("状态符号", problem, RiskTags.StateSymbolError);
        }

        if (problems.Count == 0)
        {
            report.AddPass("状态符号", notes.Count == 0 ? "↑↓ 使用正确" : string.Join("；", notes));
        }
    }

    /// <summary>按语境将问题写入失败列表或降级为提示。</summary>
    private static void ReportOrNote(bool isError, List<string> problems, List<string> notes, string message)
    {
        if (isError)
        {
            problems.Add(message);
        }
        else
        {
            notes.Add(message + "（可逆/平衡语境，仅提示）");
        }
    }

    /// <summary>
    /// 反应物侧共存检查：a、b 同在反应物一侧时，生成物必须出现 conjugates 之一，
    /// 否则说明未写出二者的反应（或弱电解质被错误拆写）。
    /// </summary>
    private static void ReactantPair(
        HashSet<string> left,
        HashSet<string> right,
        string a,
        string b,
        string[] conjugates,
        List<string> problems)
    {
        if (!left.Contains(a) || !left.Contains(b))
        {
            return;
        }

        if (conjugates.Any(right.Contains))
        {
            return;
        }

        problems.Add($"{a} 与 {b} 同在反应物一侧，应发生反应，但生成物中未见 {string.Join("、", conjugates)}");
    }

    /// <summary>生成物侧共存检查：不能共存的离子对不得同时作为生成物。</summary>
    private static void ProductPair(HashSet<string> right, string a, string b, List<string> problems)
    {
        if (right.Contains(a) && right.Contains(b))
        {
            problems.Add($"{a} 与 {b} 不能共存，不应同时作为生成物");
        }
    }
}
