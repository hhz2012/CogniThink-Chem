using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Ability;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using CogniThink.TutorBrain.Application.Reasoning;
using CogniThink.TutorBrain.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ==================== API / Swagger ====================
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "推理导学智能大脑 API",
        Version = "v1",
        Description = "步骤级自适应导学：知识图谱 + 学情模型 + 导学状态机。判定由领域规则引擎完成，LLM 只负责话术。"
    });
});

// ==================== 持久化（SQLite，可换 PostgreSQL） ====================
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "tutor-brain.db");
builder.Services.AddDbContext<TutorDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// ==================== 领域服务 ====================
builder.Services.AddScoped<KnowledgeGraphService>();
builder.Services.AddScoped<StudentModelService>();
builder.Services.AddScoped<VariantService>();
builder.Services.AddScoped<TutorStateMachine>();
builder.Services.AddScoped<TutorService>();
// P3-3 导学与求解融合：标准推理链缓存（单例）+ 取链 provider（Scoped，注入 LlmGatewayService）
builder.Services.AddSingleton<ITutorSolveChainCache, TutorSolveChainCache>();
builder.Services.AddScoped<TutorSolveChainProvider>();
builder.Services.AddSingleton<DiagnosticSessionStore>();
builder.Services.AddScoped<DiagnosticTeachingEngine>();
builder.Services.AddScoped<AbilityGraphService>();
builder.Services.AddScoped<AdaptiveTeachingOrchestrator>();

// ==================== 能力评价与导学 ====================
builder.Services.AddScoped<AbilityEvaluationService>();
builder.Services.AddScoped<ComprehensiveEvaluationService>();
builder.Services.AddScoped<AbilityGuidedTeachingService>();
builder.Services.AddScoped<PersonalAbilityGraphService>();
builder.Services.AddScoped<ErrorPracticeLoopService>();

// 推理引擎（化学规则判定，不经 LLM）
builder.Services.AddScoped<IReasoningEngine, ChemistryReasoningEngine>();

// ==================== 化学知识库（JSON 本体，启动加载） ====================
var chemistryKb = ChemicalKnowledgeBase.Load(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(chemistryKb);
builder.Services.AddSingleton<ChemicalInferenceService>();
builder.Services.AddSingleton<ProblemQueryCompiler>();
builder.Services.AddSingleton<ReactionEquationParser>();
builder.Services.AddSingleton<ChemicalSimulationEngine>();
builder.Services.AddSingleton<ChemicalReverseInferenceEngine>();

// ==================== 话术层（可切换模型） ====================
builder.Services.AddSingleton<RuleBasedNarrativeAgent>();
var llm = builder.Configuration.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();
builder.Services.AddSingleton(llm);
if (llm.Enabled)
{
    builder.Services.AddHttpClient<ITutorNarrativeAgent, LlmNarrativeAgent>();
}
else
{
    builder.Services.AddSingleton<ITutorNarrativeAgent>(sp => sp.GetRequiredService<RuleBasedNarrativeAgent>());
}

// ==================== 引擎 vs 大模型 对比评估 ====================
builder.Services.AddHttpClient<LlmJudgeService>();
builder.Services.AddScoped<EngineVsLlmEvaluationService>();
builder.Services.AddScoped<GatewayVsLlmEvaluationService>();

// ==================== LLM 代理（前端网关调用大模型） ====================
builder.Services.AddHttpClient<LlmProxyService>();

// ==================== LLM 网关（输入标准化 + 引擎调用 + 回复生成） ====================
builder.Services.AddScoped<LlmGatewayService>();

// 前端独立开发，允许跨域
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// 知识库自检结果
if (chemistryKb.LoadErrors.Count > 0)
{
    foreach (var error in chemistryKb.LoadErrors)
        app.Logger.LogWarning("化学知识库自检：{Error}", error);
}
else
{
    app.Logger.LogInformation(
        "化学知识库加载完成：物质 {Substances}，离子 {Ions}，反应 {Reactions}，离子规则 {Interactions}",
        chemistryKb.Substances.Count, chemistryKb.Ions.Count,
        chemistryKb.Reactions.Count, chemistryKb.Interactions.Count);
}

// ==================== 建库（迁移）+ 种子数据 ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TutorDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "推理导学智能大脑 API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
