namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 电极反应/半反应/方程 正确性事实库的 POCO 包装。
/// 数据位于 data/highschool/electrode-reaction-facts.json，由 RuleStore 按
/// "highschool.electrode-reaction-facts" 键反序列化整个对象后取 Correct/Incorrect 列表。
/// 列表中的字符串均为经 <see cref="RedoxChoiceValidator.NormRedoxEq"/> 归一化后的范式。
/// </summary>
public sealed class ElectrodeReactionFactData
{
    public List<string> Correct { get; set; } = new();
    public List<string> Incorrect { get; set; } = new();
}
