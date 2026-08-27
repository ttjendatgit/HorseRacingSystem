using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class UpdateViolationPenaltyStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Penalty",
                table: "ViolationRecords");

            migrationBuilder.AddColumn<int>(
                name: "PenaltyTimeSeconds",
                table: "ViolationRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyType",
                table: "ViolationRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenaltyTimeSeconds",
                table: "ViolationRecords");

            migrationBuilder.DropColumn(
                name: "PenaltyType",
                table: "ViolationRecords");

            migrationBuilder.AddColumn<string>(
                name: "Penalty",
                table: "ViolationRecords",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
