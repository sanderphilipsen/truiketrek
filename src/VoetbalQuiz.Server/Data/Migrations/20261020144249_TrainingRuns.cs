using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrainingRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DailyChallengeId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRuns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingRuns_DailyChallenges_DailyChallengeId",
                        column: x => x.DailyChallengeId,
                        principalTable: "DailyChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrainingRunId = table.Column<int>(type: "integer", nullable: false),
                    DailyQuestionId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WrongGuesses = table.Column<int>(type: "integer", nullable: false),
                    LastGuess = table.Column<string>(type: "text", nullable: true),
                    Correct = table.Column<bool>(type: "boolean", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Guesses = table.Column<int>(type: "integer", nullable: false),
                    Choice = table.Column<int>(type: "integer", nullable: true),
                    Found = table.Column<List<int>>(type: "integer[]", nullable: false),
                    Skipped = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingAttempts_DailyQuestions_DailyQuestionId",
                        column: x => x.DailyQuestionId,
                        principalTable: "DailyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingAttempts_TrainingRuns_TrainingRunId",
                        column: x => x.TrainingRunId,
                        principalTable: "TrainingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingAttempts_DailyQuestionId",
                table: "TrainingAttempts",
                column: "DailyQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingAttempts_TrainingRunId_DailyQuestionId",
                table: "TrainingAttempts",
                columns: new[] { "TrainingRunId", "DailyQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRuns_DailyChallengeId",
                table: "TrainingRuns",
                column: "DailyChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRuns_UserId_DailyChallengeId",
                table: "TrainingRuns",
                columns: new[] { "UserId", "DailyChallengeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingAttempts");

            migrationBuilder.DropTable(
                name: "TrainingRuns");
        }
    }
}
