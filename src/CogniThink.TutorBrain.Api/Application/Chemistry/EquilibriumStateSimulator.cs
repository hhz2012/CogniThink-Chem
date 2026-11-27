using System.Globalization;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Chemistry;

/// <summary>
/// 平衡态数值模拟器（第三主体类）。在已知初始物种与题给条件下，计算能唯一确定的
/// 平衡数值（pH / 离子浓度 / Ksp 残留 / 沉淀转化 K / 粒子排序 / 守恒式 / 酸碱性）。
/// <para>铁律（不蒙数）：常数只来自 <c>acid-constants.json</c> 或题给 <c>EquilibriumQuery.Given</c>；
/// 缺常数 / 缺输入 / 歧义 → 字段留空 + 加 Warning + <c>Status=Underdetermined</c>，绝不臆造数值。</para>
/// <para>常数中间量走 <see cref="ConstantSpec.ToRational"/> / <see cref="ConstantSpec.Ratio"/> 精确有理，
/// 仅最终展示时转 double 并四舍五入。</para>
/// </summary>
public sealed class EquilibriumStateSimulator
{
    private const double Epsilon = 1e-12;
    private static readonly Regex HydroxideKey = new(@"^(.+?)\(OH\)(\d+)$", RegexOptions.Compiled);

    private readonly AcidConstantsData _constants;

    public EquilibriumStateSimulator() : this(LoadConstants()) { }

    public EquilibriumStateSimulator(AcidConstantsData constants)
    {
        _constants = constants ?? new AcidConstantsData();
    }

    private static AcidConstantsData LoadConstants()
    {
        try
        {
            return RuleStore.Load().Query<AcidConstantsData>("acid-constants") ?? new AcidConstantsData();
        }
        catch
        {
            return new AcidConstantsData();
        }
    }

    // ==================== 主入口 ====================

    public EquilibriumNumericalResult Compute(ChemicalSystemInput input, EquilibriumQuery query)
    {
        var result = new EquilibriumNumericalResult();
        var quantities = query?.Quantities ?? new List<EquilibriumQuantity>();
        if (quantities.Count == 0)
        {
            result.Warnings.Add("EquilibriumQuery 未指定任何 Quantities。");
            return Underdetermined(result);
        }

        // acid-constants 为 25 ℃；题给温度非 25 ℃ 无对应常数 → 整体欠定
        if (query!.Given.TryGetValue("temperature", out var t) && Math.Abs(t - 25) > 1e-6)
        {
            result.Warnings.Add($"题给温度 {t}℃ 非 25℃，acid-constants.json 无对应常数，返回欠定。");
            return Underdetermined(result);
        }

        var determined = false;

        if (quantities.Contains(EquilibriumQuantity.Ph))
            determined |= TryComputePh(input, query, result);

        if (quantities.Contains(EquilibriumQuantity.IonConcentrations))
            determined |= TryComputeIonConcentrations(input, query, result);

        if (quantities.Contains(EquilibriumQuantity.KspResidual))
            determined |= TryComputeKspResidual(input, query, result);

        if (quantities.Contains(EquilibriumQuantity.PrecipitationConversion))
            determined |= TryComputeConversion(query, result);

        var coreQuantities = quantities.Where(q =>
            q == EquilibriumQuantity.ConcentrationOrdering ||
            q == EquilibriumQuantity.Conservations ||
            q == EquilibriumQuantity.AcidityQualitative).ToList();
        if (coreQuantities.Count > 0)
            determined |= TryDelegateCore(input, query, result, coreQuantities);

        result.Status = determined
            ? EquilibriumDetermination.Determined
            : EquilibriumDetermination.Underdetermined;
        if (!determined && result.Warnings.Count == 0)
            result.Warnings.Add("所查询项均无法确定（缺常数或输入信息不足）。");
        return result;
    }

    private static EquilibriumNumericalResult Underdetermined(EquilibriumNumericalResult r)
    {
        r.Status = EquilibriumDetermination.Underdetermined;
        return r;
    }

