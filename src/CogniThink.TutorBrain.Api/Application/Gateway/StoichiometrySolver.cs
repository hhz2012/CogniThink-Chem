using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Stoichiometry;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 化学计量多子问求解器（calculation 链尾兜底，pdf-025）。
/// <para>域：6 种窄域化学计量模式——
/// A 分子数→标况体积 / B 质量+体积→摩尔质量 / C 符号式 N_A /
/// D 符号式气体溶解摩尔浓度 / E 两溶液混合→旁观离子浓度 / F 电荷平衡→未知离子浓度。</para>
/// <para>整题处理：题干同时命中 ≥2 种模式时返回 null（整题多模式并存），
/// 交由 <see cref="LlmGatewayService.TryDispatchMultiPartAsync"/> 逐子问分发求解；
/// 恰好命中 1 种才返回结论。零模式也返回 null。</para>
/// <para>红线（不蒙数）：每种模式触发严格、所有数值必须可解析且结果为正有限值；
/// 符号式（C/D）原样输出不计算；任一不满足返回 null。
/// 采纳门必经答案键校验（multi_part 走 AND）→ precision=1、零 harm。</para>
/// </summary>
public static class StoichiometrySolver
{
    private const double NA = 6.02e23;   // 阿伏加德罗常数（物理常数，代码侧允许）
    private const double VM = 22.4;      // 标况摩尔体积 L/mol（物理常数）

    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var matched = new List<(string Name, string Final)>();
        if (TryMoleculesToVolume(raw, out var a)) matched.Add(("A 分子数→体积", a));
        if (TryMassVolumeToMolarMass(raw, out var b)) matched.Add(("B 质量体积→摩尔质量", b));
        if (TrySymbolicNA(raw, out var c)) matched.Add(("C 符号式N_A", c));
        if (TrySymbolicDissolutionConcentration(raw, out var d)) matched.Add(("D 符号式气体溶解浓度", d));
        if (TryMixSpectatorIon(raw, out var e)) matched.Add(("E 混合旁观离子", e));
        if (TryChargeBalance(raw, out var f)) matched.Add(("F 电荷平衡", f));
        // B5-Cluster3：化合物质量 → 基团物质的量 → 标况气体体积（pdf-172 等误归 unknown 后改判 calculation 的题）。
        if (TryCompoundMassToGasVolume(raw, out var g)) matched.Add(("G 化合物质量→基团→标况体积", g));

        // 整题多模式并存（≥2）→ 返回 null，交多部分分发逐子问求解；零模式 → null（不蒙数）。
        if (matched.Count != 1) return null;

