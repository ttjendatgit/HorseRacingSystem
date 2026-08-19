using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Phase2B — splits the conflated RaceStatus into event-progress
    /// (RaceStatus) and result-lifecycle (RaceResultStatus) concerns.
    ///
    /// Step order (all in this single migration transaction — if any step
    /// fails, the whole migration rolls back and no partial state is left):
    ///   1. Add RaceResults.Status as NULLABLE text.
    ///   2. Fail-fast guard: abort if any RaceResults row has legacy
    ///      ApprovalStatus/IsOfficial data outside the safe backfill domain
    ///      (ApprovalStatus not in {1,2,3}, or ApprovalStatus/IsOfficial
    ///      disagree with each other). This migration does NOT silently
    ///      guess for such rows — see the Phase2 preflight audit SQL to
    ///      classify and resolve them by hand first.
    ///   3. Backfill RaceResults.Status from the legacy ApprovalStatus/
    ///      IsOfficial fields (exhaustive given step 2 passed).
    ///   4. Backfill the three retired Races.Status values (AwaitingResult,
    ///      ResultPendingApproval, ResultApproved) to 'Finished' — a race
    ///      that already has a submitted result has, by the locked V1.1
    ///      lifecycle, already finished its event-progress lifecycle
    ///      regardless of where its result sits in review.
    ///   5. Make RaceResults.Status NOT NULL now that every row has a value.
    ///
    /// Legacy columns RaceResults.ApprovalStatus and RaceResults.IsOfficial
    /// are deliberately NOT dropped or altered here — see Down() remarks for
    /// why a lossless reverse migration is impossible once step 4 collapses
    /// three distinct legacy Races.Status values into one.
    /// </remarks>
    public partial class Phase2B_RaceResultStatusAndFinishedLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: additive, nullable-first — never breaks a concurrently
            // running old backend that doesn't know this column exists.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RaceResults",
                type: "text",
                nullable: true);

            // Step 2: fail-fast guard. Do NOT silently guess for unsafe rows.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    unsafe_count integer;
BEGIN
    SELECT COUNT(*) INTO unsafe_count
    FROM ""RaceResults""
    WHERE ""ApprovalStatus"" NOT IN (1, 2, 3)
       OR (""ApprovalStatus"" = 2 AND ""IsOfficial"" IS DISTINCT FROM true)
       OR (""ApprovalStatus"" <> 2 AND ""IsOfficial"" = true);

    IF unsafe_count > 0 THEN
        RAISE EXCEPTION 'Phase2B migration aborted: % RaceResults row(s) have ApprovalStatus/IsOfficial values outside the safe backfill domain (ApprovalStatus not in {1,2,3}, or ApprovalStatus/IsOfficial disagree). Run the Phase2 preflight audit SQL, resolve these rows manually, then retry this migration.', unsafe_count;
    END IF;
END $$;
");

            // Step 3: backfill RaceResults.Status (exhaustive given step 2 passed).
            migrationBuilder.Sql(@"
UPDATE ""RaceResults"" SET ""Status"" = 'Provisional' WHERE ""ApprovalStatus"" IN (1, 3);
UPDATE ""RaceResults"" SET ""Status"" = 'Official' WHERE ""ApprovalStatus"" = 2 AND ""IsOfficial"" = true;
");

            // Step 4: backfill retired Races.Status values. A race that already
            // has a submitted result has already Finished its event-progress
            // lifecycle under the locked V1.1 model, independent of where the
            // result sits in the Provisional/Official review pipeline.
            migrationBuilder.Sql(@"
UPDATE ""Races""
SET ""Status"" = 'Finished'
WHERE ""Status"" IN ('AwaitingResult', 'ResultPendingApproval', 'ResultApproved');
");

            // Step 5: every row now has a Status (step 2 guaranteed the domain,
            // step 3 is exhaustive over that domain) — safe to require it.
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RaceResults",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Down() only reverses what is safely reversible: it drops the new
        /// RaceResults.Status column. It deliberately does NOT attempt to
        /// restore the three retired Races.Status values (AwaitingResult,
        /// ResultPendingApproval, ResultApproved).
        ///
        /// That restoration is impossible to do losslessly: Up() step 4
        /// collapsed all three into 'Finished', and a plain 'Finished' row
        /// after Up() is indistinguishable from a row that was already
        /// legitimately 'Finished' before Up() ever ran (both had an
        /// Approved+Official result under the pre-Phase2B EndRaceAsync gate).
        /// Attempting to "reconstruct" AwaitingResult/ResultPendingApproval/
        /// ResultApproved from the retained legacy ApprovalStatus/IsOfficial
        /// fields would silently fabricate history for some rows and be
        /// correct only by coincidence for others — exactly what this project
        /// was told not to do. Legacy ApprovalStatus/IsOfficial values
        /// themselves are untouched by Up() and remain available for a human
        /// to review manually if a rollback ever needs to reconstruct intent
        /// for a specific race, but this migration will not do that
        /// reconstruction automatically.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "RaceResults");
        }
    }
}