    // ==================== #1/#2/#3/#4 pH ====================

    private bool TryComputePh(ChemicalSystemInput input, EquilibriumQuery query, EquilibriumNumericalResult result)
    {
        // 题给 pH 直接采用（不重复计算）
        if (query.Given.TryGetValue("pH", out var givenPh) && givenPh > 0 && givenPh < 14)
        {
            result.Ph = Math.Round(givenPh, 2);
            result.DerivationSteps.Add($"pH 由题给直接给出：{givenPh}");
            result.EvidenceKeys.Add("题给:pH");
            return true;
        }

        var solutes = input.Species.Where(s => s.Concentration is > 0).ToList();
        if (solutes.Count == 0)
        {
            result.Warnings.Add("pH 计算需要至少一个带 Concentration（>0）的溶质。");
            return false;
        }

        // 缓冲溶液：弱酸 + 共轭碱（同属一条 AcidLadder）
        if (TryComputeBufferPh(solutes, result)) return true;

        if (solutes.Count != 1)
        {
            result.Warnings.Add($"pH 在非缓冲且非单一溶质时无法唯一确定（当前 {solutes.Count} 个溶质）。");
            return false;
        }

        var solute = solutes[0];
        var c = solute.Concentration!.Value;

        // 弱酸（多元取 Ka1）
        if (TryGetAcidKa(solute.Substance, out var kaKey, out var kaSpec))
        {
            var ka = ToDouble(kaSpec);
            if (ka <= 0) return false;
            if (c / ka < 500)
            {
                result.Warnings.Add($"弱酸 {solute.Substance}：c/Ka = {c / ka:0.##} < 500，不满足最简近似条件，返回欠定（避免蒙数）。");
                return false;
            }
            var h = Math.Sqrt(ka * c);
            var ph = -Math.Log10(h);
            if (double.IsNaN(ph) || ph <= 0 || ph >= 14)
            {
                result.Warnings.Add($"弱酸 {solute.Substance} pH 计算结果越界，返回欠定。");
                return false;
            }
            result.Ph = Math.Round(ph, 2);
            result.IonConcentrations["H^+"] = h;
            result.DerivationSteps.Add($"弱酸 {solute.Substance}：[H⁺] = √(Ka·c) = √({FormatConst(kaSpec)} × {c}) ≈ {h:E3}");
            result.DerivationSteps.Add($"pH = -lg[H⁺] ≈ {result.Ph}");
            result.EvidenceKeys.Add(kaKey);
            return true;
        }

        // 弱碱（Kb 直接收录）
        if (TryGetBaseKb(solute.Substance, out var kbKey, out var kbSpec))
        {
            var kb = ToDouble(kbSpec);
            if (kb <= 0) return false;
            if (c / kb < 500)
            {
                result.Warnings.Add($"弱碱 {solute.Substance}：c/Kb < 500，不满足最简近似条件，返回欠定。");
                return false;
            }
            var oh = Math.Sqrt(kb * c);
            var ph = 14 + Math.Log10(oh);
            if (double.IsNaN(ph) || ph <= 0 || ph >= 14)
            {
                result.Warnings.Add($"弱碱 {solute.Substance} pH 计算结果越界，返回欠定。");
                return false;
            }
            result.Ph = Math.Round(ph, 2);
            result.IonConcentrations["OH^-"] = oh;
            result.DerivationSteps.Add($"弱碱 {solute.Substance}：Kb = {FormatConst(kbSpec)}，[OH⁻] = √(Kb·c) ≈ {oh:E3}");
            result.DerivationSteps.Add($"pH = 14 + lg[OH⁻] ≈ {result.Ph}");
            result.EvidenceKeys.Add(kbKey);
            return true;
        }

        // 盐水解（强碱弱酸盐 → 碱性；强酸弱碱盐 → 酸性）
        if (TryComputeHydrolysisPh(solute.Substance, c, result)) return true;

        result.Warnings.Add($"溶质 {solute.Substance} 未在 acid-constants.json 的 Ka/Kb/共轭阶梯中找到对应常数，pH 无法确定。");
        return false;
    }

