namespace CogniThink.TutorBrain.Application.Agent;

using CogniThink.TutorBrain.Domain;

/// <summary>
/// 离线规则话术生成器（默认实现）。
/// 特点：零依赖、可预测、严格遵循"不直接给答案"。
/// </summary>
public sealed class RuleBasedNarrativeAgent : ITutorNarrativeAgent
{
    public Task<string> ComposeAsync(NarrativeRequest request, CancellationToken cancellationToken = default)
    {
        var kp = request.KnowledgeNames.Count > 0 ? string.Join("、", request.KnowledgeNames) : "本题关键条件";
        var scene = request.Scene;

        string message = scene switch
        {
            NarrativeScene.StepCorrect => kp.Length > 0
                ? $"这一步成立。先别急着往下：你刚才依据的是「{kp}」的哪个条件？用一句话说明后再继续。"
                : "这一步成立。说说你刚才用到的依据是什么，再继续下一步。",

            NarrativeScene.PartialMistake => request.HintLevel switch
            {
                0 or 1 => $"方向可能对，但还不完整。别急着改答案，先回头检查：题干里有没有你还没用上的条件？",
                2 => request.HintText is not null
                    ? $"我们拆小一点：{request.HintText}"
                    : $"先回答一个子问题：这一步真正要证明/判断的对象是什么？",
                _ => request.HintText is not null
                    ? $"给你一个线索：{request.HintText}"
                    : $"注意「{kp}」的关键结论，再对照你的表述。"
            },

            NarrativeScene.KeyMistake => request.HintLevel switch
            {
                0 or 1 => $"这个结论和事实存在冲突，但先不告诉你错在哪。回到题干，把与「{kp}」相关的条件逐条对照一遍。",
                2 => request.HintText is not null
                    ? $"换个角度想：{request.HintText}"
                    : $"先判断这一步涉及的对象是否真的满足「{kp}」的前提条件。",
                3 => request.HintText is not null
                    ? $"给你一个关键线索：{request.HintText}"
                    : $"重点看「{kp}」的适用条件，你的结论是否满足它？",
                _ => request.HintText is not null
                    ? $"分步来：{request.HintText}"
                    : $"按这个顺序重写：先列条件 → 再判断能否发生 → 最后写结论。"
            },

            NarrativeScene.EscalatingHint => request.HintText is not null
                ? $"你已经试了几次，我们降一级：{request.HintText}"
                : $"你已经试了几次，先把这一步拆成更小的问题：这一步需要用到「{kp}」的哪条结论？",

            NarrativeScene.LevelComplete =>
                $"关卡通过。你已经走通整条推理链。接下来会给你一道同知识点的变式题，换一个条件再走一遍。",

            NarrativeScene.FormatIssue =>
                "这一步暂时无法判定。请用完整的推理语句作答（写出依据和结论），不要只写一个词。",

            _ => "再想一想，回到题干条件。"
        };

        return Task.FromResult(message);
    }
}
