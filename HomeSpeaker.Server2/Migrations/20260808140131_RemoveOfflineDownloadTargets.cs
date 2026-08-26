using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOfflineDownloadTargets : Migration
    {
        private static readonly string[] offlineTargetUniquenessColumns = { "TargetType", "ArtistName", "AlbumName", "SongPath" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfflineDownloadTargets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfflineDownloadTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlbumName = table.Column<string>(type: "TEXT", nullable: false),
                    ArtistName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineDownloadTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineDownloadTargets_TargetType_ArtistName_AlbumName_SongPath",
                table: "OfflineDownloadTargets",
                columns: offlineTargetUniquenessColumns,
                unique: true);
        }
    }
}