    private bool TryComputeBufferPh(IReadOnlyList<SpeciesInput> solutes, EquilibriumNumericalResult result)
    {
        if (solutes.Count != 2) return false;
        var a = solutes[0]; var b = solutes[1];

        // 在 AcidLadders 中找一对：酸（阶梯首项）+ 共轭碱（阶梯后续项）
        foreach (var kv in _constants.AcidLadders)
        {
            var acidKey = kv.Key;
            var ladder = kv.Value;
            if (ladder.Count < 2) continue;

            var (acidSolute, baseSolute, baseIndex) = MatchConjugatePair(a, b, acidKey, ladder);
            if (acidSolute is null || baseSolute is null || baseIndex < 1) continue;

            var kaKeyForStep = ladder.Count > 2 ? $"{acidKey}:Ka{baseIndex}" : acidKey;
            if (!_constants.Ka.TryGetValue(kaKeyForStep, out var kaSpec))
            {
                // 阶梯未给分步 Ka：回退到酸主键（若存在）
                if (!_constants.Ka.TryGetValue(acidKey, out kaSpec)) continue;
            }

            var ka = ToDouble(kaSpec);
            if (ka <= 0) continue;

            var nAcid = MolesOf(acidSolute);
            var nBase = MolesOf(baseSolute);
            if (nAcid <= 0 || nBase <= 0) continue;

            var ratio = nBase / nAcid;
            var ph = -Math.Log10(ka) + Math.Log10(ratio);
            if (double.IsNaN(ph) || ph <= 0 || ph >= 14) continue;

            result.Ph = Math.Round(ph, 2);
            result.DerivationSteps.Add(
                $"缓冲对 {acidKey}/{ladder[baseIndex]}：pH = pKa + lg([碱]/[酸]) = " +
                $"{-Math.Log10(ka):0.##} + lg({ratio:0.###}) ≈ {result.Ph}（默认同体积，以物质的量比代替浓度比）");
            result.EvidenceKeys.Add(kaKeyForStep);
            return true;
        }

        return false;
    }

    private (SpeciesInput? Acid, SpeciesInput? Base, int BaseIndex) MatchConjugatePair(
        SpeciesInput a, SpeciesInput b, string acidKey, List<string> ladder)
    {
        // a 是酸、b 是共轭碱
        if (SpeciesMatchesAcid(a.Substance, acidKey, ladder))
        {
            for (var i = 1; i < ladder.Count; i++)
            {
                if (SpeciesMatchesLadderSpecies(b.Substance, ladder[i]))
                    return (a, b, i);
            }
        }
        // b 是酸、a 是共轭碱
        if (SpeciesMatchesAcid(b.Substance, acidKey, ladder))
        {
            for (var i = 1; i < ladder.Count; i++)
            {
                if (SpeciesMatchesLadderSpecies(a.Substance, ladder[i]))
                    return (b, a, i);
            }
        }
        return (null, null, -1);
    }

