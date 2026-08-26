using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class PrizeTournamentPositionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prizes_TournamentId",
                table: "Prizes");

            migrationBuilder.CreateIndex(
                name: "IX_Prizes_TournamentId_Position_Active",
                table: "Prizes",
                columns: new[] { "TournamentId", "Position" },
                unique: true,
                filter: "\"TournamentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prizes_TournamentId_Position_Active",
                table: "Prizes");

            migrationBuilder.CreateIndex(
                name: "IX_Prizes_TournamentId",
                table: "Prizes",
                column: "TournamentId");
        }
    }
}
