namespace CogniThink.Chem.Core.Common;

/// <summary>
/// 统一风险标签常量。引擎所有校验输出只能使用这里定义的标签，
/// 便于伴思层按标签选择引导话术。标签语义：
/// <list type="bullet">
/// <item><see cref="None"/>：无风险。</item>
/// <item><see cref="OutOfScope"/>：陌生物质/陌生反应/超纲官能团，引擎不评价其化学正确性。</item>
/// <item>其余：确定性校验失败分类。</item>
/// </list>
/// </summary>
public static class RiskTags
{
    /// <summary>无风险。</summary>
    public const string None = "";

    /// <summary>超出高中课标范围（陌生物质、陌生反应、超纲官能团）。引擎只做形式校验，不评价化学正确性。</summary>
    public const string OutOfScope = "超纲风险";

    /// <summary>方程式无法配平（守恒方程组无正整数解）。</summary>
    public const string NoSolution = "方程无解";

    /// <summary>方程式欠定（零空间维数大于 1，物种组合不完整或有歧义）。</summary>
    public const string Underdetermined = "方程欠定";

    /// <summary>原子或电荷不守恒。</summary>
    public const string ConservationViolation = "守恒违反";

    /// <summary>离子方程式书写错误（弱电解质拆写、离子共存形式等）。</summary>
    public const string IonicNotationError = "离子书写错误";

    /// <summary>沉淀/气体状态符号使用错误。</summary>
    public const string StateSymbolError = "状态符号错误";

    /// <summary>氧化还原分析失败或电子转移不守恒。</summary>
    public const string RedoxError = "氧化还原错误";

    /// <summary>与课内反应知识库条目矛盾（同一反应物组合的产物记录不一致）。</summary>
    public const string KnowledgeConflict = "知识库矛盾";

    /// <summary>分子结构/官能团校验问题。</summary>
    public const string StructureError = "结构错误";

    /// <summary>输入格式无法解析。</summary>
    public const string ParseError = "格式错误";
}