    private bool TryComputeHydrolysisPh(string solute, double c, EquilibriumNumericalResult result)
    {
        var kw = ToDouble(_constants.Kw);
        if (kw <= 0)
        {
            result.Warnings.Add("acid-constants.json 缺 Kw，无法计算盐类水解 pH。");
            return false;
        }

        // 强碱弱酸盐：溶盐匹配某条 AcidLadder 的共轭碱形式 → 溶液呈碱性
        foreach (var kv in _constants.AcidLadders)
        {
            var acidKey = kv.Key;
            var ladder = kv.Value;
            for (var i = 1; i < ladder.Count; i++)
            {
                if (!SpeciesMatchesLadderSpecies(solute, ladder[i])) continue;
                var kaKey = ladder.Count > 2 ? $"{acidKey}:Ka{i}" : acidKey;
                if (!_constants.Ka.TryGetValue(kaKey, out var kaSpec)) continue;
                var ka = ToDouble(kaSpec);
                if (ka <= 0) continue;

                var kb = kw / ka;
                if (c / kb < 500)
                {
                    result.Warnings.Add($"盐 {solute} 水解：c/Kb < 500，不满足近似条件，返回欠定。");
                    return false;
                }
                var oh = Math.Sqrt(kb * c);
                var ph = 14 + Math.Log10(oh);
                if (double.IsNaN(ph) || ph <= 0 || ph >= 14) return false;

                result.Ph = Math.Round(ph, 2);
                result.IonConcentrations["OH^-"] = oh;
                result.DerivationSteps.Add($"{solute}（{ladder[i]}）水解：Kb = Kw/Ka({acidKey}) = {kw:E2}/{ka:E2} = {kb:E3}");
                result.DerivationSteps.Add($"[OH⁻] = √(Kb·c) ≈ {oh:E3}，pH = 14 + lg[OH⁻] ≈ {result.Ph}，溶液呈碱性");
                result.EvidenceKeys.Add(kaKey);
                return true;
            }
        }

        // 强酸弱碱盐：溶盐匹配某条 BaseLadder 的共轭酸形式 → 溶液呈酸性
        foreach (var kv in _constants.BaseLadders)
        {
            var baseKey = kv.Key;
            var ladder = kv.Value;
            if (ladder.Count < 2) continue;
            if (!SpeciesMatchesLadderSpecies(solute, ladder[1])) continue;
            if (!_constants.Kb.TryGetValue(baseKey, out var kbSpec)) continue;
            var kb = ToDouble(kbSpec);
            if (kb <= 0) continue;

            var kaConj = kw / kb;
            if (c / kaConj < 500)
            {
                result.Warnings.Add($"盐 {solute} 水解：c/Ka(共轭酸) < 500，不满足近似条件，返回欠定。");
                return false;
            }
            var h = Math.Sqrt(kaConj * c);
            var ph = -Math.Log10(h);
            if (double.IsNaN(ph) || ph <= 0 || ph >= 14) return false;

            result.Ph = Math.Round(ph, 2);
            result.IonConcentrations["H^+"] = h;
            result.DerivationSteps.Add($"{solute}（{ladder[1]}）水解：Ka(共轭酸) = Kw/Kb({baseKey}) = {kw:E2}/{kb:E2} = {kaConj:E3}");
            result.DerivationSteps.Add($"[H⁺] = √(Ka·c) ≈ {h:E3}，pH = -lg[H⁺] ≈ {result.Ph}，溶液呈酸性");
            result.EvidenceKeys.Add(baseKey);
            return true;
        }

        return false;
    }

    // ==================== 离子浓度（可确定项） ====================

    private bool TryComputeIonConcentrations(ChemicalSystemInput input, EquilibriumQuery query, EquilibriumNumericalResult result)
    {
        // 离子浓度通常随 pH 路径顺带产出；若 pH 未算，尝试用题给 [H+]/[OH-]
        var filled = result.IonConcentrations.Count > 0;
        if (query.Given.TryGetValue("H+", out var h)) { result.IonConcentrations["H^+"] = h; filled = true; }
        if (query.Given.TryGetValue("OH-", out var oh)) { result.IonConcentrations["OH^-"] = oh; filled = true; }

        // 题给 pH → 补 [H+]、[OH-]
        if (query.Given.TryGetValue("pH", out var ph) && ph > 0 && ph < 14)
        {
            var h2 = Math.Pow(10, -ph);
            result.IonConcentrations["H^+"] = h2;
            var kw = ToDouble(_constants.Kw);
            if (kw > 0) result.IonConcentrations["OH^-"] = kw / h2;
            filled = true;
        }

        if (!filled)
            result.Warnings.Add("无法确定主要离子浓度（需 pH 或题给离子浓度；完整排序请委托 Core 的 ConcentrationOrdering）。");
        return filled;
    }

