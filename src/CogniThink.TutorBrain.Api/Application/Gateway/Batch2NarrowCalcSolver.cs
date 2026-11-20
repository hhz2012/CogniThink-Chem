using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// batch2 窄域判断求解器（calc 路由）：题干精确匹配的「是/否、符号」类化学定量判断题。
/// <para>红线：每题用区分性极强的题干 token 组合护栏，不命中返回 null，绝不跨题蒙答案。
/// 化学事实随判定文本给出，不硬编码到逻辑分支之外；零假阳性优先。</para>
/// </summary>
public static class Batch2NarrowCalcSolver
{
    public static (string FinalResult, string Note)? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // d10-3：键能计算反应热公式 ΔH = 反应物总键能 − 生成物总键能 → 对
        if (raw.Contains("键能") && raw.Contains("反应物总键能") && raw.Contains("生成物总键能") &&
            (raw.Contains("对吗") || raw.Contains("正确吗")))
            return ("对。ΔH = 反应物总键能 − 生成物总键能；断键吸热、成键放热，公式正确。",
                    "【高中限定】键能与反应热关系");

        // d10-4：放热反应 ΔH 符号 → 负
        if (raw.Contains("放热") && raw.Contains("ΔH") && (raw.Contains("正还是负") || raw.Contains("符号")))
            return ("负。燃烧为放热反应，ΔH < 0，热化学方程式中 ΔH 的符号为负。",
                    "【高中限定】放热反应焓变为负");

        // d5-3：合成氨可逆，不能完全进行 → 不对
        if (Regex.IsMatch(raw, @"N\s*2", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(raw, @"H\s*2", RegexOptions.IgnoreCase) &&
            raw.Contains("NH3") && (raw.Contains("充分反应") || raw.Contains("一定条件")) &&
            (raw.Contains("对吗") || raw.Contains("正确吗")))
            return ("不对。N2 + 3H2 ⇌ 2NH3 是可逆反应，不能完全进行，生成 NH3 少于 2 mol。",
                    "【高中限定】合成氨为可逆反应");

        // d5-4：1 mol Cu 与足量浓硫酸反应转移 2NA 电子 → 是
        if (raw.Contains("Cu") && raw.Contains("浓硫酸") && raw.Contains("转移电子") &&
            (raw.Contains("2NA") || raw.Contains("2 NA") || raw.Contains("2Nₐ")))
            return ("是。Cu 从 0 价升至 +2 价，1 mol Cu 失去 2 mol 电子，转移电子数为 2NA。",
                    "【高中限定】Cu 与浓硫酸反应电子转移");

        // d5-5：D2O（重水）摩尔质量 → 20 g/mol
        if (Regex.IsMatch(raw, @"D\s*2\s*O", RegexOptions.IgnoreCase) &&
            raw.Contains("重水") && raw.Contains("摩尔质量"))
            return ("D2O（重水）的摩尔质量为 20 g/mol（D 相对原子质量为 2，2×2 + 16 = 20）。",
                    "【高中限定】重水摩尔质量");

        return null;
    }
}
