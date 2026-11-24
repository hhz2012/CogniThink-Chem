namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Parsing;
using CogniThink.TutorBrain.Application.Gateway;

// ============================================================
// Task 1: 引擎产物构建器 —— 从题干抽取方程式并直调底层引擎，
// 把中间真值装进 EngineArtifacts.EngineOutputs。
// 只读消费：复用 BalanceGatewaySolver 的公开抽取器 + Chem.Core 公开引擎，
// 不调用 LlmGatewayService 判分链路，不修改任何引擎逻辑（零回归）。
// 键约定（供 StepTruthExtractor / 校验门按名取值）：
//   "parsed" : ParsedEquation? 解析结果（含 AllSpecies / Left / Right）
//   "redox"  : RedoxAnalysis?  氧化还原分析（含 Changes / 半反应）
// ============================================================

public static class EngineArtifactsBuilder
{
    public const string KeyParsed = "parsed";
    public const string KeyRedox = "redox";

    /// <summary>
    /// 从题干构建引擎产物容器。任何环节失败（抽不到方程式 / 解析失败 / 引擎失败）
    /// 都不抛错，只留空对应键 —— 下游提取器按键取不到即返回 null（降级），绝不猜测。
    /// </summary>
    public static EngineArtifacts Build(QuestionCase @case)
    {
        var artifacts = new EngineArtifacts { Case = @case };

        var raw = ExtractEquation(@case.Question ?? string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return artifacts;

        var equation = BalanceGatewaySolver.NormalizeForBalancing(raw);

        if (!EquationParser.TryParse(equation, out var parsed, out _) || parsed is null)
            return artifacts;
        artifacts.EngineOutputs[KeyParsed] = parsed;

        // 氧化还原分析（非氧化还原 / 失败 → Success=false，不抛异常）。
        var redox = RedoxBalancer.SplitHalfReactions(equation, "acidic");
        artifacts.EngineOutputs[KeyRedox] = redox;

        return artifacts;
    }

    // —— 类型安全的取值辅助（供提取器 / 校验门复用） ——

    public static ParsedEquation? GetParsed(EngineArtifacts a)
        => a.EngineOutputs.TryGetValue(KeyParsed, out var v) && v is ParsedEquation p ? p : null;

    public static RedoxAnalysis? GetRedox(EngineArtifacts a)
        => a.EngineOutputs.TryGetValue(KeyRedox, out var v) && v is RedoxAnalysis r ? r : null;

    // —— 方程式抽取：优先多等号/箭头（BalanceGatewaySolver），兜底单等号 ——
    // BalanceGatewaySolver.ExtractFirstEquation 的分隔符正则要求 ≥2 个等号（==/===），
    // 不识别高中题干最常见的单等号 "A + B = C"；此处补单等号抽取。
    // 误判（如 pH=7）由下方 EquationParser.TryParse 过滤：解析失败即不填 parsed → 降级，不蒙数。

    private static string? ExtractEquation(string text)
    {
        var multi = BalanceGatewaySolver.ExtractFirstEquation(text);
        if (!string.IsNullOrWhiteSpace(multi)) return multi;
        return ExtractSingleEquals(text);
    }

    private static string? ExtractSingleEquals(string text)
    {
        const string leftBounds = "：:。；;\n（(";
        const string rightBounds = "。；;\n";
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '=') continue;
            int start = 0;
            for (int j = i - 1; j >= 0; j--)
                if (leftBounds.IndexOf(text[j]) >= 0) { start = j + 1; break; }
            int end = text.Length;
            for (int j = i + 1; j < text.Length; j++)
                if (rightBounds.IndexOf(text[j]) >= 0) { end = j; break; }
            var seg = text.Substring(start, end - start).Trim();
            var sides = seg.Split('=');
            if (sides.Length == 2 && ContainsElementLetters(sides[0]) && ContainsElementLetters(sides[1]))
                return seg;
        }
        return null;
    }

    private static bool ContainsElementLetters(string s) =>
        System.Text.RegularExpressions.Regex.IsMatch(s, @"[A-Z][a-z]?");
}
