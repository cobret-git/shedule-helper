using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SheduleHelper.Core.Migrations
{
    /// <inheritdoc />
    public partial class DayStartAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both defaults are hand-set: the scaffolded ones were "" and false, which are the
            // generic per-type fallbacks rather than the entity's own defaults. "" is not a
            // DayStartAutomation name at all and would fail to materialize on read, and false
            // would leave an existing database disagreeing with UserSetting's own initializer
            // about whether tracking resumes. Both values below match that initializer: day-start
            // automation is opt-in because it writes attendance on the user's behalf, while
            // resuming a project is on by default because it only takes effect at a clock-in the
            // user asked for, and is visible and switchable the moment it happens.
            migrationBuilder.AddColumn<string>(
                name: "DayStartAutomation",
                table: "UserSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "Off");

            migrationBuilder.AddColumn<bool>(
                name: "ResumeTrackingOnClockIn",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "ProjectTimeLogs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayStartAutomation",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "ResumeTrackingOnClockIn",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "ProjectTimeLogs");
        }
    }
}
