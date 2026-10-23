using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class GoldenBallCeremonySeen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "GoldenBallSeenMonth",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            // Bestaande spelers zagen de Gouden Bal van september al op Home; de eerste uitreiking is die van oktober.
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "GoldenBallSeenMonth" = DATE '2026-09-01';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoldenBallSeenMonth",
                table: "AspNetUsers");
        }
    }
}
