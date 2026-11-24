namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Parsing;

// ============================================================
// Task 1: 真值提取器 —— 从 EngineArtifacts 取 A/B/E/F 各步 correct。
// 铁律：correct 只来自引擎字段；取不到 → 返回 null（该步降级），绝不返回猜测值 / 空集合冒充。
// C/D 步本轮不实现（返回 null → 降级为只读揭示），见 spec Open Questions。
// ============================================================

public sealed class StepTruthExtractor : IStepTruthExtractor
{
    public StepCorrect? Extract(AtomicTaskKind kind, EngineArtifacts artifacts) => kind switch
    {
        AtomicTaskKind.A => ExtractParticles(artifacts),
        AtomicTaskKind.B => ExtractRedoxChanges(artifacts),
        AtomicTaskKind.E => ExtractProducts(artifacts),
        AtomicTaskKind.F => ExtractAnswer(artifacts),
        _ => null, // C / D → 降级
    };

    // A 步：体系粒子清单（方程式全部物种，去重），multi-check。
    private static StepCorrect? ExtractParticles(EngineArtifacts a)
    {
        var parsed = EngineArtifactsBuilder.GetParsed(a);
        if (parsed is null) return null;
        var particles = parsed.AllSpecies.Select(s => s.Formula).Distinct().ToList();
        if (particles.Count == 0) return null;
        return new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = particles,
            Provenance = "ParsedEquation.AllSpecies",
        };
    }

    // B 步：变价元素清单（RedoxAnalysis.Changes 中的元素），multi-check。
    // 非氧化还原（Changes 为空）→ null 降级。
    private static StepCorrect? ExtractRedoxChanges(EngineArtifacts a)
    {
        var redox = EngineArtifactsBuilder.GetRedox(a);
        if (redox is not { Success: true, IsRedox: true }) return null;
        var elements = redox.Changes.Select(c => c.Element).Distinct().ToList();
        if (elements.Count == 0) return null;
        return new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = elements,
            Provenance = "RedoxAnalysis.Changes",
        };
    }

    // E 步：产物物种清单（方程式右侧物种，去重），multi-check。
    // （氧化还原模块的"产物预测"真值；半反应文案由 releases 承载。）
    private static StepCorrect? ExtractProducts(EngineArtifacts a)
    {
        var parsed = EngineArtifactsBuilder.GetParsed(a);
        if (parsed is null) return null;
        var products = parsed.Right.Select(t => t.Species.Formula).Distinct().ToList();
        if (products.Count == 0) return null;
        return new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = products,
            Provenance = "ParsedEquation.Right",
        };
    }

    // F 步：原题存储 answer。F 步仅对选择题门控（spec 已决 Open Question）：
    //   - choice：归一化 answer 为选项字母集合（"A" / "AC" / "A,C" / "A、C" → 字母集），>1 → multi-check，否则 single-select；
    //   - 非 choice（fill / calc / judge 等）：无选择字母真值 → 返回 null，F 步降级为只读揭示，
    //     由校验门「关键步 F 缺失真值」判 unsuitable，绝不把整串公式存为 correct 后误报 conflict。
    // 用存储 answer 为准（eval 已验证 answer 可靠的题）。
    private static StepCorrect? ExtractAnswer(EngineArtifacts a)
    {
        var answer = (a.Case.Answer ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(answer)) return null;

        if (!(a.Case.Type ?? "").Contains("choice", StringComparison.OrdinalIgnoreCase))
            return null;

        var letters = ScaffoldGeneratorUtils.ChoiceAnswerLetters(answer);
        if (letters.Count == 0) return null;

        if (letters.Count > 1)
        {
            return new StepCorrect
            {
                Interaction = AtomicTaskInteraction.MultiCheck,
                Set = letters,
                Provenance = "case.answer(multi)",
            };
        }

        return new StepCorrect
        {
            Interaction = AtomicTaskInteraction.SingleSelect,
            Value = letters[0],
            Provenance = "case.answer",
        };
    }
}
