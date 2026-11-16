using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <summary>
    /// Brings the seeded YourClassical (MPR) streams in line with yourclassical.org/streams as of
    /// November 2026: Hygge and Focus are gone (their URLs 404), the Holiday stream moved from
    /// holiday_yc.aac to holiday.aac, three stations were renamed, and Celebrate the Maestro,
    /// Hispanic Heritage Month and Pixel &amp; Plot are new. Rows are matched by URL so streams a
    /// household added or renamed itself are left alone, and a new stream is only added when no
    /// stream from its host is saved already.
    /// </summary>
    public partial class UpdateYourClassicalStreams : Migration
    {
        private const string CreatedAt = "2026-11-16 00:00:00";

        private static readonly (string Name, string Url)[] removed =
        [
            ("Hygge", "https://hygge.stream.publicradio.org/hygge.aac"),
            ("Focus", "https://focus.stream.publicradio.org/focus.aac"),
        ];

        private static readonly (string Url, string OldName, string NewName)[] renamed =
        [
            ("https://ycradio.stream.publicradio.org/ycradio.aac", "Your Classical Radio", "YourClassical Radio"),
            ("https://favorites.stream.publicradio.org/favorites.aac", "Classical Favorites", "Essentials"),
            ("https://lullabies.stream.publicradio.org/lullabies.aac", "Lullabies", "Sleep"),
            ("https://classicalkids.stream.publicradio.org/classicalkids.aac", "Classical Kids", "YourClassical Children"),
        ];

        private static readonly (string Name, string Host, string Url)[] added =
        [
            ("Celebrate the Maestro", "celebratethemaestro.stream.publicradio.org", "https://celebratethemaestro.stream.publicradio.org/celebratethemaestro.aac"),
            ("Hispanic Heritage Month", "hispanicheritage.stream.publicradio.org", "https://hispanicheritage.stream.publicradio.org/hispanicheritage.aac"),
            ("Pixel & Plot", "pixelplot.stream.publicradio.org", "https://pixelplot.stream.publicradio.org/pixelplot.aac"),
        ];

        private const string OldHolidayUrl = "https://holiday.stream.publicradio.org/holiday_yc.aac";
        private const string NewHolidayUrl = "https://holiday.stream.publicradio.org/holiday.aac";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (_, url) in removed)
            {
                migrationBuilder.Sql($"DELETE FROM AutoPlaySources WHERE RadioStreamId IN (SELECT Id FROM RadioStreams WHERE Url = '{url}');");
                migrationBuilder.Sql($"DELETE FROM RadioStreams WHERE Url = '{url}';");
            }

            migrationBuilder.Sql($"UPDATE RadioStreams SET Url = '{NewHolidayUrl}' WHERE Url = '{OldHolidayUrl}';");

            foreach (var (url, oldName, newName) in renamed)
            {
                migrationBuilder.Sql($"UPDATE RadioStreams SET Name = '{newName}' WHERE Url = '{url}' AND Name = '{oldName}';");
            }

            foreach (var (name, host, url) in added)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO RadioStreams (Name, Url, FaviconFileName, PlayCount, DisplayOrder, CreatedAt, LastPlayedAt)
                    SELECT '{name}', '{url}', NULL, 0, (SELECT COALESCE(MAX(DisplayOrder), 0) + 1 FROM RadioStreams), '{CreatedAt}', NULL
                    WHERE NOT EXISTS (SELECT 1 FROM RadioStreams WHERE Url LIKE 'https://{host}/%' OR Url LIKE 'http://{host}/%');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (_, _, url) in added)
            {
                migrationBuilder.Sql($"DELETE FROM AutoPlaySources WHERE RadioStreamId IN (SELECT Id FROM RadioStreams WHERE Url = '{url}' AND CreatedAt = '{CreatedAt}');");
                migrationBuilder.Sql($"DELETE FROM RadioStreams WHERE Url = '{url}' AND CreatedAt = '{CreatedAt}';");
            }

            foreach (var (url, oldName, newName) in renamed)
            {
                migrationBuilder.Sql($"UPDATE RadioStreams SET Name = '{oldName}' WHERE Url = '{url}' AND Name = '{newName}';");
            }

            migrationBuilder.Sql($"UPDATE RadioStreams SET Url = '{OldHolidayUrl}' WHERE Url = '{NewHolidayUrl}';");

            foreach (var (name, url) in removed)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO RadioStreams (Name, Url, FaviconFileName, PlayCount, DisplayOrder, CreatedAt, LastPlayedAt)
                    SELECT '{name}', '{url}', NULL, 0, (SELECT COALESCE(MAX(DisplayOrder), 0) + 1 FROM RadioStreams), '2026-03-17 00:00:00', NULL
                    WHERE NOT EXISTS (SELECT 1 FROM RadioStreams WHERE Url = '{url}');
                    """);
            }
        }
    }
}
