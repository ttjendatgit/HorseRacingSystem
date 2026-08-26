using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceComplaintEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaceComplaintEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceComplaintId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    EvidenceSource = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    PublicId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceComplaintEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaceComplaintEvidence_RaceComplaints_RaceComplaintId",
                        column: x => x.RaceComplaintId,
                        principalTable: "RaceComplaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaceComplaintEvidence_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaintEvidence_RaceComplaintId",
                table: "RaceComplaintEvidence",
                column: "RaceComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaintEvidence_RaceComplaintId_EvidenceSource",
                table: "RaceComplaintEvidence",
                columns: new[] { "RaceComplaintId", "EvidenceSource" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceComplaintEvidence_UploadedByUserId",
                table: "RaceComplaintEvidence",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaceComplaintEvidence");
        }
    }
}
