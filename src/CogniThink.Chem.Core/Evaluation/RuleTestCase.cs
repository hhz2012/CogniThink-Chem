using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Evaluation;

/// <summary>一条规则回归用例：给定事实上下文，期望得到指定的裁决结果。</summary>
public sealed class RuleTestCase
{
    public string Id { get; set; } = "";

    /// <summary>被测规则（用于报表定位，不限制其他规则参与竞争）。</summary>
    public string RuleId { get; set; } = "";

    public Dictionary<string, string[]> Context { get; set; } = new();

    public List<string> ExpectedSelected { get; set; } = new();

    /// <summary>Selected / Unresolved / NoMatch；为空视为 Selected。</summary>
    public string? ExpectedKind { get; set; }

    public string? Note { get; set; }

    public FactContext ToContext() => FactContext.From(Context);
}
