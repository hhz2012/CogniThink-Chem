namespace CogniThink.Chem.Core.Structure;

/// <summary>
/// 元素周期表与原子结构的确定性计算（阶段4）。
/// 纯事实/算法：原子序数 → 基态电子排布 / 价层电子排布 / 周期 / 族 / 未成对电子数。
/// 不依赖 LLM，不蒙数；所有结果由原子序数唯一确定。
/// </summary>
public static class PeriodicTable
{
    // Z -> 元素符号（H..U，覆盖高中常考范围）
    private static readonly string[] Symbols =
    {
        "", "H","He","Li","Be","B","C","N","O","F","Ne",
        "Na","Mg","Al","Si","P","S","Cl","Ar","K","Ca",
        "Sc","Ti","V","Cr","Mn","Fe","Co","Ni","Cu","Zn",
        "Ga","Ge","As","Se","Br","Kr","Rb","Sr","Y","Zr",
        "Nb","Mo","Tc","Ru","Rh","Pd","Ag","Cd","In","Sn",
        "Sb","Te","I","Xe","Cs","Ba","La","Ce","Pr","Nd",
        "Pm","Sm","Eu","Gd","Tb","Dy","Ho","Er","Tm","Yb",
        "Lu","Hf","Ta","W","Re","Os","Ir","Pt","Au","Hg",
        "Tl","Pb","Bi","Po","At","Rn","Fr","Ra","Ac","Th",
        "Pa","U"
    };

    private static readonly string[] Names =
    {
        "", "氢","氦","锂","铍","硼","碳","氮","氧","氟","氖",
        "钠","镁","铝","硅","磷","硫","氯","氩","钾","钙",
        "钪","钛","钒","铬","锰","铁","钴","镍","铜","锌",
        "镓","锗","砷","硒","溴","氪","铷","锶","钇","锆",
        "铌","钼","锝","钌","铑","钯","银","镉","铟","锡",
        "锑","碲","碘","氙","铯","钡","镧","铈","镨","钕",
        "钷","钐","铕","钆","铽","镝","钬","铒","铥","镱",
        "镥","铪","钽","钨","铼","锇","铱","铂","金","汞",
        "铊","铅","铋","钋","砹","氡","钫","镭","锕","钍",
        "镤","铀"
    };

    private static readonly Dictionary<string, int> BySymbol = BuildSymbolIndex();
    private static readonly Dictionary<string, int> ByName = BuildNameIndex();

    // 亚层填充顺序（构造原理）：(亚层, 容量)
    private static readonly (string Subshell, int Capacity)[] FillOrder =
    {
        ("1s",2),("2s",2),("2p",6),("3s",2),("3p",6),("4s",2),("3d",10),("4p",6),
        ("5s",2),("4d",10),("5p",6),("6s",2),("4f",14),("5d",10),("6p",6),
        ("7s",2),("5f",14),("6d",10),("7p",6)
    };

    // 按主量子数输出的顺序
    private static readonly string[] OutputOrder =
    {
        "1s","2s","2p","3s","3p","3d","4s","4p","4d","4f","5s","5p","5d","5f","6s","6p","6d","7s","7p"
    };

    // 洪特/半满全满例外（相对构造原理的覆盖）。仅收录高中常考元素。
    private static readonly Dictionary<int, Dictionary<string, int>> Exceptions = new()
    {
        [24] = new() { ["4s"] = 1, ["3d"] = 5 },  // Cr
        [29] = new() { ["4s"] = 1, ["3d"] = 10 }, // Cu
        [41] = new() { ["5s"] = 1, ["4d"] = 4 },  // Nb
        [42] = new() { ["5s"] = 1, ["4d"] = 5 },  // Mo
        [44] = new() { ["5s"] = 1, ["4d"] = 7 },  // Ru
        [45] = new() { ["5s"] = 1, ["4d"] = 8 },  // Rh
        [46] = new() { ["5s"] = 0, ["4d"] = 10 }, // Pd
        [47] = new() { ["5s"] = 1, ["4d"] = 10 }, // Ag
        [78] = new() { ["6s"] = 1, ["5d"] = 9 },  // Pt
        [79] = new() { ["6s"] = 1, ["5d"] = 10 }, // Au
    };

    private static readonly Dictionary<int, string> NobleGas = new()
    {
        [2] = "He", [10] = "Ne", [18] = "Ar", [36] = "Kr", [54] = "Xe", [86] = "Rn"
    };

    private static readonly char[] Superscripts = { '⁰','¹','²','³','⁴','⁵','⁶','⁷','⁸','⁹' };

