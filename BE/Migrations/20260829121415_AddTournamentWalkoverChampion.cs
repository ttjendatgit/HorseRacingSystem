using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentWalkoverChampion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChampionHorseId",
                table: "Tournaments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinishReason",
                table: "Tournaments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_ChampionHorseId",
                table: "Tournaments",
                column: "ChampionHorseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Horses_ChampionHorseId",
                table: "Tournaments",
                column: "ChampionHorseId",
                principalTable: "Horses",
                principalColumn: "HorseID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Horses_ChampionHorseId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_ChampionHorseId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ChampionHorseId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "FinishReason",
                table: "Tournaments");
        }
    }
}
