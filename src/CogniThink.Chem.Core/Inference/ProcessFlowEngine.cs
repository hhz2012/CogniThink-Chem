using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 工业流程综合推断引擎：在 <see cref="ProcessInferenceEngine"/> 转化链回溯赋值
/// （确定各步物质与方程式，来源 RX-IN-*/RX-PL-* 条目）的基础上，为流程每一步追加
/// "目的/原理"节点：按操作类型关键词匹配流程规则 PROC-001..004（并呼应 SOL-003/SOL-004），
/// 目的文本一律取规则条目 conclusion.display 原文，或由 PROC-004 的分句判据机械拆分得到，
/// 绝不自由发挥。
/// 铁律：
/// <list type="bullet">
/// <item>转化链推不出（多解/无解）时整题如实 Underdetermined/NoSolution，禁止猜测；</item>
/// <item>某步操作匹配不到唯一规则时该步 Purpose 如实留空并说明，不编造目的；</item>
/// <item>每个目的节点可溯源到规则条目（RuleId + KnowledgeId + Source）。</item>
/// </list>
/// </summary>
public static class ProcessFlowEngine
{
    /// <summary>
    /// 操作类型关键词 → 规则条目（及 PROC-004 分句键）的机械映射。
    /// 一条步骤的操作描述命中多条规则（或 PROC-004 的多个分句）时视为不唯一，目的留空。
    /// </summary>
    private static readonly (string Keyword, string RuleId, string ClauseKey)[] OperationKeywords =
    {
        ("沉淀转化", "PROC-002", ""),
        ("pH", "PROC-003", ""),
        ("蒸发浓缩冷却结晶", "PROC-004", "蒸发浓缩冷却结晶"),
        ("蒸发结晶", "PROC-004", "蒸发结晶"),
        ("灼烧", "PROC-004", "灼烧"),
        ("洗涤", "PROC-004", "洗涤"),
        ("循环", "PROC-004", "循环利用"),
        ("除杂", "PROC-001", ""),
    };

    /// <summary>推断入口：转化链赋值 + 每步目的/原理节点。</summary>
    public static ProcessFlowResult Infer(ProcessFlowRequest request, ReactionLibrary? library = null)
    {
        // 一次性读入规则库建 id→Rule 字典，供各步目的匹配复用（重复 id 取首条，
        // 与原按 id FirstOrDefault 查找语义一致），避免每遇新规则 id 全量重读 rules.json。
        var rules = new Dictionary<string, Rule?>(StringComparer.Ordinal);
        foreach (var rule in ChemDataLoader.LoadRules(DataRoot.Resolve()))
        {
            rules.TryAdd(rule.Id, rule);
        }

        var result = new ProcessFlowResult();
        if (request.Steps.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "未提供任何流程步骤，无法推断。";
            return result;
        }

        // 1. 拆分化学转化步（from/to 均为字母标签）与纯操作步（物料流名称）。
        var chainSteps = new List<ExperimentStep>();
        foreach (var step in request.Steps)
        {
            if (IsLabelToken(step.From) && IsLabelToken(step.To))
            {
                chainSteps.Add(new ExperimentStep
                {
                    Step = step.Step,
                    From = step.From,
                    To = step.To,
                    Reagents = step.Reagents.ToList(),
                    Conditions = step.Conditions.ToList(),
                    Phenomenon = step.Phenomenon,
                });
            }
        }

        if (chainSteps.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "流程中没有字母标签构成的化学转化链步骤，无法确定各步物质。";
            return result;
        }

        // 2. 复用转化链回溯赋值引擎确定各步物质与方程式（来源 RX-IN-*/RX-PL-* 条目）。
        var chain = ProcessInferenceEngine.Infer(
            new ProcessInferenceRequest
            {
                CandidateSpecies = request.CandidateSpecies.ToList(),
                Steps = chainSteps,
            },
            library);
        result.Steps.AddRange(chain.Steps);

        if (chain.Status != InferenceStatus.Solved)
        {
            result.Status = chain.Status;
            result.Message = $"转化链未能唯一确定：{chain.Message}";
            return result;
        }

        result.Status = InferenceStatus.Solved;
        result.Assignment = new Dictionary<string, string>(chain.Assignment);
        result.StepResults.AddRange(chain.StepResults);

        // 3. 为每一步追加"目的/原理"节点（按操作类型匹配流程规则，原文引用）。
        foreach (var step in request.Steps)
        {
            var hit = ClassifyPurpose(step.Step, step.Purpose, rules);
            result.Purposes.Add(hit.Purpose);
            result.Steps.Add(hit.Node);
            if (hit.Purpose.Purpose.Length > 0)
            {
                result.PurposeMap[step.Step.ToString()] = hit.Purpose.Purpose;
            }
        }

        result.Message = "转化链唯一确定，各步目的/原理按流程规则条目原文机械匹配给出。";
        return result;
    }

