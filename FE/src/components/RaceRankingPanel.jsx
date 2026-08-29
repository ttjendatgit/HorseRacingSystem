// RESULT-APPROVAL-REVIEW-UX: single shared "review the full provisional/official ranking"
// block for the two Admin surfaces that let Admin approve/reject a RaceResult inline
// (AdminPage.jsx ScheduleManagement and TournamentDetail.jsx). RaceResultsPage.jsx has its own
// existing CSS-class-based row component and is intentionally left alone (Part B14).
//
// `rows` must already be the output of buildRankingDisplayList(rankings, entries) — this
// component never re-derives ranking data itself, so there is exactly one place (the shared
// helper) that decides how RankingsJson becomes a display list.

function RankingRow({ row }) {
  const isEliminated = Boolean(row.status) && row.status !== "Completed";
  return (
    <div
      style={{
        display: "flex",
        alignItems: "baseline",
        gap: 10,
        padding: "5px 0",
        borderBottom: "1px solid rgba(238,229,212,0.06)",
      }}
    >
      <span
        style={{
          minWidth: 26,
          fontSize: 13,
          fontWeight: 700,
          color: row.isWinner ? "var(--hr-gold-soft)" : "var(--hr-muted)",
        }}
      >
        {isEliminated ? "--" : `#${row.position}`}
      </span>
      <span style={{ display: "grid", gap: 1 }}>
        <strong style={{ fontSize: 13, color: row.isWinner ? "var(--hr-gold-soft)" : "var(--hr-paper)" }}>
          {row.horseName ?? "Chưa xác định"}
        </strong>
        <span style={{ fontSize: 11, color: "var(--hr-muted)" }}>
          {row.jockeyName ? `Kỵ sĩ: ${row.jockeyName}` : "Chưa có kỵ sĩ"}
        </span>
      </span>
    </div>
  );
}

export default function RaceRankingPanel({
  title,
  rows,
  isOfficial,
  rejectedReason,
  notes,
  actions,
}) {
  return (
    <div
      style={{
        marginTop: 12,
        padding: "10px 14px",
        borderRadius: 10,
        background: isOfficial ? "rgba(112,139,104,0.1)" : "rgba(185,138,69,0.1)",
        border: `1px solid ${isOfficial ? "rgba(112,139,104,0.25)" : "rgba(185,138,69,0.3)"}`,
      }}
    >
      <h4
        style={{
          fontSize: 14,
          margin: "0 0 6px",
          color: isOfficial ? "var(--hr-success)" : "var(--hr-warning)",
        }}
      >
        {title ?? (isOfficial ? "Kết quả chính thức" : "Kết quả tạm thời (chưa duyệt)")}
      </h4>

      {rejectedReason ? (
        <p style={{ margin: "0 0 6px", fontSize: 12, color: "var(--hr-danger)", fontWeight: 600 }}>
          Cần chỉnh sửa kết quả: {rejectedReason}
        </p>
      ) : null}

      {rows.length > 0 ? (
        <div style={{ display: "grid" }}>
          {rows.map((row) => (
            <RankingRow key={row.horseId ?? row.position} row={row} />
          ))}
        </div>
      ) : (
        <p style={{ margin: 0, fontSize: 13, color: "var(--hr-muted)" }}>
          Chưa có bảng xếp hạng hợp lệ cho cuộc đua này.
        </p>
      )}

      {notes ? (
        <p style={{ margin: "6px 0 0", fontSize: 12, color: "var(--hr-muted)" }}>Ghi chú: {notes}</p>
      ) : null}

      {actions ? (
        <div style={{ marginTop: 10, display: "flex", justifyContent: "flex-end", gap: 8, flexWrap: "wrap" }}>
          {actions}
        </div>
      ) : null}
    </div>
  );
}
