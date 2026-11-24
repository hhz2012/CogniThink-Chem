namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using System.Text;

// ============================================================
// Task 3: 每步文案元数据 + LLM 不可用时的模板回退。
// 元数据（title / goal / pitfall）镜像前端 miniapp/src/engine/scaffoldChains.ts 的 TASK_TEMPLATES，
// 保证前后端对「这一步在问什么」口径一致。
// 回退模板四档齐全（hint / probe / scaffold / reveal），永不崩；不编造化学事实，
// 只引用引擎 correct（作为已知事实）与通用 goal / pitfall。
// ============================================================

public static class ScaffoldReleaseTemplates
{
    private sealed class Meta
    {
        public string Title { get; set; } = "";
        public string Goal { get; set; } = "";
        public string Pitfall { get; set; } = "";
    }

    // 镜像前端 scaffoldChains.ts TASK_TEMPLATES（goal / pitfall）
    private static readonly Dictionary<AtomicTaskKind, Meta> MetaByKind = new()
    {
        [AtomicTaskKind.A] = new() { Title = "识别体系组分（微观粒子清单）", Goal = "把题干翻译成体系里有哪些粒子 / 物质；区分大量 / 少量 / 弱电解质部分电离。", Pitfall = "跳过这步凭感觉写反应。" },
        [AtomicTaskKind.B] = new() { Title = "识别反应角色（变价 / 平衡角色）", Goal = "区分氧化剂 / 还原剂；或谁电离 / 水解 / 沉淀；多物质共存时标记全部变价物种（多选）。", Pitfall = "漏标变价物种。" },
        [AtomicTaskKind.C] = new() { Title = "识别约束条件（最核心、引擎最大价值点）", Goal = "识别所有会改变反应走向的前提：酸 / 碱性、浓度、加热、过量少量、是否沉淀、膜隔离、通电、温度。", Pitfall = "漏条件导致整套结论错。" },
        [AtomicTaskKind.D] = new() { Title = "竞争排序（多物质共存时）", Goal = "多种还原剂 / 氧化剂、多种可沉淀离子，判断先后反应顺序；无竞争则跳过。", Pitfall = "忽略竞争直接写主反应。" },
        [AtomicTaskKind.E] = new() { Title = "推导产物 / 平衡结果", Goal = "结合前述全部条件，选出合理产物、离子浓度大小、电极产物。", Pitfall = "产物与条件矛盾。" },
        [AtomicTaskKind.F] = new() { Title = "回到原题，匹配选项 / 作答", Goal = "基于全部推理结论完成原题作答。", Pitfall = "推理对但选错。" },
    };

    public static string Title(AtomicTaskKind k) => MetaByKind.TryGetValue(k, out var m) ? m.Title : k.ToString();
    public static string Goal(AtomicTaskKind k) => MetaByKind.TryGetValue(k, out var m) ? m.Goal : "";
    public static string Pitfall(AtomicTaskKind k) => MetaByKind.TryGetValue(k, out var m) ? m.Pitfall : "";

    /// <summary>
    /// LLM 不可用 / 输出不合规 / 超时时的回退：四档齐全、非空，不抛错。
    /// 仅引用 correct（已知事实）与通用模板，不编造题干外的化学事实。
    /// </summary>
    public static StepScaffoldReleases BuildFallback(AtomicTaskKind kind, StepCorrect? correct, EngineArtifacts artifacts)
    {
        var correctText = CorrectDisplay(correct);
        var goal = Goal(kind);
        var pitfall = Pitfall(kind);

        var hint = string.IsNullOrWhiteSpace(correctText)
            ? $"{goal}先回到题干逐项核对，再作答。"
            : $"{goal}已知本步涉及：{correctText}。先确认你是否把它们都纳入了考虑。";
        if (string.IsNullOrWhiteSpace(hint)) hint = "回到题干，逐项核对后再作答。";

        var probe = new DiscriminationQuestion
        {
            Stem = $"关于「{Title(kind)}」，下列做法更合理的是？",
            Options = new List<string>
            {
                $"先完成本步再往下推（避免：{pitfall}）",
                "凭直觉直接给出最终结论",
            },
        };

        var scaffold = new ContrastCard
        {
            Correct = new ContrastSide { Title = "推荐思路", Detail = goal },
            Wrong = new ContrastSide { Title = "常见误区", Detail = pitfall },
        };

        var reveal = new RuleDrawer
        {
            Title = Title(kind),
            Body = string.IsNullOrWhiteSpace(correctText)
                ? goal
                : $"{goal}\n本步对应事实：{correctText}。",
        };

        return new StepScaffoldReleases { Hint = hint, Probe = probe, Scaffold = scaffold, Reveal = reveal };
    }

    private static string CorrectDisplay(StepCorrect? c)
    {
        if (c is null) return "";
        var set = ScaffoldGeneratorUtils.CorrectSet(c);
        return set.Count == 0 ? "" : string.Join("、", set.OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>把引擎推理节点 / 推导步骤压成提示词上下文文本（artifacts / Response 缺失则空串，不抛错）。</summary>
    public static string ReasoningContext(EngineArtifacts? artifacts)
    {
        var resp = artifacts?.Response;
        if (resp is null) return "";

        var sb = new StringBuilder();
        if (resp.ReasoningNodes is { Count: > 0 })
        {
            sb.AppendLine("【引擎推理节点】");
            foreach (var n in resp.ReasoningNodes)
            {
                sb.Append($"步骤 {n.Step}. {n.Description}");
                if (n.Premises is { Count: > 0 }) sb.Append($"；前提：{string.Join("；", n.Premises)}");
                if (!string.IsNullOrWhiteSpace(n.Conclusion)) sb.Append($"；结论：{n.Conclusion}");
                sb.AppendLine();
            }
        }
        if (resp.DerivationSteps is { Count: > 0 })
        {
            sb.AppendLine("【引擎推导步骤】");
            foreach (var s in resp.DerivationSteps) sb.AppendLine($"- {s}");
        }
        return sb.ToString();
    }
}
