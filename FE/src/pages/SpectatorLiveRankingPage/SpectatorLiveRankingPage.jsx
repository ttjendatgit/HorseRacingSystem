import { useEffect, useMemo, useState } from "react";
import { unwrapResponseData } from "../../services/authRoleUtils";
import { getLiveRanking, getRaces, getActiveTournaments } from "../../services/spectatorApi";
import "./SpectatorLiveRankingPage.css";

const JOCKEY_RANKINGS = [
  { rank: 1, name: "Marcus Chen", nationality: "USA", races: 340, wins: 82, winRate: "24.1%", experience: "8 năm" },
  { rank: 2, name: "Elena Rodriguez", nationality: "UK", races: 210, wins: 48, winRate: "22.9%", experience: "5 năm" },
  { rank: 3, name: "David Park", nationality: "AUS", races: 280, wins: 65, winRate: "23.2%", experience: "6 năm" },
  { rank: 4, name: "Maria Santos", nationality: "BRA", races: 195, wins: 40, winRate: "20.5%", experience: "4 năm" },
  { rank: 5, name: "James O'Connor", nationality: "IRE", races: 310, wins: 58, winRate: "18.7%", experience: "7 năm" },
];

const HORSE_RANKINGS = [
  { rank: 1, name: "Storm Chaser", breed: "Thoroughbred", owner: "Sarah O'Brien", races: 22, wins: 10, winRate: "45.5%" },
  { rank: 2, name: "Thunder Strike", breed: "Arabian", owner: "John Whitfield", races: 18, wins: 7, winRate: "38.9%" },
  { rank: 3, name: "Silver Comet", breed: "Thoroughbred", owner: "John Whitfield", races: 12, wins: 5, winRate: "41.7%" },
  { rank: 4, name: "Golden Arrow", breed: "Thoroughbred", owner: "Sarah O'Brien", races: 9, wins: 4, winRate: "44.4%" },
  { rank: 5, name: "Midnight Runner", breed: "Quarter Horse", owner: "John Whitfield", races: 6, wins: 2, winRate: "33.3%" },
];

const MEDAL = ["🥇", "🥈", "🥉"];

const formatTimeTaken = (value) => {
  if (value == null) return "-";
  const totalSec = Math.max(0, Math.floor(Number(value)));
  const min = Math.floor(totalSec / 60);
  const sec = totalSec % 60;
  return `${min}:${String(sec).padStart(2, "0")}`;
};

