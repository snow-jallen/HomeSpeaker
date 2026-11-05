using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // Convert to file-scoped namespace
#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddAnchorFeedOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnchorFeedOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FeedAnchorId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Done = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClientUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnchorFeedOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnchorFeedOutbox_FeedAnchorId_Date",
                table: "AnchorFeedOutbox",
                columns: new[] { "FeedAnchorId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnchorFeedOutbox");
        }
    }
}
