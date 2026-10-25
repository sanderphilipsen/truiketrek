using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoetbalQuiz.Server.Data.Migrations
{
    /// <summary>
    /// Specials krijgen een lijnicoon (naam, zie SpecialIcons) in plaats van een emoji. De emoji van de sjablonen worden
    /// hun nieuwe icoon; een ander icoon (een zelf getypte emoji) wordt "sparkle".
    /// </summary>
    public partial class SpecialIconNames : Migration
    {
        private static readonly (string Emoji, string Icon)[] Map =
        [
            ("🔥", "flame"), ("⚔️", "sword"), ("⚔", "sword"), ("🦁", "flag-banner"), ("😈", "heart"), ("🏛️", "bank"), ("🏛", "bank"),
            ("🏆", "trophy"), ("⏰", "alarm"), ("⭐", "star"), ("🌟", "star"), ("🎄", "tree-evergreen"), ("🎆", "champagne"),
            ("🎃", "ghost"), ("🎁", "gift"), ("👟", "sneaker"), ("🏅", "medal"), ("⚽", "soccer-ball"), ("🌍", "globe"),
            ("👨‍👦", "users"), ("🤡", "mask-happy"), ("🔟", "hand"), ("👑", "crown"), ("🐐", "shooting-star"), ("🧤", "hand-palm"),
            ("💪", "hand-fist"), ("🎯", "target"), ("💜", "heart"), ("❤️", "heart"), ("🛡️", "shield"), ("🛡", "shield"),
            ("⚖️", "scales"), ("⚖", "scales"), ("🇧🇪", "flag"), ("🇫🇷", "flag"), ("🇪🇸", "flag"), ("🇮🇹", "flag"), ("🇩🇪", "flag"),
            ("🇳🇱", "flag"), ("🇹🇷", "flag"), ("🇵🇹", "flag"), ("🎉", "confetti"), ("📅", "calendar-blank"), ("⚡", "lightning"),
            ("✨", "sparkle"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (emoji, icon) in Map)
                migrationBuilder.Sql($"""UPDATE "SpecialEvents" SET "Icon" = '{icon}' WHERE TRIM("Icon") = '{emoji}';""");

            // SpecialIcons.All zoals bij deze migratie, bevroren: een latere wijziging aan die lijst verandert deze migratie niet.
            string[] known =
            [
                "sparkle", "star", "shooting-star", "trophy", "medal", "crown", "soccer-ball", "sneaker", "t-shirt", "target",
                "flame", "lightning", "sword", "shield", "scales", "hand", "hand-palm", "hand-fist", "heart", "users",
                "flag", "flag-banner", "globe", "bank", "alarm", "calendar-blank", "confetti", "gift", "champagne",
                "tree-evergreen", "ghost", "mask-happy",
            ];
            migrationBuilder.Sql(
                $"""UPDATE "SpecialEvents" SET "Icon" = 'sparkle' WHERE "Icon" NOT IN ({string.Join(", ", known.Select(i => $"'{i}'"))});""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Niet terug te draaien: welke emoji er stond, is niet bewaard. Een icoonnaam toont de client nog altijd als lijnicoon.
        }
    }
}
