import { sortPrizesByPosition, formatPrizeFigure } from "../utils/prizeAllocation";

// PRIZE-V1.1/V1.2 PART 7/12: single reusable Prize breakdown block, shared by every viewer
// surface (Tournament detail — Owner/Jockey/Spectator/Referee all reach it via the same route —
// and the Owner/Spectator tournament-list modals) instead of separate ad hoc UIs. Shows the
// percentage and derived money amount together (via formatPrizeFigure) — except for historical
// rows with PercentageOfPool <= 0 (unused before PRIZE-V1.2, never migrated/mutated), which show
// the amount alone rather than a misleading "0% · amount" or a fabricated historical percentage.
// Read-only, display-only: never shows IsDistributed/DistributedAt/RaceId/Currency or any
// wallet/payout/recipient concept — those don't exist in this product. Renders nothing when there
// are no rows (e.g. Draft, hidden server-side, or a Tournament with no Prize rows configured yet).
export default function PrizeBreakdown({ prizes, title = "Cơ cấu giải thưởng" }) {
  const sorted = sortPrizesByPosition(prizes);
  if (sorted.length === 0) return null;

  return (
    <div className="prize-breakdown">
      <h3 className="prize-breakdown__title">{title}</h3>
      <ul className="prize-breakdown__list">
        {sorted.map((p) => {
          const id = p.id ?? p.Id;
          const position = p.position ?? p.Position;
          const amount = p.amount ?? p.Amount;
          const percentage = p.percentageOfPool ?? p.PercentageOfPool;
          const sponsorName = p.sponsorName ?? p.SponsorName;
          return (
            <li key={id} className="prize-breakdown__row">
              <span className="prize-breakdown__rank">Hạng {position}</span>
              <span className="prize-breakdown__figure">
                {formatPrizeFigure(percentage, amount)}
              </span>
              {sponsorName && <span className="prize-breakdown__sponsor">Nhà tài trợ: {sponsorName}</span>}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
