namespace CogniThink.TutorBrain.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using CogniThink.TutorBrain.Domain;

/// <summary>EF Core 上下文（SQLite 起步，可平滑迁移 PostgreSQL）</summary>
public sealed class TutorDbContext : DbContext
{
    public TutorDbContext(DbContextOptions<TutorDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<KnowledgeNode> KnowledgeNodes => Set<KnowledgeNode>();
    public DbSet<KnowledgeEdge> KnowledgeEdges => Set<KnowledgeEdge>();
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<ProblemStep> ProblemSteps => Set<ProblemStep>();
    public DbSet<KnowledgeMastery> KnowledgeMasteries => Set<KnowledgeMastery>();
    public DbSet<StepTrace> StepTraces => Set<StepTrace>();
    public DbSet<MistakeRecord> MistakeRecords => Set<MistakeRecord>();
    public DbSet<ReviewSchedule> ReviewSchedules => Set<ReviewSchedule>();
    public DbSet<ChallengeProgress> ChallengeProgresses => Set<ChallengeProgress>();
    public DbSet<TutorState> TutorStates => Set<TutorState>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ===== 知识点 =====
        mb.Entity<KnowledgeNode>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);

            var comparer = new ValueComparer<List<ErrorTag>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                a => a.Aggregate(0, (h, v) => HashCode.Combine(h, v)),
                a => a.ToList());

            e.Property(x => x.ErrorTags)
                .HasConversion(
                    v => string.Join(',', v.Select(t => (int)t)),
                    v => v.Length == 0
                        ? new List<ErrorTag>()
                        : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => (ErrorTag)int.Parse(s)).ToList())
                .Metadata.SetValueComparer(comparer);
        });

        // ===== 知识点依赖边 =====
        mb.Entity<KnowledgeEdge>(e =>
        {
            e.HasKey(x => new { x.FromId, x.ToId });
            e.HasOne(x => x.From).WithMany(n => n.Prerequisites)
                .HasForeignKey(x => x.FromId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.To).WithMany(n => n.Dependents)
                .HasForeignKey(x => x.ToId).OnDelete(DeleteBehavior.Restrict);
        });

        // ===== 题目 =====
        mb.Entity<Problem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Stem).IsRequired();
            e.HasIndex(x => new { x.Subject, x.Category });
        });

        mb.Entity<ProblemKnowledge>(e =>
        {
            e.HasKey(x => new { x.ProblemId, x.KnowledgeId });
            e.HasOne(x => x.Problem).WithMany(p => p.KnowledgeLinks)
                .HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Knowledge).WithMany()
                .HasForeignKey(x => x.KnowledgeId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<ProblemStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Problem).WithMany(p => p.Steps)
                .HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProblemId, x.StepIndex }).IsUnique();
            e.Property(x => x.Prompt).IsRequired();
            e.Property(x => x.ExpectedExpression).IsRequired();
        });

        // ===== 学生学情 =====
        mb.Entity<Student>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        mb.Entity<KnowledgeMastery>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StudentId, x.KnowledgeId }).IsUnique();
        });

        mb.Entity<StepTrace>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StudentId, x.ProblemId, x.StepIndex });
            e.Property(x => x.StepContent).HasMaxLength(2000);
            e.Property(x => x.Feedback).HasMaxLength(1000);
        });

        mb.Entity<MistakeRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StudentId, x.ProblemId });
        });

        mb.Entity<ReviewSchedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StudentId, x.KnowledgeId }).IsUnique();
        });

        mb.Entity<ChallengeProgress>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StudentId, x.ProblemId }).IsUnique();
        });

        mb.Entity<TutorState>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StudentId, x.ProblemId }).IsUnique();
        });
    }
}
