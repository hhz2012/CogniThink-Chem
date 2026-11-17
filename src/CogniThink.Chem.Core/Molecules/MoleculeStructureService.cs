using GraphMolWrap;

namespace CogniThink.Chem.Core.Molecules;

/// <summary>
/// 分子结构可视化服务：将已知分子式映射到 SMILES，再用 RDKit 生成 2D molblock，
/// 供前端 3Dmol.js 渲染为可交互的球棍模型。
/// 覆盖高中化学常见无机物与有机物（来源于 benchmark 题库中的实际分子式）。
/// </summary>
public static class MoleculeStructureService
{
    /// <summary>分子式 → SMILES 映射表（高中化学常见分子）。</summary>
    private static readonly Dictionary<string, string> SmilesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // —— 有机物 ——
        ["CH4"] = "C",
        ["C2H6"] = "CC",
        ["C2H4"] = "C=C",
        ["C2H5OH"] = "CCO",
        ["CH3CHO"] = "CC=O",
        ["CH3COOH"] = "CC(=O)O",
        ["CH3COONa"] = "CC(=O)[O-].[Na+]",
        ["CH3COONH4"] = "CC(=O)[O-].[NH4+]",
        ["CH3COOCH2CH3"] = "CCOC(C)=O",
        ["C6H6"] = "c1ccccc1",
        ["C6H12"] = "C1CCCCC1",
        ["C6H12O6"] = "OCC1OC(O)C(O)C(O)C1O",
        ["C6H5Br"] = "c1ccc(Br)cc1",
        ["C6H5NO2"] = "c1ccc([N+](=O)[O-])cc1",
        ["C2H5Br"] = "CCBr",
        ["C2H4Br2"] = "BrCCBr",
        ["CH3Cl"] = "CCl",
        ["CH2Cl2"] = "ClCCl",
        ["CHCl3"] = "ClC(Cl)Cl",
        ["CCl4"] = "ClC(Cl)(Cl)Cl",

        // —— 无机物 ——
        ["H2"] = "[H][H]",
        ["O2"] = "O=O",
        ["N2"] = "N#N",
        ["Cl2"] = "ClCl",
        ["Br2"] = "BrBr",
        ["HCl"] = "Cl",
        ["H2SO4"] = "OS(=O)(=O)O",
        ["H2SiO3"] = "O[Si](O)(=O)",
        ["HNO3"] = "O[N+](=O)[O-]",
        ["CO2"] = "O=C=O",
        ["SO2"] = "O=S=O",
        ["SO3"] = "O=S(=O)=O",
        ["NH3"] = "N",
        ["H2O"] = "O",
        ["H2O2"] = "OO",
        ["SiO2"] = "O=[Si]=O",
        ["Na2O2"] = "[Na+].[Na+].[O-][O-]",

        ["NaCl"] = "[Na+].[Cl-]",
        ["NaOH"] = "[Na+].[O-]",
        ["NaClO"] = "[Na+].[O-]Cl",
        ["Na2CO3"] = "[Na+].[Na+].[O-]C(=O)[O-]",
        ["NaHCO3"] = "[Na+].[O-]C(=O)O",
        ["Na2SO4"] = "[Na+].[Na+].[O-]S(=O)(=O)[O-]",
        ["Na2SiO3"] = "[Na+].[Na+].[O-][Si](=O)[O-]",
        ["CaO"] = "O=[Ca]",
        ["CaCl2"] = "[Ca+2].[Cl-].[Cl-]",
        ["CaCO3"] = "[Ca+2].[O-]C(=O)[O-]",
        ["BaSO4"] = "[Ba+2].[O-]S(=O)(=O)[O-]",
        ["MgO"] = "O=[Mg]",
        ["MgCl2"] = "Cl[Mg]Cl",
        ["Mg(OH)2"] = "O[Mg]O",
        ["Al2O3"] = "O=[Al]O[Al]=O",
        ["AlCl3"] = "Cl[Al](Cl)Cl",
        ["Al(OH)3"] = "O[Al](O)O",
        ["Fe2O3"] = "O=[Fe]O[Fe]=O",
        ["FeCl2"] = "Cl[Fe]Cl",
        ["FeCl3"] = "Cl[Fe](Cl)Cl",
        ["Fe(OH)3"] = "O[Fe](O)O",
        ["CuO"] = "O=[Cu]",
        ["CuSO4"] = "[Cu+2].[O-]S(=O)(=O)[O-]",
        ["Cu(OH)2"] = "O[Cu]O",
        ["AgCl"] = "[Ag+].[Cl-]",
        ["KBr"] = "[K+].[Br-]",
    };

    /// <summary>
    /// 获取分子式对应的 2D molblock（含原子坐标），供 3Dmol 渲染。
    /// </summary>
    /// <param name="formula">分子式（如 C2H5OH、NaCl）。</param>
    /// <returns>molblock 字符串；无法生成时返回 null。</returns>
    public static string? GetMolBlock(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;

        if (!SmilesMap.TryGetValue(formula, out var smiles))
        {
            return null;
        }

        try
        {
            using var mol = RDKFuncs.SmilesToMol(smiles);
            if (mol is null) return null;

            // 生成 2D 坐标
            RDKFuncs.set2DCoords(mol);

            // 导出 molblock
            var molblock = RDKFuncs.MolToMolBlock(mol);
            return string.IsNullOrWhiteSpace(molblock) ? null : molblock;
        }
        catch
        {
            // RDKit 解析失败时静默降级，不影响解题
            return null;
        }
    }

    /// <summary>
    /// 判断某个分子式是否有可用的 3D 结构数据。
    /// </summary>
    public static bool HasStructure(string formula) =>
        !string.IsNullOrWhiteSpace(formula) && SmilesMap.ContainsKey(formula);
}
