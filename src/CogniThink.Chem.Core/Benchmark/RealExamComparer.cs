using CogniThink.Chem.Core.Solving;

namespace CogniThink.Chem.Core.Benchmark;

/// <summary>
/// 真题比对器：引擎结论 vs 真题标准答案逐字段比对（12 类别），
/// 返回（是否一致，差异明细）。纯逻辑，供 CLI 真题验证与后续复用。
/// </summary>
public static class RealExamComparer
{
    /// <summary>引擎结论 vs 真题标准答案逐字段比对，返回（是否一致，差异明细）。</summary>
    public static (bool Ok, List<string> Detail) Compare(ProblemSpec problem, Solution solution)
    {
        var detail = new List<string>();
        var expected = problem.Expected;
        var ok = true;

        switch (problem.Category)
        {
            case ProblemCategory.IonCoexistence:
            {
                var got = solution.Coexistence?.ToString() ?? "(无结论)";
                var want = expected.Verdict?.ToString() ?? "(无)";
                var same = string.Equals(got, want, StringComparison.Ordinal);
                detail.Add($"  共存结论：引擎={got}　标准={want}　{(same ? "一致" : "不一致")}");
                ok &= same;
                break;
            }
            case ProblemCategory.SubstanceIdentification:
            {
                var got = solution.Composition.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);
                var want = expected.Composition?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal)
                           ?? new Dictionary<string, string>(StringComparer.Ordinal);
                var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
                detail.Add($"  成分结论：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
                detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
                break;
            }
            case ProblemCategory.ProcessInference:
            {
                var got = solution.SubstanceMap;
                var want = expected.SubstanceMap ?? new Dictionary<string, string>();
                var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
                detail.Add($"  物质对应：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
                detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
                break;
            }
            case ProblemCategory.StoichiometryCalc:
            {
                var want = expected.NumericAnswer;
                if (want is null)
                {
                    detail.Add("  标准答案缺少数值结论，无法比对。");
                    ok = false;
                }
                else
                {
                    var wantValue = CogniThink.Chem.Core.Math.Rational.Parse(want.Value);
                    var same = solution.NumericValue is { } v && v == wantValue
                               && string.Equals(solution.NumericUnit, want.Unit, StringComparison.Ordinal);
                    detail.Add($"  数值结论：引擎={solution.NumericValue?.ToString() ?? "(无)"} {solution.NumericUnit}　标准={want.Value} {want.Unit}　{(same ? "一致" : "不一致")}");
                    ok &= same;
                }

                break;
            }
            case ProblemCategory.Electrochemistry:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.Electrodes;
                var want = expected.Electrodes;
                foreach (var (label, g, w) in new (string Label, string? Got, string? Want)[]
                {
                    ("负极", got?.Negative, want?.Negative),
                    ("正极", got?.Positive, want?.Positive),
                    ("阳极", got?.Anode, want?.Anode),
                    ("阴极", got?.Cathode, want?.Cathode),
                })
                {
                    if (string.IsNullOrWhiteSpace(g) && string.IsNullOrWhiteSpace(w))
                    {
                        continue;
                    }

                    var same = string.Equals(g ?? "", w ?? "", StringComparison.Ordinal);
                    detail.Add($"  {label}电极式：引擎={g ?? "(无)"}　标准={w ?? "(无)"}　{(same ? "一致" : "不一致")}");
                    ok &= same;
                }

                break;
            }
            case ProblemCategory.ChemicalEquilibrium:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.Equilibrium;
                var want = expected.Equilibrium;
                if (got is null || want is null)
                {
                    detail.Add("  三段式结论缺失，无法比对。");
                    ok = false;
                    break;
                }

                var tableSame = got.Table.Count == want.Table.Count
                                && want.Table.All(wr => got.Table.Any(gr =>
                                    gr.Species == wr.Species && gr.Start == wr.Start
                                    && gr.Change == wr.Change && gr.End == wr.End));
                detail.Add($"  三段式：引擎={{{string.Join("；", got.Table.Select(r => $"{r.Species} 起始{r.Start} 转化{r.Change} 平衡{r.End}"))}}}");
                detail.Add($"  　　　　标准={{{string.Join("；", want.Table.Select(r => $"{r.Species} 起始{r.Start} 转化{r.Change} 平衡{r.End}"))}}}　{(tableSame ? "一致" : "不一致")}");
                ok &= tableSame;

                foreach (var (label, g, w) in new (string Label, string? Got, string? Want)[]
                {
                    ("转化率", got.ConversionSpecies is null ? null : $"{got.ConversionSpecies} {got.Conversion}", want.ConversionSpecies is null ? null : $"{want.ConversionSpecies} {want.Conversion}"),
                    ("摩尔分数", got.FractionSpecies is null ? null : $"{got.FractionSpecies} {got.Fraction}", want.FractionSpecies is null ? null : $"{want.FractionSpecies} {want.Fraction}"),
                    ("K 值", got.KValue, want.KValue),
                    ("移动方向", got.Direction, want.Direction),
                })
                {
                    if (string.IsNullOrWhiteSpace(g) && string.IsNullOrWhiteSpace(w))
                    {
                        continue;
                    }

                    var same = string.Equals(g ?? "", w ?? "", StringComparison.Ordinal);
                    detail.Add($"  {label}：引擎={g ?? "(无)"}　标准={w ?? "(无)"}　{(same ? "一致" : "不一致")}");
                    ok &= same;
                }

                break;
            }
            case ProblemCategory.AqueousIonEquilibrium:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.Aqueous;
                var want = expected.Aqueous;
                if (got is null || want is null)
                {
                    detail.Add("  水溶液结论缺失，无法比对。");
                    ok = false;
                    break;
                }

