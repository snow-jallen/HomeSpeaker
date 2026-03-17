using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddRadioStreams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RadioStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    FaviconFileName = table.Column<string>(type: "TEXT", nullable: true),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPlayedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadioStreams", x => x.Id);
                });

            // Seed the 22 existing streams
            migrationBuilder.InsertData(
                table: "RadioStreams",
                columns: new[] { "Name", "Url", "FaviconFileName", "PlayCount", "DisplayOrder", "CreatedAt", "LastPlayedAt" },
                values: new object[,]
                {
                    { "Church Music Stream", "https://nmcdn-lds.msvdn.net/icecastRelay/101156/GvaVK70/icecast?rnd=637109878513586401", null, 0, 1, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Tabernacle Choir Stream", "https://nmcdn-lds.msvdn.net/icecastRelay/101158/3nGepF3/icecast?rnd=637109879815090752", null, 0, 2, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Canal Espanol", "https://nmcdn-lds.msvdn.net/icecastRelay/101157/V2Pm3WE/icecast?rnd=637109879429639917", null, 0, 3, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "KBAQ", "https://kbaq.streamguys1.com/kbaq_mp3_128", null, 0, 4, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Your Classical Radio", "https://ycradio.stream.publicradio.org/ycradio.aac", null, 0, 5, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "YourClassical MPR", "https://cms.stream.publicradio.org/cms.aac", null, 0, 6, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Relax Radio", "https://relax.stream.publicradio.org/relax.aac", null, 0, 7, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Lullabies", "https://lullabies.stream.publicradio.org/lullabies.aac", null, 0, 8, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Choral Stream", "https://choral.stream.publicradio.org/choral.aac", null, 0, 9, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Classical Favorites", "https://favorites.stream.publicradio.org/favorites.aac", null, 0, 10, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Chamber Music", "https://chambermusic.stream.publicradio.org/chambermusic.aac", null, 0, 11, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Peaceful Piano", "https://peacefulpiano.stream.publicradio.org/peacefulpiano.aac", null, 0, 12, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Hygge", "https://hygge.stream.publicradio.org/hygge.aac", null, 0, 13, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Focus", "https://focus.stream.publicradio.org/focus.aac", null, 0, 14, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Concert Band", "https://concertband.stream.publicradio.org/concertband.aac", null, 0, 15, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Classical Kids", "https://classicalkids.stream.publicradio.org/classicalkids.aac", null, 0, 16, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Holiday Stream", "https://holiday.stream.publicradio.org/holiday_yc.aac", null, 0, 17, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Classical 89", "https://radio.byub.org/classical89/classical89_aac", null, 0, 18, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Guitar Stream", "https://guitar.stream.publicradio.org/guitar.aac", null, 0, 19, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Madrigals", "https://streams.calmradio.com:14228/stream", null, 0, 20, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Radio Clasique", "https://listen.radioking.com/radio/228241/stream/271810", null, 0, 21, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Positively Baroque", "https://play.organlive.com:7020/128", null, 0, 22, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null },
                    { "Adagio Radio (Madrid)", "https://stream.tunerplay.com/listen/adagioradio/adagioradio.mp3", null, 0, 23, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RadioStreams");
        }
    }
}
