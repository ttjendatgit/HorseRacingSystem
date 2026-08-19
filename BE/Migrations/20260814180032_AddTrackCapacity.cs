using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Tracks",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Tracks");
        }
    }
}
