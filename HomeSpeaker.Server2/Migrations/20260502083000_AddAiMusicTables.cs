using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMusicTables : Migration
    {
        private static readonly string[] aiPlaybackSessionsIsActiveStartedUtcColumns = { "IsActive", "StartedUtc" };
        private static readonly string[] aiProcessingWorkItemsStatusLeaseExpiresUtcColumns = { "Status", "LeaseExpiresUtc" };
        private static readonly string[] aiTrackMarkersSongPathMarkerKeyColumns = { "SongPath", "MarkerKey" };
        private static readonly string[] aiTrackProfilesStatusLastAnalyzedUtcColumns = { "Status", "LastAnalyzedUtc" };
        private static readonly string[] aiTrackSimilaritiesSongPathScoreColumns = { "SongPath", "Score" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiGenreDefinitions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGenreDefinitions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AiPlaybackSessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    GenreKey = table.Column<string>(type: "TEXT", nullable: true),
                    SeedSongPath = table.Column<string>(type: "TEXT", nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAdvancedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    TotalTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    QueuedTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessingTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentBatchId = table.Column<string>(type: "TEXT", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastScanUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    BatchId = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    QueuedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
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
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
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
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    MarkerKey = table.Column<string>(type: "TEXT", nullable: false),
                    MarkerValue = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false)
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
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    AnalysisVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    LastAnalyzedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    TempoLabel = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryMood = table.Column<string>(type: "TEXT", nullable: true),
                    Energy = table.Column<double>(type: "REAL", nullable: false),
                    Acousticness = table.Column<double>(type: "REAL", nullable: false),
                    Instrumentalness = table.Column<double>(type: "REAL", nullable: false),
                    VocalPresence = table.Column<double>(type: "REAL", nullable: false),
                    Sacredness = table.Column<double>(type: "REAL", nullable: false),
                    SeasonalityChristmas = table.Column<double>(type: "REAL", nullable: false),
                    Danceability = table.Column<double>(type: "REAL", nullable: false),
                    Warmth = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false)
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
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    ReasonsJson = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTrackSimilarities", x => new { x.SongPath, x.SimilarSongPath });
                });

            migrationBuilder.CreateTable(
                name: "AiPlaybackFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongPath = table.Column<string>(type: "TEXT", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousSongPath = table.Column<string>(type: "TEXT", nullable: true),
                    GenreKey = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPlaybackFeedbacks", x => x.Id);
                });

            var genreColumns = new[] { "Key", "Description", "DisplayName", "IsActive", "SortOrder" };

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "peaceful-instrumental", "Calm instrumental tracks for quiet moments.", "Peaceful Instrumental", true, 1 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "quiet-sunday", "Gentle vocals and soft arrangements for restful days.", "Quiet Sunday", true, 2 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "driving-tunes", "Steady rhythm and forward momentum for the road.", "Driving Tunes", true, 3 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "choral", "Choral harmonies and choir-led arrangements.", "Choral", true, 4 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "upbeat-a-cappella", "Vocal-driven, energetic a cappella performances.", "Upbeat A Cappella", true, 5 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "country", "Country storytelling with warm acoustic textures.", "Country", true, 6 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "quiet-classical", "Soft classical pieces and reflective orchestral work.", "Quiet Classical", true, 7 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "church-christmas", "Traditional church Christmas recordings and arrangements.", "Church Christmas", true, 8 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "hymns", "Classic hymns and worship standards.", "Hymns", true, 9 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "classical-christmas", "Classical takes on holiday repertoire.", "Classical Christmas", true, 10 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "vocal-christmas", "Vocal-forward holiday performances.", "Vocal Christmas", true, 11 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "worship-ensemble", "Full-band worship and ensemble recordings.", "Worship Ensemble", true, 12 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "reflective-piano", "Solo piano and reflective keys-driven pieces.", "Reflective Piano", true, 13 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "family-singalong", "Upbeat, communal songs for family listening.", "Family Singalong", true, 14 });

            migrationBuilder.InsertData(
                table: "AiGenreDefinitions",
                columns: genreColumns,
                values: new object[] { "warm-folk-acoustic", "Warm acoustic folk with organic instrumentation.", "Warm Folk Acoustic", true, 15 });

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
                columns: aiPlaybackSessionsIsActiveStartedUtcColumns);

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
                columns: aiProcessingWorkItemsStatusLeaseExpiresUtcColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackGenreScores_GenreKey",
                table: "AiTrackGenreScores",
                column: "GenreKey");

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackMarkers_SongPath_MarkerKey",
                table: "AiTrackMarkers",
                columns: aiTrackMarkersSongPathMarkerKeyColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackProfiles_Status_LastAnalyzedUtc",
                table: "AiTrackProfiles",
                columns: aiTrackProfilesStatusLastAnalyzedUtcColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AiTrackSimilarities_SongPath_Score",
                table: "AiTrackSimilarities",
                columns: aiTrackSimilaritiesSongPathScoreColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiPlaybackFeedbacks");

            migrationBuilder.DropTable(
                name: "AiTrackSimilarities");

            migrationBuilder.DropTable(
                name: "AiTrackProfiles");

            migrationBuilder.DropTable(
                name: "AiTrackMarkers");

            migrationBuilder.DropTable(
                name: "AiTrackGenreScores");

            migrationBuilder.DropTable(
                name: "AiProcessingWorkItems");

            migrationBuilder.DropTable(
                name: "AiPlaybackSessions");

            migrationBuilder.DropTable(
                name: "AiProcessingRuns");

            migrationBuilder.DropTable(
                name: "AiGenreDefinitions");
        }
    }
}
