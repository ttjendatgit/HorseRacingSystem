import { useEffect, useMemo, useState } from "react";
import { request } from "../../services/apiClient";
import "./LeaderboardPage.css";

const FALLBACK_JOCKEYS = [
  { id: "j1", name: "Nguyễn Văn A", points: 2840, totalRaces: 45, wins: 18, winRate: 40, rank: 1, trend: "up", nationality: "VN" },
  { id: "j2", name: "Trần Thị B", points: 2610, totalRaces: 38, wins: 15, winRate: 39, rank: 2, trend: "up", nationality: "VN" },
  { id: "j3", name: "Lê Văn C", points: 2350, totalRaces: 42, wins: 14, winRate: 33, rank: 3, trend: "down", nationality: "VN" },
  { id: "j4", name: "Phạm Minh D", points: 2100, totalRaces: 35, wins: 12, winRate: 34, rank: 4, trend: "up", nationality: "VN" },
  { id: "j5", name: "Hoàng Kim E", points: 1890, totalRaces: 30, wins: 10, winRate: 33, rank: 5, trend: "same", nationality: "VN" },
  { id: "j6", name: "Võ Thị F", points: 1650, totalRaces: 28, wins: 9, winRate: 32, rank: 6, trend: "up", nationality: "VN" },
  { id: "j7", name: "Đặng Văn G", points: 1420, totalRaces: 25, wins: 7, winRate: 28, rank: 7, trend: "down", nationality: "VN" },
  { id: "j8", name: "Bùi Thị H", points: 1200, totalRaces: 22, wins: 6, winRate: 27, rank: 8, trend: "new", nationality: "VN" },
];

const FALLBACK_HORSES = [
  { id: "h1", name: "Thunder Strike", points: 3120, totalRaces: 48, wins: 22, winRate: 46, breed: "Thoroughbred", age: 5, trend: "up", ownerName: "Nguyễn Văn A" },
  { id: "h2", name: "Silver Comet", points: 2890, totalRaces: 42, wins: 19, winRate: 45, breed: "Arabian", age: 4, trend: "up", ownerName: "Trần Thị B" },
  { id: "h3", name: "Midnight Runner", points: 2540, totalRaces: 38, wins: 16, winRate: 42, breed: "Thoroughbred", age: 6, trend: "down", ownerName: "Lê Văn C" },
  { id: "h4", name: "Wind Dancer", points: 2210, totalRaces: 35, wins: 13, winRate: 37, breed: "Arabian", age: 4, trend: "up", ownerName: "Phạm Minh D" },
  { id: "h5", name: "Golden Flash", points: 1980, totalRaces: 30, wins: 11, winRate: 37, breed: "Quarter Horse", age: 5, trend: "same", ownerName: "Hoàng Kim E" },
  { id: "h6", name: "Storm Breaker", points: 1750, totalRaces: 28, wins: 9, winRate: 32, breed: "Thoroughbred", age: 3, trend: "up", ownerName: "Võ Thị F" },
  { id: "h7", name: "Shadow Fox", points: 1520, totalRaces: 24, wins: 8, winRate: 33, breed: "Arabian", age: 4, trend: "down", ownerName: "Đặng Văn G" },
  { id: "h8", name: "Crimson King", points: 1300, totalRaces: 20, wins: 6, winRate: 30, breed: "Thoroughbred", age: 5, trend: "new", ownerName: "Bùi Thị H" },
];

const TREND_LABELS = { up: "↑", down: "↓", same: "→", new: "NEW" };
const TREND_COLORS = { up: "#10b981", down: "#ef4444", same: "#64748b", new: "#8f6420" };
const MEDALS = ["🥇", "🥈", "🥉"];

function DetailPanel({ item, type, onClose }) {
  if (!item) return null;
  return (
    <div className="lbd-panel-overlay" onClick={onClose}>
      <div className="lbd-panel" onClick={e => e.stopPropagation()}>
        <button className="lbd-panel-close" onClick={onClose}>×</button>
        <div className="lbd-panel-avatar">{item.name?.charAt(0) || "?"}</div>
        <h2>{item.name}</h2>
        <div className="lbd-panel-stats">
          <div><span>Điểm</span><strong>{item.points?.toLocaleString()}</strong></div>
          <div><span>Win Rate</span><strong>{item.winRate}%</strong></div>
          <div><span>Cuộc đua</span><strong>{item.totalRaces}</strong></div>
          <div><span>Thắng</span><strong>{item.wins}</strong></div>
          {type === "horse" && <div><span>Giống</span><strong>{item.breed || "-"}</strong></div>}
          {type === "horse" && <div><span>Chủ</span><strong>{item.ownerName || "-"}</strong></div>}
          {type === "jockey" && <div><span>Quốc tịch</span><strong>{item.nationality || "VN"}</strong></div>}
        </div>
      </div>
    </div>
  );
}

