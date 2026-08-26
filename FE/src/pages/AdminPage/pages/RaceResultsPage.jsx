import { useState, useEffect } from "react";
import { request } from "../../../services/apiClient";
import { apiToVNDate, apiToVNDisplay } from "../../../utils/vnDateTime";
import { isFinalRound } from "../../../utils/tournamentRegistration";
import { buildRankingDisplayList, getPlacementLabel } from "../../../utils/raceResultDisplay";

const STATUS_NUM = {
  1: "scheduled",
  2: "inprogress",
  3: "finished",
  4: "cancelled",
  7: "registrationopen",
  8: "registrationclosed",
};

const normalizeStatus = (s) => {
  if (s === null || s === undefined) return "";
  if (typeof s === "number") return STATUS_NUM[s] ?? String(s);
  const text = String(s).trim();
  if (/^\d+$/.test(text)) return STATUS_NUM[Number(text)] ?? text;
  return text.toLowerCase();
};

const RESULT_STATUSES = new Set(["finished"]);

const STATUS_LABEL = {
  finished: "Đã kết thúc",
  inprogress: "Đang đua",
  scheduled: "Sắp diễn ra",
  registrationopen: "Chuẩn bị",
  registrationclosed: "Chuẩn bị",
  cancelled: "Đã hủy",
};

const STATUS_TONE = {
  finished: "success",
  inprogress: "live",
  scheduled: "neutral",
  registrationopen: "warning",
  registrationclosed: "neutral",
  cancelled: "danger",
};

const RESULT_STATUS_LABEL = {
  official: "Chính thức",
  provisional: "Tạm thời",
};

const fmtDate = (v) => (v ? apiToVNDate(v) : "-");
const fmtDateTime = (v) => (v ? apiToVNDisplay(v) : "-");
const getValue = (obj, camel, pascal) => obj?.[camel] ?? obj?.[pascal];
const getRaceDate = (race) => getValue(race, "scheduledAt", "ScheduledAt");

const formatRoundLabel = (race) => {
  const roundNumber = getValue(race, "roundNumber", "RoundNumber");
  const roundName = getValue(race, "roundName", "RoundName");
  if (roundNumber && roundName) return `Vòng ${roundNumber} · ${roundName}`;
  if (roundNumber) return `Vòng ${roundNumber}`;
  if (roundName) return roundName;
  return "Chưa gắn vòng";
};

const formatSeconds = (value) => {
  if (value === null || value === undefined || value === "") return "";
  const numeric = Number(value);
  return Number.isFinite(numeric) ? `${numeric.toFixed(2)}s` : "";
};

const getPlacementTone = (label) => {
  if (label === "Bị loại") return "danger";
  if (label === "Đi tiếp" || label === "Vô địch") return "success";
  return "neutral";
};

function ResultStatusBadge({ status }) {
  const key = (status || "").toLowerCase();
  if (!key) return <span className="rr-result-status rr-result-status--empty">Chưa có</span>;
  return (
    <span className={`rr-result-status rr-result-status--${key}`}>
      {RESULT_STATUS_LABEL[key] ?? status}
    </span>
  );
}

function ResultRow({ entry }) {
  const isWinner = Number(entry.position) === 1;
  const placementTone = getPlacementTone(entry.label);

  return (
    <div className={`rr-result-row ${isWinner ? "rr-result-row--winner" : ""}`}>
      <span className="rr-rank">#{entry.position}</span>
      <strong className="rr-runner-name">{entry.horseName ?? "Chưa xác định"}</strong>
      <span className="rr-jockey">{entry.jockeyName ? `Kỵ sĩ: ${entry.jockeyName}` : "Chưa có kỵ sĩ"}</span>
      {entry.label ? (
        <span className={`rr-placement rr-placement--${placementTone}`}>{entry.label}</span>
      ) : (
        <span />
      )}
    </div>
  );
}

