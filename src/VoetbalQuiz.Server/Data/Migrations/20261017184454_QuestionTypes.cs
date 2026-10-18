using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuestionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyQuestions_Players_PlayerId",
                table: "DailyQuestions");

            migrationBuilder.AddColumn<int[]>(
                name: "Types",
                table: "SpecialEvents",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "DailyQuestions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<List<int>>(
                name: "Blanks",
                table: "DailyQuestions",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.AddColumn<int>(
                name: "QuizItemId",
                table: "DailyQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "DailyQuestions",
                type: "text",
                nullable: false,
                defaultValue: "WieBenIk");

            migrationBuilder.AddColumn<int>(
                name: "Choice",
                table: "Attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "Found",
                table: "Attempts",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.CreateTable(
                name: "DailySchedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySchedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuizItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Sources = table.Column<List<string>>(type: "text[]", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    CheckedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    EditedInAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyQuestions_QuizItemId",
                table: "DailyQuestions",
                column: "QuizItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySchedule_Day",
                table: "DailySchedule",
                column: "Day",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizItems_Type_Key",
                table: "QuizItems",
                columns: new[] { "Type", "Key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyQuestions_Players_PlayerId",
                table: "DailyQuestions",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyQuestions_QuizItems_QuizItemId",
                table: "DailyQuestions",
                column: "QuizItemId",
                principalTable: "QuizItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyQuestions_Players_PlayerId",
                table: "DailyQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyQuestions_QuizItems_QuizItemId",
                table: "DailyQuestions");

            migrationBuilder.DropTable(
                name: "DailySchedule");

            migrationBuilder.DropTable(
                name: "QuizItems");

            migrationBuilder.DropIndex(
                name: "IX_DailyQuestions_QuizItemId",
                table: "DailyQuestions");

            migrationBuilder.DropColumn(
                name: "Types",
                table: "SpecialEvents");

            migrationBuilder.DropColumn(
                name: "Blanks",
                table: "DailyQuestions");

            migrationBuilder.DropColumn(
                name: "QuizItemId",
                table: "DailyQuestions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "DailyQuestions");

            migrationBuilder.DropColumn(
                name: "Choice",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "Found",
                table: "Attempts");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "DailyQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyQuestions_Players_PlayerId",
                table: "DailyQuestions",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
