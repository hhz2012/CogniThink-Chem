namespace CogniThink.Chem.Core.Common;

/// <summary>
/// 统一校验报告。所有引擎入口（ChemicalCheckerService、IonicEquationChecker 等）
/// 一律返回该结构，字段约定见总设计：
/// IsValid / ErrorMessage / RiskTag / CheckItems。
/// </summary>
public sealed class CheckReport
{
    private readonly List<CheckResult> _items = new();

    /// <summary>
    /// 整体是否通过：无任何失败检查项且未设置 ErrorMessage。
    /// 该属性为计算属性，始终与 <see cref="CheckItems"/> 一致，避免手工维护产生漂移。
    /// </summary>
    public bool IsValid => ErrorMessage.Length == 0 && _items.TrueForAll(i => i.Passed);

    /// <summary>
    /// 汇总错误信息（首个致命错误，如解析失败）。
    /// 单项检查失败不写这里，写 <see cref="CheckItems"/>。
    /// </summary>
    public string ErrorMessage { get; set; } = "";

    /// <summary>
    /// 整体风险标签：取所有检查项中优先级最高的标签
    /// （<see cref="RiskTags.OutOfScope"/> 优先，其次取首个非空标签）。
    /// </summary>
    public string RiskTag
    {
        get
        {
            var tag = RiskTags.None;
            foreach (var item in _items)
            {
                if (item.RiskTag == RiskTags.None)
                {
                    continue;
                }

                if (item.RiskTag == RiskTags.OutOfScope)
                {
                    return RiskTags.OutOfScope;
                }

                if (tag == RiskTags.None)
                {
                    tag = item.RiskTag;
                }
            }

            return tag;
        }
    }

    /// <summary>全部单项检查结果（按管线执行顺序）。</summary>
    public IReadOnlyList<CheckResult> CheckItems => _items;

    /// <summary>追加一个检查项，返回自身以支持链式写法。</summary>
    public CheckReport Add(CheckResult item)
    {
        _items.Add(item);
        return this;
    }

    /// <summary>追加一个通过项。</summary>
    public CheckReport AddPass(string checkName, string detail = "") =>
        Add(CheckResult.Pass(checkName, detail));

    /// <summary>追加一个失败项。</summary>
    public CheckReport AddFail(string checkName, string detail, string riskTag) =>
        Add(CheckResult.Fail(checkName, detail, riskTag));

    /// <summary>将另一份报告的检查项合并进来（用于子检查器结果汇入总入口）。</summary>
    public CheckReport Merge(CheckReport other)
    {
        _items.AddRange(other._items);
        if (ErrorMessage.Length == 0 && other.ErrorMessage.Length > 0)
        {
            ErrorMessage = other.ErrorMessage;
        }

        return this;
    }

    /// <summary>所有失败项的明细拼接，便于日志与伴思层消费。</summary>
    public string FailureSummary() =>
        string.Join("；", _items.Where(i => !i.Passed).Select(i => $"{i.CheckName}：{i.Detail}"));

    public override string ToString() =>
        IsValid ? $"校验通过（{_items.Count} 项）" : $"校验未通过：{FailureSummary()}";
}
