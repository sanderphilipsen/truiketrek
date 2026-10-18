using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DerbyBribeExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BribedByUserId",
                table: "MatchAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtraSeconds",
                table: "MatchAnswers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BribedByUserId",
                table: "MatchAnswers");

            migrationBuilder.DropColumn(
                name: "ExtraSeconds",
                table: "MatchAnswers");
        }
    }
}
