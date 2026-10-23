using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class KaartLand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KaartCountry",
                table: "SpecialEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KaartCountry",
                table: "DailySchedule",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KaartCountry",
                table: "SpecialEvents");

            migrationBuilder.DropColumn(
                name: "KaartCountry",
                table: "DailySchedule");
        }
    }
}
