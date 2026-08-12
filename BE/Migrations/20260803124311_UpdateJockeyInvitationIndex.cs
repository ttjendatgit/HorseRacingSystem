using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class UpdateJockeyInvitationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JockeyInvitations_HorseId_JockeyId",
                table: "JockeyInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_JockeyInvitations_HorseId_JockeyId_RaceId",
                table: "JockeyInvitations",
                columns: new[] { "HorseId", "JockeyId", "RaceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JockeyInvitations_HorseId_JockeyId_RaceId",
                table: "JockeyInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_JockeyInvitations_HorseId_JockeyId",
                table: "JockeyInvitations",
                columns: new[] { "HorseId", "JockeyId" },
                unique: true);
        }
    }
}