    private static Dictionary<string, int> BuildSymbolIndex()
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var z = 1; z < Symbols.Length; z++) d[Symbols[z]] = z;
        return d;
    }

    private static Dictionary<string, int> BuildNameIndex()
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var z = 1; z < Names.Length; z++) d[Names[z]] = z;
        return d;
    }

    /// <summary>元素符号或中文名 → 原子序数；找不到返回 null。</summary>
    public static int? AtomicNumberOf(string? symbolOrName)
    {
        if (string.IsNullOrWhiteSpace(symbolOrName)) return null;
        var s = symbolOrName.Trim();
        if (BySymbol.TryGetValue(s, out var z)) return z;
        if (ByName.TryGetValue(s, out z)) return z;
        return null;
    }

    public static string? SymbolOf(int z) => z >= 1 && z < Symbols.Length ? Symbols[z] : null;
    public static string? NameOf(int z) => z >= 1 && z < Names.Length ? Names[z] : null;

    /// <summary>基态电子排布（中性原子）。返回 亚层->电子数，按主量子数排序。含洪特半满/全满例外。</summary>
    public static IReadOnlyDictionary<string, int> Configuration(int z)
    {
        if (z < 1) return new Dictionary<string, int>();
        var occ = FillAufbau(z);
        if (Exceptions.TryGetValue(z, out var overrides))
        {
            foreach (var (sh, c) in overrides) occ[sh] = c;
        }
        return OrderedCopy(occ);
    }

    /// <summary>
    /// 离子电子排布：阳离子从中性原子排布失电子（先失最外层 ns，再 (n-1)d，避免 Fe²⁺ 错得 Cr 例外排布）；
    /// 阴离子用构造原理补电子。不套用中性原子的洪特例外。
    /// </summary>
    public static IReadOnlyDictionary<string, int> IonConfiguration(int z, int charge)
    {
        if (z < 1) return new Dictionary<string, int>();
        if (charge == 0) return Configuration(z);
        if (charge > 0)
        {
            var occ = new Dictionary<string, int>(Configuration(z));
            // 失电子顺序：主量子数从高到低；同一主层内角量子数从高到低（f > d > p > s）。
            // 即先失 ns，再失 (n-1)d（而非 (n-1)s/p 内层）——如 Fe³⁺：失 4s² 后再失 1 个 3d。
            var removalOrder = occ.Keys
                .OrderByDescending(sh => sh[0] - '0')
                .ThenByDescending(sh => sh[1] == 'f' ? 3 : sh[1] == 'd' ? 2 : sh[1] == 'p' ? 1 : 0)
                .ToList();
            var toRemove = charge;
            foreach (var sh in removalOrder)
            {
                if (toRemove <= 0) break;
                var have = occ[sh];
                var take = System.Math.Min(have, toRemove);
                occ[sh] = have - take;
                toRemove -= take;
            }
            return OrderedCopy(occ);
        }
        // 阴离子：核外电子数 = z - charge（charge 为负）
        var elec = z - charge;
        return OrderedCopy(FillAufbau(elec));
    }

    // 纯构造原理填充（不含洪特例外），用于阴离子与离子派生。
    private static Dictionary<string, int> FillAufbau(int electrons)
    {
        var occ = new Dictionary<string, int>();
        var rem = electrons;
        foreach (var (sh, cap) in FillOrder)
        {
            if (rem <= 0) break;
            var put = System.Math.Min(cap, rem);
            occ[sh] = occ.GetValueOrDefault(sh) + put;
            rem -= put;
        }
        return occ;
    }

    // 按主量子数输出顺序过滤空亚层。
    private static IReadOnlyDictionary<string, int> OrderedCopy(Dictionary<string, int> occ)
    {
        var result = new Dictionary<string, int>();
        foreach (var sh in OutputOrder)
            if (occ.TryGetValue(sh, out var c) && c > 0) result[sh] = c;
        return result;
    }

    /// <summary>价层电子排布：主族 = 最外层 ns np；过渡 = (n-1)d + ns；锕系/镧系含 (n-2)f。</summary>
    public static IReadOnlyDictionary<string, int> ValenceConfiguration(int z, int charge = 0)
    {
        // 价层亚层集合由中性原子（元素分区）决定，不随电离改变；
        // 再用该集合过滤离子排布，避免 Fe²⁺ 失 4s 后 nmax 回落误把 3s/3p 内层算入价层。
        var neutralOcc = Configuration(z);
        if (neutralOcc.Count == 0) return neutralOcc;
        var valence = ValenceSubshells(neutralOcc);
        var occ = charge == 0 ? neutralOcc : IonConfiguration(z, charge);
        var result = new Dictionary<string, int>();
        foreach (var sh in OutputOrder)
            if (valence.Contains(sh) && occ.TryGetValue(sh, out var c) && c > 0)
                result[sh] = c;
        return result;
    }

    // 中性原子的价层亚层集合：最外层 ns/np；d 区/ds 区再加 (n-1)d（p 区的 (n-1)d 是内层，不计）；f 区再加 (n-2)f。
    private static HashSet<string> ValenceSubshells(IReadOnlyDictionary<string, int> neutralOcc)
    {
        var nmax = neutralOcc.Keys.Max(sh => sh[0] - '0');
        var set = new HashSet<string>();
        foreach (var sh in neutralOcc.Keys.Where(k => k[0] - '0' == nmax))
            set.Add(sh);
        if (!neutralOcc.Any(kv => kv.Key[0] - '0' == nmax && kv.Key[1] == 'p'))
        {
            foreach (var sh in neutralOcc.Keys.Where(k => k[0] - '0' == nmax - 1 && k[1] == 'd'))
                set.Add(sh);
            foreach (var sh in neutralOcc.Keys.Where(k => k[0] - '0' == nmax - 2 && k[1] == 'f'))
                set.Add(sh);
        }
        return set;
    }

    /// <summary>稀有气体简写，如 [Ar]3d⁸4s²。取稀有气体最后一个亚层之后的全部亚层（含 (n-1)d）。</summary>
    public static string NobleGasShorthand(int z, int charge = 0)
    {
        var cfg = charge == 0 ? Configuration(z) : IonConfiguration(z, charge);
        var prev = 0;
        foreach (var nz in NobleGas.Keys.OrderBy(x => x))
            if (nz < z) prev = nz;
        if (prev == 0) return FormatConfiguration(cfg);
        var nobleCfg = Configuration(prev);
        var lastNobleIdx = -1;
        for (var i = 0; i < OutputOrder.Length; i++)
            if (nobleCfg.ContainsKey(OutputOrder[i])) lastNobleIdx = i;
        var outer = cfg
            .Where(kv => Array.IndexOf(OutputOrder, kv.Key) > lastNobleIdx)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return $"[{NobleGas[prev]}]{FormatConfiguration(outer)}";
    }

    public static (string PeriodCn, string Group) PeriodGroup(int z)
    {
        // 周期由原子序数范围确定（如 Pd 4d¹⁰ 无 5s 电子，但属第 5 周期）。
        var nmax = z switch
        {
            <= 2 => 1, <= 10 => 2, <= 18 => 3, <= 36 => 4, <= 54 => 5, <= 86 => 6, _ => 7
        };
        var occ = Configuration(z);
        var s = occ.Where(kv => kv.Key[0] - '0' == nmax && kv.Key[1] == 's').Sum(kv => kv.Value);
        var p = occ.Where(kv => kv.Key[0] - '0' == nmax && kv.Key[1] == 'p').Sum(kv => kv.Value);
        var d = occ.Where(kv => kv.Key[0] - '0' == nmax - 1 && kv.Key[1] == 'd').Sum(kv => kv.Value);

        string grp;
        if (nmax == 1) grp = s == 1 ? "IA" : "0";
        else if (p > 0 || d == 0)
            grp = (s + p) switch
            {
                1 => "IA", 2 => "IIA", 3 => "IIIA", 4 => "IVA", 5 => "VA",
                6 => "VIA", 7 => "VIIA", 8 => "0", _ => "?"
            };
        else
            grp = (s + d) switch
            {
                3 => "IIIB", 4 => "IVB", 5 => "VB", 6 => "VIB", 7 => "VIIB",
                8 or 9 or 10 => "VIII", 11 => "IB", 12 => "IIB", _ => "?"
            };
        return (PeriodCn(nmax), grp);
    }

    /// <summary>未成对电子数（价层）。</summary>
    public static int UnpairedElectrons(int z, int charge = 0)
    {
        var occ = ValenceConfiguration(z, charge);
        return occ.Sum(kv => UnpairedInSubshell(kv.Key, kv.Value));
    }

    private static int UnpairedInSubshell(string sh, int c)
    {
        var half = sh[1] switch { 's' => 1, 'p' => 3, 'd' => 5, 'f' => 7, _ => 0 };
        // c<=half：各占一个轨道，全未成对；c>half：先填满 half 个单电子，剩余 (c-half) 个配对，
        // 未成对 = half - (c-half) = 2*half - c。
        return c <= half ? c : 2 * half - c;
    }

    private static string PeriodCn(int n) => n switch
    {
        1 => "一", 2 => "二", 3 => "三", 4 => "四", 5 => "五", 6 => "六", 7 => "七", _ => n.ToString()
    };

    /// <summary>排布式渲染：superscript=true 用上标字符，space=true 亚层间加空格。</summary>
    public static string FormatConfiguration(IReadOnlyDictionary<string, int> occ, bool superscript = true, bool space = false)
    {
        var parts = new List<string>();
        foreach (var sh in OutputOrder)
        {
            if (!occ.TryGetValue(sh, out var c) || c <= 0) continue;
            parts.Add(superscript ? $"{sh}{ToSuperscript(c)}" : $"{sh}{c}");
        }
        return string.Join(space ? " " : "", parts);
    }

    private static string ToSuperscript(int n)
    {
        var s = n.ToString();
        var chars = new char[s.Length];
        for (var i = 0; i < s.Length; i++) chars[i] = Superscripts[s[i] - '0'];
        return new string(chars);
    }
}
