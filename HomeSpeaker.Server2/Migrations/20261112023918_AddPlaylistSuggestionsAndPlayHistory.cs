using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistSuggestionsAndPlayHistory : Migration
    {
        private static readonly string[] suggestionUniquenessColumns = { "PlaylistName", "SongPath" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoKind",
                table: "Playlists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SecondsHeard",
                table: "Impressions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Skipped",
                table: "Impressions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlaylistSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlaylistName = table.Column<string>(type: "TEXT", nullable: false),
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistSuggestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistSuggestions_PlaylistName_SongPath",
                table: "PlaylistSuggestions",
                columns: suggestionUniquenessColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaylistSuggestions");

            migrationBuilder.DropColumn(
                name: "AutoKind",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "SecondsHeard",
                table: "Impressions");

            migrationBuilder.DropColumn(
                name: "Skipped",
                table: "Impressions");
        }
    }
}
