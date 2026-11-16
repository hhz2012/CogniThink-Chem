using CogniThink.Chem.Core.Diagnostics;

namespace CogniThink.Chem.Core.Rules;

/// <summary>候选结论的守恒校验器（L0 硬约束）。正式实现由机理层在 T2 注入。</summary>
public interface IConservationValidator
{
    ValidationReport Validate(Proposition proposition);
}

/// <summary>占位实现：不做校验。仅在机理层校验器接入前使用（见 TODO(T2-6)）。</summary>
public sealed class PassThroughConservationValidator : IConservationValidator
{
    public ValidationReport Validate(Proposition proposition) => new("conservation");
}

public enum VerdictKind
{
    /// <summary>没有规则被激活。</summary>
    NoMatch = 0,

    /// <summary>裁决出唯一（或一组等价）结论。</summary>
    Selected = 1,

    /// <summary>存在并列候选，无法在不猜测的前提下裁决。</summary>
    Unresolved = 2
}

/// <summary>被淘汰的候选及理由，是"可检查"的关键输出。</summary>
public sealed class RejectedCandidate
{
    public RejectedCandidate(Rule rule, string stage, string reason)
    {
        Rule = rule;
        Stage = stage;
        Reason = reason;
    }

    public Rule Rule { get; }

    public string Stage { get; }

    public string Reason { get; }
}

/// <summary>裁决过程的一步记录。</summary>
public sealed class DecisionTrace
{
    public DecisionTrace(string stage, string message)
    {
        Stage = stage;
        Message = message;
    }

    public string Stage { get; }

    public string Message { get; }
}

/// <summary>仲裁结果。</summary>
public sealed class Verdict
{
    public VerdictKind Kind { get; init; }

    public IReadOnlyList<Rule> Selected { get; init; } = Array.Empty<Rule>();

    public IReadOnlyList<Rule> Candidates { get; init; } = Array.Empty<Rule>();

    public IReadOnlyList<RejectedCandidate> Rejected { get; init; } = Array.Empty<RejectedCandidate>();

    public IReadOnlyList<Rule> AppliedConstraints { get; init; } = Array.Empty<Rule>();

    public IReadOnlyList<DecisionTrace> Trace { get; init; } = Array.Empty<DecisionTrace>();

    public string? Note { get; init; }

    public override string ToString()
    {
        var head = Kind switch
        {
            VerdictKind.Selected => $"选中：{string.Join(", ", Selected.Select(r => r.Id))}",
            VerdictKind.Unresolved => $"并列候选：{string.Join(", ", Selected.Select(r => r.Id))}",
            _ => "无可用规则"
        };

        return Note is null ? head : $"{head}（{Note}）";
    }
}