    // ==================== #5 Ksp 残留 ====================

    private bool TryComputeKspResidual(ChemicalSystemInput input, EquilibriumQuery query, EquilibriumNumericalResult result)
    {
        if (!query.Given.TryGetValue("pH", out var ph) || ph <= 0 || ph >= 14)
        {
            result.Warnings.Add("Ksp 残留计算需要题给 pH（0 < pH < 14）。");
            return false;
        }

        // 候选氢氧化物：库中 Ksp 键形如 M(OH)n
        var hydroxides = _constants.Ksp
            .Where(kv => HydroxideKey.IsMatch(kv.Key))
            .Select(kv => (Key: kv.Key, Spec: kv.Value, Match: HydroxideKey.Match(kv.Key)))
            .ToList();
        if (hydroxides.Count == 0)
        {
            result.Warnings.Add("acid-constants.json 无氢氧化物 Ksp 数据，无法计算残留离子浓度。");
            return false;
        }

        // 定位目标氢氧化物：题给 Given 键 / 输入物种 / 要求排序的物种中命中唯一者
        var candidates = hydroxides
            .Where(h => query.Given.ContainsKey(h.Key)
                        || input.Species.Any(s => string.Equals(s.Substance, h.Key, StringComparison.Ordinal))
                        || query.OrderSpecies.Any(o => string.Equals(o, h.Key, StringComparison.Ordinal)))
            .ToList();

        if (candidates.Count == 0)
        {
            result.Warnings.Add("未指明目标氢氧化物（需在输入物种、Given 键或 OrderSpecies 中给出形如 M(OH)n 的库中键）。");
            return false;
        }
        if (candidates.Count > 1)
        {
            result.Warnings.Add($"目标氢氧化物不唯一（{string.Join("、", candidates.Select(c => c.Key))}），存在歧义，返回欠定。");
            return false;
        }

        var target = candidates[0];
        var metal = target.Match.Groups[1].Value;
        var n = int.Parse(target.Match.Groups[2].Value, CultureInfo.InvariantCulture);
        var ksp = ToDouble(target.Spec);
        if (ksp <= 0 || n <= 0) return false;

        var ohConc = Math.Pow(10, ph - 14);
        if (ohConc <= 0) return false;
        var residual = ksp / Math.Pow(ohConc, n);
        if (double.IsNaN(residual) || double.IsInfinity(residual) || residual <= 0)
        {
            result.Warnings.Add($"Ksp 残留计算数值异常，返回欠定。");
            return false;
        }

        result.KspResidualConcentration = residual;
        result.KspResidualIon = $"{metal}{n}+";
        result.DerivationSteps.Add($"pH = {ph} → [OH⁻] = 10^(pH−14) = {ohConc:E3} mol/L");
        result.DerivationSteps.Add($"Ksp[{target.Key}] = {FormatConst(target.Spec)}，c({metal}{n}+) = Ksp/[OH⁻]^{n} ≈ {residual:E3} mol/L");
        result.EvidenceKeys.Add(target.Key);
        if (_constants.KspExpressions.TryGetValue(target.Key, out var expr))
            result.EvidenceKeys.Add(expr);
        return true;
    }

    // ==================== #6 沉淀转化 K（委托 Core 判定方向，精确 K 由常数比给出） ====================

