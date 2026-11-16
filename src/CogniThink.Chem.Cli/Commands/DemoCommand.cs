using CogniThink.Chem.Core;

namespace CogniThink.Chem.Cli.Commands;

/// <summary>
/// --demo 命令：统一校验演示。
/// </summary>
internal static class DemoCommand
{
    /// <summary>
    /// 全链路演示：统一入口 ChemicalCheckerService 的典型调用与返回结构示例。
    /// 覆盖：正确分子方程式、学生错题（未配平）、离子方程式、碱性介质氧化还原、
    /// SMILES 官能团校验、陌生物质超纲标记。
    /// </summary>
    public static void Run()
    {
        var cases = new (string Title, CheckRequest Request)[]
        {
            ("正确分子方程式（氢气燃烧）", new CheckRequest { Equation = "2H2 + O2 =点燃= 2H2O" }),
            ("学生错题（未配平的水电解）", new CheckRequest { Equation = "H2O =通电= H2 + O2" }),
            ("离子方程式（硫酸钡沉淀）", new CheckRequest { Equation = "Ba^2+ + SO4^2- = BaSO4↓" }),
            ("离子方程式错题（醋酸误拆为 H^+）", new CheckRequest { Equation = "CH3COO^- + H^+ = Na^+ + Cl^-" }),
            ("碱性介质氧化还原", new CheckRequest
            {
                Equation = "2MnO4^- + 3SO3^2- + H2O = 2MnO2↓ + 3SO4^2- + 2OH^-",
                Medium = "basic"
            }),
            ("SMILES 官能团校验（葡萄糖）", new CheckRequest { Smiles = "OCC(O)C(O)C(O)C(O)C=O" }),
            ("陌生物质（超纲干扰项）", new CheckRequest { Equation = "Xy + O2 = XyO2" })
        };

        foreach (var (title, request) in cases)
        {
            var report = ChemicalCheckerService.Check(request);
            Console.WriteLine($"■ {title}");
            Console.WriteLine($"  输入：{request.Equation ?? request.Smiles}");
            Console.WriteLine($"  结论：{(report.IsValid ? "校验通过" : "校验未通过")}　风险标签：{(report.RiskTag.Length == 0 ? "无" : report.RiskTag)}");
            if (report.ErrorMessage.Length > 0)
            {
                Console.WriteLine($"  致命错误：{report.ErrorMessage}");
            }

            foreach (var item in report.CheckItems)
            {
                var mark = item.Passed ? "√" : "×";
                var detail = item.Detail.Length > 0 ? $"：{item.Detail}" : "";
                Console.WriteLine($"  {mark} {item.CheckName}{detail}");
            }

            Console.WriteLine();
        }
    }
}