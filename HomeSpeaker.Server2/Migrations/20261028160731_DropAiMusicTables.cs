using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class DropAiMusicTables : Migration
    {
        private static readonly string[] genreDefinitionColumns = { "Key", "Description", "DisplayName", "IsActive", "SortOrder" };
        private static readonly string[] playbackSessionActiveColumns = { "IsActive", "StartedUtc" };
        private static readonly string[] workItemLeaseColumns = { "Status", "LeaseExpiresUtc" };
        private static readonly string[] trackMarkerColumns = { "SongPath", "MarkerKey" };
        private static readonly string[] trackProfileStatusColumns = { "Status", "LastAnalyzedUtc" };
        private static readonly string[] trackSimilarityScoreColumns = { "SongPath", "Score" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiGenreDefinitions");

            migrationBuilder.DropTable(
                name: "AiPlaybackFeedbacks");

            migrationBuilder.DropTable(
                name: "AiPlaybackSessions");

            migrationBuilder.DropTable(
                name: "AiProcessingRuns");

            migrationBuilder.DropTable(
                name: "AiProcessingWorkItems");

            migrationBuilder.DropTable(
                name: "AiTrackGenreScores");

            migrationBuilder.DropTable(
                name: "AiTrackMarkers");

            migrationBuilder.DropTable(
                name: "AiTrackProfiles");

            migrationBuilder.DropTable(
                name: "AiTrackSimilarities");

            // The AI tables were most of the database (~76 MB of 94 MB on the kitchen Pi); SQLite only
            // gives the space back on VACUUM, which can't run inside the migration's transaction.
            migrationBuilder.Sql("VACUUM;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiGenreDefinitions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGenreDefinitions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AiPlaybackFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", nullable: false),
                    GenreKey = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousSongPath = table.Column<string>(type: "TEXT", nullable: true),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongPath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPlaybackFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiPlaybackSessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GenreKey = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastAdvancedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    SeedSongPath = table.Column<string>(type: "TEXT", nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPlaybackSessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "AiProcessingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompletedTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentBatchId = table.Column<string>(type: "TEXT", nullable: true),
                    FailedTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastScanUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessingTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    QueuedTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    TotalTracks = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProcessingRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiProcessingWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    BatchId = table.Column<string>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QueuedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProcessingWorkItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiTrackGenreScores",
                columns: table => new
                {
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    GenreKey = table.Column<string>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Why = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTrackGenreScores", x => new { x.SongPath, x.GenreKey });
                });

            migrationBuilder.CreateTable(
                name: "AiTrackMarkers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    MarkerKey = table.Column<string>(type: "TEXT", nullable: false),
                    MarkerValue = table.Column<double>(type: "REAL", nullable: false),
                    SongPath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTrackMarkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiTrackProfiles",
                columns: table => new
                {
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    Acousticness = table.Column<double>(type: "REAL", nullable: false),
                    AnalysisVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Danceability = table.Column<double>(type: "REAL", nullable: false),
                    Energy = table.Column<double>(type: "REAL", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    Instrumentalness = table.Column<double>(type: "REAL", nullable: false),
                    LastAnalyzedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryMood = table.Column<string>(type: "TEXT", nullable: true),
                    Sacredness = table.Column<double>(type: "REAL", nullable: false),
                    SeasonalityChristmas = table.Column<double>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    TempoLabel = table.Column<string>(type: "TEXT", nullable: true),
                    VocalPresence = table.Column<double>(type: "REAL", nullable: false),
                    Warmth = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTrackProfiles", x => x.SongPath);
                });

            migrationBuilder.CreateTable(
                name: "AiTrackSimilarities",
                columns: table => new
                {
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    SimilarSongPath = table.Column<string>(type: "TEXT", nullable: false),
                    ReasonsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTrackSimilarities", x => new { x.SongPath, x.SimilarSongPath });
                });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreDefinitionColumns,
                values: new object[,]
                {
                    { "choral", "Choral harmonies and choir-led arrangements.", "Choral", true, 4 },
                    { "church-christmas", "Traditional church Christmas recordings and arrangements.", "Church Christmas", true, 8 },
                    { "classical-christmas", "Classical takes on holiday repertoire.", "Classical Christmas", true, 10 },
                    { "country", "Country storytelling with warm acoustic textures.", "Country", true, 6 },
                    { "driving-tunes", "Steady rhythm and forward momentum for the road.", "Driving Tunes", true, 3 },
                    { "family-singalong", "Upbeat, communal songs for family listening.", "Family Singalong", true, 14 },
                    { "hymns", "Classic hymns and worship standards.", "Hymns", true, 9 },
                    { "peaceful-instrumental", "Calm instrumental tracks for quiet moments.", "Peaceful Instrumental", true, 1 },
                    { "quiet-classical", "Soft classical pieces and reflective orchestral work.", "Quiet Classical", true, 7 },
                    { "quiet-sunday", "Gentle vocals and soft arrangements for restful days.", "Quiet Sunday", true, 2 },
                    { "reflective-piano", "Solo piano and reflective keys-driven pieces.", "Reflective Piano", true, 13 },
                    { "upbeat-a-cappella", "Vocal-driven, energetic a cappella performances.", "Upbeat A Cappella", true, 5 },
                    { "vocal-christmas", "Vocal-forward holiday performances.", "Vocal Christmas", true, 11 },
                    { "warm-folk-acoustic", "Warm acoustic folk with organic instrumentation.", "Warm Folk Acoustic", true, 15 },
                    { "worship-ensemble", "Full-band worship and ensemble recordings.", "Worship Ensemble", true, 12 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenreDefinitions_SortOrder",
                table: "AiGenreDefinitions",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AiPlaybackFeedbacks_SessionId",
                table: "AiPlaybackFeedbacks",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiPlaybackFeedbacks_SongPath",
                table: "AiPlaybackFeedbacks",
                column: "SongPath");

            migrationBuilder.CreateIndex(
                name: "IX_AiPlaybackSessions_IsActive_StartedUtc",
                table: "AiPlaybackSessions",
                columns: playbackSessionActiveColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiProcessingRuns_State",
                table: "AiProcessingRuns",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_AiProcessingWorkItems_SongPath",
                table: "AiProcessingWorkItems",
                column: "SongPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiProcessingWorkItems_Status_LeaseExpiresUtc",
                table: "AiProcessingWorkItems",
                columns: workItemLeaseColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackGenreScores_GenreKey",
                table: "AiTrackGenreScores",
                column: "GenreKey");

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackMarkers_SongPath_MarkerKey",
                table: "AiTrackMarkers",
                columns: trackMarkerColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackProfiles_Status_LastAnalyzedUtc",
                table: "AiTrackProfiles",
                columns: trackProfileStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackSimilarities_SongPath_Score",
                table: "AiTrackSimilarities",
                columns: trackSimilarityScoreColumns);
        }
    }
}
