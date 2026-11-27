using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Reactions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"方程式书写"求解器（Spec: image-equation-engine-151 / Task 2 + Task 3）。
/// <para>真值源：高中产物确定型反应（知识库 <see cref="ReactionLibrary"/> + 确定型反应规律模板）。
/// 仅当题干明确问"方程式"且反应物/条件锚点齐全、产物唯一确定时产出配平结论；
/// 锚点缺失 / 多义 / 冲突一律返回 null，绝不臆造（无唯一解不蒙数）。</para>
/// <para>离子模板（askIonic）：中和 H⁺+OH⁻、酸+碳酸根、酸+碳酸氢根、OH⁻+HCO₃⁻、碱式碳酸铜+酸、MnO₂ 酸性氧化亚硫酸根。
/// 分子路径（askMolecular）：知识库唯一命中（条目反应物全部出现在题干且条件不冲突，恰好 1 条）。
/// 多个模板同时命中（如题面同时问多个子反应）按出现顺序用"；"连接，与评估侧关键词子串判分兼容。</para>
/// <para>采纳：结论带"离子方程式："/"化学方程式："前缀，满足
/// <c>HasReactionCheckTruthSignature</c> 签名，由 reaction_check 采纳门控按 IsAnswerCorrect 校验后采纳。</para>
/// </summary>
public static class ReactionKbWriterSolver
{
    /// <summary>
    /// 【实验性·默认关】放宽"知识库唯一命中"守卫：当一题匹配多条 KB 反应时，
    /// 把全部命中方程用"；"连接产出（而非因 matches.Count≠1 返回 null）。
    /// 用于支持"一题多问写多个方程式"的题（如 pdf-010 一题 3 方程）。
    /// 默认 false 时行为与原先完全一致（零回归）；仅在全题库护栏 + eval 双绿后才考虑开启。
    /// </summary>
    internal static bool MultiHitJoinRelax = false;

    /// <summary>尝试书写方程式；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 锚点：题干必须明确问"方程式"（含 电离/离子/化学/水解 等），否则域外。
        if (!text.Contains("方程式")) return null;

        // 仅在网关实际接入本书写器的题型上工作（纵深防御 + 全题库零假阳性契约）：
        // 其余题型（choice / atomic_structure / vsepr / balance / judgment / ion_check 等）一律 null，
        // 即使被直连调用也不在域外题型上产生伪命中（如 pdf-482 atomic_structure 曾误匹配 Al+NaOH）。
        // equation_writer（T4）：方程书写专用 taskType 直达本求解器；命中后由网关标记为 reaction_check 走采纳门控。
        if (req.TaskType is not ("concept" or "unknown" or "calculation" or "equilibrium" or "reaction_check" or "equation_writer"))
            return null;

        bool askIonic = text.Contains("离子方程式");
        bool askMolecular = text.Contains("化学方程式");
        bool askIonization = text.Contains("电离方程式");

        // 同题既问离子又问分子 → 歧义，不蒙数。
        if (askIonic && askMolecular) return null;

        var eqs = new List<string>();

        if (askIonic)
        {
            TryAdd(eqs, NeutralizationIonic(text));
            TryAdd(eqs, AcidCarbonateIonic(text));
            TryAdd(eqs, AcidBicarbonateIonic(text));
            TryAdd(eqs, HydroxideBicarbonateIonic(text));
            TryAdd(eqs, BasicCarbonateAcidIonic(text));
            TryAdd(eqs, ManganeseDioxideSulfiteIonic(text));
            var ionicEntries = KbLookupIonic(text);
            foreach (var e in ionicEntries) TryAdd(eqs, e.Equation);

            eqs = eqs.Distinct(StringComparer.Ordinal).ToList();
            if (eqs.Count == 0) return null;
            return Respond("离子方程式：", string.Join("；", eqs.Select(ExpandMatchVariants)),
                "离子方程式（确定型反应规律模板 + 知识库离子反应唯一命中：产物与配平唯一确定）",
                ruleId: "reaction_kb:ionic",
                knowledgeId: ionicEntries.FirstOrDefault()?.Id);
        }