export default function LeaderboardPage() {
  const [tab, setTab] = useState("jockey");
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState(null);
  const [sortBy, setSortBy] = useState("points");
  const [sortAsc, setSortAsc] = useState(false);
  const [limit, setLimit] = useState(50);

  useEffect(() => {
    setLoading(true);
    const api = tab === "jockey" ? "/api/leaderboard/jockeys" : "/api/leaderboard/horses";
    (async () => {
      try {
        const d = await request(api);
        setData(Array.isArray(d) ? d : []);
      } catch {
        setData(tab === "jockey" ? FALLBACK_JOCKEYS : FALLBACK_HORSES);
      } finally {
        setLoading(false);
      }
    })();
  }, [tab]);

  const filtered = useMemo(() => {
    let items = [...data];
    if (search.trim()) items = items.filter(r => (r.name || "").toLowerCase().includes(search.toLowerCase()));
    items.sort((a, b) => {
      let c = 0;
      if (sortBy === "points") c = (a.points || 0) - (b.points || 0);
      else if (sortBy === "winrate") c = (a.winRate || 0) - (b.winRate || 0);
      else if (sortBy === "wins") c = (a.wins || 0) - (b.wins || 0);
      return sortAsc ? c : -c;
    });
    return items.slice(0, limit);
  }, [data, search, sortBy, sortAsc, limit]);

  const top3 = filtered.slice(0, 3);
  const rest = filtered.slice(3);
  const bestWinRate = data.length > 0 ? Math.max(...data.map(d => d.winRate || 0)) : 0;
  const totalWins = data.reduce((s, d) => s + (d.wins || 0), 0);
  const myRank = 1; // placeholder

  const trendIcon = (t) => {
    const c = TREND_COLORS[t] || "#64748b";
    return <span style={{ color: c, fontWeight: 700, fontSize: 12 }}>{TREND_LABELS[t] || ""}</span>;
  };

  return (
    <div className="lbd-page">
      {/* Header */}
      <div className="lbd-header">
        <h1>Bảng xếp hạng</h1>
        <p className="lbd-sub">Theo dõi thứ hạng kỵ sĩ và ngựa trên toàn hệ thống.</p>
      </div>

      {/* Segmented Control */}
      <div className="lbd-segmented">
        <button className={`lbd-seg ${tab === "jockey" ? "lbd-seg--active" : ""}`} onClick={() => { setTab("jockey"); setSelected(null); }}>Kỵ sĩ</button>
        <button className={`lbd-seg ${tab === "horse" ? "lbd-seg--active" : ""}`} onClick={() => { setTab("horse"); setSelected(null); }}>Ngựa</button>
      </div>

      {/* KPI */}
      <div className="lbd-kpis">
        <div className="lbd-kpi"><span>Hạng của tôi</span><strong>#{myRank}</strong></div>
        <div className="lbd-kpi"><span>Tổng điểm</span><strong>{data.reduce((s, d) => s + (d.points || 0), 0).toLocaleString()}</strong></div>
        <div className="lbd-kpi lbd-kpi--light"><span>Tổng thắng</span><strong>{totalWins}</strong></div>
        <div className="lbd-kpi"><span>Win Rate cao nhất</span><strong>{bestWinRate}%</strong></div>
      </div>

      {/* Top 3 Podium */}
      {!loading && top3.length > 0 && (
        <div className="lbd-podium">
          {top3.map((item, i) => (
            <div key={item.id} className={`lbd-pod lbd-pod--${i + 1}`} onClick={() => setSelected({ ...item, _type: tab })}>
              <span className="lbd-pod-medal">{MEDALS[i]}</span>
              <div className="lbd-pod-avatar">{item.name?.charAt(0) || "?"}</div>
              <h3>{item.name}</h3>
              <div className="lbd-pod-stats">
                <span><strong>{item.points?.toLocaleString()}</strong> điểm</span>
                <span><strong>{item.winRate}%</strong> WR</span>
                <span><strong>{item.wins}</strong> thắng</span>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Table */}
      <div className="lbd-table-card">
        <div className="lbd-toolbar">
          <div className="lbd-search">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
            <input placeholder="Tìm kiếm..." value={search} onChange={e => setSearch(e.target.value)} />
          </div>
          <div className="lbd-sort-group">
            {["points", "winrate", "wins"].map(s => (
              <button key={s} className={`lbd-sort ${sortBy === s ? "lbd-sort--active" : ""}`} onClick={() => { if (sortBy === s) setSortAsc(!sortAsc); else { setSortBy(s); setSortAsc(false); } }}>
                {s === "points" ? "Điểm" : s === "winrate" ? "WR" : "Thắng"}{sortBy === s && <span className="lbd-arrow">{sortAsc ? "↑" : "↓"}</span>}
              </button>
            ))}
          </div>
          <div className="lbd-limit-group">
            {[10, 50].map(n => <button key={n} className={`lbd-limit ${limit === n ? "lbd-limit--active" : ""}`} onClick={() => setLimit(n)}>Top {n}</button>)}
          </div>
        </div>

        {loading ? (
          <div className="lbd-loading"><div className="lbd-sk"/><div className="lbd-sk"/><div className="lbd-sk"/></div>
        ) : (
          <>
            <div className="lbd-table">
              <div className="lbd-tr lbd-tr--head">
                <span>#</span><span>Tên</span><span>Điểm</span><span>WR</span><span>Thắng</span><span>Xu hướng</span>
              </div>
              {rest.map((item, idx) => {
                const rank = idx + 4;
                return (
                  <div key={item.id} className={`lbd-tr ${selected?.id === item.id ? "lbd-tr--sel" : ""}`} onClick={() => setSelected({ ...item, _type: tab })}>
                    <span className="lbd-rank">{rank}</span>
                    <span><strong>{item.name}</strong></span>
                    <span>{item.points?.toLocaleString()}</span>
                    <span><span className="lbd-badge">{item.winRate}%</span></span>
                    <span>{item.wins}</span>
                    <span>{trendIcon(item.trend)}</span>
                  </div>
                );
              })}
            </div>
            {rest.length === 0 && !loading && <div className="lbd-empty">Không tìm thấy dữ liệu.</div>}
          </>
        )}
      </div>

      {/* Detail Panel */}
      {selected && <DetailPanel item={selected} type={selected._type} onClose={() => setSelected(null)} />}
    </div>
  );
}
