using System.Collections.Generic;

namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 电离平衡 / 胶体 / 电解质概念选择题的事实规则库数据模型
/// （<c>data/highschool/equilibrium-rules.json</c>）。
/// <para>每条规则描述一个可独立判定真假的化学陈述；校验器逐选项匹配，
/// 仅当一题所有选项均被唯一规则命中、且满足问法方向的选项唯一时才断言。</para>
/// </summary>
public sealed class EquilibriumRuleData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    public string Note { get; set; } = "";

    public List<EquilibriumRule> Rules { get; set; } = new();
}

/// <summary>单条事实规则。</summary>
public sealed class EquilibriumRule
{
    /// <summary>规则编号，如 "eq-005-c"。</summary>
    public string Id { get; set; } = "";

    /// <summary>归属主题：colloid / equilibrium / electrolyte / life。</summary>
    public string Topic { get; set; } = "";

    /// <summary>
    /// 选项归一化文本必须全部包含的关键词（AND 关系）。
    /// 命中要求：每个关键词都为选项文本的子串。
    /// </summary>
    public List<string> MatchAll { get; set; } = new();

    /// <summary>
    /// 题干归一化文本必须全部包含的关键词（AND 关系）。
    /// 为空表示不限制题干；非空用于把通用选项规则限定到对应题境，避免跨题误命中。
    /// </summary>
    public List<string> StemAll { get; set; } = new();

    /// <summary>该陈述化学上是否成立（true = 正确 / 成立）。</summary>
    public bool Verdict { get; set; }

    /// <summary>判定理由（讲解用）。</summary>
    public string Reason { get; set; } = "";
}
