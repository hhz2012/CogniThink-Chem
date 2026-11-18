namespace CogniThink.TutorBrain.Application.Ability;

using CogniThink.TutorBrain.Domain;

/// <summary>
/// 单次作答对各能力的影响记录。
/// </summary>
public sealed class AbilityImpact
{
    public ChemistryAbility Ability { get; set; }
    public string AbilityName { get; set; } = string.Empty;

    /// <summary>本次作答对该能力的得分贡献（正=答对提升，负=答错降低）</summary>
    public double ScoreDelta { get; set; }

    /// <summary>该能力本次是否为主要考察能力</summary>
    public bool IsPrimary { get; set; }

    /// <summary>影响原因（用于可解释性）</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 能力-表现映射器：把学生单步作答的 verdict + errorTag + hintLevel
/// 转换为对各能力得分的影响。
///
/// 评分规则：
/// - 答对：该步骤考察的所有能力 +α（主考察能力增益更大）
/// - 答错：根据 errorTag 定位薄弱能力 -β；其余考察能力轻微 -γ
/// - hintLevel 越高（越需要提示），增益越小、惩罚越大
/// - 部分正确：主考察能力 +α/2，薄弱能力 -β/2
/// </summary>
public static class AbilityPerformanceMapper
{
    private const double AlphaCorrect = 0.12;    // 答对主能力增益
    private const double AlphaSecondary = 0.06;  // 答对次能力增益
    private const double BetaWrong = 0.18;       // 答错薄弱能力惩罚
    private const double GammaWrong = 0.08;      // 答错其他考察能力惩罚
    private const double MinScore = 0.0;
    private const double MaxScore = 1.0;

    /// <summary>
    /// 计算单步作答对各能力的影响。
    /// </summary>
    /// <param name="knowledgeId">本步知识点</param>
    /// <param name="verdict">判定结果</param>
    /// <param name="errorTag">错因标签</param>
    /// <param name="hintLevel">放出的提示级别（0~4）</param>
    public static List<AbilityImpact> Map(
        string knowledgeId,
        StepVerdict verdict,
        ErrorTag errorTag,
        int hintLevel)
    {
        var impacts = new List<AbilityImpact>();
        var abilities = ChemistryAbilityLibrary.GetAbilitiesForKnowledge(knowledgeId);
        if (abilities.Count == 0) return impacts;

        // 提示依赖系数：hintLevel 越高，能力表现越弱
        var hintFactor = 1.0 - Math.Clamp(hintLevel, 0, 4) / 8.0;

        if (verdict == StepVerdict.Correct)
        {
            // 答对：所有考察能力提升，主能力提升更多
            for (var i = 0; i < abilities.Count; i++)
            {
                var isPrimary = i == 0;
                var delta = (isPrimary ? AlphaCorrect : AlphaSecondary) * hintFactor;
                impacts.Add(new AbilityImpact
                {
                    Ability = abilities[i],
                    AbilityName = ChemistryAbilityLibrary.Get(abilities[i]).Name,
                    ScoreDelta = delta,
                    IsPrimary = isPrimary,
                    Reason = isPrimary ? "答对主考察能力，提升" : "答对次考察能力，小幅提升"
                });
            }
        }
        else if (verdict == StepVerdict.PartiallyCorrect)
        {
            // 部分正确：主能力半幅提升，薄弱能力半幅惩罚
            var weakAbility = ChemistryAbilityLibrary.GetAbilityForErrorTag(errorTag);
            for (var i = 0; i < abilities.Count; i++)
            {
                var isPrimary = i == 0;
                var ab = abilities[i];
                double delta;
                string reason;
                if (weakAbility.HasValue && ab == weakAbility.Value)
                {
                    delta = -BetaWrong * 0.5 * (1.0 / hintFactor);
                    reason = "部分正确但暴露该能力薄弱，轻微降低";
                }
                else
                {
                    delta = (isPrimary ? AlphaCorrect : AlphaSecondary) * 0.5 * hintFactor;
                    reason = "部分正确，小幅提升";
                }
                impacts.Add(new AbilityImpact
                {
                    Ability = ab,
                    AbilityName = ChemistryAbilityLibrary.Get(ab).Name,
                    ScoreDelta = delta,
                    IsPrimary = isPrimary,
                    Reason = reason
                });
            }
        }
        else if (verdict == StepVerdict.Incorrect)
        {
            // 答错：薄弱能力大幅惩罚，其余考察能力轻微惩罚
            var weakAbility = ChemistryAbilityLibrary.GetAbilityForErrorTag(errorTag);
            foreach (var ab in abilities)
            {
                double delta;
                string reason;
                if (weakAbility.HasValue && ab == weakAbility.Value)
                {
                    delta = -BetaWrong * (1.0 / hintFactor);
                    reason = $"答错且错因为「{errorTag.ToText()}」，该能力薄弱，降低";
                }
                else
                {
                    delta = -GammaWrong * (1.0 / hintFactor);
                    reason = "答错，相关能力轻微降低";
                }
                impacts.Add(new AbilityImpact
                {
                    Ability = ab,
                    AbilityName = ChemistryAbilityLibrary.Get(ab).Name,
                    ScoreDelta = delta,
                    IsPrimary = ab == abilities[0],
                    Reason = reason
                });
            }
        }
        // Ungradable 不更新能力

        return impacts;
    }

    /// <summary>裁剪能力得分到 [0, 1]</summary>
    public static double Clamp(double score) => Math.Clamp(score, MinScore, MaxScore);
}
