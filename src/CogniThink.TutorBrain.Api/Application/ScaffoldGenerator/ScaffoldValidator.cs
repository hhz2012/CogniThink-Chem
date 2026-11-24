namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 4: 校验门 + 降级策略 —— 发布前最后防线，确保 precision=1 / harm=0。
// 反向核对组装好的 GeneratedScaffold 与引擎真值是否一致；不一致 → Ok=false，
// 由调用方（Task 5 ScaffoldBuilder）决定整题 null 不发布。
//
// 校验项（任一失败 → 致命错误，整题不发布）：
//   1. 关键步 A / B / E / F 必须存在且有真值；
//   2. 每步 correct 与引擎重算一致（防漂移：组装后引擎字段被改 / 提取逻辑变了）；
//   3. F 步 correct 与存储 answer 对齐（冲突检测）；
//   4. 干扰项 ∩ correct == ∅、correct ⊆ options、options 无重复；
//   5. releases 四档齐全（hint / probe / scaffold / reveal）；
//   6. provenance 审计字段齐全且与引擎一致。
// 降级策略：C / D 步无真值 → 步级降级（可发布，标 degradedSteps，渲染只读揭示），
//          不判致命；其余步无真值 → 致命。
// 本校验门只读：不改判分链路、不改题干/选项/answer/事实库。
// ============================================================

public sealed class ScaffoldValidator : IScaffoldValidator
{
    /// <summary>必须有真值的关键步（缺失 → 整题不发布）。</summary>
    private static readonly HashSet<AtomicTaskKind> KeyKinds = new()
    {
        AtomicTaskKind.A, AtomicTaskKind.B, AtomicTaskKind.E, AtomicTaskKind.F,
    };

    /// <summary>允许无真值（步级降级）的步。</summary>
    private static readonly HashSet<AtomicTaskKind> DegradableKinds = new()
    {
        AtomicTaskKind.C, AtomicTaskKind.D,
    };

    private readonly IStepTruthExtractor _extractor;

    public ScaffoldValidator(IStepTruthExtractor? extractor = null)
        => _extractor = extractor ?? new StepTruthExtractor();

    public ScaffoldValidationSummary Validate(GeneratedScaffold scaffold, EngineArtifacts artifacts)
    {
        var errors = new List<string>();
        var conflict = false;
        if (scaffold is null)
        {
            errors.Add("scaffold 为空");
            return Final(errors, conflict);
        }

        var stepsByKind = scaffold.Steps
            .GroupBy(s => s.Kind)
            .ToDictionary(g => g.Key, g => g.First());

        // 1) 关键步必须存在且有真值
        foreach (var k in KeyKinds)
        {
            if (!stepsByKind.TryGetValue(k, out var step) || step.Truth is null)
                errors.Add($"关键步 {k} 缺失真值，整题不发布");
        }

        foreach (var step in scaffold.Steps)
        {
            var kind = step.Kind;
            var expected = _extractor.Extract(kind, artifacts);

            if (step.Truth is null)
            {
                // 无真值：仅 C/D 允许降级；其余关键步已在上面报错，此处兜底
                if (!DegradableKinds.Contains(kind))
                    errors.Add($"步 {kind} 缺失真值且不允许降级");
                continue;
            }

            // 2) 有真值 → 必须与引擎重算一致（防漂移）
            if (expected is null)
            {
                errors.Add($"步 {kind} 声称有真值，但引擎无法重算（漂移）");
                continue;
            }
            if (!CorrectsEqual(step.Truth, expected))
                errors.Add($"步 {kind} correct 与引擎真值不一致（防漂移）");

            // 6) provenance 审计
            var key = kind.ToString();
            if (!scaffold.Provenance.TryGetValue(key, out var prov) || string.IsNullOrWhiteSpace(prov))
                errors.Add($"步 {kind} 缺失 provenance 溯源");
            else if (!string.IsNullOrEmpty(expected.Provenance) && prov != expected.Provenance)
                errors.Add($"步 {kind} provenance 与引擎不一致");

            // 4) + 5) options / correct / releases 完整性
            ValidateStepShape(step, errors);
        }

        // 3) F 步与存储 answer 对齐（冲突检测）
        if (stepsByKind.TryGetValue(AtomicTaskKind.F, out var fStep) && fStep.Truth is not null)
        {
            var answer = artifacts?.Case?.Answer ?? "";
            if (!CorrectMatchesAnswer(fStep.Truth, answer))
            {
                errors.Add($"F 步 correct 与存储 answer 冲突（answer={answer}）");
                conflict = true;
            }
        }

        return Final(errors, conflict);
    }

