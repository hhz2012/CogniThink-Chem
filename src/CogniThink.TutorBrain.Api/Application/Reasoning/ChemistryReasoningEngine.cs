namespace CogniThink.TutorBrain.Application.Reasoning;

using CogniThink.TutorBrain.Domain;

/// <summary>
/// 高中化学推断规则引擎。
/// 判定依据：标准推理表达式 + 必备关键词 + 矛盾词检测。
/// 红线段：物质共存、反应能否发生等结论只由本引擎给出，LLM 不参与判定。
/// </summary>
public sealed class ChemistryReasoningEngine : IReasoningEngine
{
    public Subject Subject => Subject.Chemistry;

    /// <summary>与"发生反应/生成沉淀/变色"矛盾的表述</summary>
    private static readonly string[] NegativeSignals =
    {
        "不反应", "无明显现象", "无沉淀", "不共存", "不能共存", "无颜色变化", "不生成"
    };

    /// <summary>条件类关键词：遗漏时归为"条件漏看"</summary>
    private static readonly string[] ConditionKeywords =
    {
        "过量", "少量", "足量", "酸性", "碱性", "中性", "加热", "点燃", "催化剂", "标准状况", "常温"
    };

    public Task<StepEvaluation> EvaluateAsync(
        Problem problem,
        ProblemStep step,
        string studentStep,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Evaluate(step, studentStep));
    }

    /// <summary>同步判定，便于单元测试与状态机直接调用</summary>
    public static StepEvaluation Evaluate(ProblemStep step, string studentStep)
    {
        var actual = AnswerNormalizer.Normalize(studentStep);
        var expected = AnswerNormalizer.Normalize(step.ExpectedExpression);
        if (actual.Length == 0) return StepEvaluation.Ungradable("未填写推断内容");

        if (actual == expected)
            return new StepEvaluation(StepVerdict.Correct, ErrorTag.None, "与标准推断一致", Array.Empty<string>());

        // 关键词分组（同义词用/分隔，必需要素用,分隔）
        var keywordGroups = AnswerNormalizer.SplitKeywordGroups(step.ExpectedKeywords);

        // 1) 矛盾判断：题干要求"能反应/有现象"，学生却写"不反应/无现象"
        var expectedHasReaction = ContainsAny(expected, "反应", "沉淀", "气体", "颜色", "变色", "生成", "→");
        if (expectedHasReaction && ContainsAny(actual, NegativeSignals))
        {
            return new StepEvaluation(
                StepVerdict.Incorrect,
                ErrorTag.CoexistenceJudgement,
                "结论方向与反应事实相反",
                Array.Empty<string>());
        }

        // 1.5) 否定词矛盾检测：
        // 标准答案含否定词（不能/不含/无），学生答案去掉否定后命中关键词 → 结论相反
        var expectedNegative = ContainsAny(expected, "不", "无", "没有", "非");
        var actualNegative = ContainsAny(actual, "不", "无", "没有", "非");
        if (expectedNegative != actualNegative)
        {
            // 否定方向不一致，且学生命中了关键词 → 结论相反
            var strippedActual = actual.Replace("不", "").Replace("无", "").Replace("没有", "").Replace("非", "");
            var anyHit = keywordGroups.Any(g => g.Any(k => strippedActual.Contains(k) || actual.Contains(k)));
            if (anyHit)
            {
                return new StepEvaluation(
                    StepVerdict.Incorrect,
                    ErrorTag.ConceptConfusion,
                    "结论方向与标准答案相反",
                    Array.Empty<string>());
            }
        }

        // 2) 关键词覆盖度判定（同义词组：每组命中一个即可）
        var groupHits = keywordGroups.Select(g => g.Any(k => actual.Contains(k))).ToArray();
        var hitCount = groupHits.Count(h => h);
        var missingGroups = keywordGroups
            .Where((_, i) => !groupHits[i])
            .Select(g => g[0])
            .ToArray();

        // 2.1) 干扰词检测：答案含错误额外信息
        var distractorHits = AnswerNormalizer
            .SplitKeywords(step.DistractorKeywords)
            .Any(k => actual.Contains(k));

        // 2.2) 模糊词检测：方向对但太笼统
        var vagueHits = AnswerNormalizer
            .SplitKeywords(step.VagueKeywords)
            .Any(k => actual.Contains(k));

        if (keywordGroups.Count > 0)
        {
            var coverage = (double)hitCount / keywordGroups.Count;
            if (coverage >= 1.0)
            {
                // 所有必需要素组都命中（每组至少一个同义词）
                if (distractorHits)
                {
                    // 必备词全中但混入错误信息 → 部分正确
                    return new StepEvaluation(
                        StepVerdict.PartiallyCorrect,
                        ErrorTag.ConceptConfusion,
                        "关键结论正确但含有错误额外信息",
                        missingGroups);
                }

                return new StepEvaluation(
                    StepVerdict.Correct,
                    ErrorTag.None,
                    "关键结论与条件均正确",
                    Array.Empty<string>());
            }

            if (coverage >= 0.5)
            {
                var tag = InferMissingTag(missingGroups, step.ExpectedExpression);
                return new StepEvaluation(
                    StepVerdict.PartiallyCorrect,
                    tag,
                    "主要结论正确，但缺少部分关键要素",
                    missingGroups);
            }

            // 3) 只命中少量关键词组：概念混淆或计算失误
            if (coverage > 0)
            {
                var tag = ContainsAny(actual, "mol", "g", "l", "ml", "%")
                    ? ErrorTag.Calculation
                    : ErrorTag.ConceptConfusion;
                return new StepEvaluation(StepVerdict.Incorrect, tag, "关键结论不匹配", missingGroups);
            }
        }

        // 4) 完全偏离：若命中模糊词，说明方向对但太笼统 → 部分正确
        if (vagueHits)
        {
            return new StepEvaluation(
                StepVerdict.PartiallyCorrect,
                ErrorTag.ReasoningJump,
                "方向正确但表述过于笼统，未答出关键要素",
                missingGroups);
        }

        // 5) 完全偏离：区分计算与概念
        var finalTag = ContainsAny(actual, "mol", "g", "l", "ml", "%")
            ? ErrorTag.Calculation
            : ErrorTag.ConceptConfusion;
        return new StepEvaluation(StepVerdict.Incorrect, finalTag, "与标准推断不符", missingGroups);
    }

    private static ErrorTag InferMissingTag(IReadOnlyList<string> missing, string expected)
    {
        if (missing.Count == 0) return ErrorTag.ReasoningJump;

        // 缺少条件词 → 条件漏看
        if (missing.Any(m => ConditionKeywords.Any(c => m.Contains(AnswerNormalizer.Normalize(c)))))
            return ErrorTag.ConditionMissed;

        // 缺少沉淀/气体/颜色等结论词 → 共存判断或概念混淆
        if (missing.Any(m => ContainsAny(m, "沉淀", "气体", "颜色", "变色", "共存")))
            return ErrorTag.CoexistenceJudgement;

        // 缺少数字/单位 → 计算失误
        if (missing.Any(m => m.Any(char.IsDigit)))
            return ErrorTag.Calculation;

        // 表达式含"因为/所以"但缺关系 → 推理跳跃
        return expected.Contains("→") ? ErrorTag.ReasoningJump : ErrorTag.ConceptConfusion;
    }

    private static bool ContainsAny(string text, params string[] candidates) =>
        candidates.Any(c => text.Contains(AnswerNormalizer.Normalize(c)));
}
