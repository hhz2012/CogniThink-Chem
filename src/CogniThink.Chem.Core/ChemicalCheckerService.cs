using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Ionic;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core;

/// <summary>方程式类型模式。</summary>
public enum EquationMode
{
    /// <summary>自动：含离子则按离子方程式走离子规则管线，否则按化学方程式。</summary>
    Auto,

    /// <summary>强制按化学（分子）方程式：只跑守恒与配平。</summary>
    Molecular,

    /// <summary>强制按离子方程式：追加离子拆写与状态符号规则。</summary>
    Ionic,

    /// <summary>
    /// 电极反应式（半反应）：只跑白名单/守恒/配平，并校验电子项存在；
    /// 跳过离子拆写、半反应拆解与反应库一致性（三者都以完整反应为对象，不适用于半反应）。
    /// </summary>
    HalfReaction
}

/// <summary>
/// 统一校验入口请求。
/// </summary>
public sealed class CheckRequest
{
    /// <summary>方程式字符串（如 "2H2 + O2 =点燃= 2H2O"、"Ba^2+ + SO4^2- = BaSO4↓"）。</summary>
    public string? Equation { get; init; }

    /// <summary>SMILES 结构式（需分子模块支持；模块未引入时该项校验标记为不可用）。</summary>
    public string? Smiles { get; init; }

    /// <summary>方程式类型模式，默认 <see cref="EquationMode.Auto"/>。</summary>
    public EquationMode Mode { get; init; } = EquationMode.Auto;

    /// <summary>氧化还原半反应介质："acidic"（默认）或 "basic"。</summary>
    public string? Medium { get; init; }
}

/// <summary>
/// 化学伴思统一校验入口。串行管线：
/// 白名单校验 → 守恒校验 → 配平校验 → 离子规则校验（拆写/状态符号）→ 氧化还原分析 → 反应库一致性 → 官能团校验。
/// 核心铁律（知识驱动确定性推断）：
/// <list type="bullet">
/// <item>反应知识库有记录的反应物组合，其产物正确性按知识库条目对照（反应库一致性）；
/// 知识库未收录的组合只做形式校验，绝不臆造产物。</item>
/// <item>白名单外物质标记 <see cref="RiskTags.OutOfScope"/>，后续仅做形式校验。</item>
/// <item>所有数值计算走有理数，无浮点误差。</item>
/// </list>
/// 返回统一 <see cref="CheckReport"/>，供伴思层按 CheckItems 与 RiskTag 生成递进引导提问。
/// </summary>
public static class ChemicalCheckerService
{
    /// <summary>
    /// 执行完整校验管线。
    /// </summary>
    /// <param name="request">校验请求（方程式 / SMILES 至少其一）。</param>
    /// <returns>统一校验报告；致命错误（输入为空、解析失败）写入 ErrorMessage。</returns>
    public static CheckReport Check(CheckRequest request)
    {
        var report = new CheckReport();

        if (string.IsNullOrWhiteSpace(request.Equation) && string.IsNullOrWhiteSpace(request.Smiles))
        {
            report.ErrorMessage = "输入为空：请提供方程式或 SMILES";
            report.AddFail("输入校验", report.ErrorMessage, RiskTags.ParseError);
            return report;
        }

        ParsedEquation? parsed = null;
        if (!string.IsNullOrWhiteSpace(request.Equation))
        {
            if (!EquationParser.TryParse(request.Equation!, out parsed, out var error))
            {
                report.ErrorMessage = error!;
                report.AddFail("方程式解析", error!, RiskTags.ParseError);
                return report;
            }
        }

        if (parsed is not null)
        {
            CheckWhitelist(parsed, report);
            CheckConservation(parsed, report);
            CheckBalancing(parsed, report);
            if (request.Mode == EquationMode.HalfReaction)
            {
                CheckHalfReactionElectron(parsed, report);
            }
            else
            {
                if (ShouldRunIonicRules(request, parsed))
                {
                    MergeIonicRules(parsed, report);
                }

                CheckRedox(parsed, request.Medium, report);
                CheckReactionLibraryConsistency(parsed, report);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Smiles))
        {
            CheckFunctionalGroups(request.Smiles!, report);
        }

        return report;
    }

