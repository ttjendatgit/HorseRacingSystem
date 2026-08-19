using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueRoundNumberPerTournament : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rounds_TournamentId",
                table: "Rounds");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_TournamentId_RoundNumber",
                table: "Rounds",
                columns: new[] { "TournamentId", "RoundNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rounds_TournamentId_RoundNumber",
                table: "Rounds");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_TournamentId",
                table: "Rounds",
                column: "TournamentId");
        }
    }
}
