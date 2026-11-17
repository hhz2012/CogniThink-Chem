using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>题给平衡信息（与 <see cref="EquilibriumKnownSpec"/> 对应，数值为精确有理数）。</summary>
public sealed class EquilibriumKnownFact
{
    /// <summary>"conversion"（转化率）/ "moles"（平衡物质的量）/ "amountFraction"（平衡物质的量分数）。</summary>
    public string Kind { get; set; } = "";

    /// <summary>相关物种。</summary>
    public string Species { get; set; } = "";

    /// <summary>精确值。</summary>
    public Rational Value { get; set; }
}

/// <summary>平衡扰动（与 <see cref="DisturbanceSpec"/> 对应）。</summary>
public sealed class EquilibriumDisturbance
{
    /// <summary>"compress" / "expand" / "addSpecies" / "heatUp" / "coolDown" / "catalyst"。</summary>
    public string Kind { get; set; } = "";

    /// <summary>addSpecies 时的物种。</summary>
    public string Species { get; set; } = "";

    /// <summary>正反应热符号（heatUp/coolDown 须给："exothermic" / "endothermic"）。</summary>
    public string Thermal { get; set; } = "";
}

/// <summary>
/// 化学平衡推断请求：可逆方程式、容器类型、起始投料、题给平衡信息、
/// 扰动（可空）、总压（Kp 计算用，可空）与分数目标物种（可空，缺省为题给信息物种）。
/// </summary>
public sealed class EquilibriumRequest
{
    /// <summary>可逆反应方程式（L2 写法，可用 = 或 ⇌，未配平亦可）。</summary>
    public string Equation { get; set; } = "";

    /// <summary>"constantVolume"（恒温恒容）/ "constantPressure"（恒温恒压）。</summary>
    public string Container { get; set; } = "constantVolume";

    /// <summary>起始投料：物种 → 物质的量（mol）；未给的物种按 0。</summary>
    public Dictionary<string, Rational> Initial { get; set; } = new();

    /// <summary>题给平衡信息（缺失时如实上抛欠定）。</summary>
    public EquilibriumKnownFact? Known { get; set; }

    /// <summary>平衡后施加的扰动（可空）。</summary>
    public EquilibriumDisturbance? Disturbance { get; set; }

    /// <summary>体系总压（Kp 计算用）；未给时 KValue 为空并在步骤说明。</summary>
    public Rational? TotalPressure { get; set; }

    /// <summary>要求摩尔分数的物种；为空时默认取题给信息物种。</summary>
    public string FractionSpecies { get; set; } = "";
}

/// <summary>化学平衡推断结果：三段式表 + 转化率/分数/Kp/移动方向 + 可溯源步骤。</summary>
public sealed class EquilibriumResult
{
    public InferenceStatus Status { get; set; }

    public string Message { get; set; } = "";

    /// <summary>平衡结论（Solved 时填充）。</summary>
    public EquilibriumAnswerSpec? Answer { get; set; }

    /// <summary>配平后的可逆方程式原文。</summary>
    public string BalancedEquation { get; set; } = "";

    public List<InferenceStep> Steps { get; set; } = new();
}

