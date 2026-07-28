using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations;

[DbContext(typeof(MusicContext))]
[Migration("20260728031500_AddAutoPlaySettings")]
public partial class AddAutoPlaySettings : Migration
{
    private static readonly string[] autoPlaySourceIndexColumns = { "AutoPlaySettingsId", "SortOrder" };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutoPlaySettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                VolumeLevel = table.Column<int>(type: "INTEGER", nullable: false),
                SilenceTimeoutMinutes = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoPlaySettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AutoPlaySources",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AutoPlaySettingsId = table.Column<int>(type: "INTEGER", nullable: false),
                SourceType = table.Column<string>(type: "TEXT", nullable: false),
                PlaylistName = table.Column<string>(type: "TEXT", nullable: true),
                RadioStreamId = table.Column<int>(type: "INTEGER", nullable: true),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoPlaySources", x => x.Id);
                table.ForeignKey(
                    name: "FK_AutoPlaySources_AutoPlaySettings_AutoPlaySettingsId",
                    column: x => x.AutoPlaySettingsId,
                    principalTable: "AutoPlaySettings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AutoPlaySources_AutoPlaySettingsId_SortOrder",
            table: "AutoPlaySources",
            columns: autoPlaySourceIndexColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AutoPlaySources");

        migrationBuilder.DropTable(
            name: "AutoPlaySettings");
    }
}
