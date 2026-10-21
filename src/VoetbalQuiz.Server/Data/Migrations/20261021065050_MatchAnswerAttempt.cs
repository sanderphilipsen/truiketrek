using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class MatchAnswerAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Guesses",
                table: "MatchAnswers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "MatchAnswers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Bestaande antwoorden: de start van hun vraag (de klok online is gedeeld), anders de aanmaak van de wedstrijd.
            // Guesses blijft 0: oude antwoorden zijn afgerekend; een lopende vraag telt vanaf nu.
            migrationBuilder.Sql("""
                UPDATE "MatchAnswers" AS a
                SET "StartedAt" = COALESCE(q."StartsAt", m."CreatedAt")
                FROM "MatchQuestions" AS q
                JOIN "Matches" AS m ON m."Id" = q."MatchId"
                WHERE a."MatchQuestionId" = q."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Guesses",
                table: "MatchAnswers");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "MatchAnswers");
        }
    }
}
