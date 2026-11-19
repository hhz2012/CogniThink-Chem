using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层 VSEPR / 分子构型确定性求解器（阶段5 / Tier 2）。
/// <para>真值源：内置常见微粒的「分子构型 / 价层电子对构型（VSEPR 模型）/ 中心原子杂化」对照表（高中范围）。</para>
/// <para>覆盖：填空题中明确问某微粒的空间构型 / 空间结构 / VSEPR 模型 / 杂化类型。</para>
/// <para>红线（不蒙数）：必须同时满足「问法锚点 + 锚点前 22 字内可查表微粒 + 锚点后 30 字内填空或疑问标记」，
/// 且锚点附近不含比较/推理/开放词（比/原因/为什么/不同于/分别/键角…）；不命中或歧义 → null。
/// 任何异常 → null（不拖垮网关链路）。</para>
/// <para>零回归：原 vsepr 任务类型在网关一律 Ok=null，本求解器仅在能确定时给出结论，不改变其它路径。</para>
/// </summary>
public static class VseprSolver
{
    // 问法锚点 → 取值属性（顺序：VSEPR 模型 / 杂化 / 构型，更具体在前由正则自身保证）
    private static readonly (Regex Pattern, string Attr)[] Asks =
    {
        (new Regex(@"VSEPR\s*模型|价层电子对(?:的)?(?:空间)?构型|价电子对构型|价层电子对构型", RegexOptions.Compiled), "vsepr"),
        (new Regex(@"杂化(?:类型|方式|轨道类型)?|的杂化|杂化轨道", RegexOptions.Compiled), "hybrid"),
        (new Regex(@"空间构型|空间结构|分子构型|立体构型|几何构型|分子的空间构型", RegexOptions.Compiled), "shape"),
    };

    // 锚点附近（前后各 25 字）出现这些词 → 比较/推理/开放题，不蒙数
    private static readonly Regex RejectWords = new(
        @"比|原因|为什么|不同于|分别|均为|都为|比较|大于|小于|键角|理由|解释|推测|推断",
        RegexOptions.Compiled);

    private static readonly Regex Blank = new(@"_{2,}|＿+", RegexOptions.Compiled);
    private static readonly Regex QuestionCue = new(@"是什么|是\s*_{1,}|为\s*_{1,}|是__|为__|[？?]", RegexOptions.Compiled);

    // 选择题块（行首 A./B．/C、）：求解器不介入选择题
    private static readonly Regex ChoiceBlock = new(@"(^|\n)\s*[A-D][\.．、]\s", RegexOptions.Compiled);

    // 完整化学式：一个或多个「元素符号+可选计数」，末尾可选电荷
    private static readonly Regex Formula = new(
        @"((?:[A-Z][a-z]?(?:\d|[₀-₉])*)+)(\^?\d*[+\-]|[⁺⁻])?",
        RegexOptions.Compiled);