/// <summary>
/// 化学平衡推断引擎（Hard 平衡综合：三段式 + 转化率 + Kp + 移动方向）。
/// 知识驱动流程：
/// <list type="number">
/// <item>方程式解析与配平（<see cref="EquationParser"/> + <see cref="EquationBalancer"/>），
/// 反应物计量数取负、生成物取正。失败即无解。</item>
/// <item>起始投料未给的物种按 0；题给平衡信息缺失时如实上抛欠定（"无平衡信息无法定转化量"）。</item>
/// <item>按题给信息类型（转化率/平衡量/平衡分数）解一元一次方程得转化进度 x（全程 <see cref="Rational"/> 精确）；
/// 与题给矛盾（x 非正、某物种平衡量为负）判无解，分数方程退化判欠定。</item>
/// <item>三段式表逐物种给出 起始/转化(ν·x，负号表示减少)/平衡。</item>
/// <item>结论：转化率（最简分数）、摩尔分数、Kp=Π(χ_i·P)^ν_i（仅当题给总压；否则说明不可精确计算，不算失败）。</item>
/// <item>扰动按规则定性判方向：体积/浓度/催化剂判据 EQ-001（勒夏特列）、催化剂判据 EQ-003、
/// "K 只随温度变化"判据 EQ-002；升温/降温未给反应热符号时如实欠定。</item>
/// </list>
/// 铁律：每一步结论可溯源（Evidence/KnowledgeId/Source）；推不出时如实 Underdetermined/NoSolution，绝不猜测；
/// 数值全部精确有理数，无开方，不引入浮点。
/// </summary>
public static class EquilibriumEngine
{
    private static readonly Lazy<Rule?> LeChatRule = new(() => FindRule("EQ-001"));
    private static readonly Lazy<Rule?> KTempRule = new(() => FindRule("EQ-002"));
    private static readonly Lazy<Rule?> CatalystRule = new(() => FindRule("EQ-003"));

    /// <summary>推断平衡三段式与结论。</summary>
    public static EquilibriumResult Infer(EquilibriumRequest request)
    {
        var result = new EquilibriumResult();

        if (request.Container is not ("constantVolume" or "constantPressure"))
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"容器类型只能是 constantVolume（恒容）或 constantPressure（恒压）：\"{request.Container}\"";
            return result;
        }

