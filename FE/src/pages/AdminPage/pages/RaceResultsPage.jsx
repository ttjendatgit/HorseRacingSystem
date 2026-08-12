import { useState, useEffect } from "react";
import { request } from "../../../services/apiClient";

const STATUS_NUM = {
  1: "scheduled",
  2: "inprogress",
  3: "finished",
  4: "cancelled",
  5: "awaitingresult",
  6: "resultpendingapproval",
  7: "registrationopen",
  8: "registrationclosed",
  9: "resultapproved",
};

// Backend serializes enums as numbers (raw entities) or strings (DTOs)
const normalizeStatus = (s) => {
  if (s === null || s === undefined) return "";
  if (typeof s === "number") return STATUS_NUM[s] ?? String(s);
  return String(s).toLowerCase();
};

const RESULT_STATUSES = new Set([
  "finished",
  "awaitingresult",
  "resultpendingapproval",
  "resultapproved",
]);

const STATUS_LABEL = {
  finished: "Đã kết thúc",
  awaitingresult: "Chờ kết quả",
  resultpendingapproval: "Chờ duyệt",
  resultapproved: "Đã duyệt KQ",
  inprogress: "Đang đua",
  scheduled: "Sắp diễn ra",
  cancelled: "Đã hủy",
};

const STATUS_COLOR = {
  finished: { color: "#166534", bg: "rgba(16,185,129,0.12)" },
  awaitingresult: { color: "#6d28d9", bg: "rgba(139,92,246,0.12)" },
  resultpendingapproval: { color: "#b45309", bg: "rgba(245,158,11,0.12)" },
  resultapproved: { color: "#047857", bg: "rgba(16,185,129,0.12)" },
};

const fmtDate = (v) =>
  v
    ? new Date(v).toLocaleDateString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
      })
    : "-";

