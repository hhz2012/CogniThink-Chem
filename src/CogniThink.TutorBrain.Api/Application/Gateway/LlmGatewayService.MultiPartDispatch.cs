using System.Text;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// P1-2 多部分大题子问分发（兜底链路）。
/// <para>
/// 激活条件（零回归构造）：仅在整题求解未唯一解出（<c>Ok != true</c>）且题干为多部分时，
/// 由 <see cref="ProcessStudentMessageAsync"/> 调用；已解对题（Ok=true）绝不触发。
/// </para>
/// <para>
/// 不蒙数：逐子问复用现有确定性求解器求解；任一子问未唯一解出（Ok!=true 或无结论）→ 整体返回 null，
/// 绝不输出部分答案。全部子问解出 → 聚合成 "(1).. (2).." 结论，标记 task_type=<see cref="MultiPartUtility.MultiPartTaskType"/>。
/// </para>
/// <para>
/// 采纳安全：评估侧 <c>BuildGatewayAnswerAsync</c> 对 multi_part 结论走答案键 AND 判分——
/// 全部子问答案键命中才采纳，任一未命中即 null（凡采纳必全对）→ precision=1、零 harm。
/// </para>
/// </summary>
public sealed partial class LlmGatewayService
{
    private async Task<EngineResponse?> TryDispatchMultiPartAsync(EngineRequest req, CancellationToken ct)
    {
        var rawText = req.RawText ?? string.Empty;
        if (!MultiPartUtility.IsMultiPart(rawText)) return null;

        var subs = MultiPartUtility.SplitSubQuestions(rawText);
        if (subs.Count < 2) return null; // 与检测器一致：少于 2 个子问不分发

        var parts = new List<(int Index, string Result)>(subs.Count);
        foreach (var (idx, subText) in subs)
        {
            ct.ThrowIfCancellationRequested();

            // 逐子问组装请求：与整题同口径的归一化（确定性 task_type，不走 LLM，Production 下无 LLM 也稳定）
            var subReq = NormalizeRequest(new EngineRequest { RawText = subText }, subText);
            // 子问文本求解不传图像资产（图像子问需 P2-3 才能解）→ 图像子问返回 null → 整体 null，不蒙数
            var subResp = await CallEngineAsync(subReq, imageAssets: null, ct).ConfigureAwait(false);

            // 任一子问未唯一解出 → 整体 null，不蒙数、不输出部分答案
            if (subResp.Ok is not true || string.IsNullOrWhiteSpace(subResp.FinalResult))
                return null;

            parts.Add((idx, subResp.FinalResult.Trim()));
        }

        var sb = new StringBuilder();
        foreach (var (idx, result) in parts)
        {
            sb.Append('(').Append(idx).Append(')').Append(result).Append(' ');
        }
        var aggregated = sb.ToString().TrimEnd();

        return new EngineResponse
        {
            Ok = true,
            FinalResult = aggregated,
            DerivationSteps = new List<string>
            {
                "【多部分分发】逐子问经确定性求解器求解；整题结论由各子问结论聚合，评估侧按答案键 AND 校验后采纳"
            },
            ConstraintNote = "【多部分分发聚合】仅当全部子问均唯一解出时产出；任一子问未解则整体 null（不蒙数、不输出部分答案）",
            Confidence = 1.0
        };
    }
}
