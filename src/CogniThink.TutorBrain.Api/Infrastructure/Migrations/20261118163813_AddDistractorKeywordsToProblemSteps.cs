using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CogniThink.TutorBrain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistractorKeywordsToProblemSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistractorKeywords",
                table: "ProblemSteps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VagueKeywords",
                table: "ProblemSteps",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistractorKeywords",
                table: "ProblemSteps");

            migrationBuilder.DropColumn(
                name: "VagueKeywords",
                table: "ProblemSteps");
        }
    }
}
