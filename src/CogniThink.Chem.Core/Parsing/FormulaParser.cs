using CogniThink.Chem.Core.Chemistry;

namespace CogniThink.Chem.Core.Parsing;

/// <summary>
/// 分子式解析前端。在 <see cref="Species.Parse"/> 基础上补充：
/// <list type="bullet">
/// <item>结晶水合物 "·" 连接式（CuSO4·5H2O、Na2CO3·10H2O）：按 "·" 拆分，
/// 后段系数乘到各元素计数上再合并；电荷取首段（水合物整体不带电）。</item>
/// <item>TryParse 安全入口，解析失败返回错误描述而非抛异常。</item>
/// </list>
/// 高中适用边界：只处理课内书写形式；"·" 仅支持结晶水语义，不支持加合物。
/// 易错点：FeSO4·7H2O 的 H/O 计数必须包含结晶水，摩尔质量计算同样依赖该合并结果。
/// </summary>
public static class FormulaParser
{
    /// <summary>
    /// 解析分子式为 <see cref="Species"/>（元素计数 + 电荷）。
    /// </summary>
    /// <param name="text">分子式，如 "Fe2(SO4)3"、"CuSO4·5H2O"、"SO4^2-"。</param>
    /// <returns>解析出的物种。</returns>
    /// <exception cref="FormulaException">格式非法。</exception>
    public static Species Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormulaException("化学式为空");
        }

        var trimmed = text.Trim();
        var parts = trimmed.Split('·', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return Species.Parse(parts[0]);
        }

        // 结晶水合物：合并各段元素计数
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        var charge = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            // 首段不允许带倍数（那是方程式系数语义）；后段支持 "5H2O" 形式
            var multiplier = 1;
            var body = parts[i];
            if (i > 0)
            {
                (multiplier, body) = ExtractLeadingCoefficient(parts[i]);
            }

            var part = Species.Parse(body);
            if (i == 0)
            {
                charge = part.Charge;
            }
            else if (part.Charge != 0)
            {
                throw new FormulaException($"结晶水合物后段不应带电荷：{text}");
            }

            foreach (var (symbol, count) in part.Elements)
            {
                merged.TryGetValue(symbol, out var existing);
                merged[symbol] = existing + count * multiplier;
            }
        }

        return Species.FromElements(trimmed, merged, charge);
    }

    /// <summary>安全解析：失败时 species 为 null，error 给出原因。</summary>
    public static bool TryParse(string text, out Species? species, out string? error)
    {
        try
        {
            species = Parse(text);
            error = null;
            return true;
        }
        catch (FormulaException ex)
        {
            species = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 提取结晶水段前的倍数（"5H2O" → (5, "H2O")）。
    /// 仅用于 "·" 之后的段；主体段的开头数字属于系数语义，由方程式解析器处理。
    /// </summary>
    private static (int Multiplier, string Body) ExtractLeadingCoefficient(string segment)
    {
        var i = 0;
        while (i < segment.Length && char.IsDigit(segment[i]))
        {
            i++;
        }

        if (i == 0)
        {
            return (1, segment);
        }

        if (i == segment.Length)
        {
            throw new FormulaException($"结晶水段缺少化学式：{segment}");
        }

        return (int.Parse(segment[..i]), segment[i..]);
    }
}
