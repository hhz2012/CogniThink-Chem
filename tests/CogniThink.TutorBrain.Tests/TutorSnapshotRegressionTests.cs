// P3-3 导学与求解融合：快照回归（AC-4）。
//
// 红线命题（加性锚定）：网关 DAG 为空 / Insufficient（provider 缺席或返回 null）时，
// SubmitStepResponse 既有字段（除新增 StandardReasoningChain 外）与改动前逐 case 字节一致；
// 当 provider 返回真实链时，仅 StandardReasoningChain 字段不同，其余字段完全不变。
//
// 证明方式：对每个场景，在三份独立、同种子的内存 SQLite 库上各跑一遍：
//   A) chainProvider = null（等价 P3-3 之前的行为）
//   B) provider 返回 Insufficient（网关 DAG 不可用 → 取链为 null）
//   C) provider 返回 Confirmed + 非 Trend 节点（取链非空）
// 把 SubmitStepResult 映射成与 TutorController 完全一致的 SubmitStepResponse，
// 序列化后剔除 standardReasoningChain 键，断言 A/B/C 三者字符串相等；
// 并断言 A/B 的链为 null、C 的链等于注入的节点。
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Api.Contracts;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Gateway;
using CogniThink.TutorBrain.Application.Reasoning;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public sealed class TutorSnapshotRegressionTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static ReasoningNode Node(string desc, EvidenceLevel level = EvidenceLevel.EngineDerived, string? kid = null)
        => new() { Step = 1, Description = desc, Conclusion = desc, EvidenceLevel = level, KnowledgeId = kid };

    private static GatewayResult Result(ConclusionState state, params ReasoningNode[] nodes)
    {
        var resp = new EngineResponse { ConclusionState = state };
        foreach (var n in nodes) resp.ReasoningNodes.Add(n);
        return new GatewayResult { EngineResponse = resp };
    }

    // provider 桩：覆写 CallGatewayAsync，返回受控 GatewayResult，不触真实网关（不改网关类）。
    private sealed class StubProvider : TutorSolveChainProvider
    {
        private readonly GatewayResult _result;
        public StubProvider(GatewayResult result) : base(null!, new TutorSolveChainCache()) => _result = result;
        protected override Task<GatewayResult> CallGatewayAsync(string stem, CancellationToken ct)
            => Task.FromResult(_result);
    }

    // ---------- 独立同种子库 ----------
    private static (TutorDbContext Db, SqliteConnection Conn) CreateSeededDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<TutorDbContext>().UseSqlite(conn).Options;
        var db = new TutorDbContext(options);
        db.Database.EnsureCreated();
        SeedData.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return (db, conn);
    }

    private static TutorService BuildTutor(TutorDbContext db, TutorSolveChainProvider? provider)
    {
        var kb = ChemistryFixture.Knowledge;
        var chemical = new ChemicalInferenceService(kb);
        var graph = new KnowledgeGraphService(db);
        var students = new StudentModelService(db);
        var variants = new VariantService(db);
        var compiler = new ProblemQueryCompiler(kb);
        var machine = new TutorStateMachine(new IReasoningEngine[] { new ChemistryReasoningEngine() }, new RuleBasedNarrativeAgent());
        return new TutorService(db, machine, students, graph, variants, compiler, chemical, provider);
    }

    // 与 TutorController.SubmitStep 的映射逐行一致，保证断言对象即线上 wire 对象。
    private static SubmitStepResponse MapResponse(SubmitStepResult result)
    {
        var d = result.Decision;
        return new SubmitStepResponse(
            Status: d.Status.ToString(),
            Verdict: d.Verdict.ToString(),
            ErrorTag: d.ErrorTag.ToText(),
            HintLevel: d.HintLevel,
            RecommendedAction: d.RecommendedAction.ToString(),
            Message: d.Message,
            RelatedKnowledgeIds: d.RelatedKnowledgeIds.ToList(),
            UnlockedNextStep: d.UnlockedNextStep,
            Completed: d.Completed,
            NextStepIndex: d.NextStepIndex,
            HintText: d.HintText,
            Mastery: result.Mastery is null ? null : Math.Round(result.Mastery.Value, 3),
            CurrentStep: result.CurrentStep,
            TotalSteps: result.TotalSteps,
            WeakPrerequisites: result.WeakPrerequisites.Select(DtoMapper.ToDto).ToList(),
            ChemicalEvidence: result.ChemicalEvidence is null ? null : DtoMapper.ToDto(result.ChemicalEvidence),
            Variant: result.Variant is null ? null : DtoMapper.ToDto(result.Variant),
            StandardReasoningChain: result.StandardReasoningChain?.ToList());
    }

    // 序列化并剔除新增键 standardReasoningChain，得到「既有字段」的稳定串。
    private static string Normalize(SubmitStepResponse resp)
    {
        var json = JsonSerializer.Serialize(resp, JsonOpts);
        var obj = JsonNode.Parse(json)!.AsObject();
        obj.Remove("standardReasoningChain");
        return obj.ToJsonString(JsonOpts);
    }

    private static async Task<SubmitStepResponse> RunAsync(
        TutorSolveChainProvider? provider,
        string problemId,
        params (int Step, string Answer)[] submits)
    {
        var (db, conn) = CreateSeededDb();
        try
        {
            var tutor = BuildTutor(db, provider);
            SubmitStepResult? last = null;
            foreach (var (step, answer) in submits)
            {
                last = await tutor.SubmitStepAsync(
                    new SubmitStepInput("stu-snap", problemId, step, answer));
                Assert.NotNull(last); // 每一步都应命中题目/步骤
            }
            return MapResponse(last!);
        }
        finally
        {
            db.Dispose();
            conn.Dispose();
        }
    }

    // 核心断言：A(null provider) / B(null chain) / C(real chain) 既有字段字节一致；
    // A、B 链为 null；C 链等于注入节点且不污染既有字段。
    private static async Task AssertSnapshot(string problemId, params (int Step, string Answer)[] submits)
    {
        var realNodes = new[]
        {
            Node("推导：选 KSCN 检验 Fe³⁺", EvidenceLevel.EngineDerived, "C-FE"),
            Node("事实：Fe³⁺ 遇 SCN⁻ 变红", EvidenceLevel.KnowledgeFact, "C-FE"),
        };

        var respNo = await RunAsync(null, problemId, submits);                                   // A: P3-3 前
        var respNull = await RunAsync(new StubProvider(Result(ConclusionState.Insufficient, Node("不泄露"))), problemId, submits); // B: DAG 空
        var respReal = await RunAsync(new StubProvider(Result(ConclusionState.Confirmed, realNodes)), problemId, submits);        // C: 有链

        // 既有字段（剔除 standardReasoningChain）三者逐字节一致
        var normNo = Normalize(respNo);
        Assert.Equal(normNo, Normalize(respNull));
        Assert.Equal(normNo, Normalize(respReal));

        // A/B 无链；C 有链且内容即注入节点（不混入 Trend，不被改写）
        Assert.Null(respNo.StandardReasoningChain);
        Assert.Null(respNull.StandardReasoningChain);
        Assert.NotNull(respReal.StandardReasoningChain);
        Assert.Equal(2, respReal.StandardReasoningChain!.Count);
        Assert.Equal(realNodes[0].Description, respReal.StandardReasoningChain[0].Description);
        Assert.Equal(realNodes[1].Description, respReal.StandardReasoningChain[1].Description);
        Assert.DoesNotContain(respReal.StandardReasoningChain, n => n.EvidenceLevel == EvidenceLevel.Trend);
    }

    // ==================== 场景 ====================

    [Fact]
    public async Task Snapshot_Correct_NonKeyStep_NoChemicalEvidence_NoVariant()
    {
        // C-FE-01 第 1 步正确：非关键步 → chemicalEvidence=null；未通关 → variant=null
        await AssertSnapshot("C-FE-01", (1, "KSCN"));
    }

    [Fact]
    public async Task Snapshot_Correct_KeyStep_PopulatesChemicalEvidence()
    {
        // C-FE-01 第 2 步正确（IsKeyStep=true）→ 触发 BuildChemicalEvidence
        await AssertSnapshot("C-FE-01", (2, "溶液变红色"));
    }

    [Fact]
    public async Task Snapshot_FullSequence_Completed_PopulatesVariant()
    {
        // C-COEXIST-01 三步全对 → 通关 → 生成变式（{id}-V1，确定性）
        await AssertSnapshot("C-COEXIST-01",
            (1, "H+与NO3-相当于硝酸"),
            (2, "将Fe2+氧化为Fe3+"),
            (3, "不能大量共存"));
    }

    [Fact]
    public async Task Snapshot_Incorrect_PopulatesWeakPrerequisites()
    {
        // C-FE-01 第 1 步答错（NaOH 为干扰项）→ Incorrect → 定位薄弱前置知识点
        await AssertSnapshot("C-FE-01", (1, "NaOH"));
    }
}