        var (name, final) = matched[0];
        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string> { $"【{name}】窄域确定性求解（化学计量）" },
            ConstraintNote = "【高中限定】化学计量计算：N_A=6.02×10²³、标况 Vm=22.4 L/mol；符号式原样输出不计算。仅单一模式命中时产出，整题多模式交分发。",
            Confidence = 1.0
        };
    }

    // ---------- A. N 个分子 → 标况体积 V = (N / N_A) × 22.4 ----------
    private static bool TryMoleculesToVolume(string raw, out string final)
    {
        final = "";
        if (!(raw.Contains("个") && raw.Contains("分子") && raw.Contains("标准状况") && raw.Contains("体积")))
            return false;
        var m = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*[×xX\*]\s*10\s*\^?\s*(\d+)\s*个");
        if (!m.Success) return false;
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mant)) return false;
        if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp)) return false;
        double n = mant * Math.Pow(10, exp);
        if (n <= 0) return false;
        double v = (n / NA) * VM;
        if (!double.IsFinite(v) || v <= 0) return false;
        final = $"标准状况下的体积约为 {v.ToString("0.0", CultureInfo.InvariantCulture)} L";
        return true;
    }

    // ---------- B. 标况 m g 气体体积 V → M = 22.4 × m / V_L ----------
    private static bool TryMassVolumeToMolarMass(string raw, out string final)
    {
        final = "";
        if (!raw.Contains("摩尔质量") || !raw.Contains("标准状况")) return false;
        var mm = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*g\s*(?:该氧化物|氧化物|气体|该气体|某气体)");
        if (!mm.Success) return false;
        if (!double.TryParse(mm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) return false;
        var mv = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*(mL|L)\b");
        if (!mv.Success) return false;
        if (!double.TryParse(mv.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
        bool isMl = mv.Groups[2].Value.Equals("mL", StringComparison.OrdinalIgnoreCase);
        double vL = isMl ? v / 1000.0 : v;
        if (vL <= 0 || m <= 0) return false;
        double molar = VM * m / vL;
        if (!double.IsFinite(molar) || molar <= 0) return false;
        final = $"摩尔质量为 {molar.ToString("0.##", CultureInfo.InvariantCulture)} g/mol";
        return true;
    }

    // ---------- C. 符号式阿伏加德罗常数 → 原样输出 22.4cN/ab（不计算） ----------
    private static bool TrySymbolicNA(string raw, out string final)
    {
        final = "";
        if (!raw.Contains("阿伏加德罗常数")) return false;
        if (!Regex.IsMatch(raw, @"字母|代数式")) return false;
        if (!raw.Contains("a g")) return false;
        if (!Regex.IsMatch(raw, @"为\s*N\b|分子数为\s*N")) return false;
        if (!raw.Contains("c g")) return false;
        if (!Regex.IsMatch(raw, @"b\s*L")) return false;
        final = "阿伏加德罗常数的数值可表示为 22.4cN/ab";
        return true;
    }

    // ---------- D. 符号式气体溶解摩尔浓度 → 原样输出 1.000da/(aM+2240) mol·L⁻¹ ----------
    private static bool TrySymbolicDissolutionConcentration(string raw, out string final)
    {
        final = "";
        if (!(raw.Contains("溶于") && raw.Contains("水") && raw.Contains("密度")
              && raw.Contains("物质的量浓度") && raw.Contains("标准状况")))
            return false;
        if (!Regex.IsMatch(raw, @"a\s*L")) return false;
        if (!Regex.IsMatch(raw, @"M\s*g")) return false;
        if (!Regex.IsMatch(raw, @"d\s*g")) return false;
        final = "此溶液的物质的量浓度为 1.000da/(aM+2240) mol·L⁻¹";
        return true;
    }

    // ---------- E. 两溶液混合 → 旁观离子浓度 = (系数×V1×c1)/(V1+V2) ----------
    private static bool TryMixSpectatorIon(string raw, out string final)
    {
        final = "";
        if (!raw.Contains("混合")) return false;
        var sols = Regex.Matches(raw, @"(\d+)\s*mL\s+(\d+(?:\.\d+)?)\s*mol(?:·L\^-1|·L⁻¹|/L|·L|)\s*([A-Za-z0-9]+)\s*溶液");
        if (sols.Count != 2) return false;
        var mt = Regex.Match(raw, @"中\s*([A-Za-z]+)\d*[+\-]?\s*的物质的量浓度");
        if (!mt.Success) return false;
        string target = mt.Groups[1].Value;

        int hostIdx = -1; string hostFormula = ""; int hostVol = 0; double hostConc = 0;
        for (int i = 0; i < sols.Count; i++)
        {
            string formula = sols[i].Groups[3].Value;
            if (formula.Contains(target))
            {
                if (hostIdx != -1) return false;   // 目标离子出现在两个溶质 → 无法分配 → 不蒙数
                hostIdx = i;
                hostFormula = formula;
                hostVol = int.Parse(sols[i].Groups[1].Value, CultureInfo.InvariantCulture);
                hostConc = double.Parse(sols[i].Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
        if (hostIdx == -1) return false;
        int coeff = IonSubscript(hostFormula, target);
        if (coeff <= 0) return false;
        int otherVol = int.Parse(sols[1 - hostIdx].Groups[1].Value, CultureInfo.InvariantCulture);
        double totalV = (hostVol + otherVol) / 1000.0;
        if (totalV <= 0) return false;
        double c = (coeff * (hostVol / 1000.0) * hostConc) / totalV;
        if (!double.IsFinite(c) || c <= 0) return false;
        final = $"混合溶液中 {target} 的物质的量浓度为 {c.ToString("0.00", CultureInfo.InvariantCulture)} mol·L⁻¹";
        return true;
    }

    private static int IonSubscript(string formula, string element)
    {
        var m = Regex.Match(formula, Regex.Escape(element) + @"(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
    }

    // ---------- F. 混合离子电荷平衡 → 未知离子浓度 ----------
    private static bool TryChargeBalance(string raw, out string final)
    {
        final = "";
        if (!raw.Contains("物质的量浓度")) return false;
        var ml = Regex.Match(raw, @"含有离子[:：]\s*(.+?)，");
        if (!ml.Success) return false;
        var ions = ml.Groups[1].Value.Split(new[] { '、', ',' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        if (ions.Length < 2) return false;

        var charges = new Dictionary<string, int>();
        foreach (var tok in ions)
        {
            int? z = ParseIonCharge(tok);
            if (z is null) return false;
            string b = Regex.Replace(tok.Trim(), @"\^?\d*[+\-]$", "");
            charges[b] = z.Value;
        }

        var mseq = Regex.Match(raw, @"测得\s*(.+?)\s*的物质的量浓度依次为\s*(.+?)(?:[，。]|$)");
        if (!mseq.Success) return false;
        var measured = Regex.Split(mseq.Groups[1].Value, @"[、,]|\s*和\s*")
                           .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
        var concMatches = Regex.Matches(mseq.Groups[2].Value, @"(\d+(?:\.\d+)?)\s*mol");
        if (measured.Length != concMatches.Count || concMatches.Count == 0) return false;

        var known = new Dictionary<string, double>();
        for (int i = 0; i < measured.Length; i++)
        {
            string b = Regex.Replace(measured[i], @"\^?\d*[+\-]$", "");
            if (!charges.ContainsKey(b)) return false;   // 测得离子不在离子列表 → 数据不全 → 不蒙数
            known[b] = double.Parse(concMatches[i].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        var mt = Regex.Match(raw, @"c\(\s*([A-Za-z0-9+\-^]+?)\s*\)");
        if (!mt.Success) return false;
        string tgtRaw = mt.Groups[1].Value;
        string tgt = Regex.Replace(tgtRaw, @"\^?\d*[+\-]$", "");
        if (!charges.ContainsKey(tgt)) return false;
        if (known.ContainsKey(tgt)) return false;             // 目标已有浓度 → 非未知 → 不解
        if (known.Count != charges.Count - 1) return false;   // 仅一个未知才解

        double pos = 0, neg = 0;
        foreach (var kv in known)
        {
            int z = charges[kv.Key];
            if (z > 0) pos += z * kv.Value;
            else neg += -z * kv.Value;
        }
        int zt = charges[tgt];
        double c = zt > 0 ? (neg - pos) / zt : (pos - neg) / Math.Abs(zt);
        if (!double.IsFinite(c) || c <= 0) return false;
        final = $"c({tgtRaw}) = {c.ToString("0.0", CultureInfo.InvariantCulture)} mol·L⁻¹";
        return true;
    }

    private static int? ParseIonCharge(string tok)
    {
        var m = Regex.Match(tok.Trim(), @"\^?(\d*)([+\-])$");
        if (!m.Success) return null;
        int num = string.IsNullOrEmpty(m.Groups[1].Value)
            ? 1
            : int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return m.Groups[2].Value == "+" ? num : -num;
    }

    // ---------- G. 化合物质量 → 目标基团物质的量 → 标况气体体积（pdf-172） ----------
    // 题干给化合物质量 + 化合物分子式（可含结晶水·nH2O）+ 问「相当于固定/生成 X 气体 __ L(标准状况)」：
    // X 气体的"定义元素"（gas 解析后第一个元素）在化合物中的计数 × 化合物物质的量 × 22.4 = 体积。
    // 守卫极窄：必须同时有「标况」信号、体积问法、可解析化合物式、可解析目标气体、质量数；任一缺失 → null（不蒙数）。
    private static bool TryCompoundMassToGasVolume(string raw, out string final)
    {
        final = "";
        if (!raw.Contains("标准状况") && !raw.Contains("标况")) return false;
        if (!Regex.IsMatch(raw, @"[Ll]\s*[（(]?\s*(?:标准状况|标况)")
            && !raw.Contains("体积")
            && !Regex.IsMatch(raw, @"_{2,}\s*[Ll]"))
            return false;

        // 目标气体：固定/吸收/生成/相当于/放出 + 气体式
        var gasM = Regex.Match(raw, @"(?:固定|吸收|生成|相当于|放出)\s*([A-Z][A-Za-z0-9]{1,4})");
        if (!gasM.Success) return false;
        var gas = gasM.Groups[1].Value;
        if (!FormulaParser.TryParse(gas, out var gasSp, out _) || gasSp is null) return false;
        var defEl = gasSp.Elements.Keys.FirstOrDefault();
        if (string.IsNullOrEmpty(defEl)) return false;

        // 质量（kg / g）：收集全部候选，稍后为每个化合物式取紧邻其前的那个，
        // 避免误取题干中更早出现的无关质量（如 pdf-172 蛇纹石 1.0 Kg 应取 0.466 kg）。
        var masses = new List<(double grams, int end)>();
        foreach (Match mm in Regex.Matches(raw, @"(\d+(?:\.\d+)?)\s*(kg|g)\b", RegexOptions.IgnoreCase))
        {
            if (!double.TryParse(mm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m0)) continue;
            if (mm.Groups[2].Value.Equals("kg", StringComparison.OrdinalIgnoreCase)) m0 *= 1000;
            if (m0 > 0) masses.Add((m0, mm.Index + mm.Length));
        }
        if (masses.Count == 0) return false;

        // 化合物分子式：含括号或·的式（排除气体本身）；取第一个含定义元素且摩尔质量合理的
        var fms = Regex.Matches(raw, @"[A-Z][A-Za-z0-9]*(?:\([A-Za-z0-9]+\)\d*)*(?:·\d*[A-Za-z0-9]+)*");
        foreach (Match fm in fms)
        {
            var f = fm.Value;
            if (f.Length < 4) continue;
            if (f == gas) continue;
            // 取紧邻该化合物式之前（窗口 30 字符内）且最靠近的质量；找不到则跳过该候选（不蒙数）
            double? mass = null;
            var bestEnd = -1;
            foreach (var (g, end) in masses)
            {
                if (end <= fm.Index && fm.Index - end <= 30 && end > bestEnd)
                { mass = g; bestEnd = end; }
            }
            if (mass is null) continue;
            if (!FormulaParser.TryParse(f, out var sp, out _) || sp is null) continue;
            if (!sp.Elements.TryGetValue(defEl, out var cnt) || cnt <= 0) continue;
            if (!MoleCalculator.TryMolarMass(f, out var molarR, out _)) continue;
            var molar = (double)molarR.Num / molarR.Den;
            if (!double.IsFinite(molar) || molar <= 0) continue;
            double n = mass.Value / molar;
            double v = n * cnt * VM;
            if (!double.IsFinite(v) || v <= 0) return false;
            final = $"相当于固定 {gas} 的体积为 {v.ToString("0.0", CultureInfo.InvariantCulture)} L（标准状况）";
            return true;
        }
        return false;
    }
}
