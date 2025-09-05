using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations;

/// <inheritdoc />
public partial class RemovePlaylistDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Playlists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Playlists",
                type: "TEXT",
                nullable: true);
        }
}
