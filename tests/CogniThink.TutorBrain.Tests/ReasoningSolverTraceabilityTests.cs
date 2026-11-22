using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P2-1 T3：4 个示范 solver 命中链的可溯源断言（AC-4）。
/// 直接调用各 solver 的 public static TrySolve（绕过路由，避免歧义），断言：
/// ReasoningNodes 非空 / Step 连续无跳步 / 至少一个节点 KnowledgeId 或 RuleId 非空 / 末节点 Conclusion==FinalResult。
/// 题干取自 after_p15b checkpoint 中已判正的真实题目，保证命中稳定。
/// </summary>
public class ReasoningSolverTraceabilityTests
{
    private static EngineRequest Req(string taskType, string raw) =>
        new() { TaskType = taskType, RawText = raw };

    private static ImageAsset Crystal(string json) =>
        new() { Type = "crystal", Source = "x.png", Payload = JsonDocument.Parse(json).RootElement };

    private static void AssertTraceable(EngineResponse? resp, string label)
    {
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{label}: 期望 Ok=true，实际 FinalResult={resp.FinalResult}");

        var nodes = resp.ReasoningNodes;
        Assert.NotEmpty(nodes); // AC-4: 非空

        // Step 连续 1..N，无跳步
        var steps = nodes.Select(n => n.Step).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(1, steps.Count).ToList(), steps);

        // 至少一个节点可溯源（KnowledgeId 或 RuleId 非空）
        Assert.True(
            nodes.Any(n => !string.IsNullOrEmpty(n.KnowledgeId) || !string.IsNullOrEmpty(n.RuleId)),
            $"{label}: 没有任何节点携带 KnowledgeId/RuleId（假可溯源）");

