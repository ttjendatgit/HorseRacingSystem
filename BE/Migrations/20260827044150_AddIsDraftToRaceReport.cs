using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDraftToRaceReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "RaceReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "RaceReports");
        }
    }
}
