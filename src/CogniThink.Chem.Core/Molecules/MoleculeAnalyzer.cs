using System.Text.RegularExpressions;
using GraphMolWrap;

namespace CogniThink.Chem.Core.Molecules;

/// <summary>单原子结构特征（供结构化判分比对）。</summary>
public sealed record AtomFeature
{
    public string Symbol { get; init; } = "";
    /// <summary>规范杂化：sp / sp2 / sp3 / sp3d …</summary>
    public string Hybridization { get; init; } = "";
    /// <summary>价层电子对数（σ 键 + 孤电子对）= VSEPR 立体数。</summary>
    public int StericNumber { get; init; }
    public int Degree { get; init; }
    /// <summary>相连 H 原子总数（隐式 + 显式），用于配体对称性判定。</summary>
    public int HCount { get; init; }
    public int LonePairs { get; init; }
    public bool IsCentral { get; init; }
}

/// <summary>分子结构特征分析结果（RDKit 真值，不臆造）。</summary>
public sealed class MoleculeAnalysisResult
{
    public bool Success { get; init; }
    public string? FailReason { get; init; }

    public string MolecularFormula { get; init; } = "";
    public int DegreeOfUnsaturation { get; init; }
    public int RingCount { get; init; }

    public IReadOnlyList<string> FunctionalGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AtomFeature> AtomFeatures { get; init; } = Array.Empty<AtomFeature>();

    public string? CentralAtomSymbol { get; init; }
    public string? CentralAtomHybridization { get; init; }
    public int CentralAtomStericNumber { get; init; }
    public int CentralAtomLonePairs { get; init; }

    /// <summary>极性近似（高中口径）：true=极性，false=非极性。</summary>
    public bool IsPolar { get; init; }

    /// <summary>手性碳原子数（RDKit numAtomStereoCenters，assignStereochemistry 后）。</summary>
    public int ChiralCenterCount { get; init; }
    /// <summary>等效氢组数 = 一氯代物种数（逐带 H 碳 ¹³C 标记 → 规范 SMILES 去重）。</summary>
    public int EquivalentHydrogenGroups { get; init; }
    /// <summary>1 mol 该分子最多可加成的 H₂ mol 数（C=C/C=O/C=N 计 1、C#C 计 2、芳环键数/2）。</summary>
    public int AddableH2Count { get; init; }
    /// <summary>sp² 杂化的碳原子数。</summary>
    public int Sp2CarbonCount { get; init; }
    /// <summary>sp³ 杂化的碳原子数。</summary>
    public int Sp3CarbonCount { get; init; }

    /// <summary>人类可读结论（含可被关键词命中的规范词：极性/非极性、sp³、不饱和度 Ω=n、官能团名）。</summary>
    public string FinalResult { get; init; } = "";
}

/// <summary>
/// MoleculeAnalyzer：molecule 资产（SMILES / molblock）→ RDKit 结构特征。
/// 计算：分子式、不饱和度 Ω、官能团识别（SMARTS）、中心原子杂化与价层电子对、极性近似。
/// 铁律：RDKit 解析失败 → Success=false 且不抛异常、不臆造任何数值。
/// </summary>
public static class MoleculeAnalyzer
{
    /// <summary>高中常见官能团：名称 → SMARTS（与 MoleculeValidator 白名单一致，保证口径统一）。</summary>
    private static readonly (string Name, string Smarts)[] GroupSmarts =
    {
        ("苯环", "c1ccccc1"),
        ("碳碳双键", "C=C"),
        ("碳碳三键", "C#C"),
        ("羟基", "[OX2H;!$([OX2H][CX3]=O)]"),
        ("羧基", "[CX3](=O)[OX2H]"),
        ("醛基", "[$([CX3H1](=O)[#6]),$([CX3H2]=O)]"),
        ("酮羰基", "[CX3](=O)([#6])[#6]"),
        ("酯基", "[CX3](=O)[OX2][#6]"),
        ("醚键", "[OD2]([CX4])[CX4]"),
        ("氨基", "[NX3H2][#6]"),
        ("硝基", "[$([NX3](=O)=O),$([NX3+](=O)[O-])]"),
        ("卤原子", "[F,Cl,Br,I][#6]"),
    };

