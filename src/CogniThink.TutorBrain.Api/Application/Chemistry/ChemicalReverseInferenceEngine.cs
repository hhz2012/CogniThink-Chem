namespace CogniThink.TutorBrain.Application.Chemistry;

using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>逆向推理请求：已知物质 + 观察到的最终现象/颜色/产物</summary>
public sealed class ReverseInferenceRequest
{
    /// <summary>已知投入或已确认的物质</summary>
    public List<string> Known { get; set; } = new();

    /// <summary>希望反推的目标产物</summary>
    public List<string> TargetProducts { get; set; } = new();

    /// <summary>观察到的溶液颜色，如"蓝色""血红色"</summary>
    public string? SolutionColor { get; set; }

    /// <summary>观察到的沉淀颜色，如"红褐色""白色"</summary>
    public string? PrecipitateColor { get; set; }

    /// <summary>观察到的气体，如"无色气泡""刺激性气味气体"</summary>
    public string? Gas { get; set; }

    /// <summary>观察到的实验现象</summary>
    public List<string> Phenomena { get; set; } = new();

    /// <summary>反向制备路径最大深度</summary>
    public int MaxDepth { get; set; } = 3;

    /// <summary>最多返回的候选数</summary>
    public int MaxCandidates { get; set; } = 6;

    /// <summary>正向验证使用的环境；为空则按水溶液 + 空气处理</summary>
    public SimulationRequest? Conditions { get; set; }
}

/// <summary>一条反向制备路径（目标产物 ← 反应物）</summary>
public sealed record ReversePathNode(
    string ProductId,
    string ProductName,
    string ReactionId,
    string Equation,
    List<string> KnownReactants,
    List<string> MissingReactants);

/// <summary>正向回放验证结果：把反推得到的物质投入模拟，是否复现观察</summary>
public sealed record ReverseVerification(
    bool Matched,
    List<string> PredictedFinalSpecies,
    string PredictedSolutionColor,
    List<string> PredictedPrecipitates,
    List<string> PredictedGases,
    List<string> MatchedObservations,
    string Summary);

/// <summary>一条逆向候选：哪条反应、还缺哪些物质、可追溯到哪些制备路径</summary>
public sealed class ReverseCandidate
{
    public string ReactionId { get; set; } = string.Empty;
    public string Equation { get; set; } = string.Empty;
    public double Score { get; set; }
    public List<string> KnownReactants { get; set; } = new();
    public List<string> MissingReactants { get; set; } = new();
    public List<ReversePathNode> UpstreamPaths { get; set; } = new();
    public ReverseVerification? Verification { get; set; }
    public List<string> Evidence { get; set; } = new();
}

