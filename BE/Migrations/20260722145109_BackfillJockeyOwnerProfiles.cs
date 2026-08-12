using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    public partial class BackfillJockeyOwnerProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older jockey accounts only had a Jockey profile even though jockeys are
            // allowed to manage their own horses. Horse.OwnerId must point to Owners,
            // so create the missing companion profile for those existing accounts.
            migrationBuilder.Sql(
                """
                INSERT INTO "Owners"
                    ("Id", "UserId", "OwnerCode", "OwnerType", "JoinDate", "Status", "CreatedAt", "UpdatedAt")
                SELECT
                    u."Id",
                    u."Id",
                    'OWN-' || UPPER(REPLACE(u."Id"::text, '-', '')),
                    'Cá nhân',
                    CURRENT_TIMESTAMP,
                    'Đang hoạt động',
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "Users" u
                WHERE u."Role" = 'Jockey'
                  AND NOT EXISTS (
                      SELECT 1 FROM "Owners" o WHERE o."UserId" = u."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty: owner profiles may have horses after this
            // migration runs, so deleting them during rollback would lose user data.
        }
    }
}
