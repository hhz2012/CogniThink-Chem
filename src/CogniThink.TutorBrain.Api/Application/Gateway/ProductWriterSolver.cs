using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Math;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"少量/过量产物书写"求解器（阶段3C / AC-13，P1）。
/// <para>真值源：<c>data/knowledge/product-rules.json</c>（化学事实只在 JSON；本文件仅含触发识别、配平与响应构造，无硬编码产物骨架）。
/// 命中规则表后给出产物骨架，由 <see cref="EquationBalancer"/> 守恒配平；本类不推导未知产物，只在规则命中时给出确定骨架。</para>
/// <para>红线（不蒙数）：规则表外 / 量关键词缺失 / 配平失败（无解/欠定）→ 一律返回 null，绝不臆造产物。</para>
/// </summary>
public static class ProductWriterSolver
{
    // ==================== KB（产物骨架在 JSON） ====================

    private sealed class ExpandTerm
    {
        public string Species { get; set; } = "";
        public int Mult { get; set; }
    }

    private sealed class ProductRuleEntry
    {
        public string Id { get; set; } = "";
        public List<string> Left { get; set; } = new();
        public List<string> Right { get; set; } = new();
        public string ExpandPlaceholder { get; set; } = "";
        public List<ExpandTerm> ExpandInto { get; set; } = new();
    }

    private sealed class ProductRulesData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<ProductRuleEntry> Entries { get; set; } = new();
    }

    private static readonly Lazy<Dictionary<string, ProductRuleEntry>> Rules = new(LoadRules);

    private static Dictionary<string, ProductRuleEntry> LoadRules()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "knowledge", "product-rules.json");
            if (!File.Exists(path)) return new Dictionary<string, ProductRuleEntry>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var data = JsonSerializer.Deserialize<ProductRulesData>(json, opts);
            return data?.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal)
                   ?? new Dictionary<string, ProductRuleEntry>();
        }
        catch
        {
            return new Dictionary<string, ProductRuleEntry>();
        }
    }

    /// <summary>尝试书写离子方程式；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;
        // 只在明确问"方程式"的题干触发，避免污染其他 reaction_check 题
        if (!text.Contains("方程式") && !text.Contains("方程")) return null;

        var rule = MatchProductRule(text);
        if (rule is null) return null;

        var skeletonStr = string.Join(" + ", rule.Left) + " = " + string.Join(" + ", rule.Right);

        BalanceResult result;
        try
        {
            result = EquationBalancer.Balance(skeletonStr);
        }
        catch
        {
            return null;
        }
        if (!result.Success || string.IsNullOrWhiteSpace(result.BalancedEquation)) return null;

        // 用返回的系数（与 Left+Right 顺序对齐）重建方程式，避免依赖 BalancedEquation 字符串格式。
        var coefs = result.Coefficients;
        if (coefs.Length != rule.Left.Count + rule.Right.Count) return null;

        var leftParts = new List<string>();
        var idx = 0;
        foreach (var sp in rule.Left)
        {
            // 骨架中可能用中性分子占位以固定元素比（消除配平欠定），命中后按 JSON 配置展开为离子。
            if (!string.IsNullOrEmpty(rule.ExpandPlaceholder) && sp == rule.ExpandPlaceholder && rule.ExpandInto.Count > 0)
            {
                var n = coefs[idx].Num; // ToSmallestIntegers 后 Den==1
                foreach (var term in rule.ExpandInto)
                    leftParts.Add(FormatTerm(n * term.Mult, term.Species));
            }
            else
            {
                leftParts.Add(FormatTerm(coefs[idx].Num, sp));
            }
            idx++;
        }

        var rightParts = new List<string>();
        foreach (var sp in rule.Right)
        {
            rightParts.Add(FormatTerm(coefs[idx].Num, sp));
            idx++;
        }

        // 输出规范化：去掉 ^ 电荷标注，分隔符用 →，
        // 以匹配评估侧 NormalizeChemicalNotation（不去 ^）与 PDF 整句答案关键词。
        var equation = (string.Join(" + ", leftParts) + " → " + string.Join(" + ", rightParts))
            .Replace("^", "");

        var finalResult = $"离子方程式：{equation}";
        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            DerivationSteps = new List<string>
            {
                $"识别反应物与量条件，确定产物骨架：{skeletonStr.Replace("^", "")}",
                "守恒配平（原子/电荷守恒 → 最小正整数系数）",
                $"配平结果：{equation}"
            },
            // 加性推理链（spec R4）：命中 JSON 规则即携带真实 knowledge_id；
            // 无命中返回 null（无响应）→ 节点天然缺席，不臆造。
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "少量/过量产物书写（骨架取自 product-rules.json）",
                    Premises = new List<string> { $"题干量条件命中 data/knowledge/product-rules.json 条目：{rule.Id}" },
                    KnowledgeId = rule.Id,
                    Conclusion = finalResult,
                    EvidenceLevel = EvidenceLevel.KnowledgeFact
                }
            },
            Confidence = 1.0
        };
    }

    private static string FormatTerm(long coef, string species) =>
        coef == 1 ? species : $"{coef}{species}";

    /// <summary>触发识别（条件保持原样）；命中返回 JSON 规则条目，未命中返回 null。</summary>
    private static ProductRuleEntry? MatchProductRule(string text)
    {
        // 亚铁溴化物 + 氯气：还原性序决定少量只氧化亚铁、过量全氧化；
        // 过量时原子+电荷守恒欠定（两种离子可任意比例被氧化），故 JSON 骨架用中性分子占位
        // 固定元素比，配平后再展开为离子。
        if (Regex.IsMatch(text, @"FeBr2") && text.Contains("Cl2"))
        {
            if (text.Contains("少量"))
                return Rules.Value.TryGetValue("pw-febr2-cl2-little", out var little) ? little : null;
            if (text.Contains("过量"))
                return Rules.Value.TryGetValue("pw-febr2-cl2-excess", out var excess) ? excess : null;
        }

        // 偏铝酸盐 + 二氧化碳：量不同产物不同（碱性强弱序）
        if ((text.Contains("NaAlO2") || text.Contains("AlO2")) && text.Contains("CO2"))
        {
            if (text.Contains("少量"))
                return Rules.Value.TryGetValue("pw-naalo2-co2-little", out var a1) ? a1 : null;
            if (text.Contains("过量"))
                return Rules.Value.TryGetValue("pw-naalo2-co2-excess", out var a2) ? a2 : null;
        }

        // 碳酸氢钙 + 少量氢氧化钠：1 mol 氢氧根中和 1 mol 碳酸氢根
        if (text.Contains("Ca(HCO3)2") && text.Contains("NaOH") && text.Contains("少量"))
            return Rules.Value.TryGetValue("pw-cahco32-naoh-little", out var c) ? c : null;

        // 氰化钠 + 少量二氧化碳：pKa 序决定产物（生成碳酸氢根而非碳酸根）
        if ((text.Contains("NaCN") || Regex.IsMatch(text, @"CN-")) && text.Contains("CO2") && text.Contains("少量"))
            return Rules.Value.TryGetValue("pw-nacn-co2-little", out var n) ? n : null;

        return null;
    }
}
