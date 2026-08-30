using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddJockeySharePercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "JockeyAmount",
                table: "PrizeDistributionLogs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "JockeyId",
                table: "PrizeDistributionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "JockeySharePercentage",
                table: "PrizeDistributionLogs",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "JockeyUserId",
                table: "PrizeDistributionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OwnerAmount",
                table: "PrizeDistributionLogs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "JockeySharePercentage",
                table: "JockeyInvitations",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JockeyAmount",
                table: "PrizeDistributionLogs");

            migrationBuilder.DropColumn(
                name: "JockeyId",
                table: "PrizeDistributionLogs");

            migrationBuilder.DropColumn(
                name: "JockeySharePercentage",
                table: "PrizeDistributionLogs");

            migrationBuilder.DropColumn(
                name: "JockeyUserId",
                table: "PrizeDistributionLogs");

            migrationBuilder.DropColumn(
                name: "OwnerAmount",
                table: "PrizeDistributionLogs");

            migrationBuilder.DropColumn(
                name: "JockeySharePercentage",
                table: "JockeyInvitations");
        }
    }
}