function SpectatorLiveRankingPage() {
  const [races, setRaces] = useState([]);
  const [tournaments, setTournaments] = useState([]);
  const [selectedTournamentId, setSelectedTournamentId] = useState("");
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [ranking, setRanking] = useState({ raceName: "", rankings: [] });
  const [isLoadingRaces, setIsLoadingRaces] = useState(true);
  const [isLoadingRanking, setIsLoadingRanking] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setIsLoadingRaces(true);
      try {
        const [raceRes, tourRes] = await Promise.all([getRaces(), getActiveTournaments()]);
        if (cancelled) return;
        const racePayload = unwrapResponseData(raceRes);
        const tourPayload = unwrapResponseData(tourRes);
        const items = Array.isArray(racePayload) ? racePayload : [];
        const tours = Array.isArray(tourPayload) ? tourPayload : [];
        setRaces(items);
        setTournaments(tours);
        if (tours.length > 0 && !selectedTournamentId) {
          setSelectedTournamentId(tours[0]?.id ?? tours[0]?.Id ?? "");
        }
      } catch { /* ignore */ }
      finally { if (!cancelled) setIsLoadingRaces(false); }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (selectedTournamentId && races.length > 0) {
      const tournamentRaces = races.filter((r) => (r.tournamentId ?? r.TournamentId) === selectedTournamentId);
      if (tournamentRaces.length > 0) {
        setSelectedRaceId(tournamentRaces[0]?.id ?? tournamentRaces[0]?.Id ?? "");
      } else {
        setSelectedRaceId("");
        setRanking({ raceName: "", rankings: [] });
      }
    }
  }, [selectedTournamentId, races]);

  useEffect(() => {
    if (!selectedRaceId) return;
    let cancelled = false;
    const load = async () => {
      setIsLoadingRanking(true);
      try {
        const response = await getLiveRanking(selectedRaceId);
        const payload = unwrapResponseData(response);
        if (!cancelled) {
          setRanking({
            raceName: payload?.raceName ?? payload?.RaceName ?? "",
            rankings: payload?.rankings ?? payload?.Rankings ?? [],
          });
        }
      } catch { /* ignore */ }
      finally { if (!cancelled) setIsLoadingRanking(false); }
    };
    load();
    return () => { cancelled = true; };
  }, [selectedRaceId]);

  const tournamentOptions = useMemo(() =>
    tournaments.map((t) => ({ id: t?.id ?? t?.Id, label: t?.name ?? t?.Name ?? "Giải đấu" })),
    [tournaments],
  );

  const filteredRaces = useMemo(() =>
    races.filter((r) => (r.tournamentId ?? r.TournamentId) === selectedTournamentId),
    [races, selectedTournamentId],
  );

  const raceOptions = useMemo(() =>
    filteredRaces.map((r) => ({ id: r?.id ?? r?.Id, label: r?.name ?? r?.Name ?? "Cuộc đua" })),
    [filteredRaces],
  );

  const { rankings, raceName } = ranking;

  return (
    <div className="srp-page">
      <header className="srp-header">
        <span className="srp-eyebrow">Khán giả</span>
        <h1 className="srp-title">Bảng Xếp Hạng</h1>
        <p className="srp-subtitle">BXH mùa giải và kết quả từng cuộc đua.</p>
      </header>

      {/* ── BXH Mùa Giải ── */}
      <div className="srp-season-grid">
        <section className="srp-season-panel">
          <h2 className="srp-season-title">🏇 BXH Kỵ Sĩ</h2>
          <div className="srp-season-table">
            <div className="srp-season-row srp-season-row--header">
              <span className="sst-col--rank">#</span>
              <span className="sst-col--name">Kỵ sĩ</span>
              <span className="sst-col--stat">Đua</span>
              <span className="sst-col--stat">Thắng</span>
              <span className="sst-col--stat">Tỷ lệ</span>
            </div>
            {JOCKEY_RANKINGS.map((j) => (
              <div key={j.rank} className={`srp-season-row ${j.rank <= 3 ? "srp-season-row--top" : ""}`}>
                <span className="sst-col--rank">{j.rank <= 3 ? MEDAL[j.rank - 1] : j.rank}</span>
                <span className="sst-col--name"><strong>{j.name}</strong><small>{j.nationality} · {j.experience}</small></span>
                <span className="sst-col--stat">{j.races}</span>
                <span className="sst-col--stat sst-col--highlight">{j.wins}</span>
                <span className="sst-col--stat sst-col--rate">{j.winRate}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="srp-season-panel">
          <h2 className="srp-season-title">🐎 BXH Ngựa Đua</h2>
          <div className="srp-season-table">
            <div className="srp-season-row srp-season-row--header">
              <span className="sst-col--rank">#</span>
              <span className="sst-col--name">Ngựa</span>
              <span className="sst-col--stat">Đua</span>
              <span className="sst-col--stat">Thắng</span>
              <span className="sst-col--stat">Tỷ lệ</span>
            </div>
            {HORSE_RANKINGS.map((h) => (
              <div key={h.rank} className={`srp-season-row ${h.rank <= 3 ? "srp-season-row--top" : ""}`}>
                <span className="sst-col--rank">{h.rank <= 3 ? MEDAL[h.rank - 1] : h.rank}</span>
                <span className="sst-col--name"><strong>{h.name}</strong><small>{h.breed} · Chủ: {h.owner}</small></span>
                <span className="sst-col--stat">{h.races}</span>
                <span className="sst-col--stat sst-col--highlight">{h.wins}</span>
                <span className="sst-col--stat sst-col--rate">{h.winRate}</span>
              </div>
            ))}
          </div>
        </section>
      </div>

      {/* ── BXH Cuộc Đua ── */}
      <section className="srp-race-section">
        <h2 className="srp-season-title">🏁 BXH Cuộc Đua</h2>
        <div className="srp-race-controls">
          <div className="srp-select-group">
            <label className="srp-label">Chọn giải đấu</label>
            <select
              className="srp-select"
              value={selectedTournamentId}
              onChange={(e) => setSelectedTournamentId(e.target.value)}
              disabled={isLoadingRaces}
            >
              {tournamentOptions.length === 0 && <option value="">Không có giải đấu</option>}
              {tournamentOptions.map((opt) => (
                <option key={opt.id} value={opt.id}>{opt.label}</option>
              ))}
            </select>
          </div>
          <div className="srp-select-group">
            <label className="srp-label">Chọn cuộc đua</label>
            <select
              className="srp-select"
              value={selectedRaceId}
              onChange={(e) => setSelectedRaceId(e.target.value)}
              disabled={isLoadingRaces || filteredRaces.length === 0}
            >
              {raceOptions.length === 0 && <option value="">Không có cuộc đua</option>}
              {raceOptions.map((opt) => (
                <option key={opt.id} value={opt.id}>{opt.label}</option>
              ))}
            </select>
          </div>
          {raceName && <span className="srp-race-name">{raceName}</span>}
        </div>

        {isLoadingRanking ? (
          <div className="srp-empty"><h3>Đang tải...</h3></div>
        ) : rankings.length === 0 ? (
          <div className="srp-empty"><h3>Chưa có dữ liệu</h3><p>Chọn một cuộc đua đã hoàn thành để xem kết quả.</p></div>
        ) : (
          <div className="srp-table-wrapper">
            <div className="srp-table">
              <div className="srp-table__row srp-table__header">
                <span className="srp-col--pos">Hạng</span>
                <span className="srp-col--horse">Ngựa</span>
                <span className="srp-col--jockey">Kỵ sĩ</span>
                <span className="srp-col--time">Thời gian</span>
              </div>
              {rankings.map((entry) => {
                const pos = entry?.position ?? entry?.Position;
                return (
                  <div key={entry?.horseId ?? entry?.HorseId ?? pos} className={`srp-table__row${pos <= 3 ? " srp-table__row--podium" : ""}`}>
                    <span className="srp-col--pos">
                      {pos <= 3 ? <span className="srp-rank-medal">{MEDAL[pos - 1]}</span> : <span className="srp-rank-num">#{pos}</span>}
                    </span>
                    <span className="srp-col--horse">{entry?.horseName ?? entry?.HorseName ?? "—"}</span>
                    <span className="srp-col--jockey">{entry?.jockeyName ?? entry?.JockeyName ?? "—"}</span>
                    <span className="srp-col--time">{formatTimeTaken(entry?.timeTaken ?? entry?.TimeTaken)}</span>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

export default SpectatorLiveRankingPage;