    /// <summary>白名单校验：课内已知物质放行，陌生物质标记超纲风险（不中断后续形式校验）。</summary>
    private static void CheckWhitelist(ParsedEquation parsed, CheckReport report)
    {
        var (known, tag, unknown) = WhitelistGuard.CheckRange(parsed.AllSpecies);
        if (known)
        {
            report.AddPass("白名单校验", "全部物种在高中课内白名单中");
            return;
        }

        report.AddFail("白名单校验", $"陌生物质 {unknown}，超出高中课标范围，引擎只做形式校验", tag);
    }

    /// <summary>守恒校验：按学生系数检查元素与电荷守恒。</summary>
    private static void CheckConservation(ParsedEquation parsed, CheckReport report)
    {
        var violations = EquationBalancer.ConservationViolations(parsed);
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

    /// <summary>配平校验：零空间求解判断该物种组合是否可配平（不覆盖学生系数，守恒项负责）。</summary>
    private static void CheckBalancing(ParsedEquation parsed, CheckReport report)
    {
        var result = EquationBalancer.Balance(parsed);
        if (result.Success)
        {
            report.AddPass("配平", $"引擎配平结果：{result.BalancedEquation}");
            return;
        }

        report.AddFail("配平", result.FailReason ?? "无法配平", result.RiskTag);
    }

    /// <summary>
    /// 半反应（电极反应式）专属校验：必须含电子项，且电子只出现在一侧。
    /// 元素与电荷守恒由通用检查项负责（电子按 -1 电荷计入）。
    /// </summary>
    private static void CheckHalfReactionElectron(ParsedEquation parsed, CheckReport report)
    {
        var left = parsed.Left.Where(t => t.Species.Body == "e").ToList();
        var right = parsed.Right.Where(t => t.Species.Body == "e").ToList();
        if (left.Count > 0 && right.Count > 0)
        {
            report.AddFail("电子守恒", "电子项同时出现在方程式两侧，电极反应式只应有一侧得失电子", RiskTags.RedoxError);
            return;
        }

        if (left.Count == 0 && right.Count == 0)
        {
            report.AddFail("电子守恒", "电极反应式缺少电子项 e^-", RiskTags.RedoxError);
            return;
        }

        var electrons = left.Count > 0 ? left.Sum(t => t.Coefficient) : right.Sum(t => t.Coefficient);
        // 电子在反应物侧 = 得电子（还原，阴极/正极）；在生成物侧 = 失电子（氧化，阳极/负极）
        report.AddPass("电子守恒", $"{(left.Count > 0 ? "得" : "失")} {electrons} 个电子");
    }

    /// <summary>是否执行离子规则：显式 Ionic 模式，或 Auto 模式且方程式含离子。</summary>
    private static bool ShouldRunIonicRules(CheckRequest request, ParsedEquation parsed) =>
        request.Mode == EquationMode.Ionic || (request.Mode == EquationMode.Auto && parsed.HasIons);

    /// <summary>并入离子规则报告（守恒项由入口统一出具，此处去重）。</summary>
    private static void MergeIonicRules(ParsedEquation parsed, CheckReport report)
    {
        var ionic = IonicEquationChecker.Check(parsed);
        foreach (var item in ionic.CheckItems)
        {
            if (item.CheckName is "元素守恒" or "电荷守恒" or "元素与电荷守恒")
            {
                continue;
            }

            report.Add(item);
        }

        if (report.ErrorMessage.Length == 0 && ionic.ErrorMessage.Length > 0)
        {
            report.ErrorMessage = ionic.ErrorMessage;
        }
    }

    /// <summary>氧化还原分析：氧化数变化、电子守恒与半反应拆解（非氧化还原反应直接通过）。</summary>
    private static void CheckRedox(ParsedEquation parsed, string? medium, CheckReport report)
    {
        var analysis = RedoxBalancer.SplitHalfReactions(parsed.Raw, medium ?? "acidic");
        if (!analysis.Success)
        {
            report.AddFail("氧化还原分析", analysis.FailReason ?? "分析失败", analysis.RiskTag);
            return;
        }

        if (!analysis.IsRedox)
        {
            report.AddPass("氧化还原分析", "非氧化还原反应");
            return;
        }

        if (!analysis.ElectronConservation)
        {
            report.AddFail("氧化还原分析", "失电子总数与得电子总数不相等，请检查系数", RiskTags.RedoxError);
            return;
        }

        // 半反应拆解为尽力推导（离子-电子法不适用于气相/固相分子反应等场景），
        // 失败不影响电子守恒结论，仅附注说明，供伴思层决定是否引导人工分析。
        var note = analysis.RiskTag != RiskTags.None && analysis.FailReason is not null
            ? $"（{analysis.FailReason}）"
            : "";
        report.AddPass("氧化还原分析", $"转移电子数 {analysis.ElectronsTransferred}，电子守恒{note}");
    }

    /// <summary>
    /// 反应库一致性：用课内反应知识库对照"同一反应物组合"的产物记录（知识驱动的产物判错）。
    /// 规则：
    /// <list type="bullet">
    /// <item>知识库无该反应物组合的任何条目 → 不判（保持"库外不推断"边界），记通过并注明。</item>
    /// <item>有条目且方程式产物集与任一条目产物集一致（忽略系数与状态符号）→ 通过。</item>
    /// <item>有条目但产物集与全部条目均不一致 → 判错（如 Fe+H2O(高温) 误写 Fe2O3，应为 Fe3O4）。</item>
    /// </list>
    /// 条件差异（浓稀/过量）不做强制对照：只要产物与任一条目一致即放行，避免误伤条件变体。
    /// </summary>
    private static void CheckReactionLibraryConsistency(ParsedEquation parsed, CheckReport report)
    {
        var library = Reactions.ReactionLibrary.Default.Value;
        var reactants = parsed.Left.Select(t => t.Species.ToString()).ToList();
        var hits = library.FindByReactants(reactants);
        if (hits.Count == 0)
        {
            report.AddPass("反应库一致性", "该反应物组合在课内知识库中无条目，不做产物一致性判定");
            return;
        }

        var productSet = new HashSet<string>(
            parsed.Right.Select(t => t.Species.ToString()), StringComparer.Ordinal);
        var matched = hits.FirstOrDefault(e => ProductSetMatches(e.Products, productSet));
        if (matched is not null)
        {
            report.AddPass("反应库一致性", $"产物与知识库条目 {matched.Id}（{matched.Name}）一致");
            return;
        }

        var recorded = string.Join("；", hits.Select(h => $"{h.Id}：{h.Equation}"));
        report.AddFail(
            "反应库一致性",
            $"产物与课内知识库矛盾。该反应物组合的记录为：{recorded}",
            RiskTags.KnowledgeConflict);
    }

    /// <summary>条目产物集与方程式产物集比较（归一化后按集合相等，忽略系数与状态符号）。</summary>
    private static bool ProductSetMatches(List<string> entryProducts, HashSet<string> equationProducts)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in entryProducts)
        {
            normalized.Add(NormalizeForComparison(product));
        }

        return normalized.SetEquals(equationProducts);
    }

    private static string NormalizeForComparison(string formula)
    {
        var trimmed = formula.Trim().Replace(" ", "");
        if (trimmed.Contains('·'))
        {
            return trimmed;
        }

        try
        {
            return Species.Parse(trimmed).ToString();
        }
        catch (FormulaException)
        {
            return trimmed;
        }
    }

    /// <summary>
    /// 官能团校验：委托分子模块（RDKit 封装）做 SMILES 解析与高中官能团白名单识别。
    /// 解析失败或检出超纲结构时报告相应风险标签。
    /// </summary>
    private static void CheckFunctionalGroups(string smiles, CheckReport report)
    {
        report.Merge(Molecules.MoleculeValidator.Check(smiles));
    }
}
