using CogniThink.Chem.Core.Common;
using GraphMolWrap;

namespace CogniThink.Chem.Core.Molecules;

/// <summary>
/// SMILES 结构分析结果。
/// </summary>
public sealed class MoleculeAnalysis
{
    public bool Success { get; init; }

    /// <summary>解析失败原因（成功为 null）。</summary>
    public string? FailReason { get; init; }

    /// <summary>分子式（RDKit 计算，如 C2H6O）。</summary>
    public string MolecularFormula { get; init; } = "";

    /// <summary>命中的高中课内官能团名称列表。</summary>
    public IReadOnlyList<string> FunctionalGroups { get; init; } = Array.Empty<string>();

    /// <summary>命中的超纲（竞赛/大学）结构特征列表。</summary>
    public IReadOnlyList<string> OutOfScopeGroups { get; init; } = Array.Empty<string>();

    /// <summary>风险标签：存在超纲特征时为 <see cref="RiskTags.OutOfScope"/>。</summary>
    public string RiskTag { get; init; } = RiskTags.None;
}

/// <summary>
/// 分子结构校验器：基于 RDKitDotNetCore（RDKit C# SWIG 封装）的薄门面。
/// 职责：SMILES 解析、分子式提取、官能团识别；
/// 铁律：只识别不预测——不做反应预测、不做逆合成、不做高级立体化学。
/// 官能团白名单严格限定高中课内（来源：人教版《有机化学基础》/选择性必修三）；
/// 白名单外的结构特征（磺酸基、巯基、磷/金属元素等）标记【超纲风险】。
/// 易错点：
/// <list type="bullet">
/// <item>羧基中的羟基不计入"羟基"（用 SMARTS 负向环境排除）。</item>
/// <item>酯的烷氧基氧不计入"醚键"（要求两个邻碳均为 sp3）。</item>
/// <item>SMILES 解析失败（非法价态等）直接返回失败，引擎不猜测结构。</item>
/// </list>
/// </summary>
public static class MoleculeValidator
{
    /// <summary>
    /// 高中课内官能团白名单：名称 → SMARTS。
    /// 来源：人教版《化学·选择性必修三·有机化学基础》常见官能团。
    /// </summary>
    private static readonly (string Name, string Smarts)[] WhitelistGroups =
    {
        ("碳碳双键", "C=C"),
        ("碳碳三键", "C#C"),
        ("羧基", "[CX3](=O)[OX2H]"),
        ("醛基", "[$([CX3H1](=O)[#6]),$([CX3H2]=O)]"),
        ("酯基", "[CX3](=O)[OX2][#6]"),
        ("羟基", "[OX2H;!$([OX2H][CX3]=O)]"),
        ("酮羰基", "[CX3](=O)([#6])[#6]"),
        ("醚键", "[OD2]([CX4])[CX4]"),
        ("卤原子", "[F,Cl,Br,I][#6]"),
        ("硝基", "[$([NX3](=O)=O),$([NX3+](=O)[O-])]"),
        ("氨基", "[NX3H2][#6]"),
        ("肽键", "[CX3](=O)[NX3]"),
        ("苯环", "c1ccccc1")
    };

    /// <summary>
    /// 超纲（竞赛/大学）结构特征：名称 → SMARTS。
    /// 命中即标记 <see cref="RiskTags.OutOfScope"/>，引擎不评价其化学性质。
    /// </summary>
    private static readonly (string Name, string Smarts)[] OutOfScopeGroups =
    {
        ("磺酸基", "[SX4](=O)(=O)[OX2H]"),
        ("巯基", "[SX2H]"),
        ("磷酸酯/磷氧结构", "[#15]")
    };

    /// <summary>高中有机化学常见元素（其余元素视为超纲）。</summary>
    private static readonly HashSet<string> OrganicCommonElements = new(StringComparer.Ordinal)
    {
        "C", "H", "O", "N", "F", "Cl", "Br", "I"
    };

    /// <summary>
    /// 分析 SMILES：解析、分子式、官能团白名单识别。
    /// </summary>
    /// <param name="smiles">SMILES 字符串，如 "CCO"（乙醇）、"CC(=O)O"（乙酸）。</param>
    /// <returns>分析结果；解析失败时 Success=false 并给出原因。</returns>
    public static MoleculeAnalysis Analyze(string smiles)
    {
        if (string.IsNullOrWhiteSpace(smiles))
        {
            return Fail("SMILES 为空");
        }

        using var mol = RDKFuncs.SmilesToMol(smiles);
        if (mol is null)
        {
            return Fail($"SMILES 无法解析（结构或价态非法）：{smiles}");
        }

        var formula = RDKFuncs.calcMolFormula(mol);
        var groups = new List<string>();
        var outOfScope = new List<string>();

        foreach (var (name, smarts) in WhitelistGroups)
        {
            if (Matches(mol, smarts))
            {
                groups.Add(name);
            }
        }

        foreach (var (name, smarts) in OutOfScopeGroups)
        {
            if (Matches(mol, smarts))
            {
                outOfScope.Add(name);
            }
        }

        // 元素白名单：高中有机仅 C/H/O/N/卤素，其余元素（S、P、金属等）标超纲
        var count = mol.getNumAtoms();
        var foreign = new HashSet<string>(StringComparer.Ordinal);
        for (uint i = 0; i < count; i++)
        {
            using var atom = mol.getAtomWithIdx(i);
            var symbol = atom.getSymbol();
            if (!OrganicCommonElements.Contains(symbol))
            {
                foreign.Add(symbol);
            }
        }

        foreach (var element in foreign.OrderBy(e => e, StringComparer.Ordinal))
        {
            outOfScope.Add($"非高中有机常见元素 {element}");
        }

        return new MoleculeAnalysis
        {
            Success = true,
            MolecularFormula = formula,
            FunctionalGroups = groups,
            OutOfScopeGroups = outOfScope,
            RiskTag = outOfScope.Count > 0 ? RiskTags.OutOfScope : RiskTags.None
        };
    }

    /// <summary>
    /// 校验入口（供 <see cref="ChemicalCheckerService"/> 调用），返回统一报告。
    /// 课内结构且无超纲特征 → IsValid=true；否则给出失败项与风险标签。
    /// </summary>
    public static CheckReport Check(string smiles)
    {
        var report = new CheckReport();
        var analysis = Analyze(smiles);
        if (!analysis.Success)
        {
            report.ErrorMessage = analysis.FailReason ?? "SMILES 解析失败";
            report.AddFail("结构解析", report.ErrorMessage, RiskTags.ParseError);
            return report;
        }

        report.AddPass("结构解析", $"分子式 {analysis.MolecularFormula}");

        var groupText = analysis.FunctionalGroups.Count > 0
            ? string.Join("、", analysis.FunctionalGroups)
            : "无官能团（如烷烃）";
        report.AddPass("官能团识别", groupText);

        if (analysis.OutOfScopeGroups.Count > 0)
        {
            report.AddFail(
                "官能团白名单",
                $"检出超纲结构：{string.Join("、", analysis.OutOfScopeGroups)}，引擎不评价其化学性质",
                RiskTags.OutOfScope);
        }
        else
        {
            report.AddPass("官能团白名单", "全部结构特征在高中课内白名单中");
        }

        return report;
    }

    private static bool Matches(ROMol mol, string smarts)
    {
        using var pattern = RDKFuncs.SmartsToMol(smarts);
        return pattern is not null && mol.hasSubstructMatch(pattern);
    }

    private static MoleculeAnalysis Fail(string reason) =>
        new() { Success = false, FailReason = reason };
}
