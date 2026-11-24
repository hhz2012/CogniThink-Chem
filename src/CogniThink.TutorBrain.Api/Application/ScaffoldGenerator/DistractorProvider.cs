namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 2: 干扰项提供者。
// 策略（用户确认）：确定性库为主 + 上下文相关 + LLM 补充（Task 3 接入）。
//   - B 步：方程式中的「非变价元素」作干扰项（让学生区分变价/不变价，强贴合题干）。
//   - E 步：方程式中的「反应物物种」作干扰项（让学生区分产物/反应物）。
//   - A 步及其余：静态常见物种/元素库兜底。
// 红线：所有干扰项必须与 correct 不相交（相交即丢弃），由 OptionsBuilder 最终校验。
// ============================================================

public interface IDistractorProvider
{
    /// <summary>返回候选干扰项原始值集合（未过滤 correct；过滤由 OptionsBuilder 统一做）。</summary>
    IReadOnlyList<string> GetDistractors(AtomicTaskKind kind, StepCorrect correct, EngineArtifacts artifacts);
}

public sealed class StaticDistractorProvider : IDistractorProvider
{
    public IReadOnlyList<string> GetDistractors(AtomicTaskKind kind, StepCorrect correct, EngineArtifacts artifacts)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        // —— 上下文相关干扰项（优先，最贴合题干） ——
        var parsed = EngineArtifactsBuilder.GetParsed(artifacts);
        if (parsed is not null)
        {
            if (kind == AtomicTaskKind.B)
            {
                // 非变价元素 = 方程式全部元素 − 变价元素(correct)
                var allElements = parsed.AllSpecies.SelectMany(s => s.Elements.Keys).Distinct();
                foreach (var e in allElements) result.Add(e);
            }
            else if (kind == AtomicTaskKind.E)
            {
                // 反应物作产物的干扰项
                foreach (var t in parsed.Left) result.Add(t.Species.Formula);
            }
        }

        // —— 静态库兜底（按 stepKind） ——
        foreach (var d in StaticLibrary(kind)) result.Add(d);

        return result.ToList();
    }

    /// <summary>
    /// 按 stepKind 的常见混淆项库。覆盖 ≥10 项（TR-2.2），按 kind 分布。
    /// 这些是「与该步 correct 同类但常见易错」的候选项；最终是否采用由 OptionsBuilder 过滤 correct 后决定。
    /// </summary>
    private static IReadOnlyList<string> StaticLibrary(AtomicTaskKind kind) => kind switch
    {
        // A 步（粒子）：常见体系物种 / 离子，学生易多填或漏填
        AtomicTaskKind.A => new[]
        {
            "H2O", "H+", "OH-", "Na+", "Cl-", "SO4^2-", "NO3-", "K+", "Ca2+", "CO3^2-",
            "Fe2+", "Fe3+", "Cu2+", "Zn2+", "MnO4-", "Ag+", "Ba2+", "Mg2+", "Al3+", "NH4+"
        },
        // B 步（变价元素）：常见金属/非金属元素，学生易误判为变价
        AtomicTaskKind.B => new[]
        {
            "Na", "K", "Ca", "Mg", "Al", "Zn", "Fe", "Cu", "Ag", "Cl", "O", "S", "N", "H", "Mn", "Cr"
        },
        // E 步（产物）：常见错误产物 / 相关物种
        AtomicTaskKind.E => new[]
        {
            "H2O", "H2", "O2", "Cl2", "NaCl", "NaOH", "H2SO4", "HCl", "Cu", "Fe", "AgCl", "BaSO4", "CO2", "NH3"
        },
        // F 步（选项字母）：选择题常见选项字母作为干扰
        AtomicTaskKind.F => new[] { "A", "B", "C", "D" },
        _ => Array.Empty<string>(),
    };
}