    /// <summary>
    /// 按操作类型关键词匹配唯一规则：命中且仅命中一条规则（或 PROC-004 的仅一个分句）时，
    /// 目的取规则 conclusion.display 原文或对应分句；否则如实留空并说明。
    /// </summary>
    private static (ProcessStepPurpose Purpose, InferenceStep Node) ClassifyPurpose(
        int stepNumber,
        string operationText,
        Dictionary<string, Rule?> rules)
    {
        var hits = new List<(string Keyword, string RuleId, string ClauseKey)>();
        foreach (var (keyword, ruleId, clauseKey) in OperationKeywords)
        {
            if (operationText.Contains(keyword, StringComparison.Ordinal))
            {
                hits.Add((keyword, ruleId, clauseKey));
            }
        }

        // PROC-004 的多个分句同时命中（如"洗涤…循环"）也视为不唯一。
        var distinctHits = hits.DistinctBy(h => (h.RuleId, h.ClauseKey)).ToList();

        if (distinctHits.Count != 1)
        {
            var reason = distinctHits.Count == 0
                ? "未匹配到任何流程规则"
                : $"同时命中多条规则判据（{string.Join("、", distinctHits.Select(h => h.Keyword))}），目的不唯一";
            return (
                new ProcessStepPurpose { Step = stepNumber, Operation = operationText, Purpose = "" },
                new InferenceStep
                {
                    Description = $"第 {stepNumber} 步目的/原理：操作描述「{operationText}」{reason}，Purpose 如实留空。",
                    Evidence = "ProcessFlowEngine",
                });
        }

        var hit0 = distinctHits[0];
        // id 不存在时 rule 为 null，与原 LoadRule 缓存缺失条目的行为一致（Purpose 如实留空）。
        rules.TryGetValue(hit0.RuleId, out var rule);
        if (rule is null)
        {
            return (
                new ProcessStepPurpose { Step = stepNumber, Operation = operationText, Purpose = "" },
                new InferenceStep
                {
                    Description = $"第 {stepNumber} 步目的/原理：操作描述「{operationText}」命中 {hit0.RuleId}，但规则库中缺失该条目，Purpose 如实留空。",
                    Evidence = hit0.RuleId,
                });
        }

        var display = rule.Conclusion.Display ?? "";
        var purpose = hit0.ClauseKey.Length == 0
            ? display
            : SplitClauses(display).FirstOrDefault(c => c.StartsWith(hit0.ClauseKey, StringComparison.Ordinal), "");
        if (string.IsNullOrEmpty(purpose))
        {
            return (
                new ProcessStepPurpose { Step = stepNumber, Operation = operationText, Purpose = "" },
                new InferenceStep
                {
                    Description = $"第 {stepNumber} 步目的/原理：操作描述「{operationText}」命中 {hit0.RuleId} 分句「{hit0.ClauseKey}」，但规则原文中无该分句，Purpose 如实留空。",
                    Evidence = hit0.RuleId,
                });
        }

        return (
            new ProcessStepPurpose
            {
                Step = stepNumber,
                Operation = hit0.Keyword,
                Purpose = purpose,
                RuleId = rule.Id,
                KnowledgeId = rule.KnowledgeId,
                Source = rule.Source,
            },
            new InferenceStep
            {
                Description = $"第 {stepNumber} 步目的/原理：{purpose}（依据 {rule.Id}「{rule.Name}」，知识点 {rule.KnowledgeId}）",
                Evidence = rule.Id,
                KnowledgeId = rule.KnowledgeId,
                Source = rule.Source,
            });
    }

