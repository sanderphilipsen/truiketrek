using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <summary>Moeilijkheid van 1–3 naar 1–5 sterren: 1 → 1, 2 → 3, 3 → 5 (ook voor wat in het admin-paneel aangepast werd).</summary>
    public partial class DifficultyFiveStars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Players" SET "Difficulty" = "Difficulty" * 2 - 1 WHERE "Difficulty" BETWEEN 1 AND 3;""");
            migrationBuilder.Sql("""UPDATE "QuizItems" SET "Difficulty" = "Difficulty" * 2 - 1 WHERE "Difficulty" BETWEEN 1 AND 3;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Players" SET "Difficulty" = ("Difficulty" + 1) / 2;""");
            migrationBuilder.Sql("""UPDATE "QuizItems" SET "Difficulty" = ("Difficulty" + 1) / 2;""");
        }
    }
}
