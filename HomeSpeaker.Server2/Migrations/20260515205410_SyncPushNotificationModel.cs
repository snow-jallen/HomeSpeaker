using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class SyncPushNotificationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BundleId",
                table: "PushNotificationDevices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceTokenUpdatedUtc",
                table: "PushNotificationDevices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BundleId",
                table: "PushNotificationDevices");

            migrationBuilder.DropColumn(
                name: "DeviceTokenUpdatedUtc",
                table: "PushNotificationDevices");
        }
    }
}
