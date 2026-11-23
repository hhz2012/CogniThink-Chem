using GraphMolWrap;

namespace CogniThink.Chem.Core.Molecules;

/// <summary>
/// 高中窄域、保守的中文系统命名。
/// 铁律：仅当结构可被确定性命名时返回非空；任何不确定（多取代基类型、复杂侧链、解析异常）一律返回 null，
/// 绝不臆造名称。当前覆盖：单一取代基类型的取代酚（如 2,4,6-三溴苯酚）。
/// </summary>
public static class MoleculeNamer
{
    private static readonly Dictionary<int, string> CnNum = new()
    {
        { 1, "" }, { 2, "二" }, { 3, "三" }, { 4, "四" }, { 5, "五" }, { 6, "六" },
    };

    /// <summary>
    /// 尝试对取代酚给出中文系统命名（母体“苯酚”，OH 所在碳为 1 位，取最低位号集）。
    /// 不满足“单一取代基类型 / 单一环上 OH”等确定性条件时返回 null。
    /// </summary>
    public static string? NamePhenol(ROMol? mol)
    {
        if (mol is null) return null;
        ROMol? query = null;
        try
        {
            query = RDKFuncs.SmartsToMol("[OX2H;$([OX2H]c)]c1ccccc1");
            if (query is null) return null;
            var matches = mol.getSubstructMatches(query);
            if (matches is null || matches.Count == 0) return null;

            // 取第一个匹配：match[0]=O, match[1..6]=苯环原子（按 SMARTS 顺序，match[1] 即连 OH 的碳=1 位）。
            var match = matches[0];
            if (match.Count < 7) return null;
            var ring = new int[6];
            for (int i = 0; i < 6; i++) ring[i] = match[i + 1].second;

            var ringSet = new HashSet<int>(ring);
            // 必须是 6 个不同的芳香碳
            if (ringSet.Count != 6) return null;
            foreach (var c in ring)
            {
                using var a = mol.getAtomWithIdx((uint)c);
                if (!a.getIsAromatic() || a.getSymbol() != "C") return null;
            }

            // 只能有一个环上 OH（单一母体，避免对苯二酚等歧义）
            int ohCount = 0;
            var atomCount = (int)mol.getNumAtoms();
            var symbols = new string[atomCount];
            var adj = new List<int>[atomCount];
            for (int i = 0; i < atomCount; i++) adj[i] = new List<int>();
            for (int i = 0; i < atomCount; i++)
            {
                using var a = mol.getAtomWithIdx((uint)i);
                symbols[i] = a.getSymbol();
            }
            var doubleBonds = new HashSet<(int, int)>();
            var bondCount = (int)mol.getNumBonds();
            for (int b = 0; b < bondCount; b++)
            {
                using var bond = mol.getBondWithIdx((uint)b);
                int x = (int)bond.getBeginAtomIdx(), y = (int)bond.getEndAtomIdx();
                if (x < atomCount && y < atomCount) { adj[x].Add(y); adj[y].Add(x); }
                if (bond.getBondType().ToString() == "DOUBLE")
                {
                    doubleBonds.Add((x, y));
                    doubleBonds.Add((y, x));
                }
            }
            foreach (var c in ring)
            {
                foreach (var j in adj[c])
                {
                    if (ringSet.Contains(j) || symbols[j] == "H") continue;
                    using var a = mol.getAtomWithIdx((uint)j);
                    if (symbols[j] == "O" && a.getTotalNumHs() == 1) ohCount++;
                }
            }
            if (ohCount != 1) return null;

            // 两个方向取最低位号集（IUPAC）
            int[] forward = ring;                         // ring[0]=1, ring[1]=2, ..., ring[5]=6
            int[] reverse = { ring[0], ring[5], ring[4], ring[3], ring[2], ring[1] };

            var fwd = Collect(forward, ringSet, symbols, adj, doubleBonds, mol);
            var rev = Collect(reverse, ringSet, symbols, adj, doubleBonds, mol);
            if (fwd is null || rev is null) return null;

            var best = CompareLocants(fwd.Value.Locants, rev.Value.Locants) <= 0 ? fwd : rev;
            // 保守：仅单一取代基类型才命名（多类型的先后顺序高中规则复杂，避免出错）
            if (best.Value.Groups.Count != 1) return null;

            var (name, positions) = best.Value.Groups.First();
            positions.Sort();
            var posStr = string.Join(",", positions);
            var prefix = CnNum.TryGetValue(positions.Count, out var p) ? p : "";
            return $"{posStr}-{prefix}{name}苯酚";
        }
        catch
        {
            return null;
        }
        finally
        {
            query?.Dispose();
        }
    }

    private static (int[] Locants, Dictionary<string, List<int>> Groups)? Collect(
        int[] ringOrder, HashSet<int> ringSet, string[] symbols, List<int>[] adj, HashSet<(int, int)> doubleBonds, ROMol mol)
    {
        // pos 以原子索引为键（原子索引可能 ≥ 环长，必须用字典，不能用定长数组）。
        var pos = new Dictionary<int, int>();
        for (int i = 0; i < ringOrder.Length; i++) pos[ringOrder[i]] = i + 1;

        var groups = new Dictionary<string, List<int>>();
        for (int i = 1; i < ringOrder.Length; i++) // 跳过 1 位（OH 母体）
        {
            int c = ringOrder[i];
            var subs = adj[c].Where(j => !ringSet.Contains(j) && symbols[j] != "H").ToList();
            if (subs.Count == 0) continue;
            if (subs.Count != 1) return null; // 同位多取代 → 不确定
            int a = subs[0];
            var key = ClassifySubstituent(a, ringSet, symbols, adj, doubleBonds, mol);
            if (key is null) return null;
            if (!groups.ContainsKey(key)) groups[key] = new List<int>();
            groups[key].Add(pos[c]);
        }
        var locants = groups.SelectMany(kv => kv.Value).OrderBy(p => p).ToArray();
        return (locants, groups);
    }

    /// <summary>判定环上一个取代基的规范名；不确定返回 null（触发整体放弃）。</summary>
    private static string? ClassifySubstituent(int a, HashSet<int> ringSet, string[] symbols, List<int>[] adj,
        HashSet<(int, int)> doubleBonds, ROMol mol)
    {
        string sym = symbols[a];
        var other = adj[a].Where(j => !ringSet.Contains(j) && symbols[j] != "H").ToList();
        using var atom = mol.getAtomWithIdx((uint)a);
        int h = (int)atom.getTotalNumHs();
        switch (sym)
        {
            case "F": return other.Count == 0 ? "氟" : null;
            case "Cl": return other.Count == 0 ? "氯" : null;
            case "Br": return other.Count == 0 ? "溴" : null;
            case "I": return other.Count == 0 ? "碘" : null;
            case "O": return h == 1 && other.Count == 0 ? "羟基" : null;
            case "N":
                // 硝基：N 与 O 之间存在双键
                foreach (var j in adj[a])
                {
                    if (symbols[j] == "O" && doubleBonds.Contains((a, j))) return "硝基";
                }
                return h >= 2 && other.Count == 0 ? "氨基" : null;
            case "C":
                return other.Count == 0 && h == 3 ? "甲基" : null;
            default:
                return null;
        }
    }

    private static int CompareLocants(int[] x, int[] y)
    {
        int n = System.Math.Min(x.Length, y.Length);
        for (int i = 0; i < n; i++)
        {
            if (x[i] != y[i]) return x[i].CompareTo(y[i]);
        }
        return x.Length.CompareTo(y.Length);
    }
}