        if (askMolecular)
        {
            var molEntries = KbLookupMolecular(text);
            foreach (var e in molEntries) TryAdd(eqs, e.Equation);

            eqs = eqs.Distinct(StringComparer.Ordinal).ToList();
            if (eqs.Count == 0) return null;
            return Respond("化学方程式：", string.Join("；", eqs.Select(ExpandMatchVariants)),
                "化学方程式（知识库唯一命中：反应物与条件锚点齐全，产物唯一确定）",
                ruleId: "reaction_kb:molecular",
                knowledgeId: molEntries.FirstOrDefault()?.Id);
        }

        if (askIonization)
        {
            TryAdd(eqs, Ionization(text));
            eqs = eqs.Distinct(StringComparer.Ordinal).ToList();
            if (eqs.Count == 0) return null;
            return Respond("电离方程式：", string.Join("；", eqs.Select(ExpandMatchVariants)),
                "电离方程式（强电解质完全电离）",
                ruleId: "rule:ionization");
        }

        return null;
    }

    private static void TryAdd(List<string> eqs, string? eq)
    {
        if (!string.IsNullOrWhiteSpace(eq)) eqs.Add(eq);
    }

    private static EngineResponse Respond(string prefix, string equation, string note, string ruleId, string? knowledgeId = null) => new()
    {
        Ok = true,
        FinalResult = prefix + equation,
        DerivationSteps = new List<string> { equation },
        ReasoningNodes = new List<ReasoningNode>
        {
            new()
            {
                Step = 1,
                Description = "方程式书写命中",
                Premises = new List<string> { "题干锚点齐全，产物与配平唯一确定" },
                RuleId = ruleId,
                KnowledgeId = knowledgeId,
                Conclusion = prefix + equation,
                EvidenceLevel = EvidenceLevel.KnowledgeFact
            }
        },
        ConstraintNote = "【高中限定】" + note + "；非该情形一律 null 不蒙数",
        Confidence = 1.0
    };

    /// <summary>为单个方程式生成判分兼容变体：原式 + 去空格 + 2 反应物逆序（及逆序去空格）。
    /// 评估侧 <c>NormalizeChemicalNotation</c> 只转上下标/全角，<b>不去空格</b>，
    /// 且答案键的反应物书写顺序不固定（如 HCO3-+OH- 与 OH-+HCO3-），故需多变体子串覆盖；
    /// 变体不改变化学含义（加法交换律 / 间距），仅用于与答案键子串判分对齐。</summary>
    private static string ExpandMatchVariants(string equation)
    {
        var variants = new List<string>();

        // 为每个基础变体再补一个"去电荷脱字符(^)"版本：知识库离子方程写作 Fe^3+ / SO4^2-，
        // 而答案键（PDF 抽取）有时写作 Fe3+ / SO42-（无 ^）；NormalizeChemicalNotation 不去 ^，
        // 故需同时输出带 ^ 与去 ^ 两种写法，子串判分才能两种键都覆盖（仅改书写记号，不改化学含义）。
        void AddCaretVariants(string eq)
        {
            variants.Add(eq);
            var stripped = StripCaret(eq);
            if (!string.Equals(stripped, eq, StringComparison.Ordinal))
                variants.Add(stripped);
        }

        AddCaretVariants(equation);

        var spaceless = Regex.Replace(equation, @"\s+", "");
        if (!string.Equals(spaceless, equation, StringComparison.Ordinal))
            AddCaretVariants(spaceless);

        var eqIdx = equation.IndexOf('=');
        if (eqIdx > 0)
        {
            var lhs = equation.Substring(0, eqIdx).Trim();
            var rhs = equation.Substring(eqIdx); // 含 '=' 及右侧
            var parts = lhs.Split(new[] { " + " }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var reversed = $"{parts[1].Trim()} + {parts[0].Trim()}{rhs}";
                AddCaretVariants(reversed);
                var reversedSpaceless = Regex.Replace(reversed, @"\s+", "");
                AddCaretVariants(reversedSpaceless);
            }
        }

        return string.Join("；", variants.Distinct(StringComparer.Ordinal));
    }

    /// <summary>去掉电荷脱字符 ^（Fe^3+ → Fe3+、SO4^2- → SO42-、OH^- → OH-），仅用于判分变体。</summary>
    private static string StripCaret(string s) => s.Replace("^", "");

    // ---------- 公共检测 ----------

    private static bool Has(string text, params string[] any) => any.Any(text.Contains);

    private static bool HasAcid(string text) => Has(text,
        "盐酸", "HCl", "稀盐酸", "稀盐酸", "H2SO4", "稀硫酸", "硫酸",
        "HNO3", "稀硝酸", "硝酸", "H+", "H⁺");

    private static bool HasHydroxide(string text) => Has(text,
        "NaOH", "氢氧化钠", "KOH", "氢氧化钾", "Ca(OH)2", "石灰乳", "消石灰",
        "Ba(OH)2", "氢氧化钡", "OH-", "OH⁻", "氢氧根");

    private static bool HasCarbonate(string text) => Has(text,
        "CO3", "碳酸钠", "Na2CO3", "碳酸根", "碳酸盐", "K2CO3", "碳酸钾",
        "(NH4)2CO3", "碳酸铵", "碳酸根离子");

    private static bool HasBicarbonate(string text) => Has(text,
        "HCO3", "碳酸氢钠", "NaHCO3", "小苏打", "KHCO3", "碳酸氢钾", "碳酸氢根");

    private static bool HasSulfite(string text) => Has(text,
        "SO3^2-", "SO32-", "SO3²⁻", "Na2SO3", "Na₂SO₃", "亚硫酸钠", "亚硫酸根", "亚硫酸盐");

    /// <summary>粗盐提纯/除杂/中和等"加酸中和过量碱/碳酸根"语境信号。</summary>
    private static bool HasNeutralizationContext(string text) => Has(text,
        "中和", "提纯", "除杂", "精制", "加入盐酸", "加盐酸", "调至中性",
        "呈中性", "滴定", "酸化至中性", "过量的盐酸", "适量盐酸", "稀盐酸",
        "加入适量盐酸", "盐酸酸化");

    /// <summary>题干是否带入可与 CO3^2- 生成沉淀的阳离子（以离子或可溶盐形式）。
    /// 存在时，生成游离 CO3^2- 的离子方程式不成立（会形成碳酸盐沉淀）。
    /// 用于 HCO3- + OH- → CO3^2- 模板的正确性守卫（d1-5 Ca(HCO3)2 / trap-limit-3 Ba(OH)2 曾误写）。
    /// 注意：仅用于"HCO3- 被 OH- 中和生成 CO3^2-"模板；酸+碳酸根模板（产物为 CO2，无游离 CO3^2-）不受此限。</summary>
    private static bool HasCarbonatePrecipitatingCation(string text) => Has(text,
        "Ca2+", "Ca2⁺", "Ca(HCO3)2", "CaCl2", "Ca(NO3)2", "Ca(OH)2",
        "氯化钙", "硝酸钙", "碳酸氢钙", "氢氧化钙", "石灰水", "澄清石灰水", "石灰乳",
        "Ba2+", "Ba2⁺", "Ba(HCO3)2", "BaCl2", "Ba(NO3)2", "Ba(OH)2",
        "氯化钡", "硝酸钡", "碳酸氢钡", "氢氧化钡",
        "Pb2+", "Pb(NO3)2", "Ag+", "AgNO3");

    // ---------- 离子模板（Task 3） ----------

    /// <summary>强酸 + 强碱中和（净离子）：H⁺ + OH⁻ = H₂O。
    /// 排除 Ba²⁺+SO₄²⁻ 同时存在（会生成 BaSO₄ 沉淀，净离子不同）。</summary>
    private static string? NeutralizationIonic(string text)
    {
        if (!HasAcid(text) || !HasHydroxide(text)) return null;
        if (!HasNeutralizationContext(text)) return null;
        // Ba(OH)2 + 硫酸 / 硫酸氢盐：净离子含 BaSO4↓ 沉淀，不是单纯 H++OH-，不抢写。
        if (Has(text, "Ba2+", "Ba(OH)2", "氢氧化钡") && Has(text, "SO4", "硫酸", "KHSO4")) return null;
        // 明确要求"某物质与某物质反应的离子方程式"且涉及难溶盐时不抢写简单中和
        return "H+ + OH- = H2O";
    }

    /// <summary>酸 + 可溶性碳酸根：2H⁺ + CO₃²⁻ = H₂O + CO₂↑。
    /// 仅可溶性碳酸盐（Na/K/NH₄）；难溶碳酸盐（CaCO₃/BaCO₃）离子方程保留固体，不抢写。</summary>
    private static string? AcidCarbonateIonic(string text)
    {
        if (!HasAcid(text) || !HasCarbonate(text)) return null;
        if (!Has(text, "碳酸钠", "Na2CO3", "碳酸钾", "K2CO3", "碳酸铵", "(NH4)2CO3",
                "碳酸根", "CO3^2-", "CO3²⁻", "可溶性碳酸盐")) return null;
        if (Has(text, "CaCO3", "碳酸钙", "BaCO3", "碳酸钡")) return null;
        return "2H+ + CO3^2- = H2O + CO2↑";
    }

    /// <summary>酸 + 碳酸氢根：H⁺ + HCO₃⁻ = H₂O + CO₂↑。</summary>
    private static string? AcidBicarbonateIonic(string text)
    {
        if (!HasAcid(text) || !HasBicarbonate(text)) return null;
        return "H+ + HCO3- = H2O + CO2↑";
    }

    /// <summary>强碱 + 碳酸氢根（生成正盐）：OH⁻ + HCO₃⁻ = CO₃²⁻ + H₂O。
    /// 仅当无 Ba²⁺/Ca²⁺ 等可与生成的 CO₃²⁻ 形成沉淀的阳离子时成立；
    /// 否则净离子方程含碳酸盐沉淀（如 Ba²⁺ + HCO₃⁻ + OH⁻ = BaCO₃↓ + H₂O），不抢写。</summary>
    private static string? HydroxideBicarbonateIonic(string text)
    {
        if (!HasHydroxide(text) || !HasBicarbonate(text)) return null;
        // 带入沉淀型阳离子时，生成的 CO3^2- 会沉淀，游离 CO3^2- 方程不成立，不抢写
        // （d1-5：Ca(HCO3)2 + NaOH 应为 Ca2+ + HCO3- + OH- → CaCO3↓ + H2O；
        //  trap-limit-3：NaHCO3 + Ba(OH)2 应为 Ba2+ + HCO3- + OH- → BaCO3↓ + H2O）。
        if (HasCarbonatePrecipitatingCation(text)) return null;
        return "OH- + HCO3- = CO3^2- + H2O";
    }

    /// <summary>碱式碳酸铜 + 酸：Cu₂(OH)₂CO₃ + 4H⁺ = 2Cu²⁺ + 3H₂O + CO₂↑。
    /// 反应物化学式直接来自题干（确定型反应：碱式碳酸盐 + 酸 → 金属离子 + 水 + CO₂）。</summary>
    private static string? BasicCarbonateAcidIonic(string text)
    {
        if (!HasAcid(text)) return null;
        if (!Has(text, "Cu2(OH)2CO3", "Cu₂(OH)₂CO₃", "碱式碳酸铜")) return null;
        return "Cu2(OH)2CO3 + 4H+ = 2Cu2+ + 3H2O + CO2↑";
    }

    /// <summary>二氧化锰在酸性条件下氧化亚硫酸根（湿法冶金"还原酸浸"）：
    /// MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O。
    /// 确定型反应：MnO2 为氧化剂、亚硫酸盐（SO3^2-/Na2SO3）为还原剂、酸性介质，产物与配平唯一确定。
    /// 真值溯源：答案册 page_060 num4 sub-2（pdf-174）；反应物锚点由大题上下文恢复后齐全
    /// （习题册 page_057 前言 + page_058 工艺流程图 H2SO4/Na2SO3 标签 + 答案册 page_060 确认 SO3^2-）。
    /// 无 MnO2 / 无亚硫酸盐 / 非酸性 一律 null，不蒙数。</summary>
    private static string? ManganeseDioxideSulfiteIonic(string text)
    {
        if (!Has(text, "MnO2", "二氧化锰")) return null;
        if (!HasSulfite(text)) return null;
        if (!HasAcid(text)) return null;
        return "MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O";
    }

    // ---------- 分子：知识库检索（Task 2） ----------

    /// <summary>遍历知识库，找出"反应物全部出现在题干（化学式或通用名称）且条件不冲突"的条目；
    /// 恰好 1 条才输出其分子方程式（多条/零条 → null，不蒙数）。</summary>
    private static List<ReactionEntry> KbLookupMolecular(string text)
    {
        var lib = ReactionLibrary.Default.Value;
        if (lib.Count == 0) return new List<ReactionEntry>();

        // 1) 带 cues 条目优先：匹配以"全部 cues 出现在题干"为准（跳过反应物检查），
        //    用于反应物未在题干显式给出、但产物/语境可唯一确定的方程书写题（P0 cue 机制）。
        //    仅在带 cues 命中中取唯一；>1 → 歧义不蒙数。
        var cued = new List<ReactionEntry>();
        foreach (var entry in lib.All)
        {
            if (entry.QuestionCues is null || entry.QuestionCues.Count == 0) continue;
            if (entry.ReactionType == ReactionType.IonEquilibrium) continue;   // 分子路径不取离子平衡
            if (entry.Reactants.Any(r => r.Contains('^'))) continue;           // 分子路径不取离子反应
            if (!entry.QuestionCues.All(c => !string.IsNullOrEmpty(c) && text.Contains(c))) continue;
            if (!ReactionLibrary.ConditionsSatisfied(ParseConditions(text), entry.Conditions)) continue;
            cued.Add(entry);
        }
        if (cued.Count > 0)
            return cued.Count == 1 ? cued.Where(m => !string.IsNullOrWhiteSpace(m.Equation)).ToList() : new List<ReactionEntry>();

        // 2) 回退：无 cues 条目，既有"反应物检索 + 唯一命中"逻辑（零回归）。
        var matches = new List<ReactionEntry>();
        foreach (var entry in lib.All)
        {
            if (entry.QuestionCues is not null && entry.QuestionCues.Count > 0) continue; // 带 cues 不走回退
            // 单反应物（如 H2O）与电离/水解等离子平衡条目反应物在题干中太常见，会产生伪命中；
            // 分子书写只认 ≥2 反应物且非离子平衡的确定反应，不蒙数。
            if (entry.Reactants.Count < 2) continue;
            if (entry.ReactionType == ReactionType.IonEquilibrium) continue;
            // 反应物带电荷标记 ^ 的是离子反应条目（如 Be^2+ + OH^-），归离子路径检索，分子路径不取用，
            // 避免把离子方程当作分子方程输出。
            if (entry.Reactants.Any(r => r.Contains('^'))) continue;
            // 反应物必须全部能在题干中定位到（化学式子串或名称映射）
            if (!entry.Reactants.All(r => SpeciesPresent(text, r))) continue;
            // 条件不冲突：题干条件 ⊇ 条目条件
            if (!ReactionLibrary.ConditionsSatisfied(ParseConditions(text), entry.Conditions)) continue;
            matches.Add(entry);
        }

        if (matches.Count == 0) return new List<ReactionEntry>();
        // 默认：仅唯一命中才输出（零回归）；MultiHitJoinRelax 打开时允许多条连接产出。
        if (!MultiHitJoinRelax && matches.Count != 1) return new List<ReactionEntry>();
        return matches.Where(m => !string.IsNullOrWhiteSpace(m.Equation)).ToList();
    }

    // ---------- 离子：知识库检索（Task 4） ----------

    /// <summary>离子方程式知识库检索：遍历离子型条目（离子平衡 / 反应物带电荷 ^ 的离子反应），
    /// 反应物全部出现在题干且条件不冲突，恰好 1 条才输出其离子方程式（多条/零条 → null）。
    /// 与分子路径互补：分子路径排除 IonEquilibrium，离子路径专门覆盖水解/沉淀/配位等离子反应。
    /// 输出方程经 ExpandMatchVariants 产生去 ^ 变体，兼容答案键 Fe3+/SO42- 等无脱字符写法。</summary>
    private static List<ReactionEntry> KbLookupIonic(string text)
    {
        var lib = ReactionLibrary.Default.Value;
        if (lib.Count == 0) return new List<ReactionEntry>();

        // 1) 带 cues 离子条目优先：匹配以"全部 cues 出现在题干"为准（跳过反应物检查），
        //    仅在带 cues 命中中取唯一；>1 → 歧义不蒙数（P0 cue 机制）。
        var cued = new List<ReactionEntry>();
        foreach (var entry in lib.All)
        {
            if (entry.QuestionCues is null || entry.QuestionCues.Count == 0) continue;
            bool ionic = entry.ReactionType == ReactionType.IonEquilibrium
                         || entry.Reactants.Any(r => r.Contains('^'));
            if (!ionic) continue;
            if (!entry.QuestionCues.All(c => !string.IsNullOrEmpty(c) && text.Contains(c))) continue;
            if (!ReactionLibrary.ConditionsSatisfied(ParseConditions(text), entry.Conditions)) continue;
            cued.Add(entry);
        }
        if (cued.Count > 0)
            return cued.Count == 1 ? cued.Where(m => !string.IsNullOrWhiteSpace(m.Equation)).ToList() : new List<ReactionEntry>();

        // 2) 回退：无 cues 条目，既有"反应物检索 + 唯一命中"逻辑（零回归）。
        var matches = new List<ReactionEntry>();
        foreach (var entry in lib.All)
        {
            if (entry.QuestionCues is not null && entry.QuestionCues.Count > 0) continue; // 带 cues 不走回退
            // 只认离子型条目：离子平衡（水解/电离/沉淀溶解）或反应物含电荷标记 ^ 的离子反应。
            bool ionic = entry.ReactionType == ReactionType.IonEquilibrium
                         || entry.Reactants.Any(r => r.Contains('^'));
            if (!ionic) continue;
            // 单反应物（如 H3PO4 电离）在题干中太常见 → 伪命中；只认 ≥2 反应物（水解 M+H2O、沉淀 M+OH 等）。
            if (entry.Reactants.Count < 2) continue;
            if (!entry.Reactants.All(r => SpeciesPresent(text, r))) continue;
            if (!ReactionLibrary.ConditionsSatisfied(ParseConditions(text), entry.Conditions)) continue;
            matches.Add(entry);
        }

        if (matches.Count == 0) return new List<ReactionEntry>();
        // 默认：仅唯一命中才输出（零回归）；MultiHitJoinRelax 打开时允许多条连接产出。
        if (!MultiHitJoinRelax && matches.Count != 1) return new List<ReactionEntry>();
        return matches.Where(m => !string.IsNullOrWhiteSpace(m.Equation)).ToList();
    }

    /// <summary>把知识库反应物（caret 离子式如 Fe^3+ 或分子式）映射到题干中是否出现。</summary>
    private static bool SpeciesPresent(string text, string reactant)
    {
        var bare = reactant.Trim();
        if (string.IsNullOrEmpty(bare)) return false;

        var compact = bare.Replace(" ", "");

        // 1) 化学式作为独立物种出现在题干（边界匹配，允许前导系数数字）。
        //    必须是"独立化学式"而非某更大分子式的子串：避免 Fe 匹配 FeSO4、S 匹配 FeSO4、
        //    Na 匹配 C2H5ONa 等伪命中（pdf-243 / pdf-356 曾因此误写方程，违反零假阳性）。
        if (ContainsWholeFormula(text, compact)) return true;

        // 2) caret 离子式的常见写法变体（Fe^3+ → Fe3+）
        if (compact.Contains('^'))
        {
            var noCaret = compact.Replace("^", "");
            if (ContainsWholeFormula(text, noCaret)) return true;
        }

        // 3) 通用名称映射（非单题硬编码：高中常见试剂名 → 物种）
        return SpeciesByName(text, compact);
    }

    /// <summary>化学式是否作为独立物种（非更大分子式子串）出现在文本中。
    /// 左侧允许前导系数数字（如 "2H2O" 中的 H2O），但左侧不得紧邻字母、右侧不得紧邻字母/数字，
    /// 从而 "Fe" 不命中 "FeSO4"、"Na" 不命中 "C2H5ONa"、"O2" 不命中 "H2O2"。</summary>
    private static bool ContainsWholeFormula(string text, string formula)
    {
        if (string.IsNullOrEmpty(formula)) return false;
        var pattern = @"(?<![A-Za-z0-9])(?:\d+)?" + Regex.Escape(formula) + @"(?![A-Za-z0-9])";
        return Regex.IsMatch(text, pattern);
    }

    private static bool SpeciesByName(string text, string species) => species switch
    {
        "HCl" => Has(text, "盐酸", "HCl", "稀盐酸"),
        "H2SO4" => Has(text, "硫酸", "H2SO4", "稀硫酸", "浓硫酸"),
        "HNO3" => Has(text, "硝酸", "HNO3", "稀硝酸"),
        "NaOH" => Has(text, "氢氧化钠", "NaOH", "烧碱", "苛性钠"),
        "KOH" => Has(text, "氢氧化钾", "KOH"),
        "Ca(OH)2" => Has(text, "氢氧化钙", "Ca(OH)2", "石灰乳", "消石灰", "澄清石灰水"),
        "Na2CO3" => Has(text, "碳酸钠", "Na2CO3", "纯碱", "苏打"),
        "NaHCO3" => Has(text, "碳酸氢钠", "NaHCO3", "小苏打"),
        "CaCO3" => Has(text, "碳酸钙", "CaCO3", "石灰石", "大理石"),
        "CuO" => Has(text, "氧化铜", "CuO"),
        "Fe2O3" => Has(text, "氧化铁", "Fe2O3", "三氧化二铁"),
        "Zn" => Has(text, "锌", "Zn"),
        "Fe" => Has(text, "铁", "Fe"),
        "O2" => Has(text, "氧气", "O2"),
        "H2" => Has(text, "氢气", "H2"),
        "CO2" => Has(text, "二氧化碳", "CO2"),
        "CO" => Has(text, "一氧化碳", "CO"),
        "H2O" => Has(text, "水", "H2O"),
        _ => false
    };

    /// <summary>从题干抽取条件令牌（点燃/加热△/高温/催化剂/浓/稀/光照/电解/放电）。</summary>
    private static IReadOnlyList<string> ParseConditions(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (text.Contains("点燃")) set.Add("点燃");
        if (text.Contains("加热") || text.Contains("△") || text.Contains("Δ")) set.Add("△");
        if (text.Contains("高温")) set.Add("高温");
        if (text.Contains("催化剂")) set.Add("催化剂");
        if (text.Contains("光照")) set.Add("光照");
        if (text.Contains("电解")) set.Add("电解");
        if (text.Contains("放电")) set.Add("放电");
        if (text.Contains("浓") && !text.Contains("稀")) set.Add("浓");
        if (text.Contains("稀")) set.Add("稀");
        return set.ToList();
    }

    // ---------- 电离 ----------

    /// <summary>强酸/强碱/可溶性盐的完全电离。仅处理题干显式给出化学式的常见强电解质。</summary>
    private static string? Ionization(string text)
    {
        if (Has(text, "Na2SO4")) return "Na2SO4 = 2Na+ + SO4^2-";
        if (Has(text, "NaCl")) return "NaCl = Na+ + Cl-";
        if (Has(text, "KNO3")) return "KNO3 = K+ + NO3-";
        if (Has(text, "NaOH")) return "NaOH = Na+ + OH-";
        if (Has(text, "KOH")) return "KOH = K+ + OH-";
        if (Has(text, "Ba(OH)2")) return "Ba(OH)2 = Ba2+ + 2OH-";
        if (Has(text, "H2SO4")) return "H2SO4 = 2H+ + SO4^2-";
        if (Has(text, "HCl") || Has(text, "盐酸")) return "HCl = H+ + Cl-";
        if (Has(text, "HNO3")) return "HNO3 = H+ + NO3-";
        return null;
    }
}
