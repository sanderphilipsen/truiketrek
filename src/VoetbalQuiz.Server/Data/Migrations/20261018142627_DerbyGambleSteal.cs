using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DerbyGambleSteal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BribedByUserId",
                table: "MatchAnswers",
                newName: "StolenByUserId");

            migrationBuilder.AddColumn<bool>(
                name: "Gamble",
                table: "MatchAnswers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gamble",
                table: "MatchAnswers");

            migrationBuilder.RenameColumn(
                name: "StolenByUserId",
                table: "MatchAnswers",
                newName: "BribedByUserId");
        }
    }
}
