using CogniThink.Chem.Core.Balancing;
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
    Ionic
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
/// 白名单校验 → 守恒校验 → 配平校验 → 离子规则校验（拆写/状态符号）→ 氧化还原分析 → 官能团校验。
/// 核心铁律：
/// <list type="bullet">
/// <item>引擎只做确定性校验，绝不预测产物、绝不推断未知反应。</item>
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
            if (ShouldRunIonicRules(request, parsed))
            {
                MergeIonicRules(parsed, report);
            }

            CheckRedox(parsed, request.Medium, report);
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
    /// 官能团校验：委托分子模块（RDKit 封装）做 SMILES 解析与高中官能团白名单识别。
    /// 解析失败或检出超纲结构时报告相应风险标签。
    /// </summary>
    private static void CheckFunctionalGroups(string smiles, CheckReport report)
    {
        report.Merge(Molecules.MoleculeValidator.Check(smiles));
    }
}