    private static readonly HashSet<string> Halogens = new(StringComparer.Ordinal) { "F", "Cl", "Br", "I", "At" };
    private static readonly Regex FormulaToken = new(@"([A-Z][a-z]?)(\d*)", RegexOptions.Compiled);

    /// <summary>优先用 SMILES；为空则用 molblock；均空 → 失败（如实返回空）。</summary>
    public static MoleculeAnalysisResult Analyze(string? smiles, string? molblock = null)
    {
        ROMol? mol = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(smiles))
                mol = RDKFuncs.SmilesToMol(smiles!);
            else if (!string.IsNullOrWhiteSpace(molblock))
                mol = RDKFuncs.MolBlockToMol(molblock!);

            if (mol is null)
                return Fail("分子结构解析失败（SMILES/molblock 为空或 RDKit 无法解析），不臆造特征");

            return BuildResult(mol);
        }
        catch (Exception ex)
        {
            return Fail("RDKit 解析异常：" + ex.Message);
        }
        finally
        {
            mol?.Dispose();
        }
    }

    private static MoleculeAnalysisResult BuildResult(ROMol mol)
    {
        var formula = RDKFuncs.calcMolFormula(mol);
        var ringCount = (int)RDKFuncs.calcNumRings(mol);
        var dou = CalcDegreeOfUnsaturation(formula);

        // 官能团
        var groups = new List<string>();
        foreach (var (name, smarts) in GroupSmarts)
        {
            using var pattern = RDKFuncs.SmartsToMol(smarts);
            if (pattern is not null && mol.hasSubstructMatch(pattern))
                groups.Add(name);
        }

        // 原子特征 + 邻接表（用于极性判定）
        var atomCount = (int)mol.getNumAtoms();
        var features = new AtomFeature[atomCount];
        var symbols = new string[atomCount];
        var adj = new List<int>[atomCount];
        for (int i = 0; i < atomCount; i++) adj[i] = new List<int>();

        for (int i = 0; i < atomCount; i++)
        {
            using var atom = mol.getAtomWithIdx((uint)i);
            symbols[i] = atom.getSymbol();
            var steric = (int)atom.numBondsPlusLonePairs();
            // totalDegree 包含与隐式 H 的成键；用它计算孤电子对（= 价层电子对 − 总 σ 键数）才正确。
            var degree = (int)atom.getTotalDegree();
            features[i] = new AtomFeature
            {
                Symbol = symbols[i],
                Hybridization = MapHybrid(atom.getHybridization().ToString()),
                StericNumber = steric,
                Degree = degree,
                HCount = (int)atom.getTotalNumHs(),
                LonePairs = System.Math.Max(0, steric - degree),
            };
        }

        var bondCount = (int)mol.getNumBonds();
        for (int b = 0; b < bondCount; b++)
        {
            using var bond = mol.getBondWithIdx((uint)b);
            int a = (int)bond.getBeginAtomIdx(), e = (int)bond.getEndAtomIdx();
            if (a < atomCount && e < atomCount) { adj[a].Add(e); adj[e].Add(a); }
        }

        // 中心原子：成键数（totalDegree）最多即连接度最高；并列取价层电子对最多；再并列取首个重原子（非 H）。
        // 先比连接度而非价层电子对，避免把带孤对的端原子（如 CO2 的 O）误选为中心。
        int central = -1;
        for (int i = 0; i < atomCount; i++)
        {
            if (symbols[i] == "H") continue;
            if (central < 0
                || features[i].Degree > features[central].Degree
                || (features[i].Degree == features[central].Degree && features[i].StericNumber > features[central].StericNumber))
                central = i;
        }

        var polar = EstimatePolarity(formula, features, adj, central);

        // —— 选择题图像判定所需的真值（RDKit，失败不抛异常）——
        // 手性碳数
        int chiral = 0;
        try { RDKFuncs.assignStereochemistry(mol); chiral = (int)RDKFuncs.numAtomStereoCenters(mol); } catch { }

        // sp² / sp³ 碳数（复用已算的 features）
        int sp2c = 0, sp3c = 0;
        for (int i = 0; i < atomCount; i++)
        {
            if (symbols[i] != "C") continue;
            if (features[i].Hybridization == "sp2") sp2c++;
            else if (features[i].Hybridization == "sp3") sp3c++;
        }

        // 可加成 H₂：双键(含 C=O/C=N) +1、三键 +2、芳环键数/2
        int addH2 = 0, aromBonds = 0;
        for (int b = 0; b < bondCount; b++)
        {
            using var bond = mol.getBondWithIdx((uint)b);
            int bi = (int)bond.getBeginAtomIdx(), ei = (int)bond.getEndAtomIdx();
            bool hasC = bi < atomCount && ei < atomCount && (symbols[bi] == "C" || symbols[ei] == "C");
            var bt = bond.getBondType().ToString();
            if (bt == "AROMATIC") aromBonds++;
            else if (bt == "DOUBLE" && hasC) addH2++;
            else if (bt == "TRIPLE" && hasC) addH2 += 2;
        }
        addH2 += aromBonds / 2;

        // 等效氢组数 = 一氯代物种数：逐带 H 碳 ¹³C 标记 → 规范 SMILES 去重（原地标记/恢复）
        int equivH = 0;
        try
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < atomCount; i++)
            {
                using var a = mol.getAtomWithIdx((uint)i);
                if (a.getSymbol() != "C" || (int)a.getTotalNumHs() == 0) continue;
                var oldIso = a.getIsotope();
                a.setIsotope(13);
                set.Add(RDKFuncs.MolToSmiles(mol));
                a.setIsotope(oldIso);
            }
            equivH = set.Count;
        }
        catch { }

        var centralFeat = central >= 0 ? features[central] : null;
        if (centralFeat != null) centralFeat = centralFeat with { IsCentral = true };

        var finalResult = BuildFinalResult(formula, dou, ringCount, groups, centralFeat, polar, chiral, equivH, addH2, sp2c, sp3c);

        return new MoleculeAnalysisResult
        {
            Success = true,
            MolecularFormula = formula,
            DegreeOfUnsaturation = dou,
            RingCount = ringCount,
            FunctionalGroups = groups,
            AtomFeatures = features,
            CentralAtomSymbol = centralFeat?.Symbol,
            CentralAtomHybridization = centralFeat?.Hybridization,
            CentralAtomStericNumber = centralFeat?.StericNumber ?? 0,
            CentralAtomLonePairs = centralFeat?.LonePairs ?? 0,
            IsPolar = polar,
            ChiralCenterCount = chiral,
            EquivalentHydrogenGroups = equivH,
            AddableH2Count = addH2,
            Sp2CarbonCount = sp2c,
            Sp3CarbonCount = sp3c,
            FinalResult = finalResult,
        };
    }

    /// <summary>分子式 → 不饱和度 Ω = (2C + 2 + N − H − X)/2（X=卤素）。无碳时为 0（有机公式约定）。</summary>
    private static int CalcDegreeOfUnsaturation(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return 0;
        int c = 0, h = 0, n = 0, x = 0;
        foreach (Match m in FormulaToken.Matches(formula))
        {
            var el = m.Groups[1].Value;
            var cnt = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value) : 1;
            switch (el)
            {
                case "C": c += cnt; break;
                case "H": h += cnt; break;
                case "N": n += cnt; break;
                default:
                    if (Halogens.Contains(el)) x += cnt;
                    break;
            }
        }
        return System.Math.Max(0, (2 * c + 2 + n - h - x) / 2);
    }

    /// <summary>
    /// 极性近似（高中口径，可解释）：
    /// 1) 单原子 / 全同元素双原子（O2、N2）→ 非极性；
    /// 2) 中心原子有孤电子对 → 通常极性（H2O、NH3、SOCl2 等）；
    /// 3) 中心原子无孤对但配体不全同 → 极性（CH3Cl）；
    /// 4) 无孤对且配体全同、立体数 ∈ {2,3,4}（直线/平面三角/正四面体对称）→ 非极性（CO2、BF3、CCl4、CH4）；
    /// 5) 其余含不同元素成键 → 极性。
    /// </summary>
    private static bool EstimatePolarity(string formula, AtomFeature[] feats, List<int>[] adj, int central)
    {
        // 分子式仅一种元素 → 单质（O2/N2/Cl2）→ 非极性。
        // 用分子式而非重原子列表判定：H2O/NH3 虽重原子仅一种，但含 H，不会命中此条。
        var elements = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in FormulaToken.Matches(formula)) elements.Add(m.Groups[1].Value);
        if (elements.Count <= 1) return false;

        if (central < 0) return true;
        var cf = feats[central];

        // 中心原子有孤电子对 → 通常极性（H2O、NH3、SOCl2 等）。
        if (cf.LonePairs > 0) return true;

        // 配体全多重集 = 重原子邻接 + 隐式 H（显式 H 已在 adj 中，需扣除避免重复）。
        int explicitH = adj[central].Count(j => feats[j].Symbol == "H");
        int implicitH = System.Math.Max(0, cf.HCount - explicitH);
        var neighborSymbols = adj[central].Select(j => feats[j].Symbol)
            .Concat(Enumerable.Repeat("H", implicitH)).ToList();
        if (neighborSymbols.Count == 0) return false; // 仅 H 配体且无重原子邻接（如 CH4）→ 对称非极性
        var allSame = neighborSymbols.Distinct(StringComparer.Ordinal).Count() == 1;
        if (allSame && cf.StericNumber is 2 or 3 or 4) return false; // 直线/平面三角/正四面体对称抵消（CO2、BF3、CCl4、CH4）
        return true; // 配体不全同 → 极性（CH3Cl 等）
    }

    private static string MapHybrid(string? raw) => raw?.ToUpperInvariant() switch
    {
        "SP" => "sp",
        "SP2" => "sp2",
        "SP3" => "sp3",
        "SP3D" => "sp3d",
        "SP3D2" => "sp3d2",
        _ => raw?.ToLowerInvariant() ?? "",
    };

    private static string HybridDisplay(string canon) => canon switch
    {
        "sp" => "sp",
        "sp2" => "sp²",
        "sp3" => "sp³",
        "sp3d" => "sp³d",
        "sp3d2" => "sp³d²",
        _ => canon,
    };

    private static string BuildFinalResult(string formula, int dou, int rings, List<string> groups, AtomFeature? central, bool polar,
        int chiral, int equivH, int addH2, int sp2c, int sp3c)
    {
        var parts = new List<string> { $"分子式 {formula}", $"不饱和度 Ω={dou}" };
        if (rings > 0) parts.Add($"环数 {rings}");
        if (central != null)
        {
            parts.Add($"中心原子 {central.Symbol} 杂化 {HybridDisplay(central.Hybridization)}（{central.Hybridization}），价层电子对 {central.StericNumber}，孤电子对 {central.LonePairs}");
        }
        parts.Add($"分子极性：{(polar ? "极性" : "非极性")}");
        parts.Add($"官能团：{(groups.Count > 0 ? string.Join("、", groups) : "无")}");
        parts.Add($"手性碳 {chiral} 个；等效氢 {equivH} 组（一氯代物 {equivH} 种）；最多加成 H₂ {addH2} mol；sp² 碳 {sp2c} 个，sp³ 碳 {sp3c} 个");
        return string.Join("；", parts);
    }

    private static MoleculeAnalysisResult Fail(string reason) =>
        new() { Success = false, FailReason = reason, FinalResult = reason };
}