/// <summary>逆向推理结果</summary>
public sealed class ReverseInferenceResult
{
    public List<ReverseCandidate> Candidates { get; set; } = new();
    public string Conclusion { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 化学逆向推理引擎：从"观察到的颜色/沉淀/气体/现象/目标产物"出发，
/// 在知识库中反查可能发生的反应，列出还缺少的反应物；
/// 再对这些反应物继续反查制备路径，最后用正向推演引擎回放验证。
/// 铁律：候选只来自知识库已有反应；验证不通过时如实标注，不强行给结论。
/// </summary>
public sealed class ChemicalReverseInferenceEngine
{
    private readonly ChemicalKnowledgeBase _kb;
    private readonly ChemicalSimulationEngine _simulator;

    public ChemicalReverseInferenceEngine(
        ChemicalKnowledgeBase kb,
        ChemicalSimulationEngine simulator)
    {
        _kb = kb;
        _simulator = simulator;
    }

    public ReverseInferenceResult Infer(ReverseInferenceRequest request)
    {
        var result = new ReverseInferenceResult();

        var knownIds = ResolveIds(request.Known, result.Warnings);
        var targetIds = ResolveIds(request.TargetProducts, result.Warnings);

        if (knownIds.Count == 0 && targetIds.Count == 0 &&
            string.IsNullOrWhiteSpace(request.SolutionColor) &&
            string.IsNullOrWhiteSpace(request.PrecipitateColor) &&
            string.IsNullOrWhiteSpace(request.Gas) &&
            request.Phenomena.Count == 0)
        {
            result.Conclusion = "请至少提供已知物质、目标产物或一项观察结果。";
            return result;
        }

        var candidates = new List<ReverseCandidate>();
        foreach (var reaction in _kb.Reactions)
        {
            var matched = new List<string>();
            var score = ScoreReaction(reaction, request, targetIds, matched);
            if (score <= 0) continue;

            var knownReactants = reaction.Reactants.Where(knownIds.Contains).ToList();
            var missingReactants = reaction.Reactants.Where(id => !knownIds.Contains(id)).ToList();

            // 与已知物质有交集的反应优先，避免把所有产气反应都排在同一层。
            score += knownReactants.Count > 0 ? 5 + knownReactants.Count * 2 : -5;

            var candidate = new ReverseCandidate
            {
                ReactionId = reaction.Id,
                Equation = reaction.Equation,
                KnownReactants = knownReactants.Select(Describe).ToList(),
                MissingReactants = missingReactants.Select(Describe).ToList(),
                Evidence = matched
            };

            foreach (var missing in missingReactants.Take(2))
            {
                candidate.UpstreamPaths.AddRange(
                    BuildUpstream(missing, knownIds, Math.Max(1, request.MaxDepth), new HashSet<string>(StringComparer.Ordinal)));
            }

            candidate.Score = score;
            candidates.Add(candidate);
        }

        foreach (var candidate in candidates)
        {
            candidate.Verification = Verify(candidate, request, knownIds);
            if (candidate.Verification.Matched) candidate.Score += 20;
        }

        result.Candidates = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.ReactionId, StringComparer.Ordinal)
            .Take(Math.Max(1, request.MaxCandidates))
            .ToList();

        if (result.Candidates.Count == 0)
        {
            result.Conclusion = "现有知识库中没有能解释这些观察的反应，无法反推。";
            return result;
        }

        var best = result.Candidates[0];
        var missingText = best.MissingReactants.Count == 0
            ? "已知物质已经齐全"
            : $"还需要补充 {string.Join("、", best.MissingReactants)}";

        var tied = result.Candidates
            .Count(c => Math.Abs(c.Score - best.Score) < 0.01 &&
                        !c.MissingReactants.SequenceEqual(best.MissingReactants));

        var prefix = tied > 0
            ? $"存在 {tied + 1} 条同类候选，最可能之一："
            : "最可能：";

        var hitText = best.Evidence.Count == 0
            ? string.Empty
            : $"；命中观察：{string.Join("、", best.Evidence)}";

        result.Conclusion = best.Verification?.Matched == true
            ? $"{prefix}{best.Equation}；{missingText}，正向回放可复现观察到的主要现象{hitText}。"
            : $"{prefix}{best.Equation}；{missingText}（正向回放未能完全复现，请补充条件或观察信息）{hitText}。";

        result.Evidence.AddRange(result.Candidates.Select(c =>
            $"{c.ReactionId} {c.Equation}：命中 {string.Join("、", c.Evidence)}；" +
            (c.MissingReactants.Count == 0
                ? "反应物已知，可直接验证。"
                : $"缺少 {string.Join("、", c.MissingReactants)}。")));

        return result;
    }

    // ==================== 观察匹配 ====================

    private double ScoreReaction(
        Reaction reaction,
        ReverseInferenceRequest request,
        IReadOnlySet<string> targetIds,
        List<string> matched)
    {
        var score = 0.0;

        foreach (var target in reaction.Products.Where(targetIds.Contains))
        {
            score += 10;
            matched.Add($"生成目标产物 {Describe(target)}");
        }

        if (!string.IsNullOrWhiteSpace(request.SolutionColor))
        {
            var coloredProducts = reaction.Products
                .Select(_kb.FindSubstanceById)
                .Where(s => s is not null &&
                            (!string.IsNullOrWhiteSpace(s.SolutionColor) ||
                             !string.IsNullOrWhiteSpace(s.Color)))
                .ToList();

            var byPhenomenon = reaction.Phenomena.Any(p =>
                (p.Contains("变为", StringComparison.Ordinal) ||
                 p.Contains("呈", StringComparison.Ordinal) ||
                 p.Contains("显", StringComparison.Ordinal)) &&
                p.Contains(request.SolutionColor!, StringComparison.Ordinal));
            var byProduct = coloredProducts.Any(s =>
                ColorMatches(request.SolutionColor, s!.SolutionColor) ||
                ColorMatches(request.SolutionColor, s.Color));

            if (byPhenomenon || byProduct)
            {
                score += 6;
                matched.Add($"溶液颜色 {request.SolutionColor}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PrecipitateColor))
        {
            // 只有"生成难溶物"的反应才能解释沉淀颜色；溶解/转化原有沉淀的反应不算。
            var insolubleProducts = reaction.Products
                .Select(_kb.FindSubstanceById)
                .Where(s => s is not null && s.Solubility == SolubilityLevel.Insoluble)
                .ToList();

            var consumesMatchingPrecipitate = reaction.Reactants
                .Select(_kb.FindSubstanceById)
                .Any(s => s is not null &&
                          s.Solubility == SolubilityLevel.Insoluble &&
                          ColorMatches(request.PrecipitateColor, s.Color));

            var byPhenomenon = insolubleProducts.Count > 0 && !consumesMatchingPrecipitate &&
                reaction.Phenomena.Any(p =>
                    p.Contains(request.PrecipitateColor!, StringComparison.Ordinal) &&
                    p.Contains("沉淀", StringComparison.Ordinal));
            var byProduct = insolubleProducts.Any(s =>
                ColorMatches(request.PrecipitateColor, s!.Color));

            if (byPhenomenon || byProduct)
            {
                score += 6;
                matched.Add($"沉淀颜色 {request.PrecipitateColor}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Gas))
        {
            var productGases = reaction.Products
                .Select(_kb.FindSubstanceById)
                .Where(s => s is not null && s.State == PhysicalState.Gas)
                .ToList();

            var byProduct = productGases.Any(s => GasMatches(request.Gas!, $"{s!.Color}{s.Name}"));
            var byPhenomenon = reaction.Phenomena.Any(p => TextMatches(p, request.Gas!));

            if (byProduct || byPhenomenon)
            {
                score += 5;
                matched.Add($"气体 {request.Gas}");
            }
        }

        foreach (var phenomenon in request.Phenomena.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            if (reaction.Phenomena.Any(p => TextMatches(p, phenomenon)))
            {
                score += 3;
                matched.Add($"现象 {phenomenon}");
            }
        }

        return score;
    }

    // ==================== 反向制备路径 ====================

    private List<ReversePathNode> BuildUpstream(
        string productId,
        IReadOnlySet<string> knownIds,
        int depth,
        HashSet<string> visiting)
    {
        var nodes = new List<ReversePathNode>();
        if (depth <= 0 || visiting.Contains(productId)) return nodes;
        visiting.Add(productId);

        foreach (var reaction in _kb.GetReactionsProducing(productId).Take(6))
        {
            var knownReactants = reaction.Reactants.Where(knownIds.Contains).ToList();
            var missing = reaction.Reactants.Where(id => !knownIds.Contains(id)).ToList();

            // 至少要有一个已知起点，否则该路径无法溯源到题给信息
            if (knownReactants.Count == 0) continue;

            nodes.Add(new ReversePathNode(
                productId,
                Describe(productId),
                reaction.Id,
                reaction.Equation,
                knownReactants.Select(Describe).ToList(),
                missing.Select(Describe).ToList()));

            if (missing.Count > 0 && depth > 1)
            {
                foreach (var next in missing.Take(2))
                    nodes.AddRange(BuildUpstream(next, knownIds, depth - 1, visiting));
            }

            if (nodes.Count >= 8) break;
        }

        visiting.Remove(productId);
        return nodes.Take(8).ToList();
    }

    // ==================== 正向回放验证 ====================

    private ReverseVerification Verify(
        ReverseCandidate candidate,
        ReverseInferenceRequest request,
        IReadOnlySet<string> knownIds)
    {
        var initial = knownIds
            .Concat(_kb.Reactions
                .First(r => r.Id == candidate.ReactionId)
                .Reactants
                .Where(id => !knownIds.Contains(id)))
            .Distinct()
            .ToList();

        var simulationRequest = request.Conditions is null
            ? new SimulationRequest { Aqueous = true, Air = true }
            : new SimulationRequest
            {
                Aqueous = request.Conditions.Aqueous,
                Heating = request.Conditions.Heating,
                Ignition = request.Conditions.Ignition,
                Catalyst = request.Conditions.Catalyst,
                Electricity = request.Conditions.Electricity,
                Light = request.Conditions.Light,
                Concentrated = request.Conditions.Concentrated,
                Dilute = request.Conditions.Dilute,
                Air = request.Conditions.Air,
                Acidic = request.Conditions.Acidic,
                Alkaline = request.Conditions.Alkaline,
                MaxSteps = request.Conditions.MaxSteps
            };

        simulationRequest.Species = initial
            .Select(id => new SimulationSpeciesInput { Substance = id, Moles = 1 })
            .ToList();

        var simulated = _simulator.Simulate(simulationRequest);
        var finalIds = simulated.FinalSpecies.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var matchedObservations = new List<string>();

        var targetIds = ResolveIds(request.TargetProducts, new List<string>());
        var targetMatched = targetIds.Count > 0 && targetIds.All(finalIds.Contains);
        if (targetIds.Count > 0 && targetIds.Any(finalIds.Contains))
            matchedObservations.Add($"目标产物 {string.Join("、", targetIds.Where(finalIds.Contains).Select(Describe))}");

        var colorMatched = ColorMatches(request.SolutionColor, simulated.FinalSolutionColor);
        if (!string.IsNullOrWhiteSpace(request.SolutionColor) && colorMatched)
            matchedObservations.Add($"溶液颜色 {simulated.FinalSolutionColor}");

        var precipitateMatched = string.IsNullOrWhiteSpace(request.PrecipitateColor) ||
            simulated.FinalPrecipitates.Any(p => ColorMatches(request.PrecipitateColor, p));
        if (!string.IsNullOrWhiteSpace(request.PrecipitateColor) && precipitateMatched)
            matchedObservations.Add($"沉淀 {string.Join("、", simulated.FinalPrecipitates)}");

        var gasMatched = string.IsNullOrWhiteSpace(request.Gas) ||
            simulated.FinalGases.Any(g => GasMatches(request.Gas!, g));
        if (!string.IsNullOrWhiteSpace(request.Gas) && gasMatched)
            matchedObservations.Add($"气体 {string.Join("、", simulated.FinalGases)}");

        var phenomenonHits = request.Phenomena
            .Where(p => simulated.Phenomena.Any(s => TextMatches(s, p)))
            .ToList();
        matchedObservations.AddRange(phenomenonHits.Select(p => $"现象 {p}"));

        var checkedAny = false;
        var failedAny = false;

        if (targetIds.Count > 0)
        {
            checkedAny = true;
            if (!targetMatched) failedAny = true;
        }

        if (!string.IsNullOrWhiteSpace(request.SolutionColor))
        {
            checkedAny = true;
            if (!colorMatched) failedAny = true;
        }

        if (!string.IsNullOrWhiteSpace(request.PrecipitateColor))
        {
            checkedAny = true;
            if (!precipitateMatched) failedAny = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Gas))
        {
            checkedAny = true;
            if (!gasMatched) failedAny = true;
        }

        if (request.Phenomena.Count > 0)
        {
            checkedAny = true;
            if (phenomenonHits.Count == 0) failedAny = true;
        }

        var matched = checkedAny && !failedAny && matchedObservations.Count > 0;

        var summary = matched
            ? $"回放成功：{simulated.FinalStateText}"
            : $"回放结果：{simulated.FinalStateText}";

        return new ReverseVerification(
            matched,
            simulated.FinalSpecies.Select(s => Describe(s.Id)).ToList(),
            simulated.FinalSolutionColor,
            simulated.FinalPrecipitates,
            simulated.FinalGases,
            matchedObservations.Distinct().ToList(),
            summary);
    }

    // ==================== 工具 ====================

    private HashSet<string> ResolveIds(IEnumerable<string> keys, List<string> warnings)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            var substance = _kb.FindSubstance(key);
            if (substance is null)
            {
                warnings.Add($"知识库中未找到物质「{key}」。");
                continue;
            }

            ids.Add(substance.Id);
        }

        return ids;
    }

    private string Describe(string substanceId)
    {
        var substance = _kb.FindSubstanceById(substanceId);
        return substance is null ? substanceId : $"{substance.Name}（{substance.Formula}）";
    }

    private static string StripColor(string color) => color.Replace("色", string.Empty).Trim();

    private static bool ColorMatches(string? observed, string? predicted)
    {
        if (string.IsNullOrWhiteSpace(observed) || string.IsNullOrWhiteSpace(predicted))
            return false;

        var a = StripColor(observed);
        var b = StripColor(predicted);
        return a.Length > 0 && b.Length > 0 &&
               (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));
    }

    private static bool TextMatches(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }

    /// <summary>气体匹配：允许"无色气泡""有刺激性气味气体"等只描述外观的观察</summary>
    private static bool GasMatches(string observed, string predicted)
    {
        if (string.IsNullOrWhiteSpace(observed) || string.IsNullOrWhiteSpace(predicted))
            return false;
        if (TextMatches(observed, predicted)) return true;

        var o = NormalizeGasText(observed);
        var p = NormalizeGasText(predicted);
        if (o.Length == 0) return p.Length > 0;
        if (p.Length == 0) return true;
        return o.Contains(p, StringComparison.Ordinal) || p.Contains(o, StringComparison.Ordinal);
    }

    private static string NormalizeGasText(string text) =>
        text.Replace("无色", string.Empty)
            .Replace("刺激性气味", string.Empty)
            .Replace("有", string.Empty)
            .Replace("气体", string.Empty)
            .Replace("气泡", string.Empty)
            .Replace("逸出", string.Empty)
            .Replace("生成", string.Empty)
            .Replace("产生", string.Empty)
            .Trim();
}
