using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 氧化还原/电化学类选择题确定性判定器（T1：redox_balance + other 中可判定的子集）。
/// <para>当前覆盖：电极反应式/半反应/化学方程式「正确或错误」四选一。判定信号：</para>
/// <list type="bullet">
/// <item>事实库 <c>data/highschool/electrode-reaction-facts.json</c>：归一化后精确匹配已知正确/错误反应式（零假阳性）。</item>
/// <item>守恒违规（<see cref="EquationBalancer.ConservationViolations"/>）：能解析的方程若原子/电荷不守恒 → 必错。</item>
/// </list>
/// <para>红线：只有当全部选项都得到「确定对/确定错」的判定、且正确(或错误)项唯一时才返回答案，否则 null。</para>
/// </summary>
public static class RedoxChoiceValidator
{
    private static readonly Lazy<ElectrodeReactionFactData> _facts = new(() =>
    {
        try
        {
            return RuleStore.Load().Query<ElectrodeReactionFactData>("electrode-reaction-facts")
                ?? new ElectrodeReactionFactData();
        }
        catch
        {
            return new ElectrodeReactionFactData();
        }
    });

    private static HashSet<string> CorrectSet => _facts.Value.Correct.ToHashSet();
    private static HashSet<string> IncorrectSet => _facts.Value.Incorrect.ToHashSet();

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        if (req?.RawText is null) return null;
        var (stem, opts) = ChoiceVerifier.ParseOptions(req.RawText);
        if (opts.Count < 2) return null;

        // 仅处理「反应式/方程式/电极反应 正确或错误」类题干
        if (!IsReactionJudgementStem(stem)) return null;

        var askWrong = Regex.IsMatch(stem, @"错误|不正确|不相符|不符合|有误|不对|不可能");
        var verdicts = new Dictionary<string, string>(); // letter -> correct/incorrect/unknown/noneq
        foreach (var kv in opts)
        {
            var v = Judge(kv.Value);
            verdicts[kv.Key] = v;
        }

        // 全部选项必须有确定判定，否则不答（避免假阳性）
        if (verdicts.Values.Any(x => x is "unknown" or "noneq")) return null;

        var correct = verdicts.Where(kv => kv.Value == "correct").Select(kv => kv.Key).ToList();
        var incorrect = verdicts.Where(kv => kv.Value == "incorrect").Select(kv => kv.Key).ToList();

        List<string> target;
        string note;
        if (askWrong)
        {
            target = incorrect;
            note = "问错误项：" + string.Join(",", incorrect);
        }
        else
        {
            target = correct;
            note = "问正确项：" + string.Join(",", correct);
        }

        if (target.Count != 1) return null;
        var ans = target[0];
        return new ChoiceVerifier.Outcome(
            AnswerLetter: ans,
            FinalResult: $"{ans}。依据事实库与守恒判定：{note}",
            Reasons: new List<string> { note },
            ConstraintNote: "RedoxChoiceValidator：仅在全部选项均获确定判定且唯一时作答");
    }

    private static bool IsReactionJudgementStem(string stem)
    {
        // 题干须含「反应式/方程式/电极反应」且含「正确/错误」类判定词
        var hasReaction = Regex.IsMatch(stem, @"反应式|方程式|电极反应|化学方程式|离子方程式");
        var hasJudge = Regex.IsMatch(stem, @"正确|错误|不正确|相符|符合|有误|不对|可能");
        return hasReaction && hasJudge;
    }

    /// <summary>判定单个选项：correct / incorrect / unknown / noneq。</summary>
    private static string Judge(string optionText)
    {
        var eq = ExtractEquation(optionText);
        if (eq.Length == 0 || !Regex.IsMatch(eq, @"[=═→]")) return "noneq";
        var norm = NormRedoxEq(eq);
        if (norm.Length == 0) return "noneq";

        if (CorrectSet.Contains(norm)) return "correct";
        if (IncorrectSet.Contains(norm)) return "incorrect";

        // 守恒：能解析则不守恒必错；守恒通过不代表化学正确 → 仍 unknown。
        // 含电子 E 的半反应不做守恒判定（E 会被解析器当成元素，造成误判）。
        if (!norm.Contains('E') && EquationParser.TryParse(norm, out var parsed, out _) && parsed is not null)
        {
            var violations = EquationBalancer.ConservationViolations(parsed);
            if (violations.Count > 0) return "incorrect";
        }
        return "unknown";
    }

    /// <summary>从选项文本中抽出方程部分（语境:方程 取冒号后；否则取整段）。</summary>
    internal static string ExtractEquation(string optionText)
    {
        var idx = optionText.LastIndexOfAny(new[] { ':', '：' });
        var s = idx >= 0 ? optionText[(idx + 1)..] : optionText;
        return s.Trim();
    }

    /// <summary>归一化为范式：Unicode→ASCII，电子→E，等号/箭头统一，去状态符号与条件，去空白。</summary>
    public static string NormRedoxEq(string raw)
    {
        var s = IonicEquationValidator.PreNormText(raw ?? "");
        // 统一各种减号/短横线为 ASCII '-'：U+2212 减号、U+2013 短破折号
        s = s.Replace('\u2212', '-').Replace('\u2013', '-');
        // 电子 e^- / e-（PreNorm 后 e⁻→e^-）统一为 E；负向回顾避免吃掉 Fe/He 等元素尾部的 e-
        s = Regex.Replace(s, @"(?<![A-Za-z])e\^?-", "E");
        // 等号 / 箭头 / 长破折号统一为 =
        s = Regex.Replace(s, @"[═→—]+", "=");
        s = Regex.Replace(s, @"={2,}", "=");
        // 状态符号
        s = s.Replace("↑", "").Replace("↓", "");
        // 反应条件（括号内中文/相态），但保留 (x+2)、[Fe(CN)6] 等
        s = Regex.Replace(s, @"\((浓|稀|△|催化剂|通电|电解|高温|点燃|加热|沸水|冷水)\)", "");
        s = Regex.Replace(s, @"\([sglaq]\)", "");
        // 去空白
        s = Regex.Replace(s, @"\s+", "");
        return s;
    }
}