export default function RaceResultsPage() {
  const [groups, setGroups] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [racesRes, tournamentsRes] = await Promise.all([
          request("/api/races"),
          request("/api/tournaments"),
        ]);
        const races = Array.isArray(racesRes?.data ?? racesRes)
          ? (racesRes?.data ?? racesRes)
          : [];
        const tournamentList = Array.isArray(tournamentsRes?.data ?? tournamentsRes)
          ? (tournamentsRes?.data ?? tournamentsRes)
          : [];

        const finishedRaces = races.filter((r) =>
          RESULT_STATUSES.has(normalizeStatus(r.status ?? r.Status)),
        );

        const loaded = await Promise.all(
          finishedRaces.map(async (race) => {
            const raceId = race.id ?? race.Id;
            try {
              const [entriesRes, resultRes] = await Promise.all([
                request(`/api/referees/race/${raceId}/entries`),
                request(`/api/races/${raceId}/result`),
              ]);
              const entries = Array.isArray(entriesRes?.data ?? entriesRes)
                ? (entriesRes?.data ?? entriesRes)
                : [];
              const resultData = resultRes?.data ?? resultRes;
              const winnerHorseId =
                resultData?.winningHorseId ?? resultData?.WinningHorseId;
              const winnerEntry = entries.find(
                (e) => (e.horseId ?? e.HorseId) === winnerHorseId,
              );
              return {
                race,
                det: {
                  entries,
                  winnerHorseName:
                    winnerEntry?.horseName ??
                    winnerEntry?.HorseName ??
                    resultData?.winningHorse?.name ??
                    (winnerHorseId ? "Chưa xác định" : null),
                  winnerJockeyName:
                    winnerEntry?.jockeyName ?? winnerEntry?.JockeyName ?? null,
                  winnerOdds: winnerEntry?.odds ?? winnerEntry?.Odds ?? null,
                  winnerTime:
                    resultData?.winnerFinishTime ??
                    resultData?.WinnerFinishTime ??
                    null,
                },
              };
            } catch {
              return { race, det: { entries: [], winnerHorseName: null } };
            }
          }),
        );

        if (!cancelled) {
          const tMap = new Map(
            tournamentList.map((t) => [
              t.id ?? t.Id,
              { name: t.name ?? t.Name, startDate: t.startDate ?? t.StartDate },
            ]),
          );
          const byTournament = new Map();
          loaded.filter(Boolean).forEach(({ race, det }) => {
            const tournamentId = race.tournamentId ?? race.TournamentId;
            if (!byTournament.has(tournamentId)) {
              byTournament.set(tournamentId, {
                id: tournamentId,
                name: tMap.get(tournamentId)?.name ?? "Giải đấu",
                startDate: tMap.get(tournamentId)?.startDate,
                races: [],
              });
            }
            byTournament.get(tournamentId).races.push({ race, det });
          });
          const result = [...byTournament.values()]
            .sort(
              (a, b) =>
                new Date(b.startDate ?? 0) - new Date(a.startDate ?? 0),
            );
          result.forEach((g) =>
            g.races.sort(
              (a, b) =>
                new Date(b.race.scheduledAt ?? b.race.ScheduledAt) -
                new Date(a.race.scheduledAt ?? a.race.ScheduledAt),
            ),
          );
          setGroups(result);
        }
      } catch {
        /* ignore */
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (loading)
    return (
      <div style={{ padding: 40, textAlign: "center", color: "#657086" }}>
        Đang tải...
      </div>
    );

  return (
    <div style={{ maxWidth: 1100, margin: "0 auto", padding: "24px 32px" }}>
      <h1 style={{ margin: "0 0 8px", fontSize: 28, color: "#172033" }}>
        Kết quả cuộc đua
      </h1>
      <p style={{ margin: "0 0 24px", fontSize: 13, color: "#657086" }}>
        Ngựa và kỵ sĩ chiến thắng theo từng giải đấu.
      </p>

      {groups.length === 0 ? (
        <p style={{ color: "#657086" }}>Chưa có cuộc đua nào kết thúc.</p>
      ) : (
        groups.map((group) => (
          <section key={group.id} style={{ marginBottom: 28 }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                marginBottom: 14,
                paddingBottom: 10,
                borderBottom: "2px solid rgba(143,100,32,0.16)",
              }}
            >
              <span
                style={{
                  width: 34,
                  height: 34,
                  borderRadius: 10,
                  background: "rgba(143,100,32,0.12)",
                  color: "#8f6420",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontWeight: 700,
                  fontSize: 15,
                }}
              >
                {String(group.name || "?").slice(0, 1)}
              </span>
              <div>
                <h2 style={{ margin: 0, fontSize: 19, color: "#172033" }}>
                  {group.name}
                </h2>
                <span style={{ fontSize: 12, color: "#657086" }}>
                  {group.races.length} cuộc đua · {fmtDate(group.startDate)}
                </span>
              </div>
            </div>

            <div
              style={{
                display: "grid",
                gap: 12,
                gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))",
              }}
            >
              {group.races.map(({ race, det }) => {
                const id = race.id ?? race.Id;
                const status = normalizeStatus(race.status ?? race.Status);
                const hasWinner = Boolean(det.winnerHorseName);
                const stColor =
                  STATUS_COLOR[status] ?? { color: "#64748b", bg: "rgba(100,116,139,0.1)" };
                return (
                  <article
                    key={id}
                    style={{
                      borderRadius: 14,
                      border: "1px solid rgba(143,100,32,0.16)",
                      background: "rgba(255,250,240,0.96)",
                      padding: "16px 18px",
                      display: "flex",
                      flexDirection: "column",
                      gap: 10,
                    }}
                  >
                    <div
                      style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "flex-start",
                        gap: 8,
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: 700, fontSize: 15, color: "#172033" }}>
                          {race.name ?? race.Name}
                        </div>
                        {(race.roundNames ?? race.RoundNames) && (
                          <div style={{ fontSize: 12, color: "#8f6420", marginTop: 2 }}>
                            {race.roundNames ?? race.RoundNames}
                          </div>
                        )}
                      </div>
                      <span
                        style={{
                          padding: "3px 10px",
                          borderRadius: 999,
                          fontSize: 11,
                          fontWeight: 700,
                          whiteSpace: "nowrap",
                          background: stColor.bg,
                          color: stColor.color,
                        }}
                      >
                        {STATUS_LABEL[status] ?? race.status ?? race.Status}
                      </span>
                    </div>

                    <div
                      style={{
                        fontSize: 12,
                        color: "#657086",
                        display: "flex",
                        justifyContent: "space-between",
                      }}
                    >
                      <span>
                        {fmtDate(race.scheduledAt ?? race.ScheduledAt)}
                        {(race.location ?? race.Location)
                          ? ` · ${race.location ?? race.Location}`
                          : ""}
                      </span>
                      <span>{det.entries?.length ?? 0} ngựa</span>
                    </div>

                    <div
                      style={{
                        borderTop: "1px solid rgba(143,100,32,0.12)",
                        paddingTop: 10,
                      }}
                    >
                      {hasWinner ? (
                        <>
                          <div style={{ fontSize: 13, color: "#166534", fontWeight: 700 }}>
                            🏆 {det.winnerHorseName}
                          </div>
                          <div style={{ fontSize: 12, color: "#34415b", marginTop: 4 }}>
                            Kỵ sĩ: <strong>{det.winnerJockeyName ?? "Chưa xác định"}</strong>
                            {det.winnerOdds ? (
                              <span style={{ color: "#8f6420" }}>
                                {" "}
                                · Tỉ lệ {Number(det.winnerOdds).toFixed(2)}x
                              </span>
                            ) : null}
                            {det.winnerTime ? (
                              <span> · {Number(det.winnerTime).toFixed(2)}s</span>
                            ) : null}
                          </div>
                        </>
                      ) : (
                        <div style={{ fontSize: 13, color: "#657086" }}>
                          Chưa có kết quả — chờ trọng tài nộp và admin duyệt.
                        </div>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          </section>
        ))
      )}
    </div>
  );
}
