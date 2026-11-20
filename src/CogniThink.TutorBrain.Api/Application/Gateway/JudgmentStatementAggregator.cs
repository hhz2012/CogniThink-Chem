namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 正误判断题（填 √/×）概念聚合求解器（Task 2）。
/// <para>
/// 把一道 √/× 题拆成小题（复用 <see cref="MultiSubCorrectnessSolver"/> 的切分口径），逐小题判定：
/// <list type="bullet">
/// <item>方程小题 → <see cref="EquationCorrectnessJudge"/>（符号引擎真值）；</item>
/// <item>概念/事实小题 → <see cref="CompositeStatementJudge"/>（事实库匹配 / 计数 / 氧化还原电子转移）。</item>
/// </list>
/// </para>
/// <para>
/// <b>聚合铁律（守 precision=1.0，不蒙数）</b>：仅当全部小题都得到确定判决（无 Undecided / 无 null）
/// 才输出 √/× 序列；任一小题不确定 → 整题返回 <c>null</c>（不输出部分答案——错一位会被采纳却判分不中 → precision 掉）。
/// 缺图/文本畸形/超出建模的小题其陈述无法被任何真值源确定 → 自然落 Undecided → 整题诚实 null。
/// </para>
/// <para>
/// 仅在 <c>judgment</c> 分支（<see cref="LlmGatewayService.CallJudgment"/>）中、
/// <see cref="MultiSubCorrectnessSolver"/>（纯方程正误）之后调用：纯方程题仍由 MultiSub 先命中，字节级不变；
/// 混合 / 纯概念题由本聚合器接管。不参与 <c>reaction_check</c> 链路（其采纳路径无 IsAnswerCorrect 预校验），零回归。
/// </para>
/// </summary>
public static class JudgmentStatementAggregator
{
    // 复用既有陈述判定原语：事实库匹配 + 计数 + 氧化还原电子转移（不新增任何单题策展事实）。
    private static readonly CompositeStatementJudge StatementJudge = new(
        new FactBaseStatementJudge(),
        new CountingStatementJudge(),
        new RedoxElectronStatementJudge());

    /// <summary>尝试求解；不命中（非判断题 / 任一小题不确定）返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = !string.IsNullOrWhiteSpace(req.RawText) ? req.RawText : (req.StudentAnswer ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!MultiSubCorrectnessSolver.LooksLikeJudgmentQuestion(text)) return null;

        try
        {
            var subs = MultiSubCorrectnessSolver.SplitSubQuestions(text);
            if (subs.Count == 0) return null;

            var symbols = new List<char>(subs.Count);
            var steps = new List<string>(subs.Count);
            foreach (var sub in subs)
            {
                // 1) 先试方程抽取：是方程小题 → 走符号引擎真值
                var (equation, context) = MultiSubCorrectnessSolver.ExtractEquationAndContext(sub.Text);
                if (!string.IsNullOrWhiteSpace(equation))
                {
                    var v = EquationCorrectnessJudge.Judge(equation, context);
                    if (!v.IsDetermined) return null;   // 方程不确定 → 整题 null（不蒙数）
                    symbols.Add(v.Symbol!.Value);
                    steps.Add($"第({sub.Index})小题[方程]：{v.Symbol} — {string.Join("；", v.Reasons)}");
                    continue;
                }

                // 2) 非方程小题 → 取核心陈述，走 CompositeStatementJudge
                var core = MultiSubCorrectnessSolver.ExtractSubCore(sub.Text);
                if (string.IsNullOrWhiteSpace(core)) return null;
                var verdict = StatementJudge.Judge(text, core);
                if (verdict == Verdict.Undecided) return null;   // 任一不确定 → 整题 null（不蒙数）
                var sym = verdict == Verdict.True
                    ? EquationCorrectnessJudge.Right
                    : EquationCorrectnessJudge.Wrong;
                symbols.Add(sym);
                steps.Add($"第({sub.Index})小题[陈述]：{sym}");
            }

            return new EngineResponse
            {
                Ok = true,
                FinalResult = MultiSubCorrectnessSolver.FormatVerdict(symbols),
                DerivationSteps = steps,
                ConstraintNote = "【高中限定】逐小题方程/概念判定聚合；任一小题不确定则整题不输出（不蒙数、零假阳性）",
                Confidence = 1.0
            };
        }
        catch
        {
            // 任何意外异常都不拖垮网关链路；保守回退 null → 由后续判断求解器链接管
            return null;
        }
    }
}
