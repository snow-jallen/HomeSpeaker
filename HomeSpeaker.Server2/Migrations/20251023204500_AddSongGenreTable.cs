using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations;

/// <inheritdoc />
public partial class AddSongGenreTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SongGenres",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SongPath = table.Column<string>(type: "TEXT", nullable: false),
                Genre = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SongGenres", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SongGenres_SongPath",
            table: "SongGenres",
            column: "SongPath");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SongGenres");
    }
}
