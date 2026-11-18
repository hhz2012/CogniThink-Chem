namespace CogniThink.TutorBrain.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Infrastructure;
/// <summary>诊断引擎测试夹具：内存 SQLite + 种子数据 + 化学知识库</summary>
public sealed class DiagnosticFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public TutorDbContext Db { get; }
    public ChemicalKnowledgeBase Knowledge { get; }
    public ChemicalInferenceService Chemical { get; }
    public KnowledgeGraphService Graph { get; }
    public DiagnosticSessionStore Store { get; }
    public DiagnosticTeachingEngine Engine { get; }
    public StudentModelService StudentModel { get; }
    public VariantService Variants { get; }
    public AbilityGraphService AbilityGraph { get; }
    public AdaptiveTeachingOrchestrator Adaptive { get; }

    public DiagnosticFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TutorDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new TutorDbContext(options);
        Db.Database.EnsureCreated();

        // 填充种子数据（知识点 + 题目）
        SeedData.EnsureSeededAsync(Db).GetAwaiter().GetResult();

        var apiRoot = ChemistryFixture.ApiRoot;
        Knowledge = ChemicalKnowledgeBase.Load(apiRoot);
        Chemical = new ChemicalInferenceService(Knowledge);
        Graph = new KnowledgeGraphService(Db);
        Store = new DiagnosticSessionStore();
        Engine = new DiagnosticTeachingEngine(Db, Chemical, Knowledge, Graph, Store);
        StudentModel = new StudentModelService(Db);
        Variants = new VariantService(Db);
        AbilityGraph = new AbilityGraphService(Db);
        Adaptive = new AdaptiveTeachingOrchestrator(Engine, StudentModel, Variants, Db);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
