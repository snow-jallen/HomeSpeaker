using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // Convert to file-scoped namespace
#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class RebuildAnchors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old AnchorDefinitions / UserAnchors / DailyAnchors tables are left in place on purpose:
            // LegacyAnchorImporter reads them after migrations run, copies them into these tables and then drops them.
            migrationBuilder.CreateTable(
                name: "AnchorPeople",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FeedPersonId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnchorPeople", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Anchors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anchors_AnchorPeople_PersonId",
                        column: x => x.PersonId,
                        principalTable: "AnchorPeople",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnchorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnchorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Done = table.Column<bool>(type: "INTEGER", nullable: false),
                    DoneAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ClientUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnchorLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnchorLogs_Anchors_AnchorId",
                        column: x => x.AnchorId,
                        principalTable: "Anchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnchorPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnchorId = table.Column<int>(type: "INTEGER", nullable: false),
                    From = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    To = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Days = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnchorPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnchorPeriods_Anchors_AnchorId",
                        column: x => x.AnchorId,
                        principalTable: "Anchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnchorLogs_AnchorId_Date",
                table: "AnchorLogs",
                columns: new[] { "AnchorId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnchorLogs_Date",
                table: "AnchorLogs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AnchorPeriods_AnchorId_From",
                table: "AnchorPeriods",
                columns: new[] { "AnchorId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_Anchors_PersonId_SortOrder",
                table: "Anchors",
                columns: new[] { "PersonId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnchorLogs");

            migrationBuilder.DropTable(
                name: "AnchorPeriods");

            migrationBuilder.DropTable(
                name: "Anchors");

            migrationBuilder.DropTable(
                name: "AnchorPeople");
        }
    }
}
