using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DerbyMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FlareAt",
                table: "MatchQuestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlareUserId",
                table: "MatchQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwapUserId",
                table: "MatchQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrumpUserId",
                table: "MatchQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weapon",
                table: "MatchPlayers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeaponEffect",
                table: "MatchPlayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeaponQuestion",
                table: "MatchPlayers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeaponTargetUserId",
                table: "MatchPlayers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeaponUsedAt",
                table: "MatchPlayers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Derby",
                table: "Matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RedCardFromUserId",
                table: "MatchAnswers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlareAt",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "FlareUserId",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "SwapUserId",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "TrumpUserId",
                table: "MatchQuestions");

            migrationBuilder.DropColumn(
                name: "Weapon",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "WeaponEffect",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "WeaponQuestion",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "WeaponTargetUserId",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "WeaponUsedAt",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "Derby",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RedCardFromUserId",
                table: "MatchAnswers");
        }
    }
}
