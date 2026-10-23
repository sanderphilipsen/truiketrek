using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class KaartdagTweeDelen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Picks",
                table: "TrainingAttempts",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<List<string>>(
                name: "Picks",
                table: "MatchAnswers",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<int>(
                name: "Part",
                table: "DailyQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<string>>(
                name: "Picks",
                table: "Attempts",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Picks",
                table: "TrainingAttempts");

            migrationBuilder.DropColumn(
                name: "Picks",
                table: "MatchAnswers");

            migrationBuilder.DropColumn(
                name: "Part",
                table: "DailyQuestions");

            migrationBuilder.DropColumn(
                name: "Picks",
                table: "Attempts");
        }
    }
}
