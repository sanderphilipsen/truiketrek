using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerReviewAndSpecials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyChallenges_Date",
                table: "DailyChallenges");

            // Active verdwijnt (vervangen door Status); EditedInAdmin is nieuw en start op false.
            migrationBuilder.DropColumn(
                name: "Active",
                table: "Players");

            migrationBuilder.AddColumn<bool>(
                name: "EditedInAdmin",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CheckedAt",
                table: "Players",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Sources",
                table: "Players",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "Review");

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                table: "Players",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialEventId",
                table: "DailyChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecialEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    PlayerIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyChallenges_Date",
                table: "DailyChallenges",
                column: "Date",
                unique: true,
                filter: "\"SpecialEventId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyChallenges_SpecialEventId",
                table: "DailyChallenges",
                column: "SpecialEventId",
                unique: true,
                filter: "\"SpecialEventId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyChallenges_SpecialEvents_SpecialEventId",
                table: "DailyChallenges",
                column: "SpecialEventId",
                principalTable: "SpecialEvents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyChallenges_SpecialEvents_SpecialEventId",
                table: "DailyChallenges");

            migrationBuilder.DropTable(
                name: "SpecialEvents");

            migrationBuilder.DropIndex(
                name: "IX_DailyChallenges_Date",
                table: "DailyChallenges");

            migrationBuilder.DropIndex(
                name: "IX_DailyChallenges_SpecialEventId",
                table: "DailyChallenges");

            migrationBuilder.DropColumn(
                name: "CheckedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Sources",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SpecialEventId",
                table: "DailyChallenges");

            migrationBuilder.DropColumn(
                name: "EditedInAdmin",
                table: "Players");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyChallenges_Date",
                table: "DailyChallenges",
                column: "Date",
                unique: true);
        }
    }
}
