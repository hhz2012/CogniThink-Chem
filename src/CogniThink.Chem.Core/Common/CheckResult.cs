namespace CogniThink.Chem.Core.Common;

/// <summary>
/// 单项校验结果。一次 <see cref="CheckReport"/> 由若干 CheckResult 组成，
/// 每项对应一个独立的确定性检查（如"原子守恒"、"电荷守恒"、"沉淀符号"）。
/// </summary>
public sealed class CheckResult
{
    /// <summary>检查项名称（如 "原子守恒"、"电荷守恒"、"弱电解质拆写"）。</summary>
    public string CheckName { get; init; } = "";

    /// <summary>是否通过。</summary>
    public bool Passed { get; init; }

    /// <summary>
    /// 结论明细。通过时可为空；失败时必须给出可直接定位错误的描述
    /// （如 "Fe 不守恒：左 3，右 2"），供伴思层转化为引导提问。
    /// </summary>
    public string Detail { get; init; } = "";

    /// <summary>本检查项的风险标签，取 <see cref="RiskTags"/> 常量。</summary>
    public string RiskTag { get; init; } = RiskTags.None;

    /// <summary>快捷构造：通过项。</summary>
    public static CheckResult Pass(string checkName, string detail = "") =>
        new() { CheckName = checkName, Passed = true, Detail = detail };

    /// <summary>快捷构造：失败项。</summary>
    public static CheckResult Fail(string checkName, string detail, string riskTag) =>
        new() { CheckName = checkName, Passed = false, Detail = detail, RiskTag = riskTag };
}
