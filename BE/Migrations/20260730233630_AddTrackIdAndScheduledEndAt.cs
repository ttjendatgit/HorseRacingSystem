using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackIdAndScheduledEndAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndAt",
                table: "Races",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrackId",
                table: "Races",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Races_TrackId",
                table: "Races",
                column: "TrackId");

            migrationBuilder.AddForeignKey(
                name: "FK_Races_Tracks_TrackId",
                table: "Races",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Races_Tracks_TrackId",
                table: "Races");

            migrationBuilder.DropIndex(
                name: "IX_Races_TrackId",
                table: "Races");

            migrationBuilder.DropColumn(
                name: "ScheduledEndAt",
                table: "Races");

            migrationBuilder.DropColumn(
                name: "TrackId",
                table: "Races");
        }
    }
}