    private bool TryComputeConversion(EquilibriumQuery query, EquilibriumNumericalResult result)
    {
        var conv = query.Conversion;
        if (conv is null || string.IsNullOrEmpty(conv.Original) || string.IsNullOrEmpty(conv.Target))
        {
            result.Warnings.Add("沉淀转化查询需在 EquilibriumQuery.Conversion 给出 Original 与 Target。");
            return false;
        }

        // 常数必须存在，否则无法给出 K（Core 也会返回欠定）
        if (!_constants.Ksp.TryGetValue(conv.Original, out var kspOriginal)
            || !_constants.Ksp.TryGetValue(conv.Target, out var kspTarget))
        {
            result.Warnings.Add($"acid-constants.json 缺少 {conv.Original} 或 {conv.Target} 的 Ksp，无法计算转化平衡常数。");
            return false;
        }

        // 委托 Core 判定转化方向与推导步骤（含反应库校验，不臆造转化路径）
        var aq = new AqueousRequest
        {
            Solutes = new List<SoluteSpec>(),
            Conversion = new AqueousConversionSpec
            {
                Original = conv.Original,
                AddedIon = conv.AddedIon,
                Target = conv.Target
            }
        };

        AqueousResult core;
        try { core = AqueousEquilibriumEngine.Infer(aq); }
        catch (Exception ex)
        {
            result.Warnings.Add($"AqueousEquilibriumEngine 转化推断调用失败：{ex.Message}");
            return false;
        }

        // 精确 K 来自常数比（与 Core 内部 ConstantSpec.Ratio 同源），避免从散文中解析
        var k = ConstantSpec.Ratio(kspOriginal, kspTarget);
        var proceeds = k > Rational.One;

        result.ConversionConstantK = k.ToString();
        result.ConversionProceeds = proceeds;
        result.DerivationSteps.Add($"K = Ksp({conv.Original})/Ksp({conv.Target}) = {FormatConst(kspOriginal)}/{FormatConst(kspTarget)} = {k}");
        result.DerivationSteps.Add($"K {(proceeds ? ">" : "<")} 1 → {conv.Original} {(proceeds ? "可" : "难以")}转化为 {conv.Target}");
        result.EvidenceKeys.Add($"Ksp:{conv.Original}");
        result.EvidenceKeys.Add($"Ksp:{conv.Target}");

        if (core.Status == InferenceStatus.Solved)
        {
            result.DerivationSteps.AddRange((core.Steps ?? new()).Select(s => s.Description).Where(d => !string.IsNullOrEmpty(d)));
            result.EvidenceKeys.AddRange((core.Steps ?? new()).Select(s => s.Evidence ?? "").Where(e => !string.IsNullOrEmpty(e)).Distinct());
            return true;
        }

        // Core 未通过反应库校验（无转化路径记录）：K 数值仍由常数确定，但不臆造反应路径
        result.Warnings.Add($"Core 未在反应库中找到 {conv.Original}→{conv.Target} 的转化记录：{core.Message}；仅给出由 Ksp 比值确定的 K，不声明转化可发生。");
        result.ConversionProceeds = null; // 方向判定存疑，置空
        return false;
    }

    // ==================== #7/#8 委托 Core（排序 / 守恒 / 酸碱性） ====================

    private static bool TryDelegateCore(ChemicalSystemInput input, EquilibriumQuery query, EquilibriumNumericalResult result, List<EquilibriumQuantity> requested)
    {
        var solutes = input.Species
            .Where(s => s.Concentration is > 0 && s.Volume is > 0)
            .Select(s => new SoluteSpec
            {
                Species = s.Substance,
                Concentration = s.Concentration!.Value.ToString(CultureInfo.InvariantCulture),
                VolumeLiters = s.Volume!.Value.ToString(CultureInfo.InvariantCulture)
            })
            .ToList();

        if (solutes.Count == 0)
        {
            result.Warnings.Add("粒子排序/守恒/酸碱性推断需要至少一个同时带 Concentration 与 Volume 的溶质（Core 要求）。");
            return false;
        }

        var aq = new AqueousRequest
        {
            Solutes = solutes,
            OrderSpecies = query.OrderSpecies,
            Ph = query.Given.TryGetValue("pH", out var ph) ? ph.ToString(CultureInfo.InvariantCulture) : ""
        };

        AqueousResult core;
        try { core = AqueousEquilibriumEngine.Infer(aq); }
        catch (Exception ex)
        {
            result.Warnings.Add($"AqueousEquilibriumEngine 调用失败：{ex.Message}");
            return false;
        }

        if (core.Status != InferenceStatus.Solved)
        {
            result.Warnings.Add($"Core 平衡推断未求解（{core.Status}）：{core.Message}");
            return false;
        }

        if (requested.Contains(EquilibriumQuantity.ConcentrationOrdering))
            result.ConcentrationOrdering.AddRange(core.Order ?? new());
        if (requested.Contains(EquilibriumQuantity.Conservations))
            result.ConservationEquations.AddRange(core.ConservationEquations ?? new());
        if (requested.Contains(EquilibriumQuantity.AcidityQualitative))
            result.AcidityVerdict = core.QuantitativeConclusion;

        result.DerivationSteps.AddRange((core.Steps ?? new()).Select(s => s.Description).Where(d => !string.IsNullOrEmpty(d)));
        result.EvidenceKeys.AddRange((core.Steps ?? new()).Select(s => s.Evidence ?? "").Where(e => !string.IsNullOrEmpty(e)).Distinct());
        return true;
    }

