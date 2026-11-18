using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CogniThink.TutorBrain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Subject = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ErrorTags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Problems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Stem = table.Column<string>(type: "TEXT", nullable: false),
                    AnswerKey = table.Column<string>(type: "TEXT", nullable: true),
                    VariantOfId = table.Column<string>(type: "TEXT", nullable: true),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TutorStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    ProblemId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsecutiveStuck = table.Column<int>(type: "INTEGER", nullable: false),
                    HintLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeEdges",
                columns: table => new
                {
                    FromId = table.Column<string>(type: "TEXT", nullable: false),
                    ToId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeEdges", x => new { x.FromId, x.ToId });
                    table.ForeignKey(
                        name: "FK_KnowledgeEdges_KnowledgeNodes_FromId",
                        column: x => x.FromId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeEdges_KnowledgeNodes_ToId",
                        column: x => x.ToId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProblemKnowledge",
                columns: table => new
                {
                    ProblemId = table.Column<string>(type: "TEXT", nullable: false),
                    KnowledgeId = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemKnowledge", x => new { x.ProblemId, x.KnowledgeId });
                    table.ForeignKey(
                        name: "FK_ProblemKnowledge_KnowledgeNodes_KnowledgeId",
                        column: x => x.KnowledgeId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProblemKnowledge_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProblemId = table.Column<string>(type: "TEXT", nullable: false),
                    StepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedExpression = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedKeywords = table.Column<string>(type: "TEXT", nullable: true),
                    KnowledgeId = table.Column<string>(type: "TEXT", nullable: false),
                    IsKeyStep = table.Column<bool>(type: "INTEGER", nullable: false),
                    HintSelfCheck = table.Column<string>(type: "TEXT", nullable: true),
                    HintSubQuestion = table.Column<string>(type: "TEXT", nullable: true),
                    HintClue = table.Column<string>(type: "TEXT", nullable: true),
                    Scaffold = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemSteps_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    ProblemId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStep = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeProgresses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeMasteries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    KnowledgeId = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Corrects = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeMasteries_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MistakeRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    ProblemId = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorTag = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstWrongAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Passed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MistakeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MistakeRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    KnowledgeId = table.Column<string>(type: "TEXT", nullable: false),
                    DueAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    EaseFactor = table.Column<double>(type: "REAL", nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSchedules_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StepTraces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    ProblemId = table.Column<string>(type: "TEXT", nullable: false),
                    StepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    StepContent = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Verdict = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorTag = table.Column<int>(type: "INTEGER", nullable: false),
                    HintLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepTraces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepTraces_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeProgresses_StudentId_ProblemId",
                table: "ChallengeProgresses",
                columns: new[] { "StudentId", "ProblemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEdges_ToId",
                table: "KnowledgeEdges",
                column: "ToId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeMasteries_StudentId_KnowledgeId",
                table: "KnowledgeMasteries",
                columns: new[] { "StudentId", "KnowledgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MistakeRecords_StudentId_ProblemId",
                table: "MistakeRecords",
                columns: new[] { "StudentId", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemKnowledge_KnowledgeId",
                table: "ProblemKnowledge",
                column: "KnowledgeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemSteps_ProblemId_StepIndex",
                table: "ProblemSteps",
                columns: new[] { "ProblemId", "StepIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Subject_Category",
                table: "Problems",
                columns: new[] { "Subject", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSchedules_StudentId_KnowledgeId",
                table: "ReviewSchedules",
                columns: new[] { "StudentId", "KnowledgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StepTraces_StudentId_ProblemId_StepIndex",
                table: "StepTraces",
                columns: new[] { "StudentId", "ProblemId", "StepIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorStates_StudentId_ProblemId",
                table: "TutorStates",
                columns: new[] { "StudentId", "ProblemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChallengeProgresses");

            migrationBuilder.DropTable(
                name: "KnowledgeEdges");

            migrationBuilder.DropTable(
                name: "KnowledgeMasteries");

            migrationBuilder.DropTable(
                name: "MistakeRecords");

            migrationBuilder.DropTable(
                name: "ProblemKnowledge");

            migrationBuilder.DropTable(
                name: "ProblemSteps");

            migrationBuilder.DropTable(
                name: "ReviewSchedules");

            migrationBuilder.DropTable(
                name: "StepTraces");

            migrationBuilder.DropTable(
                name: "TutorStates");

            migrationBuilder.DropTable(
                name: "KnowledgeNodes");

            migrationBuilder.DropTable(
                name: "Problems");

            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
