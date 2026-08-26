using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceComplaintWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaceComplaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiledByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedRefereeAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RefereeResponse = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RefereeRespondedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RuledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ruling = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AffectsResult = table.Column<bool>(type: "boolean", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceComplaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaceComplaints_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaceComplaints_RefereeAssignments_AssignedRefereeAssignment~",
                        column: x => x.AssignedRefereeAssignmentId,
                        principalTable: "RefereeAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaceComplaints_Users_FiledByUserId",
                        column: x => x.FiledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaceComplaints_Users_RuledByUserId",
                        column: x => x.RuledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaints_AssignedRefereeAssignmentId",
                table: "RaceComplaints",
                column: "AssignedRefereeAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaints_FiledByUserId",
                table: "RaceComplaints",
                column: "FiledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaints_RaceId",
                table: "RaceComplaints",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaints_RuledByUserId",
                table: "RaceComplaints",
                column: "RuledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaints_Status",
                table: "RaceComplaints",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaceComplaints");
        }
    }
}