    // ==================== 常数 / 物种匹配辅助 ====================

    private bool TryGetAcidKa(string substance, out string kaKey, out ConstantSpec kaSpec)
    {
        if (_constants.Ka.TryGetValue(substance, out var exact))
        {
            kaKey = substance; kaSpec = exact; return true;
        }
        var poly = substance + ":Ka1";
        if (_constants.Ka.TryGetValue(poly, out var first))
        {
            kaKey = poly; kaSpec = first; return true;
        }
        kaKey = ""; kaSpec = default!; return false;
    }

    private bool TryGetBaseKb(string substance, out string kbKey, out ConstantSpec kbSpec)
    {
        if (_constants.Kb.TryGetValue(substance, out var exact))
        {
            kbKey = substance; kbSpec = exact; return true;
        }
        kbKey = ""; kbSpec = default!; return false;
    }

    /// <summary>物种是否是阶梯的酸（首项）：与酸主键或 Ka 键匹配</summary>
    private bool SpeciesMatchesAcid(string substance, string acidKey, List<string> ladder)
    {
        if (string.Equals(substance, acidKey, StringComparison.Ordinal)) return true;
        if (_constants.Ka.ContainsKey(substance) && ladder.Contains(substance, StringComparer.Ordinal)) return true;
        return false;
    }

    /// <summary>物种（化学式）是否匹配阶梯中的某个共轭碱/酸形式（如 CH3COONa 匹配 CH3COO^-，NH4Cl 匹配 NH4^+）</summary>
    private static bool SpeciesMatchesLadderSpecies(string solute, string ladderSpecies)
    {
        if (string.Equals(solute, ladderSpecies, StringComparison.Ordinal)) return true;
        var core = StripCharge(ladderSpecies);
        if (string.IsNullOrEmpty(core)) return false;
        // 溶盐包含该离子核心（如 CH3COONa ⊇ CH3COO，Na2CO3 ⊇ CO3，NH4Cl ⊇ NH4）
        if (solute.Contains(core, StringComparison.Ordinal)) return true;
        // NaX / KX 形式
        if (solute.Equals("Na" + core, StringComparison.Ordinal) ||
            solute.Equals("K" + core, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string StripCharge(string species)
    {
        var core = species
            .Replace("^2-", "").Replace("^3-", "").Replace("^-", "")
            .Replace("⁻", "").Replace("²", "").Replace("³", "")
            .Replace("^+", "").Replace("^2+", "").Replace("^3+", "")
            .Replace("⁺", "")
            .TrimEnd('-', '+');
        return core;
    }

    private static double MolesOf(SpeciesInput s) =>
        (s.Concentration ?? 0) * (s.Volume ?? 1);

    private static double ToDouble(ConstantSpec s) =>
        double.Parse(s.Mantissa, NumberStyles.Float, CultureInfo.InvariantCulture) * Math.Pow(10, s.Exponent);

    private static string FormatConst(ConstantSpec s) => $"{s.Mantissa}×10^{s.Exponent}";
}
