using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnlineQuestionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchQuestions_Players_PlayerId",
                table: "MatchQuestions");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "MatchQuestions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<List<int>>(
                name: "Blanks",
                table: "MatchQuestions",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.AddColumn<List<int>>(
                name: "CounterPlayerIds",
                table: "MatchQuestions",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.AddColumn<int>(
                name: "QuizItemId",
                table: "MatchQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "MatchQuestions",
                type: "text",
                nullable: false,
                defaultValue: "WieBenIk");

            migrationBuilder.AddColumn<int[]>(
                name: "Types",
                table: "Matches",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<int>(
                name: "Choice",
                table: "MatchAnswers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "Found",
                table: "MatchAnswers",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.AddColumn<List<int>>(
                name: "Skipped",
                table: "MatchAnswers",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.CreateIndex(
                name: "IX_MatchQuestions_QuizItemId",
                table: "MatchQuestions",
                column: "QuizItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchQuestions_Players_PlayerId",
                table: "MatchQuestions",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchQuestions_QuizItems_QuizItemId",
                table: "MatchQuestions",
                column: "QuizItemId",
                principalTable: "QuizItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchQuestions_Players_PlayerId",
                table: "MatchQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchQuestions_QuizItems_QuizItemId",
                table: "MatchQuestions");

            migrationBuilder.DropIndex(
                name: "IX_MatchQuestions_QuizItemId",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "Blanks",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "CounterPlayerIds",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "QuizItemId",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "Types",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Choice",
                table: "MatchAnswers");

            migrationBuilder.DropColumn(
                name: "Found",
                table: "MatchAnswers");

            migrationBuilder.DropColumn(
                name: "Skipped",
                table: "MatchAnswers");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "MatchQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchQuestions_Players_PlayerId",
                table: "MatchQuestions",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
