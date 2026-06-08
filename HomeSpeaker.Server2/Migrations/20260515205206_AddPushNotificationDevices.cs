using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSpeaker.Server2.Migrations
{
    /// <inheritdoc />
    public partial class AddPushNotificationDevices : Migration
    {
        private static readonly string[] activePlatformColumns = { "IsActive", "Platform" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushNotificationAlertStates",
                columns: table => new
                {
                    AlertKey = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveStateKey = table.Column<string>(type: "TEXT", nullable: true),
                    LastSentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushNotificationAlertStates", x => x.AlertKey);
                });

            migrationBuilder.CreateTable(
                name: "PushNotificationDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstallationId = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceToken = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true),
                    TemperatureAlertsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BloodSugarAlertsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegisteredUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastNotificationAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSuccessfulNotificationUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushNotificationDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDevices_DeviceToken",
                table: "PushNotificationDevices",
                column: "DeviceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDevices_InstallationId",
                table: "PushNotificationDevices",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDevices_IsActive_Platform",
                table: "PushNotificationDevices",
                columns: activePlatformColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushNotificationAlertStates");

            migrationBuilder.DropTable(
                name: "PushNotificationDevices");
        }
    }
}
