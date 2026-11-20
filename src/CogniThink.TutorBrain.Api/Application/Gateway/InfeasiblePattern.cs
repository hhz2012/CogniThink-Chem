namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 离子方程式「守恒但化学错」的高置信不可行模式（POCO 包装）。
/// 数据位于 data/highschool/infeasible-equation-patterns.json，由 RuleStore 按
/// "infeasible-equation-patterns" 键加载。每条规则的 AllOf 正则全部命中时，判定该方程化学上不可行。
/// </summary>
public sealed class InfeasiblePatternData
{
    public List<InfeasibleRule> Rules { get; set; } = new();
}

public sealed class InfeasibleRule
{
    /// <summary>必须全部命中的正则模式（对归一化后的方程文本做 Regex.IsMatch）。</summary>
    public List<string> AllOf { get; set; } = new();
    /// <summary>命中后给出的判定理由。</summary>
    public string Reason { get; set; } = "";
}
