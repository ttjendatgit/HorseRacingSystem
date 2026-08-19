using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class EnforceActiveTournamentRegistrationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Task B Final: the pre-existing full single-column indexes (HorseId, OwnerId) are
            // kept — the new indexes below are PARTIAL (WHERE "Status" IN (1, 2)) and cannot
            // serve queries that include Rejected/Withdrawn registrations, so they do not fully
            // replace the originals.
            migrationBuilder.CreateIndex(
                name: "IX_TournamentHorseRegistrations_HorseId_TournamentId_Active",
                table: "TournamentHorseRegistrations",
                columns: new[] { "HorseId", "TournamentId" },
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentHorseRegistrations_OwnerId_TournamentId_Active",
                table: "TournamentHorseRegistrations",
                columns: new[] { "OwnerId", "TournamentId" },
                unique: true,
                filter: "\"Status\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only drop what Up() created — the full single-column indexes were never removed,
            // so Down() must not recreate them.
            migrationBuilder.DropIndex(
                name: "IX_TournamentHorseRegistrations_HorseId_TournamentId_Active",
                table: "TournamentHorseRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_TournamentHorseRegistrations_OwnerId_TournamentId_Active",
                table: "TournamentHorseRegistrations");
        }
    }
}
