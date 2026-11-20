using System.Collections.Concurrent;
using GraphMolWrap;

namespace CogniThink.Chem.Core.Molecules;

/// <summary>
/// 构造异构体计数器（高中口径：仅构造异构，不考虑立体异构）。
/// <para>真值源：Prüfer 序列枚举所有 n 碳骨架树 → 价键过滤（碳度数 ≤4）→
/// 树转 SMILES → RDKit 规范式去重。RDKit 规范 SMILES 默认忽略立体化学，
/// 与高中"不考虑立体异构"约定一致。</para>
/// <para>铁律：n 越界 / 枚举异常 → 返回空集合（让上层诚实 null），绝不臆造数目。
/// 已对照教材值验证：C4=2 / C5=3 / C6=5 / C7=9 / C8=18；丁基(C4)=4 / 戊基(C5)=8；
/// C7H16 含 3 个甲基 = 3。</para>
/// <para>性能：每个 n 的结果静态缓存，只算一次；n≤8 上限（高中极少超过 C7；
/// C8 冷启动约 27s，仅在题目确实请求 C8 时触发）。</para>
/// </summary>
public static class ConstitutionalIsomerCounter
{
    /// <summary>支持的最大碳数（Prüfer 枚举复杂度 n^(n-2)，C8 已需 26 万次）。</summary>
    public const int MaxCarbons = 8;

    private static readonly ConcurrentDictionary<int, HashSet<string>> AlkaneCache = new();
    private static readonly ConcurrentDictionary<(int n, int methyl), int> MethylFilterCache = new();

    /// <summary>
    /// n 个碳的烷烃（CnH2n+2）构造异构体规范 SMILES 集合。
    /// n 不在 [1, <see cref="MaxCarbons"/>] → 返回空集合。
    /// </summary>
    public static HashSet<string> EnumerateAlkanes(int n)
    {
        if (n < 1 || n > MaxCarbons) return new HashSet<string>(StringComparer.Ordinal);
        return AlkaneCache.GetOrAdd(n, ComputeAlkanes);
    }

    /// <summary>n 个碳的烷烃构造异构体数目。</summary>
    public static int CountAlkaneIsomers(int n) => EnumerateAlkanes(n).Count;

    /// <summary>
    /// CnH2n+1 烷基异构体数目 = 所有 CnH2n+2 烷烃中"可取代碳位点"的等价类数之和。
    /// 可取代位点：该碳至少连一个 H（季碳无 H 不能取代，排除）。
    /// 等价类判定：对该碳做 13C 同位素标记后再规范，规范串相同即等价。
    /// </summary>
    public static int CountAlkylIsomers(int n)
    {
        if (n < 1 || n > MaxCarbons) return 0;
        var total = 0;
        foreach (var canon in EnumerateAlkanes(n))
            total += DistinctSubstitutionSites(canon);
        return total;
    }

    /// <summary>含恰好 <paramref name="methylCount"/> 个甲基（端基 -CH3）的 CnH2n+2 烷烃异构体数目。</summary>
    public static int CountAlkaneIsomersWithMethylCount(int n, int methylCount)
    {
        if (n < 1 || n > MaxCarbons) return 0;
        return MethylFilterCache.GetOrAdd((n, methylCount), key =>
        {
            var cnt = 0;
            foreach (var canon in EnumerateAlkanes(key.n))
                if (MethylCount(canon) == key.methyl) cnt++;
            return cnt;
        });
    }

    // ===================== 内核：Prüfer 枚举 → SMILES → 规范去重 =====================

    private static HashSet<string> ComputeAlkanes(int n)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (n == 1) { result.Add("C"); return result; }

        var seq = new int[n - 2];
        long total = 1;
        for (var i = 0; i < n - 2; i++) total *= n;

        for (long code = 0; code < total; code++)
        {
            var c = code;
            for (var i = 0; i < n - 2; i++) { seq[i] = (int)(c % n); c /= n; }

            var tree = PrueferToTree(n, seq);
            // 碳价键 ≤ 4：任一度数 > 4 的树是无效烷烃，跳过
            var valid = true;
            for (var i = 0; i < n; i++)
                if (tree[i].Count > 4) { valid = false; break; }
            if (!valid) continue;

            var smiles = TreeToSmiles(n, tree);
            ROMol? mol;
            try { mol = RDKFuncs.SmilesToMol(smiles); } catch { continue; }
            if (mol is null) continue;
            var canon = RDKFuncs.MolToSmiles(mol);
            mol.Dispose();
            result.Add(canon);
        }
        return result;
    }

    /// <summary>Prüfer 序列 → 无根树邻接表（Cayley 双射）。</summary>
    private static List<int>[] PrueferToTree(int n, int[] seq)
    {
        var adj = new List<int>[n];
        for (var i = 0; i < n; i++) adj[i] = new List<int>();
        var deg = new int[n];
        for (var i = 0; i < n; i++) deg[i] = 1;
        for (var i = 0; i < seq.Length; i++) deg[seq[i]]++;

        for (var i = 0; i < seq.Length; i++)
        {
            var leaf = -1;
            for (var k = 0; k < n; k++)
                if (deg[k] == 1) { leaf = k; break; }
            adj[leaf].Add(seq[i]);
            adj[seq[i]].Add(leaf);
            deg[leaf]--;
            deg[seq[i]]--;
        }

        var a = -1;
        var b = -1;
        for (var k = 0; k < n; k++)
        {
            if (deg[k] != 1) continue;
            if (a < 0) a = k; else { b = k; break; }
        }
        if (a >= 0 && b >= 0)
        {
            adj[a].Add(b);
            adj[b].Add(a);
        }
        return adj;
    }

    /// <summary>
    /// 树 → SMILES：每个孩子都写成 "(子树)" 分支，确保全部连到当前节点。
    /// （若把首孩子内联、后续孩子加括号，后续分支会连到子树内的原子，是 bug。）
    /// </summary>
    private static string TreeToSmiles(int n, List<int>[] adj)
    {
        string Dfs(int node, int parent)
        {
            var kids = adj[node].Where(x => x != parent).OrderBy(x => x).ToList();
            var s = "C";
            foreach (var k in kids) s += "(" + Dfs(k, node) + ")";
            return s;
        }
        return Dfs(0, -1);
    }

    /// <summary>某烷烃异构体中可取代碳位点的等价类数（= 该骨架贡献的烷基异构体数）。</summary>
    private static int DistinctSubstitutionSites(string canon)
    {
        using var mol = RDKFuncs.SmilesToMol(canon);
        if (mol is null) return 0;
        var n = (int)mol.getNumAtoms();
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < n; i++)
        {
            using var atom0 = mol.getAtomWithIdx((uint)i);
            if ((int)atom0.getTotalNumHs() == 0) continue; // 季碳无 H，不能作取代点
            using var cp = RDKFuncs.SmilesToMol(canon);
            using var atom = cp.getAtomWithIdx((uint)i);
            atom.setIsotope(13);
            set.Add(RDKFuncs.MolToSmiles(cp));
        }
        return set.Count;
    }

    /// <summary>甲基数 = 重原子度数为 1 的碳数（端基 -CH3）。</summary>
    private static int MethylCount(string canon)
    {
        using var mol = RDKFuncs.SmilesToMol(canon);
        if (mol is null) return 0;
        var n = (int)mol.getNumAtoms();
        var cnt = 0;
        for (var i = 0; i < n; i++)
        {
            using var atom = mol.getAtomWithIdx((uint)i);
            if (atom.getSymbol() == "C" && (int)atom.getDegree() == 1) cnt++;
        }
        return cnt;
    }
}
