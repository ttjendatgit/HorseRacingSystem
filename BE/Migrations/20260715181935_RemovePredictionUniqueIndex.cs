using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class RemovePredictionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Predictions_RaceId_SpectatorUserId",
                table: "Predictions");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_RaceId",
                table: "Predictions",
                column: "RaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Predictions_RaceId",
                table: "Predictions");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_RaceId_SpectatorUserId",
                table: "Predictions",
                columns: new[] { "RaceId", "SpectatorUserId" },
                unique: true);
        }
    }
}
