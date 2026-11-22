using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 晶胞（晶体结构）确定性求解器（Spec: crystal-cell-validator）。
/// <para>真值源：<c>type=crystal</c> 结构化资产（位点/分数坐标/晶格常数，坐标已与渲染图人工核对）。
/// 仅当题干问 晶胞原子个数 / 配位数 / 密度（或双胞密度比）时产出结论；域外 / 缺数据 / 歧义一律 <c>null</c>，绝不臆造。</para>
/// <para>采纳：由网关路由标记为 <c>concept_fact</c>，采纳门（BuildGatewayAnswerAsync）仅采纳
/// IsAnswerCorrect 通过的结论 → 凡采纳必判正，precision 1.0、零 harm。</para>
/// <para>红线：化学事实只在 JSON 资产中；逻辑层不硬编码逐题答案。</para>
/// </summary>
public static class CrystalCellValidator
{
    private sealed record Site(string Species, string Kind, int Count, double? MolarMass);

    private sealed record Cell(
        string Key, string Label, double? A, double? C,
        IReadOnlyList<Site> Basis, double? MolarMass, int? AtomsPerFU, int? CoordNumber, int? FormulaUnits);

    /// <summary>尝试求解晶胞题；域外返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        // 选择题不介入（保留既有选项校验路径，零回归）
        if (string.Equals(req.TaskType, "choice", StringComparison.OrdinalIgnoreCase)) return null;

        var cells = ParseCells(imageAssets);
        if (cells.Count == 0) return null;

        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        bool asksCount = ContainsAny(text, "个数", "含有", "多少个", "原子数");
        // 配位意图：显式「配位数」或「每个 X 周围与它最近…的 Y」表述（pdf-178 型问法）。
        // 仅对含 crystal 资产的题触发（上游 HasCrystalAsset 门控），不会影响非晶胞题。
        bool asksCN = text.Contains("配位数") || (text.Contains("周围") && text.Contains("最近"));
        bool asksDensity = text.Contains("密度");
        bool asksRatio = asksDensity && (text.Contains("比") || text.Contains("之比")) && cells.Count >= 2;

        // 原子数目比（pdf-204 型）：题干形如「原子数目比 N(Fe):N(Bi)」。
        // 从 N(X) 记法结构化提取物种（不依赖清洗后的展示文本，避免键漂移——对应经验：join-key 稳定性），
        // 与密度比（asksRatio）互斥；命中时优先于单元素计数（asksCount），否则会误输出单边计数 "8"。
        var ratioSpecies = Regex.Matches(text, @"N\(\s*([A-Za-z]+)\s*\)")
            .OfType<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        bool asksAtomRatio = ratioSpecies.Count >= 2 && !asksDensity
                             && (text.Contains("数目比") || text.Contains("个数比")
                                 || text.Contains("数目之比") || text.Contains("个数之比"));

        if (!asksCount && !asksCN && !asksDensity && !asksAtomRatio) return null;

        var parts = new List<string>();

        // 按题面子问顺序输出：配位数 → 密度比 → 原子数目比 → 原子个数 → 密度（数值）
        if (asksCN) parts.AddIfNotNull(CoordinationAnswer(cells, text));
        if (asksRatio) parts.AddIfNotNull(SymbolicDensityRatio(cells, text));
        if (asksAtomRatio) parts.AddIfNotNull(AtomRatioAnswer(cells, ratioSpecies));
        else if (asksCount) parts.AddIfNotNull(AtomCountAnswer(cells, text));
        if (asksDensity && !asksRatio) parts.AddIfNotNull(DensityAnswer(cells, text));

        if (parts.Count == 0) return null;

