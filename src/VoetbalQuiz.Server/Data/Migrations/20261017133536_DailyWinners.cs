using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyWinners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                table: "DailyChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerPoints",
                table: "DailyChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerUserId",
                table: "DailyChallenges",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyChallenges_WinnerUserId",
                table: "DailyChallenges",
                column: "WinnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyChallenges_AspNetUsers_WinnerUserId",
                table: "DailyChallenges",
                column: "WinnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyChallenges_AspNetUsers_WinnerUserId",
                table: "DailyChallenges");

            migrationBuilder.DropIndex(
                name: "IX_DailyChallenges_WinnerUserId",
                table: "DailyChallenges");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                table: "DailyChallenges");

            migrationBuilder.DropColumn(
                name: "WinnerPoints",
                table: "DailyChallenges");

            migrationBuilder.DropColumn(
                name: "WinnerUserId",
                table: "DailyChallenges");
        }
    }
}