    // ---------- 内部辅助 ----------

    private static ScaffoldValidationSummary Final(List<string> errors, bool conflict)
        => new() { Ok = errors.Count == 0, Errors = errors, Conflict = conflict };

    /// <summary>比较存储 correct 与引擎重算 correct（集合相等，忽略大小写与顺序）。</summary>
    private static bool CorrectsEqual(StepGroundTruth stored, StepCorrect expected)
    {
        var storedSet = StoredCorrectSet(stored);
        var expectedSet = ScaffoldGeneratorUtils.CorrectSet(expected);
        if (storedSet.Count == 0 || storedSet.Count != expectedSet.Count) return false;
        return storedSet.SetEquals(expectedSet);
    }

    /// <summary>从 StepGroundTruth.Correct（运行时 string 或 List&lt;string&gt;）抽出规范化集合。</summary>
    private static HashSet<string> StoredCorrectSet(StepGroundTruth t)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (t.Correct)
        {
            case string s when !string.IsNullOrWhiteSpace(s):
                set.Add(s.Trim());
                break;
            case System.Collections.IEnumerable e:
                foreach (var item in e)
                    if (item is string s && !string.IsNullOrWhiteSpace(s))
                        set.Add(s.Trim());
                break;
        }
        return set;
    }

    /// <summary>F 步 correct 是否与存储 answer 对齐（与 ExtractAnswer 共用 ChoiceAnswerLetters 归一化口径）。</summary>
    private static bool CorrectMatchesAnswer(StepGroundTruth t, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var stored = StoredCorrectSet(t);
        var ans = new HashSet<string>(ScaffoldGeneratorUtils.ChoiceAnswerLetters(answer), StringComparer.OrdinalIgnoreCase);
        return ans.Count > 0 && stored.SetEquals(ans);
    }

    private static void ValidateStepShape(StepPractice step, List<string> errors)
    {
        var t = step.Truth!;
        var kind = step.Kind;

        var correctSet = StoredCorrectSet(t);
        if (correctSet.Count == 0)
        {
            errors.Add($"步 {kind} correct 为空");
            return;
        }

        var optionKeys = t.Options.Select(o => o.Key).ToList();

        // correct 必须全部出现在 options
        foreach (var c in correctSet)
            if (!optionKeys.Contains(c, StringComparer.OrdinalIgnoreCase))
                errors.Add($"步 {kind} options 缺失 correct 项：{c}");

        // 干扰项（options \ correct）不得与 correct 相交（防御；OptionsBuilder 已保证，这里兜底）
        if (optionKeys.Any(k => correctSet.Contains(k) && optionKeys.Count(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase)) > 1))
            errors.Add($"步 {kind} 干扰项与 correct 相交 / correct 重复出现");

        // options 无重复
        if (optionKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != optionKeys.Count)
            errors.Add($"步 {kind} options 含重复项");

        // releases 四档齐全
        var r = step.Releases;
        if (r is null)
        {
            errors.Add($"步 {kind} releases 为空");
            return;
        }
        if (string.IsNullOrWhiteSpace(r.Hint))
            errors.Add($"步 {kind} releases.hint 为空");
        if (r.Probe is null || string.IsNullOrWhiteSpace(r.Probe.Stem) || r.Probe.Options is not { Count: > 0 })
            errors.Add($"步 {kind} releases.probe 不完整");
        if (r.Scaffold is null
            || string.IsNullOrWhiteSpace(r.Scaffold.Correct?.Title)
            || string.IsNullOrWhiteSpace(r.Scaffold.Wrong?.Title))
            errors.Add($"步 {kind} releases.scaffold 不完整");
        if (r.Reveal is null || string.IsNullOrWhiteSpace(r.Reveal.Title) || string.IsNullOrWhiteSpace(r.Reveal.Body))
            errors.Add($"步 {kind} releases.reveal 不完整");
    }
}
