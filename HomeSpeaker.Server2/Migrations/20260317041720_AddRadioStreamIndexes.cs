using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // Convert to file-scoped namespace
#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddRadioStreamIndexes : Migration
    {
        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1825:Avoid unnecessary zero-length array allocations", Justification = "EF Core migration generated code")]
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RadioStreams_DisplayOrder",
                table: "RadioStreams",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_RadioStreams_Name",
                table: "RadioStreams",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RadioStreams_PlayCount",
                table: "RadioStreams",
                column: "PlayCount",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RadioStreams_DisplayOrder",
                table: "RadioStreams");

            migrationBuilder.DropIndex(
                name: "IX_RadioStreams_Name",
                table: "RadioStreams");

            migrationBuilder.DropIndex(
                name: "IX_RadioStreams_PlayCount",
                table: "RadioStreams");
        }
    }
}