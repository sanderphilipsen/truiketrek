using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class HomeRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeRank",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HomeRankDate",
                table: "AspNetUsers",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeRank",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HomeRankDate",
                table: "AspNetUsers");
        }
    }
}