    /// <summary>把 PROC-004 的判据原文按"；"拆成逐操作分句（机械拆分，不改写）。</summary>
    private static IEnumerable<string> SplitClauses(string display) =>
        display.Split('；', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>单一大写字母视为字母标签（与 ProcessInferenceEngine 约定一致）。</summary>
    private static bool IsLabelToken(string text) =>
        text.Length == 1 && text[0] >= 'A' && text[0] <= 'Z';
}

/// <summary>工业流程综合推断请求：候选物种 + 各步操作（含操作类型描述）。</summary>
public sealed class ProcessFlowRequest
{
    /// <summary>候选物种（化学式或 caret 离子写法），供转化链回溯赋值。</summary>
    public IReadOnlyList<string> CandidateSpecies { get; init; } = Array.Empty<string>();

    /// <summary>流程步骤（按顺序），from/to 均为字母标签的是化学转化步，其余为纯操作步。</summary>
    public IReadOnlyList<ProcessFlowStep> Steps { get; init; } = Array.Empty<ProcessFlowStep>();
}

/// <summary>工业流程的单个步骤。</summary>
public sealed class ProcessFlowStep
{
    /// <summary>步骤号（1 起）。</summary>
    public int Step { get; init; }

    /// <summary>起点（字母标签或物料流名称）。</summary>
    public string From { get; init; } = "";

    /// <summary>终点（字母标签或物料流名称）。</summary>
    public string To { get; init; } = "";

    /// <summary>加入的试剂。</summary>
    public IReadOnlyList<string> Reagents { get; init; } = Array.Empty<string>();

    /// <summary>反应/操作条件。</summary>
    public IReadOnlyList<string> Conditions { get; init; } = Array.Empty<string>();

    /// <summary>现象（可空）。</summary>
    public string Phenomenon { get; init; } = "";

    /// <summary>操作类型描述（受控关键词，如"除杂：…"、"洗涤：…"），供目的规则机械匹配。</summary>
    public string Purpose { get; init; } = "";
}

/// <summary>某一步的目的/原理匹配结果（目的文本来自规则条目原文）。</summary>
public sealed class ProcessStepPurpose
{
    /// <summary>步骤号。</summary>
    public int Step { get; init; }

    /// <summary>命中的操作类型关键词（未匹配时为原操作描述）。</summary>
    public string Operation { get; init; } = "";

    /// <summary>目的文本（规则 conclusion.display 原文或其分句；匹配不唯一时为空）。</summary>
    public string Purpose { get; init; } = "";

    /// <summary>依据的规则条目 id（未匹配时为空）。</summary>
    public string RuleId { get; init; } = "";

    /// <summary>规则归属知识点 id（可溯源）。</summary>
    public string KnowledgeId { get; init; } = "";

    /// <summary>规则教材出处。</summary>
    public string Source { get; init; } = "";
}

/// <summary>工业流程综合推断结果。</summary>
public sealed class ProcessFlowResult
{
    public InferenceStatus Status { get; set; } = InferenceStatus.NoSolution;

    public string Message { get; set; } = "";

    /// <summary>字母标签 → 物质（来自转化链回溯赋值）。</summary>
    public Dictionary<string, string> Assignment { get; set; } = new();

    /// <summary>化学转化步的匹配结果（含方程式与知识库条目溯源）。</summary>
    public List<ProcessStepResult> StepResults { get; set; } = new();

    /// <summary>每一步的目的/原理匹配结果（与请求步骤一一对应）。</summary>
    public List<ProcessStepPurpose> Purposes { get; set; } = new();

    /// <summary>步骤号（字符串）→ 目的文本（仅收录匹配到唯一规则的步骤）。</summary>
    public Dictionary<string, string> PurposeMap { get; set; } = new();

    /// <summary>完整推理链：转化链赋值步骤 + 每步目的/原理节点。</summary>
    public List<InferenceStep> Steps { get; set; } = new();
}
