using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Peildatum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AsOf",
                table: "QuizItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AsOf",
                table: "Players",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsOf",
                table: "QuizItems");

            migrationBuilder.DropColumn(
                name: "AsOf",
                table: "Players");
        }
    }
}
