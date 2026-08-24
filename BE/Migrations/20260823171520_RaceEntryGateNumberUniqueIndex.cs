using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class RaceEntryGateNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RaceEntries_RaceId_GateNumber_Active",
                table: "RaceEntries",
                columns: new[] { "RaceId", "GateNumber" },
                unique: true,
                filter: "\"GateNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RaceEntries_RaceId_GateNumber_Active",
                table: "RaceEntries");
        }
    }
}
