using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateJockeyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the newest row for each horse/jockey pair so the unique index can
            // also repair databases that already contain duplicate invitations.
            migrationBuilder.Sql(
                """
                DELETE FROM "JockeyInvitations"
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "HorseId", "JockeyId"
                                   ORDER BY "CreatedAt" DESC, "Id" DESC
                               ) AS row_number
                        FROM "JockeyInvitations"
                    ) duplicates
                    WHERE row_number > 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_JockeyInvitations_HorseId",
                table: "JockeyInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_JockeyInvitations_HorseId_JockeyId",
                table: "JockeyInvitations",
                columns: new[] { "HorseId", "JockeyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JockeyInvitations_HorseId_JockeyId",
                table: "JockeyInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_JockeyInvitations_HorseId",
                table: "JockeyInvitations",
                column: "HorseId");
        }
    }
}
