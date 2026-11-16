namespace CogniThink.Chem.Core.Diagnostics;

/// <summary>
/// 通用校验报告。用于知识点、规则集、题库等数据资产的结构与完整性检查。
/// </summary>
public sealed class ValidationReport
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public ValidationReport()
    {
    }

    public ValidationReport(string scope)
    {
        Scope = scope;
    }

    public string Scope { get; init; } = "";

    public IReadOnlyList<string> Errors => _errors;

    public IReadOnlyList<string> Warnings => _warnings;

    public bool Ok => _errors.Count == 0;

    public ValidationReport AddError(string message)
    {
        _errors.Add(message);
        return this;
    }

    public ValidationReport AddWarning(string message)
    {
        _warnings.Add(message);
        return this;
    }

    public ValidationReport Merge(ValidationReport other, string? prefix = null)
    {
        foreach (var e in other.Errors)
        {
            _errors.Add(prefix is null ? e : $"{prefix}{e}");
        }

        foreach (var w in other.Warnings)
        {
            _warnings.Add(prefix is null ? w : $"{prefix}{w}");
        }

        return this;
    }

    public override string ToString()
    {
        var scope = string.IsNullOrEmpty(Scope) ? "" : $"[{Scope}] ";
        if (Ok && _warnings.Count == 0)
        {
            return $"{scope}校验通过";
        }

        var lines = new List<string> { $"{scope}错误 {_errors.Count} 条，警告 {_warnings.Count} 条" };
        lines.AddRange(_errors.Select(e => $"  ERROR {e}"));
        lines.AddRange(_warnings.Select(w => $"  WARN  {w}"));
        return string.Join(Environment.NewLine, lines);
    }
}
