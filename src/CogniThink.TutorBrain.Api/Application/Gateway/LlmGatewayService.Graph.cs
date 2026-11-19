using CogniThink.Chem.Core.Inference;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// LlmGatewayService 的 graph 资产路由分部。
/// 资产类型是权威路由信号（不依赖 LLM 的 task_type）：graph 题优先走 GraphSolver。
/// 能唯一解 → 返回 Ok=true 真值结论；不能唯一解 → 返回 null，回退通用 task_type 分支（不损失原有能力，不蒙数）。
/// </summary>
public sealed partial class LlmGatewayService
{
    private EngineResponse? TryRouteGraph(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        var asset = imageAssets?.FirstOrDefault(a => a.Type == "graph");
        if (asset is null) return null;
        var rawText = req.RawText ?? string.Empty;

        // 1) 选择题：选项为物质组 → VerifyOptions（知识库一步转化可行性）
        var groups = GraphInputExtractor.ExtractOptionGroups(rawText);
        if (groups.Count >= 2)
        {
            var opts = groups.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
            var result = GraphSolver.Solve(asset, Array.Empty<string>(), GraphSolveMode.VerifyOptions, opts);
            if (result.Status == InferenceStatus.Solved && result.OptionFeasibility.Count > 0)
            {
                var feasible = result.OptionFeasibility.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
                var infeasible = result.OptionFeasibility.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
                var polarity = GraphInputExtractor.AskPolarity(rawText);
                string? answer = polarity == "infeasible" && infeasible.Count == 1 ? infeasible[0]
                    : polarity == "feasible" && feasible.Count == 1 ? feasible[0]
                    : null;
                if (answer != null)
                {
                    var dirWord = polarity == "infeasible" ? "不符合要求" : "符合要求";
                    return new EngineResponse
                    {
                        Ok = true,
                        FinalResult = $"{dirWord}的是选项 {answer}",
                        ConstraintNote = "【graph 引擎】转化图选项验证（知识库一步转化）：" + result.Message,
                        Confidence = 0.95,
                    };
                }
            }
            return null; // 无法唯一判定 → 回退通用 choice 分支（ChoiceVerifier）兜底
        }

        // 2) 非选择题（填空/赋值）：从文本提取候选物种 → Assign
        var candidates = GraphInputExtractor.ExtractCandidates(rawText);
        if (candidates.Count < 2) return null;
        var r = GraphSolver.Solve(asset, candidates, GraphSolveMode.Assign);
        if (r.Status == InferenceStatus.Solved && r.Assignment.Count > 0)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = r.FinalResult,
                ConstraintNote = "【graph 引擎】转化图赋值",
                Confidence = 0.9,
            };
        }
        return null; // 无解/多解 → 不蒙数
    }
}