        // 末节点 Conclusion == FinalResult
        var last = nodes.OrderBy(n => n.Step).Last();
        Assert.Equal(resp.FinalResult, last.Conclusion);
    }

    [Fact]
    public void ConceptFactSolver_命中概念事实库_可溯源_AC4()
    {
        // checkpoint concept-1，已判正
        var resp = ConceptFactSolver.TrySolve(Req("concept", "什么是电解质？"));
        AssertTraceable(resp, "ConceptFact");
        // knowledge_id 来自 concept-facts.json 条目 id（非空）
        var node = resp!.ReasoningNodes[0];
        Assert.False(string.IsNullOrEmpty(node.KnowledgeId), "ConceptFact 命中节点应带 knowledge_id");
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
    }

    [Fact]
    public void AtomicStructureSolver_命中电子排布_可溯源_双节点无跳步_AC4()
    {
        // checkpoint pdf-173，已判正：基态 Cu 价层电子排布式
        var resp = AtomicStructureSolver.TrySolve(Req("atomic_structure", "（1）基态 Cu 原子的价层电子排布式为____________________。"));
        AssertTraceable(resp, "AtomicStructure");
        Assert.Equal(2, resp!.ReasoningNodes.Count); // 识别元素 → 构造原理计算
        Assert.Equal("periodic_table", resp.ReasoningNodes[0].KnowledgeId);
        Assert.Equal("rule:aufbau_hund_pauli", resp.ReasoningNodes[1].RuleId);
    }

    [Fact]
    public void VseprSolver_查表命中空间构型_可溯源_AC4()
    {
        // checkpoint d6-1，已判正：NH3 空间构型
        var resp = VseprSolver.TrySolve(Req("vsepr", "NH3 分子的空间构型是什么？"));
        AssertTraceable(resp, "Vsepr");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("vsepr_table", node.KnowledgeId);
    }

    [Fact]
    public void ReactionKbWriterSolver_离子方程式模板命中_可溯源_AC4()
    {
        // 酸 + 碳酸盐 → 离子方程式模板命中（ReactionKbWriterSolver，指纹 = 与 ^）
        var resp = ReactionKbWriterSolver.TrySolve(Req("equation_writer", "写出碳酸钠与盐酸反应的离子方程式。"));
        AssertTraceable(resp, "ReactionKb");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("reaction_kb:ionic", node.RuleId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
    }

    [Fact]
    public void 无命中_不臆造节点_R4()
    {
        // 域外题干 → solver 返回 null（不蒙数），不得返回带 Ok=true + 假节点的响应
        var resp = ReactionKbWriterSolver.TrySolve(Req("equation_writer", "今天天气怎么样？"));
        Assert.Null(resp);
    }

    // —— Tier 1 快赢：HalfReactionBalancer + CrystalCellValidator 迁移后可溯源自检 ——

    [Fact]
    public void HalfReactionBalancer_知识库溯源命中_可溯源_AC4()
    {
        // pdf-304：data/knowledge/electrode-half-reactions.json 唯一条目命中
        // （triggerAll=[CO, 燃料电池, 电极反应]，输出 == entry.halfReaction）→ 推理链非空且可溯源
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-304");
        var resp = HalfReactionBalancer.TrySolve(Req("fill", c.Question));
        AssertTraceable(resp, "HalfReactionKb");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("hr-co-air-fuelcell-anode-base", node.KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
    }

    [Fact]
    public void HalfReactionBalancer_萃取配平_无知识锚点_推理链留空_R4()
    {
        // pdf-313：KB 仅含 CO 条目，本题为硼氢化物电对 + 介质 → 走萃取式纯计算，无 knowledge_id 命中
        // → 推理链必须留空（spec R4：不得假可溯源 / 不蒙数）
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-313");
        var resp = HalfReactionBalancer.TrySolve(Req("fill", c.Question));
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, "萃取配平应产出 Ok=true 的结论");
        Assert.Empty(resp.ReasoningNodes);
    }

    [Fact]
    public void CrystalCellValidator_晶胞资产命中_可溯源_AC4()
    {
        // NiO 岩盐晶胞：均摊 Ni = 8×1/8 + 6×1/2 = 4；资产 key=NiO 作为 knowledge_id 锚点
        var asset = Crystal(@"{""key"":""NiO"",""label"":""NiO（岩盐）"",""basis"":[{""species"":""Ni"",""site"":""corner"",""count"":8},{""species"":""Ni"",""site"":""face"",""count"":6}]}");
        var resp = CrystalCellValidator.TrySolve(Req("unknown", "NiO 晶胞如右图所示，晶胞中含有 Ni2+的个数为________。"), new[] { asset });
        AssertTraceable(resp, "CrystalCell");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("NiO", node.KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
    }

    [Fact]
    public void CrystalCellValidator_无晶胞资产_不臆造节点_R4()
    {
        // 无 type=crystal 资产 → 求解器返回 null，不得产出带 Ok=true + 假节点的响应
        var resp = CrystalCellValidator.TrySolve(Req("unknown", "晶胞中 Ni 的个数为"), Array.Empty<ImageAsset>());
        Assert.Null(resp);
    }

    // —— Tier 2 快赢：HydrolysisPhCalculator 迁移后可溯源自检 ——

    [Fact]
    public void HydrolysisPhCalculator_常数查表命中_可溯源_AC4()
    {
        // pdf-517：0.01 mol·L⁻¹ NaClO 溶液 pH → FindSaltAcid 命中 ladder ["HClO","ClO^-"]，
        // kaKey="HClO"（acid-constants.json 稳定字典键）→ 推理链非空且可溯源
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-517");
        var resp = HydrolysisPhCalculator.TrySolve(c.Question);
        AssertTraceable(resp, "HydrolysisPh");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("HClO", node.KnowledgeId);
        Assert.Equal(EvidenceLevel.EngineDerived, node.EvidenceLevel);
    }

    [Fact]
    public void HydrolysisPhCalculator_无已知弱酸盐_不臆造节点_R4()
    {
        // 含 pH + 浓度，但盐物种不在 acid-constants.json 的 AcidLadders 中 → 域外返回 null，
        // 不得产出带 Ok=true + 假节点的响应（spec R4：无 knowledge_id 命中不得填充节点）
        var resp = HydrolysisPhCalculator.TrySolve("0.1 mol/L 葡萄糖溶液的 pH");
        Assert.Null(resp);
    }

    // —— B-外置批 Task 1：KnownEquationWriterSolver 外置后可溯源 + 零漂移 ——

    [Fact]
    public void KnownEquationWriter_GaN_命中可溯源_零漂移()
    {
        // pdf-539 真实题干 → 命中 known-eq-gan-strong-base；FinalResult 必须与基线字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-539");
        var resp = KnownEquationWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "KnownEq-GaN");
        Assert.Equal("离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑", resp!.FinalResult);
        Assert.Equal("known-eq-gan-strong-base", resp.ReasoningNodes[0].KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, resp.ReasoningNodes[0].EvidenceLevel);
    }

    [Fact]
    public void KnownEquationWriter_SO2_命中可溯源_零漂移()
    {
        // pdf-456 真实题干 → 命中 known-eq-na2so3-so2；FinalResult 字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-456");
        var resp = KnownEquationWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "KnownEq-SO2");
        Assert.Equal("化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O", resp!.FinalResult);
        Assert.Equal("known-eq-na2so3-so2", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void KnownEquationWriter_域外_不臆造节点_R4()
    {
        // 非目标方程 → 返回 null，不产出带 Ok=true + 假节点的响应
        var resp = KnownEquationWriterSolver.TrySolve(Req("reaction_check", "写出 Fe 与 HCl 反应的化学方程式。"));
        Assert.Null(resp);
    }

    // —— B-外置批 Task 2：ProductWriterSolver 外置后可溯源 + 零漂移 ——

    [Fact]
    public void ProductWriter_FeBr2Cl2少量_命中可溯源_零漂移()
    {
        // d1-1 真实题干 → 命中 pw-febr2-cl2-little；FinalResult 与基线字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d1-1");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-FeBr2-little");
        Assert.Equal("离子方程式：2Fe2+ + Cl2 → 2Fe3+ + 2Cl-", resp!.FinalResult);
        Assert.Equal("pw-febr2-cl2-little", resp.ReasoningNodes[0].KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, resp.ReasoningNodes[0].EvidenceLevel);
    }

    [Fact]
    public void ProductWriter_FeBr2Cl2过量_命中可溯源_零漂移()
    {
        // d1-2 真实题干 → 命中 pw-febr2-cl2-excess（骨架占位展开为离子）；FinalResult 字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d1-2");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-FeBr2-excess");
        Assert.Equal("离子方程式：2Fe2+ + 4Br- + 3Cl2 → 2Fe3+ + 2Br2 + 6Cl-", resp!.FinalResult);
        Assert.Equal("pw-febr2-cl2-excess", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void ProductWriter_NaAlO2CO2少量_命中可溯源_零漂移()
    {
        // d1-3 真实题干 → 命中 pw-naalo2-co2-little
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d1-3");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-NaAlO2-little");
        Assert.Equal("离子方程式：2AlO2- + CO2 + 3H2O → 2Al(OH)3 + CO32-", resp!.FinalResult);
        Assert.Equal("pw-naalo2-co2-little", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void ProductWriter_NaAlO2CO2过量_命中可溯源_零漂移()
    {
        // d1-4 真实题干 → 命中 pw-naalo2-co2-excess
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d1-4");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-NaAlO2-excess");
        Assert.Equal("离子方程式：AlO2- + CO2 + 2H2O → Al(OH)3 + HCO3-", resp!.FinalResult);
        Assert.Equal("pw-naalo2-co2-excess", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void ProductWriter_CaHCO32NaOH少量_命中可溯源_零漂移()
    {
        // d1-5 真实题干 → 命中 pw-cahco32-naoh-little
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d1-5");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-CaHCO32-little");
        Assert.Equal("离子方程式：Ca2+ + HCO3- + OH- → CaCO3 + H2O", resp!.FinalResult);
        Assert.Equal("pw-cahco32-naoh-little", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void ProductWriter_pdf044_同d1_命中可溯源_零漂移()
    {
        // pdf-044 真实题干 → 与 d1-1 同规则（pw-febr2-cl2-little），FinalResult 字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-044");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-pdf044");
        Assert.Equal("离子方程式：2Fe2+ + Cl2 → 2Fe3+ + 2Cl-", resp!.FinalResult);
        Assert.Equal("pw-febr2-cl2-little", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void ProductWriter_NaCNCO2少量_命中可溯源_零漂移()
    {
        // pdf-515 真实题干 → 命中 pw-nacn-co2-little
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-515");
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", c.Question));
        AssertTraceable(resp, "Pw-NaCN-little");
        Assert.Equal("离子方程式：CN- + CO2 + H2O → HCN + HCO3-", resp!.FinalResult);
        Assert.Equal("pw-nacn-co2-little", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void ProductWriter_域外_不臆造节点_R4()
    {
        // 非目标方程（规则表外）→ 返回 null，不产出带 Ok=true + 假节点的响应
        var resp = ProductWriterSolver.TrySolve(Req("reaction_check", "FeCl3 溶液中加入少量 NaOH，反应的离子方程式是什么？"));
        Assert.Null(resp);
    }

    // —— B-外置批 Task 3：RedoxGatewaySolver 外置后可溯源 + 零漂移（R1 保持空） ——

    [Fact]
    public void Redox_R2_稀硝酸还原产物_命中可溯源_零漂移()
    {
        // d2-4 真实题干 → R2 命中 redox-no3-dilute；FinalResult 字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d2-4");
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", c.Question));
        AssertTraceable(resp, "Redox-R2-NO3");
        Assert.Equal("还原产物是 NO", resp!.FinalResult);
        Assert.Equal("redox-no3-dilute", resp.ReasoningNodes[0].KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, resp.ReasoningNodes[0].EvidenceLevel);
    }

    [Fact]
    public void Redox_P3_两性金属强碱_命中可溯源_零漂移()
    {
        // trap-limit-1 真实题干 → P3 命中 redox-feasibility-amphoteric-strong-base
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "trap-limit-1");
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", c.Question));
        AssertTraceable(resp, "Redox-P3-amphoteric");
        Assert.Equal("能。Al 是两性金属，与强碱溶液反应生成 H2。", resp!.FinalResult);
        Assert.Equal("redox-feasibility-amphoteric-strong-base", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void Redox_P3_酸性无OH_命中可溯源_零漂移()
    {
        // d8-5 真实题干 → P3 命中 redox-feasibility-acidic-no-oh
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "d8-5");
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", c.Question));
        AssertTraceable(resp, "Redox-P3-acidic-no-oh");
        Assert.Equal("不能。酸性电解质溶液中，电极反应式不出现 OH-。", resp!.FinalResult);
        Assert.Equal("redox-feasibility-acidic-no-oh", resp.ReasoningNodes[0].KnowledgeId);
    }

    [Fact]
    public void Redox_R1_角色识别_无知识锚点_推理链留空_R4()
    {
        // redox-1：R1 由 Core 氧化态计算得出，无 knowledge_id 锚点 → 节点必须为空（spec R4）
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "redox-1");
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", c.Question));
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, "redox-1 应产出 Ok=true 结论");
        Assert.Equal("还原剂是 Cu", resp.FinalResult);
        Assert.True(resp.ReasoningNodes is null || resp.ReasoningNodes.Count == 0,
            "R1 无知识锚点，不得填充推理节点（R4）");
    }

    [Fact]
    public void Redox_R1_氧化剂识别_推理链留空_R4()
    {
        // redox-2：R1 → 节点空
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "redox-2");
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", c.Question));
        Assert.NotNull(resp);
        Assert.Equal("氧化剂是 FeCl3", resp!.FinalResult);
        Assert.True(resp.ReasoningNodes is null || resp.ReasoningNodes.Count == 0, "R1 不得填充节点（R4）");
    }

    [Fact]
    public void Redox_R1_还原剂识别_推理链留空_R4()
    {
        // redox-3：R1 → 节点空
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "redox-3");
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", c.Question));
        Assert.NotNull(resp);
        Assert.Equal("还原剂是 KI", resp!.FinalResult);
        Assert.True(resp.ReasoningNodes is null || resp.ReasoningNodes.Count == 0, "R1 不得填充节点（R4）");
    }

    [Fact]
    public void Redox_R2_介质缺失_不臆造节点_R4()
    {
        // KMnO4 熔融（无酸/中/碱介质词）→ 规则表外，返回 null，不发节点
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", "KMnO4 在熔融条件下的还原产物是什么？"));
        Assert.Null(resp);
    }

    [Fact]
    public void Redox_R2_无浓稀_不臆造节点_R4()
    {
        // 硝酸无稀/浓 → 不蒙数，返回 null
        var resp = RedoxGatewaySolver.TrySolve(Req("redox", "硝酸的还原产物是什么？"));
        Assert.Null(resp);
    }

    // —— B-外置批 Task 4：ConceptVerdictSolver 外置后可溯源 + 零漂移 ——

    [Fact]
    public void ConceptVerdict_酸性氧化物断言_命中可溯源_零漂移()
    {
        // pdf-467 真实题干 → 命中 concept-verdict-sio2-basic-oxide-false；FinalResult 字节级相等
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-467");
        var resp = ConceptVerdictSolver.TrySolve(Req("concept", c.Question));
        AssertTraceable(resp, "ConceptVerdict");
        Assert.Equal("×", resp!.FinalResult);
        Assert.Equal("concept-verdict-sio2-basic-oxide-false", resp.ReasoningNodes[0].KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, resp.ReasoningNodes[0].EvidenceLevel);
    }

    [Fact]
    public void ConceptVerdict_否定式_不命中_R4()
    {
        // 否定断言（"不是"）→ 负向后行排除 → 返回 null，不发节点
        var resp = ConceptVerdictSolver.TrySolve(Req("concept", "(9)SiO2不是碱性氧化物（ ）"));
        Assert.Null(resp);
    }

    [Fact]
    public void ConceptVerdict_无判断空_不命中_R4()
    {
        // 无 ( ) / （ ）判断空 → 不是正误判断题，返回 null
        var resp = ConceptVerdictSolver.TrySolve(Req("concept", "SiO2为碱性氧化物"));
        Assert.Null(resp);
    }
}