                if (want.Order.Count > 0 || got.Order.Count > 0)
                {
                    var same = got.Order.SequenceEqual(want.Order, StringComparer.Ordinal);
                    detail.Add($"  浓度顺序：引擎={string.Join(" > ", got.Order)}");
                    detail.Add($"  　　　　　标准={string.Join(" > ", want.Order)}　{(same ? "一致" : "不一致")}");
                    ok &= same;
                }

                if (want.ConservationEquations.Count > 0 || got.ConservationEquations.Count > 0)
                {
                    var same = new HashSet<string>(got.ConservationEquations, StringComparer.Ordinal)
                        .SetEquals(want.ConservationEquations);
                    detail.Add($"  守恒等式：引擎={{{string.Join("；", got.ConservationEquations)}}}");
                    detail.Add($"  　　　　　标准={{{string.Join("；", want.ConservationEquations)}}}　{(same ? "一致" : "不一致")}");
                    ok &= same;
                }

                break;
            }
            case ProblemCategory.IndustrialProcess:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.SubstanceMap;
                var want = expected.SubstanceMap ?? new Dictionary<string, string>();
                if (want.Count > 0 || got.Count > 0)
                {
                    var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
                    detail.Add($"  物质对应：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
                    detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
                    ok &= same;
                }

                foreach (var (stepNumber, keyword) in expected.Purposes ?? new Dictionary<string, string>())
                {
                    var actual = solution.Purposes.TryGetValue(stepNumber, out var p) ? p : "(缺)";
                    var hit = actual.Contains(keyword, StringComparison.Ordinal);
                    detail.Add($"  步骤{stepNumber}目的：引擎={actual}　标准须含\"{keyword}\"　{(hit ? "一致" : "不一致")}");
                    ok &= hit;
                }

                break;
            }
            case ProblemCategory.IonPowderIdentification:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.Composition.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);
                var want = expected.Composition?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal)
                           ?? new Dictionary<string, string>(StringComparer.Ordinal);
                var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
                detail.Add($"  成分结论：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
                detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
                break;
            }
            case ProblemCategory.OrganicInference:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.SubstanceMap;
                var want = expected.SubstanceMap ?? new Dictionary<string, string>();
                var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
                detail.Add($"  物质对应：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
                detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
                break;
            }
            case ProblemCategory.ExperimentalInquiry:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                    break;
                }

                var got = solution.Experiment?.Conclusions ?? new List<string>();
                var want = expected.Experiment?.Conclusions ?? new List<string>();
                var same = got.Count == want.Count && got.SequenceEqual(want, StringComparer.Ordinal);
                detail.Add($"  实验结论：引擎={{{string.Join("；", got)}}}");
                detail.Add($"  　　　　　标准={{{string.Join("；", want)}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
                break;
            }
            default:
            {
                if (!solution.IsSolved)
                {
                    detail.Add($"  引擎未解出：{solution.Message}");
                    ok = false;
                }

                break;
            }
        }

        if (expected.Equations.Count > 0 || solution.Equations.Count > 0)
        {
            var sameEq = new HashSet<string>(solution.Equations, StringComparer.Ordinal)
                .SetEquals(expected.Equations);
            detail.Add($"  方程式：引擎={{{string.Join("；", solution.Equations)}}}");
            detail.Add($"  　　　　标准={{{string.Join("；", expected.Equations)}}}　{(sameEq ? "一致" : "不一致")}");
            ok &= sameEq;
        }

        return (ok, detail);
    }
}