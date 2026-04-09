using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // Convert to file-scoped namespace
#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlaylistItems_PlaylistId",
                table: "PlaylistItems");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnchors_UserId_AnchorDefinitionId",
                table: "UserAnchors",
                columns: new[] { "UserId", "AnchorDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_Artist_Album",
                table: "Thumbnails",
                columns: new[] { "Artist", "Album" });

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_Name",
                table: "Playlists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_Order",
                table: "PlaylistItems",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_SongPath",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "SongPath" });

            migrationBuilder.CreateIndex(
                name: "IX_Impressions_PlayedBy",
                table: "Impressions",
                column: "PlayedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Impressions_SongPath",
                table: "Impressions",
                column: "SongPath");

            migrationBuilder.CreateIndex(
                name: "IX_Impressions_Timestamp",
                table: "Impressions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAnchors_Date_IsCompleted",
                table: "DailyAnchors",
                columns: new[] { "Date", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyAnchors_UserId_Date",
                table: "DailyAnchors",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AnchorDefinitions_IsActive_Name",
                table: "AnchorDefinitions",
                columns: new[] { "IsActive", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAnchors_UserId_AnchorDefinitionId",
                table: "UserAnchors");

            migrationBuilder.DropIndex(
                name: "IX_Thumbnails_Artist_Album",
                table: "Thumbnails");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_Name",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_PlaylistItems_Order",
                table: "PlaylistItems");

            migrationBuilder.DropIndex(
                name: "IX_PlaylistItems_PlaylistId_SongPath",
                table: "PlaylistItems");

            migrationBuilder.DropIndex(
                name: "IX_Impressions_PlayedBy",
                table: "Impressions");

            migrationBuilder.DropIndex(
                name: "IX_Impressions_SongPath",
                table: "Impressions");

            migrationBuilder.DropIndex(
                name: "IX_Impressions_Timestamp",
                table: "Impressions");

            migrationBuilder.DropIndex(
                name: "IX_DailyAnchors_Date_IsCompleted",
                table: "DailyAnchors");

            migrationBuilder.DropIndex(
                name: "IX_DailyAnchors_UserId_Date",
                table: "DailyAnchors");

            migrationBuilder.DropIndex(
                name: "IX_AnchorDefinitions_IsActive_Name",
                table: "AnchorDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId",
                table: "PlaylistItems",
                column: "PlaylistId");
        }
    }
}