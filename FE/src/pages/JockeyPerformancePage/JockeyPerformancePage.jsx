import { useEffect, useMemo, useState } from "react";
import { getJockeyAssignedRaces, getMyJockeyProfile } from "../../services/jockeyApi";
import "./JockeyPerformancePage.css";

export default function JockeyPerformancePage() {
  const [races, setRaces] = useState([]);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [sortBy, setSortBy] = useState("date");
  const [sortAsc, setSortAsc] = useState(false);
  const [filter, setFilter] = useState("all");

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        setLoading(true);
        const data = await getJockeyAssignedRaces();
        if (!cancelled) setRaces(data);
      } catch {
        if (!cancelled) setRaces([]);
      } finally { if (!cancelled) setLoading(false); }
    };
    load();
    getMyJockeyProfile().then((p) => { if (!cancelled) setProfile(p); }).catch(() => {});
    return () => { cancelled = true; };
  }, []);

  const { assignedRaces, confirmed, totalStarts, totalWins, winRate } = useMemo(() => {
    const a = races.length;
    const c = races.filter(r => r.jockeyConfirmed).length;
    const s = Number(profile?.totalRaces ?? 0);
    const w = Number(profile?.totalWins ?? 0);
    const wr = profile?.winRate != null ? Number(profile.winRate) : s > 0 ? Math.round((w / s) * 100) : 0;
    return { assignedRaces: a, confirmed: c, totalStarts: s, totalWins: w, winRate: wr };
  }, [races, profile]);

  // Horse stats
  const horseStats = useMemo(() => {
    const map = {};
    races.forEach(r => {
      if (!map[r.horseName]) map[r.horseName] = { name: r.horseName, races: 0, wins: 0 };
      map[r.horseName].races += Number(r.horseTotalRaces || 0);
      map[r.horseName].wins += Number(r.horseTotalWins || 0);
    });
    return Object.values(map);
  }, [races]);
  const maxRaces = Math.max(...horseStats.map(h => h.races), 1);
  const bestHorse = horseStats.length > 0 ? horseStats.reduce((a, b) => ((a.wins / Math.max(a.races, 1)) > (b.wins / Math.max(b.races, 1)) ? a : b)) : null;

  // Monthly chart from real data
  const monthlyData = useMemo(() => {
    const months = {};
    races.forEach(r => {
      const d = new Date(r.scheduledAt);
      if (Number.isNaN(d.getTime())) return;
      const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
      if (!months[key]) months[key] = { month: key, races: 0, wins: 0 };
      months[key].races += Number(r.horseTotalRaces || 0);
      months[key].wins += Number(r.horseTotalWins || 0);
    });
    // Sort and take last 6
    return Object.values(months).sort((a, b) => a.month.localeCompare(b.month)).slice(-6);
  }, [races]);

  const maxMonthly = Math.max(...monthlyData.map(d => d.races), 1);

  // Filter + Sort
  const filtered = useMemo(() => {
    let items = [...races];
    if (search.trim()) items = items.filter(r => (r.title || "").toLowerCase().includes(search.toLowerCase()) || (r.horseName || "").toLowerCase().includes(search.toLowerCase()));
    if (filter === "confirmed") items = items.filter(r => r.jockeyConfirmed);
    else if (filter === "pending") items = items.filter(r => !r.jockeyConfirmed);
    items.sort((a, b) => {
      let cmp = 0;
      if (sortBy === "date") cmp = new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime();
      else if (sortBy === "name") cmp = (a.title || "").localeCompare(b.title || "");
      else if (sortBy === "winrate") cmp = (Number(a.horseTotalWins || 0) / Math.max(Number(a.horseTotalRaces || 0), 1)) - (Number(b.horseTotalWins || 0) / Math.max(Number(b.horseTotalRaces || 0), 1));
      return sortAsc ? cmp : -cmp;
    });
    return items;
  }, [races, search, sortBy, sortAsc, filter]);

  return (
    <div className="jp-page">
      <div className="jp-header">
        <h1>Thành tích</h1>
        <p className="jp-sub">Theo dõi hiệu suất qua các cuộc đua được phân công.</p>
      </div>

      {/* KPI Cards */}
      <div className="jp-kpis">
        <div className="jp-kpi"><span className="jp-kpi-label">Tỉ lệ thắng</span><strong className="jp-kpi-value">{winRate}%</strong><span className="jp-kpi-trend">{totalWins} thắng / {totalStarts} đua</span></div>
        <div className="jp-kpi jp-kpi--light"><span className="jp-kpi-label">Cuộc đua</span><strong className="jp-kpi-value">{assignedRaces}</strong><span className="jp-kpi-trend">{confirmed} đã xác nhận</span></div>
        <div className="jp-kpi"><span className="jp-kpi-label">Chiến thắng</span><strong className="jp-kpi-value">{totalWins}</strong><span className="jp-kpi-trend">{winRate >= 50 ? "Trên trung bình" : "Đang cải thiện"}</span></div>
      </div>

      {/* Monthly Chart */}
      <div className="jp-chart-card">
        <h3>Hiệu suất theo tháng</h3>
        <div className="jp-monthly-chart">
          {monthlyData.map(d => (
            <div key={d.month} className="jp-mcol">
              <span className="jp-mval">{d.wins}</span>
              <div className="jp-mbar" style={{ height: `${(d.races / maxMonthly) * 100}%` }}>
                <div className="jp-mfill" style={{ height: `${(d.wins / Math.max(d.races, 1)) * 100}%` }} />
              </div>
              <span className="jp-mlabel">{d.month.split("-")[1] && "T" + String(parseInt(d.month.split("-")[1]))}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Horse Stats + Best Performer */}
      {horseStats.length > 0 && (
        <div className="jp-chart-card">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: 15, fontWeight: 700, color: "#1a1d23" }}>Hiệu suất theo ngựa</h3>
            {bestHorse && (
              <span className="jp-best-badge">Tốt nhất: {bestHorse.name} ({Math.round((bestHorse.wins / Math.max(bestHorse.races, 1)) * 100)}%)</span>
            )}
          </div>
          <div className="jp-chart">
            {horseStats.map(h => {
              const pct = Math.round((h.wins / Math.max(h.races, 1)) * 100);
              const isBest = bestHorse && h.name === bestHorse.name;
              return (
                <div key={h.name} className={`jp-chart-row ${isBest ? "jp-chart-row--best" : ""}`}>
                  <span className="jp-chart-name">{isBest && <span className="jp-crown">&#9733;</span>} {h.name}</span>
                  <div className="jp-chart-bar-wrap">
                    <div className="jp-chart-bar" style={{ width: `${(h.races / maxRaces) * 100}%` }}>
                      <div className="jp-chart-fill" style={{ width: `${pct}%` }} />
                    </div>
                  </div>
                  <span className="jp-chart-stat">{h.wins}/{h.races}</span>
                  <span className="jp-chart-pct">{pct}%</span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Table */}
      <div className="jp-table-card">
        <div className="jp-table-toolbar">
          <div className="jp-search">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
            <input placeholder="Tìm kiếm..." value={search} onChange={e => setSearch(e.target.value)} />
          </div>
          <div className="jp-filter-group">
            {["all", "confirmed", "pending"].map(f => (
              <button key={f} className={`jp-fbtn ${filter === f ? "jp-fbtn--active" : ""}`} onClick={() => setFilter(f)}>
                {f === "all" ? "Tất cả" : f === "confirmed" ? "Đã xác nhận" : "Chờ"}
              </button>
            ))}
          </div>
          <div className="jp-sort-group">
            {["date", "name", "winrate"].map(s => (
              <button key={s} className={`jp-sort-btn ${sortBy === s ? "jp-sort-btn--active" : ""}`} onClick={() => { if (sortBy === s) setSortAsc(!sortAsc); else { setSortBy(s); setSortAsc(false); } }}>
                {s === "date" ? "Ngày" : s === "name" ? "Tên" : "Tỉ lệ"}
                {sortBy === s && <span className="jp-arrow">{sortAsc ? "↑" : "↓"}</span>}
              </button>
            ))}
          </div>
        </div>

        {loading ? (
          <div className="jp-loading"><div className="jp-skeleton"/><div className="jp-skeleton"/><div className="jp-skeleton"/></div>
        ) : filtered.length === 0 ? (
          <div className="jp-empty">Không tìm thấy dữ liệu.</div>
        ) : (
          <div className="jp-table">
            <div className="jp-tr jp-tr--head">
              <span className="jp-td">Cuộc đua</span>
              <span className="jp-td">Ngựa</span>
              <span className="jp-td">Kết quả</span>
              <span className="jp-td">Tỉ lệ</span>
              <span className="jp-td">Trạng thái</span>
            </div>
            {filtered.map(r => {
              const wRH = Number(r.horseTotalRaces || 0) > 0 ? Math.round((Number(r.horseTotalWins || 0) / Number(r.horseTotalRaces || 0)) * 100) : 0;
              return (
                <div key={r.id} className="jp-tr">
                  <span className="jp-td"><strong>{r.title}</strong><small>{r.location}</small></span>
                  <span className="jp-td">{r.horseName}</span>
                  <span className="jp-td">{r.horseTotalWins || 0}W / {r.horseTotalRaces || 0}R</span>
                  <span className="jp-td"><span className="jp-rate" style={{ background: wRH >= 50 ? "rgba(16,185,129,0.1)" : "rgba(245,158,11,0.1)", color: wRH >= 50 ? "#0f7a5a" : "#b8860b" }}>{wRH}%</span></span>
                  <span className="jp-td"><span className={`jp-badge ${r.jockeyConfirmed ? "jp-badge--ok" : "jp-badge--warn"}`}>{r.jockeyConfirmed ? "Đã xác nhận" : "Chờ"}</span></span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