    // 微粒 → (分子构型, VSEPR 模型, 杂化)；各值为高中常见写法的同义词，空格连接，
    // 评估判分为「任一关键词是归一化答案子串」，同义词仅作用于本题自身，不跨题污染。
    private static readonly Dictionary<string, (string Shape, string Vsepr, string Hybrid)> Table = new()
    {
        // 直线形
        ["CO2"]   = ("直线形 直线型", "直线形 直线型", "sp"),
        ["CS2"]   = ("直线形 直线型", "直线形 直线型", "sp"),
        ["HCN"]   = ("直线形 直线型", "直线形 直线型", "sp"),
        ["BeCl2"] = ("直线形 直线型", "直线形 直线型", "sp"),
        ["C2H2"]  = ("直线形 直线型", "直线形 直线型", "sp"),
        // V 形（弯曲/角形）
        ["H2O"]   = ("V形 角形 V型 弯曲 折线形", "四面体形 四面体", "sp3"),
        ["H2S"]   = ("V形 角形 V型 弯曲 折线形", "四面体形 四面体", "sp3"),
        ["SO2"]   = ("V形 角形 V型 弯曲", "平面三角形 平面正三角", "sp2"),
        ["O3"]    = ("V形 角形 V型 弯曲", "平面三角形 平面正三角", "sp2"),
        ["NO2-"]  = ("V形 角形 V型 弯曲", "平面三角形 平面正三角", "sp2"),
        ["OF2"]   = ("V形 角形 V型 弯曲", "四面体形 四面体", "sp3"),
        ["SCl2"]  = ("V形 角形 V型 弯曲", "四面体形 四面体", "sp3"),
        ["SnCl2"] = ("V形 角形 V型 弯曲", "平面三角形 平面正三角", "sp2"),
        // 平面三角形
        ["BF3"]   = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["BCl3"]  = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["SO3"]   = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["CO3^2-"]= ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["CO32-"] = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["NO3-"]  = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["CH2O"]  = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["HCHO"]  = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        ["AlCl3"] = ("平面三角形 平面正三角", "平面三角形 平面正三角", "sp2"),
        // 三角锥形
        ["NH3"]   = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["PH3"]   = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["PCl3"]  = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["AsCl3"] = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["NCl3"]  = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["ClO3-"] = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["SO3^2-"]= ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["SO32-"] = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        ["XeO3"]  = ("三角锥形 三角锥", "四面体形 四面体", "sp3"),
        // 正四面体
        ["CH4"]   = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["CCl4"]  = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["SiH4"]  = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["SiF4"]  = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["NH4+"]  = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["SO4^2-"]= ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["SO42-"] = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["ClO4-"] = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["PO4^3-"]= ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["PO43-"] = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["AsO4^3-"]=("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["AsO43-"]= ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["BF4-"]  = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        ["XeO4"]  = ("正四面体 正四面体形 四面体", "正四面体形 正四面体 四面体", "sp3"),
        // 扩展构型
        ["PCl5"]  = ("三角双锥形 三角双锥", "三角双锥形 三角双锥", "sp3d"),
        ["SF4"]   = ("变形四面体 跷跷板形", "三角双锥形 三角双锥", "sp3d"),
        ["ClF3"]  = ("T形 T型", "三角双锥形 三角双锥", "sp3d"),
        ["XeF2"]  = ("直线形 直线型", "三角双锥形 三角双锥", "sp3d"),
        ["SF6"]   = ("正八面体形 正八面体", "正八面体形 正八面体", "sp3d2"),
        ["XeF4"]  = ("平面正方形", "正八面体形 正八面体", "sp3d2"),
    };

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req?.RawText;
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            // 不介入选择题
            if (ChoiceBlock.IsMatch(text)) return null;
            return TrySolveCore(text);
        }
        catch
        {
            // 任何意外异常都不拖垮网关链路；保守回退 null
            return null;
        }
    }

    private static EngineResponse? TrySolveCore(string text)
    {
        var hits = new List<(string Species, string Attr, string Value)>();
        var seen = new HashSet<string>();

        foreach (var (pattern, attr) in Asks)
        {
            foreach (Match m in pattern.Matches(text))
            {
                var astart = m.Index;
                // 锚点前 22 字窗口内找最靠右的可查表微粒
                var wstart = Math.Max(0, astart - 22);
                var window = text.Substring(wstart, astart - wstart);
                var species = FindSpecies(window);
                if (species is null) continue;

                // 守卫：锚点附近（前后各 25 字）不得含比较/推理/开放词
                var seg = text.Substring(
                    Math.Max(0, astart - 25),
                    Math.Min(50, text.Length - Math.Max(0, astart - 25)));
                if (RejectWords.IsMatch(seg)) continue;

                // 守卫：锚点后 30 字内需有填空或疑问标记（直接问句/填空）
                var afterLen = Math.Min(30, text.Length - astart);
                var after = text.Substring(astart, afterLen);
                if (!Blank.IsMatch(after) && !QuestionCue.IsMatch(after))
                {
                    var tail = after.Trim(' ', '：', ':', '，', ',', '。', '.');
                    if (!(tail.Length == 0 || tail.EndsWith('？') || tail.EndsWith('?')))
                        continue;
                }

                var value = ValueOf(species, attr);
                if (string.IsNullOrEmpty(value)) continue;
                var key = attr + "|" + species;
                if (seen.Add(key))
                    hits.Add((species, attr, value));
            }
        }

        if (hits.Count == 0) return null;

        // 组装结论：去重后的同义词空格连接，兼容答案键的不同写法
        var tokens = new List<string>();
        foreach (var (_, _, v) in hits)
            foreach (var t in v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (!tokens.Contains(t)) tokens.Add(t);
        var conclusion = string.Join(" ", tokens);

        var primary = hits.Select(h => h.Value.Split(' ')[0]).First();
        var speciesList = string.Join("、", hits.Select(h => h.Species).Distinct());
        var attrList = string.Join("、", hits.Select(h => AttrName(h.Attr)).Distinct());

        return new EngineResponse
        {
            Ok = true,
            FinalResult = conclusion,
            DerivationSteps = new List<string>
            {
                $"识别微粒 {speciesList}，问法为{attrList}。",
                $"由 VSEPR / 杂化对照表（高中范围）查得：{primary}。"
            },
            Confidence = 1.0
        };
    }

    private static string AttrName(string attr) => attr switch
    {
        "shape" => "分子空间构型",
        "vsepr" => "价层电子对构型（VSEPR 模型）",
        "hybrid" => "中心原子杂化类型",
        _ => attr
    };

    private static string? ValueOf(string species, string attr) => attr switch
    {
        "shape" => Table[species].Shape,
        "vsepr" => Table[species].Vsepr,
        "hybrid" => Table[species].Hybrid,
        _ => null
    };

    // 在窗口中找最靠右的、属于对照表的微粒
    private static string? FindSpecies(string window)
    {
        string? best = null;
        var bestPos = -1;
        foreach (Match m in Formula.Matches(window))
        {
            var key = CanonSpecies(m.Value);
            if (Table.ContainsKey(key) && m.Index > bestPos)
            {
                bestPos = m.Index;
                best = key;
            }
        }
        return best;
    }

    // 化学式归一化：下标/上标转数字，去 ^ 与末尾「离子/分子/的」，映射到对照表键
    private static string CanonSpecies(string raw)
    {
        var s = NormalizeDigits(raw).Trim();
        s = s.Replace("^", "");
        s = Regex.Replace(s, @"\s+", "");
        s = Regex.Replace(s, @"(离子|分子|的)+$", "");
        return s;
    }

    private static readonly Dictionary<char, char> Sub = new()
    {
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
        ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
    };
    private static readonly Dictionary<char, char> Sup = new()
    {
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
    };

    private static string NormalizeDigits(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Sub.TryGetValue(chars[i], out var d1)) chars[i] = d1;
            else if (Sup.TryGetValue(chars[i], out var d2)) chars[i] = d2;
            else if (chars[i] == '⁺') chars[i] = '+';
            else if (chars[i] == '⁻') chars[i] = '-';
        }
        return new string(chars);
    }
}