        // 1. 方程式解析与配平：计量数 ν（反应物取负、生成物取正）。
        if (!EquationParser.TryParse(request.Equation, out var parsed, out var parseError))
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"可逆反应方程式解析失败：{parseError}";
            return result;
        }

        var balance = EquationBalancer.Balance(parsed!);
        if (!balance.Success)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"可逆反应方程式配平失败：{balance.FailReason}";
            return result;
        }

        var balanced = balance.BalancedEquation!;
        result.BalancedEquation = balanced;

        var nu = new Dictionary<string, Rational>(StringComparer.Ordinal);
        var order = new List<string>();
        var terms = parsed!.AllTerms.ToList();
        for (var i = 0; i < terms.Count; i++)
        {
            var key = terms[i].Species.ToString();
            var signed = i < parsed.Left.Count ? balance.Coefficients[i].Negate() : balance.Coefficients[i];
            if (!nu.ContainsKey(key))
            {
                order.Add(key);
            }

            nu[key] = nu.GetValueOrDefault(key) + signed;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = $"方程式解析与配平：\"{request.Equation}\" → 配平式 {balanced}；"
                          + $"计量数 ν（反应物取负、生成物取正）：{string.Join("，", order.Select(k => $"{k}={nu[k]}"))}。",
            Evidence = balanced,
            KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
            Source = LeChatRule.Value?.Source ?? "",
        });

        // 2. 起始投料：未给的物种按 0；投料物种必须在方程式中。
        var stray = request.Initial.Keys.Where(k => !nu.ContainsKey(k)).ToList();
        if (stray.Count > 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"起始投料物种 {string.Join("、", stray)} 不出现在方程式中，无法列入三段式。";
            return result;
        }

        var start = new Dictionary<string, Rational>(StringComparer.Ordinal);
        var total0 = Rational.Zero;
        foreach (var key in order)
        {
            var value = request.Initial.TryGetValue(key, out var v) ? v : Rational.Zero;
            if (value < Rational.Zero)
            {
                result.Status = InferenceStatus.NoSolution;
                result.Message = $"起始投料 {key} = {value} 为负，物理上不成立。";
                return result;
            }

            start[key] = value;
            total0 += value;
        }

        // 3. 题给平衡信息 → 解转化进度 x（一元一次，Rational 精确）。
        if (request.Known is not { } known)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = "题给缺少平衡信息（转化率/平衡量/平衡分数），无平衡信息无法定转化量，三段式无法闭合。";
            return result;
        }

        if (!nu.TryGetValue(known.Species, out var nuS))
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"题给平衡信息物种 {known.Species} 不出现在方程式中。";
            return result;
        }

        var deltaNu = Rational.Zero;
        foreach (var key in order)
        {
            deltaNu += nu[key];
        }

        Rational x;
        switch (known.Kind)
        {
            case "conversion":
            {
                if (nuS > Rational.Zero)
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"转化率只能针对反应物给出：{known.Species} 是生成物。";
                    return result;
                }

                if (known.Value <= Rational.Zero || known.Value > Rational.One)
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"转化率 {known.Value} 不在 (0, 1] 内，与题设矛盾。";
                    return result;
                }

                x = known.Value * start[known.Species] / nuS.Abs();
                if (!TableFeasible(order, nu, start, x, out var failReason))
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"题给转化率与起始投料矛盾：{failReason}";
                    return result;
                }

                break;
            }

            case "moles":
            {
                if (known.Value < Rational.Zero)
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"平衡物质的量 {known.Value} 为负，与题设矛盾。";
                    return result;
                }

                x = nuS > Rational.Zero
                    ? (known.Value - start[known.Species]) / nuS
                    : (start[known.Species] - known.Value) / nuS.Abs();
                if (!TableFeasible(order, nu, start, x, out var failReason))
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"题给平衡量与起始投料矛盾：{failReason}";
                    return result;
                }

                break;
            }

            case "amountFraction":
            {
                // (start_S + ν_S·x) / (total0 + Δν·x) = f → x·(ν_S − f·Δν) = f·total0 − start_S
                var coef = nuS - known.Value * deltaNu;
                if (coef.IsZero)
                {
                    result.Status = InferenceStatus.Underdetermined;
                    result.Message = "平衡分数与计量数退化（x 系数为零），分数条件对转化量无约束，无法唯一确定 x。";
                    return result;
                }

                x = (known.Value * total0 - start[known.Species]) / coef;
                if (!TableFeasible(order, nu, start, x, out var failReason))
                {
                    result.Status = InferenceStatus.Underdetermined;
                    result.Message = $"题给平衡分数解出的转化量非法（{failReason}），无法据此闭合三段式。";
                    return result;
                }

                break;
            }

            default:
                result.Status = InferenceStatus.NoSolution;
                result.Message = $"题给平衡信息类型只能是 conversion / moles / amountFraction：\"{known.Kind}\"";
                return result;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = known.Kind switch
            {
                "conversion" => $"由 {known.Species} 的转化率 {known.Value} 定转化进度：x = 转化率 × 起始量 ÷ |ν| = {x} mol。",
                "moles" => $"由 {known.Species} 的平衡量 {known.Value} mol 定转化进度：x = |平衡量 − 起始量| ÷ |ν| = {x} mol。",
                _ => $"由 {known.Species} 的平衡分数 {known.Value} 解一元一次方程定转化进度：x = {x} mol。",
            },
            Evidence = $"given.equilibrium.known={known.Kind}({known.Species})={known.Value}",
            KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
            Source = LeChatRule.Value?.Source ?? "",
        });

        // 4. 三段式表：起始 / 转化(ν·x，负号表示减少) / 平衡。
        var table = new List<EquilibriumRowSpec>();
        var end = new Dictionary<string, Rational>(StringComparer.Ordinal);
        var totalEnd = Rational.Zero;
        foreach (var key in order)
        {
            var change = nu[key] * x;
            var endValue = start[key] + change;
            end[key] = endValue;
            totalEnd += endValue;
            table.Add(new EquilibriumRowSpec
            {
                Species = key,
                Start = start[key].ToString(),
                Change = change.ToString(),
                End = endValue.ToString(),
            });
        }

        result.Steps.Add(new InferenceStep
        {
            Description = "三段式（mol）：" + string.Join("；", table.Select(r =>
                $"{r.Species} 起始 {r.Start}，转化 {r.Change}，平衡 {r.End}"))
                + $"；平衡总物质的量 = {totalEnd} mol。",
            Evidence = $"x = {x}，ν·x 逐物种列表",
            KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
            Source = LeChatRule.Value?.Source ?? "",
        });

        // 5. 结论：转化率 / 摩尔分数 / Kp。
        string? conversionSpecies;
        Rational? conversion;
        if (known.Kind == "conversion")
        {
            conversionSpecies = known.Species;
            conversion = known.Value;
        }
        else if (start[known.Species] > Rational.Zero)
        {
            conversionSpecies = known.Species;
            conversion = (nuS * x).Abs() / start[known.Species];
        }
        else
        {
            conversionSpecies = null;
            conversion = null;
        }

        var fractionKey = string.IsNullOrWhiteSpace(request.FractionSpecies) ? known.Species : request.FractionSpecies;
        if (!nu.ContainsKey(fractionKey))
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"分数目标物种 {fractionKey} 不出现在方程式中。";
            return result;
        }

        var fraction = end[fractionKey] / totalEnd;

        Rational? kp = null;
        string kpNote;
        if (request.TotalPressure is { } pressure)
        {
            var numerator = Rational.One;
            var denominator = Rational.One;
            var zeroPartial = false;
            foreach (var key in order)
            {
                var partial = end[key] / totalEnd * pressure;
                if (partial.IsZero)
                {
                    zeroPartial = true;
                }

                var power = nu[key];
                var factor = Pow(partial, power.Abs());
                if (power > Rational.Zero)
                {
                    numerator *= factor;
                }
                else
                {
                    denominator *= factor;
                }
            }

            if (zeroPartial && (denominator.IsZero || numerator.IsZero))
            {
                kpNote = "有物种平衡量为 0，分压乘积中出现零因子，Kp 不可精确计算（题设未要求即可）。";
            }
            else
            {
                kp = numerator / denominator;
                kpNote = $"Kp = Π(χ_i·P)^ν_i = {kp}（P = {pressure}，χ 为摩尔分数）。";
            }
        }
        else
        {
            kpNote = "题未给总压/体积，K 不可精确计算（题设未要求即可）。";
        }

        result.Steps.Add(new InferenceStep
        {
            Description = "结论："
                + (conversionSpecies is not null
                    ? $"{conversionSpecies} 转化率 = 转化量 ÷ 起始量 = {conversion}；"
                    : $"{known.Species} 起始量为 0，转化率不适用；")
                + $"平衡时 {fractionKey} 摩尔分数 = {end[fractionKey]} ÷ {totalEnd} = {fraction}；"
                + kpNote,
            Evidence = conversionSpecies is not null
                ? $"ν·x = {nuS * x}，start = {start[known.Species]}，totalEnd = {totalEnd}"
                : $"totalEnd = {totalEnd}",
            KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
            Source = LeChatRule.Value?.Source ?? "",
        });

        // 6. 扰动：定性判移动方向（不重算新平衡）。
        string? direction = null;
        if (request.Disturbance is { } disturbance)
        {
            if (!TryJudgeDirection(request, disturbance, nu, deltaNu, result, out direction))
            {
                return result;
            }
        }

        result.Answer = new EquilibriumAnswerSpec
        {
            Table = table,
            ConversionSpecies = conversionSpecies,
            Conversion = conversion?.ToString(),
            FractionSpecies = fractionKey,
            Fraction = fraction.ToString(),
            KValue = kp?.ToString(),
            Direction = direction,
        };
        result.Status = InferenceStatus.Solved;
        result.Message = $"平衡推断完成：x = {x} mol，"
            + (conversionSpecies is not null ? $"{conversionSpecies} 转化率 {conversion}，" : "")
            + $"{fractionKey} 分数 {fraction}"
            + (kp is not null ? $"，Kp = {kp}" : "")
            + (direction is not null ? $"，扰动后{DirectionText(direction)}" : "");
        return result;
    }

    // -----------------------------------------------------------------------
    // 扰动方向判定（规则溯源：EQ-001 / EQ-002 / EQ-003）
    // -----------------------------------------------------------------------

    private static bool TryJudgeDirection(
        EquilibriumRequest request,
        EquilibriumDisturbance disturbance,
        Dictionary<string, Rational> nu,
        Rational deltaNu,
        EquilibriumResult result,
        out string? direction)
    {
        direction = null;
        switch (disturbance.Kind)
        {
            case "compress" or "expand":
            {
                // 本题库体系均为气相反应：全部物种计气，方向由 Δν 决定。
                var pressurize = disturbance.Kind == "compress";
                direction = deltaNu.IsZero
                    ? "noShift"
                    : (deltaNu < Rational.Zero) == pressurize
                        ? "forward"
                        : "reverse";
                var action = pressurize ? "缩小体积增大压强" : "增大体积减小压强";
                var side = deltaNu < Rational.Zero
                    ? "正反应为气体分子数减小的方向（Δν = " + deltaNu + "）"
                    : deltaNu > Rational.Zero
                        ? "逆反应为气体分子数减小的方向（Δν = " + deltaNu + "）"
                        : "反应前后气体分子数不变（Δν = 0）";
                result.Steps.Add(new InferenceStep
                {
                    Description = $"扰动分析（{action}）：{side}。依据勒夏特列原理，"
                                  + $"{(direction == "noShift" ? "压强改变不使平衡移动" : $"平衡向气体分子数减小的方向移动，即{DirectionText(direction)}")}。"
                                  + "（" + (LeChatRule.Value?.Conclusion.Display ?? "改变一个条件，平衡向减弱这种改变的方向移动") + "）",
                    Evidence = $"given.equilibrium.disturbance={disturbance.Kind}，Δν={deltaNu}",
                    KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
                    Source = LeChatRule.Value?.Source ?? "",
                });
                AddKUnchangedStep(result, "温度不变");
                return true;
            }

            case "addSpecies":
            {
                if (!nu.TryGetValue(disturbance.Species, out var nuAdded))
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"扰动加入物种 {disturbance.Species} 不出现在方程式中。";
                    return false;
                }

                direction = nuAdded < Rational.Zero ? "forward" : "reverse";
                result.Steps.Add(new InferenceStep
                {
                    Description = $"扰动分析（增大 {disturbance.Species} 浓度）：{disturbance.Species} 是"
                                  + $"{(nuAdded < Rational.Zero ? "反应物" : "生成物")}，"
                                  + $"平衡{DirectionText(direction)}以消耗加入的该物种。"
                                  + $"（{LeChatRule.Value?.Conclusion.Display ?? "改变一个条件，平衡向减弱这种改变的方向移动"}）",
                    Evidence = $"given.equilibrium.disturbance=addSpecies({disturbance.Species})，ν={nuAdded}",
                    KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
                    Source = LeChatRule.Value?.Source ?? "",
                });
                AddKUnchangedStep(result, "温度不变");
                return true;
            }

            case "heatUp" or "coolDown":
            {
                if (disturbance.Thermal is not ("exothermic" or "endothermic"))
                {
                    result.Status = InferenceStatus.Underdetermined;
                    result.Message = "升温/降温扰动未给反应热符号（exothermic/endothermic），无法判定移动方向。";
                    return false;
                }

                var heating = disturbance.Kind == "heatUp";
                var exothermic = disturbance.Thermal == "exothermic";
                direction = heating == exothermic ? "reverse" : "forward";
                var thermalText = exothermic ? "正反应放热" : "正反应吸热";
                result.Steps.Add(new InferenceStep
                {
                    Description = $"扰动分析（{(heating ? "升高" : "降低")}温度）：{thermalText}，"
                                  + $"平衡向{(heating ? "吸热" : "放热")}方向移动，即{DirectionText(direction)}。"
                                  + $"（{LeChatRule.Value?.Conclusion.Display ?? "改变一个条件，平衡向减弱这种改变的方向移动"}）",
                    Evidence = $"given.equilibrium.disturbance={disturbance.Kind}，thermal={disturbance.Thermal}",
                    KnowledgeId = LeChatRule.Value?.KnowledgeId ?? "",
                    Source = LeChatRule.Value?.Source ?? "",
                });
                var kChange = (heating && exothermic) || (!heating && !exothermic) ? "减小" : "增大";
                result.Steps.Add(new InferenceStep
                {
                    Description = $"平衡常数只随温度变化：{(heating ? "升温" : "降温")}使 K {kChange}。"
                                  + $"（{KTempRule.Value?.Conclusion.Display ?? "K 只与温度有关，与浓度、压强、催化剂无关"}）",
                    Evidence = $"given.equilibrium.disturbance={disturbance.Kind}",
                    KnowledgeId = KTempRule.Value?.KnowledgeId ?? "",
                    Source = KTempRule.Value?.Source ?? "",
                });
                return true;
            }

            case "catalyst":
            {
                direction = "noShift";
                result.Steps.Add(new InferenceStep
                {
                    Description = $"扰动分析（加入催化剂）：催化剂同等程度加快正、逆反应速率，平衡不移动、转化率不变。"
                                  + $"（{CatalystRule.Value?.Conclusion.Display ?? "催化剂同等程度加快正逆反应速率，不改变转化率"}）",
                    Evidence = "given.equilibrium.disturbance=catalyst",
                    KnowledgeId = CatalystRule.Value?.KnowledgeId ?? "",
                    Source = CatalystRule.Value?.Source ?? "",
                });
                AddKUnchangedStep(result, "温度不变");
                return true;
            }

            default:
                result.Status = InferenceStatus.NoSolution;
                result.Message = $"扰动类型只能是 compress / expand / addSpecies / heatUp / coolDown / catalyst：\"{disturbance.Kind}\"";
                return false;
        }
    }

    private static void AddKUnchangedStep(EquilibriumResult result, string reason)
    {
        result.Steps.Add(new InferenceStep
        {
            Description = $"{reason}，平衡常数 K 不变。"
                          + $"（{KTempRule.Value?.Conclusion.Display ?? "K 只与温度有关，与浓度、压强、催化剂无关"}）",
            Evidence = "given.equilibrium.disturbance",
            KnowledgeId = KTempRule.Value?.KnowledgeId ?? "",
            Source = KTempRule.Value?.Source ?? "",
        });
    }

    // -----------------------------------------------------------------------
    // 辅助
    // -----------------------------------------------------------------------

    /// <summary>三段式可行性：转化进度为正且各物种平衡量非负。</summary>
    private static bool TableFeasible(
        List<string> order,
        Dictionary<string, Rational> nu,
        Dictionary<string, Rational> start,
        Rational x,
        out string failReason)
    {
        failReason = "";
        if (x <= Rational.Zero)
        {
            failReason = $"解出转化进度 x = {x} mol 非正";
            return false;
        }

        foreach (var key in order)
        {
            if (start[key] + nu[key] * x < Rational.Zero)
            {
                failReason = $"{key} 平衡量为负（超限量），x = {x} mol";
                return false;
            }
        }

        return true;
    }

    /// <summary>整数幂（指数来自配平计量数，恒为小正整数；Rational 连乘，无浮点）。</summary>
    private static Rational Pow(Rational basis, Rational exponent)
    {
        var power = exponent;
        if (!power.IsInteger)
        {
            throw new InvalidOperationException($"Kp 计算出现非整数计量数 {power}");
        }

        var result = Rational.One;
        var times = (long)power.Num;
        for (var i = 0; i < times; i++)
        {
            result *= basis;
        }

        return result;
    }

    private static string DirectionText(string direction) => direction switch
    {
        "forward" => "正向移动",
        "reverse" => "逆向移动",
        _ => "不移动",
    };

    private static Rule? FindRule(string id) =>
        ChemDataLoader.LoadRules(DataRoot.Resolve()).FirstOrDefault(r => r.Id == id);
}
