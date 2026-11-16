using CogniThink.Chem.Core.Diagnostics;
using CogniThink.Chem.Core.Knowledge;

namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 规则集体检：完整性、引用一致性、例外声明一致性、冲突。
/// 发布门槛：无错误（冲突数必须为 0）。
/// </summary>
public static class RuleSetValidator
{
    public static ValidationReport Validate(IEnumerable<Rule> rules, KnowledgeBase knowledge)
    {
        var list = rules.ToList();
        var report = new ValidationReport("rules");
        var byId = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in list)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                report.AddError("存在缺少 id 的规则");
                continue;
            }

            if (!byId.TryAdd(rule.Id, rule))
            {
                report.AddError($"规则 id 重复：{rule.Id}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.Conclusion.Predicate) || string.IsNullOrWhiteSpace(rule.Conclusion.Subject))
            {
                report.AddError($"{rule.Id} 结论缺少谓词或主体");
            }

            if (string.IsNullOrWhiteSpace(rule.KnowledgeId) || !knowledge.TryGet(rule.KnowledgeId, out _))
            {
                report.AddError($"{rule.Id} 归属知识点未登记：{rule.KnowledgeId}");
            }

            if (rule.Layer == RuleLayer.Exception && rule.Preconditions.Count == 0)
            {
                report.AddError($"{rule.Id} 是例外规则但没有触发条件（不允许无条件覆盖）");
            }

            if (rule.Layer == RuleLayer.Exception && rule.Overrides.Count == 0)
            {
                report.AddWarning($"{rule.Id} 是例外规则但未声明覆盖对象");
            }

            if (rule.Tests.Count == 0)
            {
                report.AddWarning($"{rule.Id} 无显式回归用例，将使用自动激活冒烟用例");
            }
        }

        foreach (var rule in list)
        {
            foreach (var target in rule.Overrides)
            {
                if (!byId.ContainsKey(target))
                {
                    report.AddError($"{rule.Id} 覆盖了未登记的规则：{target}");
                }
            }

            foreach (var exception in rule.Exceptions)
            {
                if (!byId.TryGetValue(exception, out var exceptionRule))
                {
                    report.AddError($"{rule.Id} 声明了未登记的例外：{exception}");
                    continue;
                }

                if (exceptionRule.Layer != RuleLayer.Exception)
                {
                    report.AddWarning($"{rule.Id} 的例外 {exception} 不是例外层规则");
                }

                if (!exceptionRule.Overrides.Contains(rule.Id, StringComparer.OrdinalIgnoreCase))
                {
                    report.AddWarning($"{rule.Id} 声明例外 {exception}，但该例外未声明覆盖它（不对称）");
                }
            }
        }

        foreach (var conflict in ConflictScanner.Scan(list))
        {
            report.AddError($"规则冲突：{conflict}");
        }

        return report;
    }
}