function LegacyWinner({ det }) {
  const time = formatSeconds(det.winnerTime);
  const odds = det.winnerOdds ? `${Number(det.winnerOdds).toFixed(2)}x` : "";
  const secondary = [det.winnerJockeyName && `Kỵ sĩ: ${det.winnerJockeyName}`, time && `Thành tích ${time}`, odds && `Tỉ lệ ${odds}`]
    .filter(Boolean)
    .join(" · ");

  return (
    <div className="rr-result-row rr-result-row--winner">
      <span className="rr-rank">#1</span>
      <strong className="rr-runner-name">{det.winnerHorseName}</strong>
      <span className="rr-jockey">{secondary || "Chưa có kỵ sĩ"}</span>
      <span className="rr-placement rr-placement--success">Thắng cuộc</span>
    </div>
  );
}

export default function RaceResultsPage() {
  const [groups, setGroups] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setError("");
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
              const resultStatus = (
                resultData?.resultStatus ?? resultData?.ResultStatus ?? ""
              ).toLowerCase();
              const rejectedReason =
                resultData?.rejectedReason ?? resultData?.RejectedReason ?? "";
              const isOfficial = resultStatus === "official";
              const winnerHorseId =
                resultData?.winningHorseId ?? resultData?.WinningHorseId;
              const winnerEntry = entries.find(
                (e) => (e.horseId ?? e.HorseId) === winnerHorseId,
              );

              const tournamentId = race.tournamentId ?? race.TournamentId;
              const tournament = tournamentList.find(
                (t) => (t.id ?? t.Id) === tournamentId,
              );
              const isFinal = isFinalRound(race, tournament);
              const qualificationSlots = race.qualificationSlots ?? race.QualificationSlots;
              const rankings = resultData?.rankings ?? resultData?.Rankings ?? [];
              // RESULT-APPROVAL-REVIEW-UX: the full ranking is built regardless of
              // Provisional/Official — Admin must be able to review every position before
              // approving, not just the winner. Only the qualification label ("Đi tiếp"/"Bị
              // loại"/"Vô địch") stays Official-only, since that outcome isn't final until then.
              const rankedEntries = buildRankingDisplayList(rankings, entries).map((r) => ({
                ...r,
                label: isOfficial ? getPlacementLabel({ position: r.position, isFinal, qualificationSlots }) : "",
              }));

              return {
                race,
                det: {
                  entries,
                  resultStatus,
                  rejectedReason,
                  rankedEntries,
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
              return {
                race,
                det: {
                  entries: [],
                  resultStatus: "",
                  rankedEntries: [],
                  winnerHorseName: null,
                },
              };
            }
          }),
        );

        if (!cancelled) {
          const tMap = new Map(
            tournamentList.map((t) => [
              t.id ?? t.Id,
              {
                name: t.name ?? t.Name,
                startDate: t.startDate ?? t.StartDate,
                endDate: t.endDate ?? t.EndDate,
              },
            ]),
          );
          const byTournament = new Map();
          loaded.filter(Boolean).forEach(({ race, det }) => {
            const tournamentId = race.tournamentId ?? race.TournamentId;
            const tournamentKey = tournamentId ?? "__unknown";
            if (!byTournament.has(tournamentKey)) {
              byTournament.set(tournamentKey, {
                id: tournamentKey,
                name: tMap.get(tournamentId)?.name ?? "Giải đấu",
                startDate: tMap.get(tournamentId)?.startDate,
                endDate: tMap.get(tournamentId)?.endDate,
                races: [],
              });
            }
            byTournament.get(tournamentKey).races.push({ race, det });
          });
          const result = [...byTournament.values()]
            .sort(
              (a, b) =>
                new Date(b.startDate ?? 0) - new Date(a.startDate ?? 0),
            );
          result.forEach((g) =>
            g.races.sort(
              (a, b) =>
                new Date(getRaceDate(b.race) ?? 0) -
                new Date(getRaceDate(a.race) ?? 0),
            ),
          );
          setGroups(result);
        }
      } catch (err) {
        if (!cancelled) {
          setGroups([]);
          setError(err?.message || "Không thể tải dữ liệu kết quả cuộc đua.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const totalRaces = groups.reduce((sum, group) => sum + group.races.length, 0);
  const officialRaces = groups.reduce(
    (sum, group) =>
      sum + group.races.filter(({ det }) => det.resultStatus === "official").length,
    0,
  );
  const totalRankedEntries = groups.reduce(
    (sum, group) =>
      sum + group.races.reduce((raceSum, { det }) => raceSum + (det.rankedEntries?.length ?? 0), 0),
    0,
  );

  if (loading) {
    return (
      <div className="rr-page">
        <section className="rr-state-card">
          <strong>Đang tải kết quả</strong>
          <span>Đang tổng hợp các cuộc đua đã kết thúc.</span>
        </section>
      </div>
    );
  }

  return (
    <div className="rr-page">
      <section className="rr-title">
        <div>
          <h1>Quản lý bảng xếp hạng</h1>
          <p>Theo dõi kết quả đã nộp và thứ hạng chính thức theo từng giải đấu.</p>
        </div>
        <p className="rr-summary-line">
          {groups.length} giải đấu · {totalRaces} cuộc đua · {officialRaces} chính thức · {totalRankedEntries} lượt xếp hạng
        </p>
      </section>

      {error ? <p className="admin-notice admin-notice--error">{error}</p> : null}

      {groups.length === 0 ? (
        <section className="rr-state-card">
          <strong>Chưa có cuộc đua nào kết thúc</strong>
          <span>Khi trọng tài nộp kết quả và admin duyệt, bảng xếp hạng sẽ xuất hiện tại đây.</span>
        </section>
      ) : (
        groups.map((group) => (
          <section key={group.id} className="rr-tournament">
            <header className="rr-tournament__header">
              <div>
                <h2>{group.name}</h2>
                <p>
                  {group.races.length} cuộc đua · {fmtDate(group.startDate)} → {fmtDate(group.endDate)}
                </p>
              </div>
            </header>

            <div className="rr-race-list">
              {group.races.map(({ race, det }) => {
                const id = race.id ?? race.Id;
                const status = normalizeStatus(race.status ?? race.Status);
                const statusTone = STATUS_TONE[status] ?? "neutral";
                const isOfficial = det.resultStatus === "official";
                const hasWinner = Boolean(det.winnerHorseName) && isOfficial;
                const hasProvisionalWinner =
                  Boolean(det.winnerHorseName) && det.resultStatus === "provisional";
                const raceName = race.name ?? race.Name ?? "Cuộc đua";
                const location = race.location ?? race.Location;

                return (
                  <article key={id} className="rr-race">
                    <header className="rr-race__header">
                      <div>
                        <span className="rr-round">{formatRoundLabel(race)}</span>
                        <h3>{raceName}</h3>
                      </div>
                      <span className={`rr-status rr-status--${statusTone}`}>
                        {STATUS_LABEL[status] ?? race.status ?? race.Status}
                      </span>
                    </header>

                    <div className="rr-race__meta">
                      <span>{fmtDateTime(getRaceDate(race))}</span>
                      <span>{det.entries?.length ?? 0} ngựa</span>
                      <span>{location || "Đường đua chưa xác định"}</span>
                      <ResultStatusBadge status={det.resultStatus} />
                    </div>

                    {det.resultStatus === "provisional" && det.rejectedReason ? (
                      <p className="rr-inline-state" style={{ color: "var(--hr-danger)" }}>
                        Cần chỉnh sửa kết quả: {det.rejectedReason}
                      </p>
                    ) : null}

                    {det.rankedEntries?.length > 0 ? (
                      <div className="rr-result-list">
                        {det.rankedEntries.map((entry) => (
                          <ResultRow key={`${entry.horseId}-${entry.position}`} entry={entry} />
                        ))}
                      </div>
                    ) : hasWinner ? (
                      <div className="rr-result-list">
                        <LegacyWinner det={det} />
                      </div>
                    ) : hasProvisionalWinner ? (
                      <p className="rr-inline-state">
                        Kết quả tạm thời: <strong>{det.winnerHorseName}</strong> đang chờ duyệt chính thức.
                      </p>
                    ) : (
                      <p className="rr-inline-state">Chưa có kết quả. Chờ trọng tài nộp và admin duyệt.</p>
                    )}
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