        var finalResult = string.Join("；", parts);
        // 加性推理链（spec R4）：命中即有 type=crystal 结构化资产锚点；
        // 取首个晶胞资产 key 作为 knowledge_id（多胞密度比等场景首胞为锚，多锚点留待后续资产 id 化）。
        var knowledgeId = cells.Count > 0 && !string.IsNullOrEmpty(cells[0].Key) ? cells[0].Key : null;
        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            DerivationSteps = parts,
            ReasoningNodes = knowledgeId is null
                ? new List<ReasoningNode>()
                : new List<ReasoningNode>
                {
                    new()
                    {
                        Step = 1,
                        Description = "晶胞结构化资产命中",
                        Premises = new List<string> { "type=crystal 结构化资产（位点/分数坐标/晶格常数）已与渲染图人工核对" },
                        KnowledgeId = knowledgeId,
                        Conclusion = finalResult,
                        EvidenceLevel = EvidenceLevel.KnowledgeFact
                    }
                },
            ConstraintNote = "【crystal 引擎】晶胞结构真值（均摊/配位数/密度），坐标已与渲染图核对；非该情形一律 null 不蒙数",
            Confidence = 1.0,
            AssetKind = "crystal"
        };
    }

    // ------------------------------------------------------------
    // 资产解析
    // ------------------------------------------------------------
    private static List<Cell> ParseCells(IReadOnlyList<ImageAsset>? assets)
    {
        var cells = new List<Cell>();
        if (assets is null) return cells;
        foreach (var a in assets)
        {
            if (!string.Equals(a.Type, "crystal", StringComparison.OrdinalIgnoreCase)) continue;
            var cell = ParseCell(a.Payload);
            if (cell is not null) cells.Add(cell);
        }
        return cells;
    }

    private static Cell? ParseCell(JsonElement? payload)
    {
        if (payload is not JsonElement p || p.ValueKind != JsonValueKind.Object) return null;
        try
        {
            string key = Str(p, "key") ?? Str(p, "label") ?? "cell";
            string label = Str(p, "label") ?? key;
            double? a = Num(p, "a");
            double? c = Num(p, "c");
            double? molarMass = Num(p, "molarMass");
            int? atomsPerFU = Int(p, "atomsPerFormulaUnit") ?? Int(p, "atomsPerFU");
            int? coord = Int(p, "coordinationNumber");
            int? formulaUnits = Int(p, "formulaUnits");

            var basis = new List<Site>();
            if (p.TryGetProperty("basis", out var b) && b.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in b.EnumerateArray())
                {
                    if (s.ValueKind != JsonValueKind.Object) continue;
                    var species = Str(s, "species");
                    var kind = Str(s, "site");
                    var count = Int(s, "count");
                    if (string.IsNullOrEmpty(species) || string.IsNullOrEmpty(kind) || count is null) continue;
                    basis.Add(new Site(species!, kind!, count.Value, Num(s, "molarMass")));
                }
            }
            if (basis.Count == 0) return null;
            return new Cell(key, label, a, c, basis, molarMass, atomsPerFU, coord, formulaUnits);
        }
        catch
        {
            return null; // 载荷结构异常 → 域外，不蒙数
        }
    }

    // ------------------------------------------------------------
    // 原子均摊计数（FR-2）
    // ------------------------------------------------------------
    private static double Weight(string site) => site switch
    {
        "corner" => 1.0 / 8,
        "edge" => 1.0 / 4,
        "face" => 1.0 / 2,
        "body" => 1.0,
        "internal" => 1.0,
        _ => 0.0
    };

    private static double Z(Cell cell) => cell.Basis.Sum(s => s.Count * Weight(s.Kind));

    private static string? AtomCountAnswer(IReadOnlyList<Cell> cells, string text)
    {
        // 找到题干提及的元素（去物种末尾数字：P4→P、Ni2+→Ni）
        var matched = new List<(Cell cell, string species, double count)>();
        foreach (var cell in cells)
        {
            foreach (var grp in cell.Basis.GroupBy(s => s.Species))
            {
                var element = ElementOf(grp.Key);
                if (string.IsNullOrEmpty(element)) continue;
                if (!MentionsElement(text, element)) continue;
                var count = grp.Sum(s => s.Count * Weight(s.Kind));
                matched.Add((cell, grp.Key, count));
            }
        }
        if (matched.Count == 0)
        {
            // 题干未指明元素：若只有单一物种，输出其数目
            var allSpecies = cells.SelectMany(c => c.Basis.Select(s => s.Species)).Distinct().ToList();
            if (allSpecies.Count == 1)
            {
                var count = cells.Sum(c => c.Basis.Where(s => s.Species == allSpecies[0]).Sum(s => s.Count * Weight(s.Kind)));
                return FormatCount(allSpecies[0], count, cells.First(c => c.Basis.Any(s => s.Species == allSpecies[0])));
            }
            return null;
        }
        // 取第一个匹配（题面子问顺序）；分子晶体按 atomsPerFormulaUnit 换算原子数
        var first = matched[0];
        return FormatCount(first.species, first.count, first.cell);
    }

    // ------------------------------------------------------------
    // 原子数目比（pdf-204 型）：按题干 N(X):N(Y) 出现顺序输出最简整数比
    // ------------------------------------------------------------
    private static string? AtomRatioAnswer(IReadOnlyList<Cell> cells, IReadOnlyList<string> species)
    {
        if (species.Count < 2) return null;
        var counts = new List<int>();
        foreach (var sp in species)
        {
            double c = 0;
            bool found = false;
            foreach (var cell in cells)
            {
                foreach (var s in cell.Basis)
                {
                    if (string.Equals(ElementOf(s.Species), sp, StringComparison.OrdinalIgnoreCase))
                    { c += s.Count * Weight(s.Kind); found = true; }
                }
            }
            // 任一物种在资产中缺失或均摊非正整数 → 域外，不蒙数（precision 红线）
            if (!found || c <= 0) return null;
            var rounded = (int)Math.Round(c);
            if (Math.Abs(c - rounded) > 1e-6 || rounded <= 0) return null;
            counts.Add(rounded);
        }
        var g = counts.Aggregate(Gcd);
        return string.Join(":", counts.Select(i => (i / g).ToString()));
    }

    private static string FormatCount(string species, double formulaUnits, Cell cell)
    {
        if (cell.AtomsPerFU is { } apfu && apfu > 0)
        {
            var atoms = formulaUnits * apfu;
            return FormatInt(atoms);
        }
        return FormatInt(formulaUnits);
    }

    private static string FormatInt(double v)
    {
        var rounded = Math.Round(v);
        return Math.Abs(v - rounded) < 1e-6 ? ((long)rounded).ToString() : v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string ElementOf(string species)
    {
        var m = Regex.Match(species ?? "", @"^([A-Za-z]+)");
        return m.Success ? m.Groups[1].Value : species;
    }

    private static bool MentionsElement(string text, string element)
    {
        // 边界匹配：避免 "P" 命中 "pdf" 等；允许元素后接数字/电荷/空白/中文
        var pattern = @"(?<![A-Za-z])" + Regex.Escape(element) + @"(?![A-Za-z])";
        return Regex.IsMatch(text, pattern);
    }

    // ------------------------------------------------------------
    // 配位数（FR-3）：取资产中已核对的配位数事实
    // ------------------------------------------------------------
    private static string? CoordinationAnswer(IReadOnlyList<Cell> cells, string text)
    {
        // 优先匹配题面提及的晶胞（label/key）
        var candidates = cells.Where(c => c.CoordNumber.HasValue).ToList();
        if (candidates.Count == 0) return null;
        var hit = candidates.FirstOrDefault(c => text.Contains(c.Key) || text.Contains(c.Label))
                  ?? candidates[0];
        return hit.CoordNumber!.Value.ToString();
    }

    // ------------------------------------------------------------
    // 密度（数值，FR-4）
    // ------------------------------------------------------------
    private static string? DensityAnswer(IReadOnlyList<Cell> cells, string text)
    {
        var cell = PickCell(cells, text) ?? cells[0];
        // 多物种 basis（如 Mn₃Sb：Sb+Mn）时 Z(cell) 是总原子数，密度需公式单元数 → 优先用资产 formulaUnits；
        // 单物种（pdf-222/206）无此字段 → 回退 Z(cell)，行为不变（零回归）。
        var z = cell.FormulaUnits ?? Z(cell);
        if (z <= 0) return null;
        var m = cell.MolarMass ?? cell.Basis.FirstOrDefault()?.MolarMass;
        if (m is null || m <= 0) return null;
        if (cell.A is null || cell.A <= 0) return null;

        // V = a^3 (cubic) 或 a^2·c (tetragonal)，单位 cm：a nm → a×10^-7 cm
        double vDim;
        string volExpr;
        if (cell.C is { } cVal && cVal > 0 && Math.Abs(cVal - cell.A.Value) > 1e-9)
        {
            vDim = cell.A.Value * cell.A.Value * cVal;
            volExpr = "a^2·c";
        }
        else
        {
            vDim = cell.A.Value * cell.A.Value * cell.A.Value;
            volExpr = "a^3";
        }
        // ρ = z·m / (N_A · vDim · 10^-21) = z·m·10^21 / (N_A · vDim)
        var coeff = z * m.Value * 1e21;
        var sci = FormatScientific(coeff);
        var canonical = $"{sci} / (N_A · {volExpr})";
        var spaceless = Regex.Replace(canonical, @"\s+", "");
        return canonical + "；" + spaceless;
    }

    // ------------------------------------------------------------
    // 密度比（符号式，FR-5）：双胞同组分（M 约等）→ 质量消去
    // ------------------------------------------------------------
    private static string? SymbolicDensityRatio(IReadOnlyList<Cell> cells, string text)
    {
        if (cells.Count < 2) return null;
        // 按题面「最后一次出现」顺序确定 cell1（分子项）/ cell2（分母项）：
        // 密度比子问通常形如「白锡和灰锡的密度之比」，两 key 在该子问中按序出现于文末；
        // 用 LastIndexOf 避免配位子问（如「灰锡配位数」）提前出现导致顺序颠倒。
        var ordered = cells
            .Select(c => (cell: c, idx: text.LastIndexOf(c.Key, StringComparison.Ordinal)))
            .OrderBy(t => t.idx < 0 ? int.MaxValue : t.idx)
            .Select(t => t.cell)
            .ToList();
        var c1 = ordered[0];
        var c2 = ordered[1];
        var z1 = (int)Math.Round(Z(c1));
        var z2 = (int)Math.Round(Z(c2));
        if (z1 <= 0 || z2 <= 0) return null;

        // 同组分（M 约等）：ρ1/ρ2 = (Z1/V1)/(Z2/V2) = Z1·V2/(Z2·V1)
        var m1 = c1.MolarMass ?? c1.Basis.FirstOrDefault()?.MolarMass;
        var m2 = c2.MolarMass ?? c2.Basis.FirstOrDefault()?.MolarMass;
        if (m1 is null || m2 is null || Math.Abs(m1.Value - m2.Value) > 0.5)
        {
            // 质量不约等 → 保留质量符号（无候选命中，输出仅作运行时展示）
            return $"({z1}·M1·V2)/({z2}·M2·V1)";
        }

        var g = Gcd(z1, z2);
        var num = z1 / g;
        var den = z2 / g;
        return (num, den) switch
        {
            (1, 1) => "V2/V1",
            (1, _) => $"V2/{den}V1",
            (_, 1) => $"{num}V2/V1",
            _ => $"{num}V2/{den}V1"
        };
    }

    private static Cell? PickCell(IReadOnlyList<Cell> cells, string text) =>
        cells.FirstOrDefault(c => text.Contains(c.Key) || text.Contains(c.Label));

    // ------------------------------------------------------------
    // 工具
    // ------------------------------------------------------------
    private static string FormatScientific(double v)
    {
        if (v == 0) return "0";
        var exp = (int)Math.Floor(Math.Log10(Math.Abs(v)));
        var mant = v / Math.Pow(10, exp);
        mant = Math.Round(mant, 3);
        if (mant >= 10.0) { mant /= 10.0; exp++; }
        if (mant <= -10.0) { mant /= 10.0; exp++; }
        var mantStr = mant.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{mantStr}×10^{exp}";
    }

    private static int Gcd(int a, int b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) { (a, b) = (b, a % b); } return a == 0 ? 1 : a; }

    private static bool ContainsAny(string text, params string[] any) => any.Any(text.Contains);

    private static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;

    private static int? Int(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}

internal static class CrystalCellListExtensions
{
    public static void AddIfNotNull(this List<string> list, string? item) { if (!string.IsNullOrWhiteSpace(item)) list.Add(item); }
}
