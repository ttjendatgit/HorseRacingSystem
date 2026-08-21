import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import "./RefereeDashboardPage.css";

const STATUS_PILL = {
  Confirmed: "rd-pill--Confirmed",
  Completed: "rd-pill--Completed",
  Assigned:  "rd-pill--Assigned",
  Pending:   "rd-pill--Pending",
  Rejected:  "rd-pill--Rejected",
  Cancelled: "rd-pill--Rejected",
};

//Helper function to format date in Vietnamese locale
function fmtDate(v) {
  if (!v) return "—";
  return new Date(v).toLocaleDateString("vi-VN", { day: "2-digit", month: "short", year: "numeric" });
}

//Racetrack 
function RacetrackSVG() {
  return (
    <svg viewBox="0 0 320 200" fill="none" xmlns="http://www.w3.org/2000/svg">
      {/* Track surface — oval */}
      <rect x="18" y="38" width="284" height="124" rx="62" fill="#E8E2D7" stroke="#D4CFC4" strokeWidth="1" />
      {/* Inner grass */}
      <rect x="46" y="58" width="228" height="84" rx="42" fill="#D8E6D0" stroke="#C5D6BB" strokeWidth="0.5" />

      {/* Lane lines */}
      <ellipse cx="160" cy="100" rx="130" ry="54" stroke="rgba(0,0,0,0.06)" strokeWidth="0.5" fill="none" />
      <ellipse cx="160" cy="100" rx="112" ry="46" stroke="rgba(0,0,0,0.06)" strokeWidth="0.5" fill="none" />

      {/* Finish line (checkered) */}
      <defs>
        <pattern id="checker" x="0" y="0" width="8" height="8" patternUnits="userSpaceOnUse">
          <rect x="0" y="0" width="4" height="4" fill="#f2d28b" />
          <rect x="4" y="4" width="4" height="4" fill="#f2d28b" />
          <rect x="4" y="0" width="4" height="4" fill="#D4CFC4" />
          <rect x="0" y="4" width="4" height="4" fill="#D4CFC4" />
        </pattern>
      </defs>
      <rect x="157" y="38" width="6" height="124" rx="1" fill="url(#checker)" opacity="0.8" />

      {/* Horses (3 silhouettes) */}
      {/* Horse 1 — leading */}
      <g transform="translate(210, 74)" opacity="0.95">
        <path d="M0 8 C2 0,6 -2,10 0 L14 -4 L13 1 C16 0,18 2,16 6 L15 8 L10 10 L6 10 Z" fill="#f2d28b" />
        <rect x="2" y="10" width="4" height="6" rx="1" fill="#f2d28b" />
        <rect x="10" y="10" width="4" height="6" rx="1" fill="#f2d28b" />
        <circle cx="3" cy="3" r="1.5" fill="#1a1d23" />
      </g>

      {/* Horse 2 — mid-pack */}
      <g transform="translate(130, 72) scale(0.85)" opacity="0.75">
        <path d="M0 8 C2 0,6 -2,10 0 L14 -4 L13 1 C16 0,18 2,16 6 L15 8 L10 10 L6 10 Z" fill="#d7aa4d" />
        <rect x="2" y="10" width="4" height="6" rx="1" fill="#d7aa4d" />
        <rect x="10" y="10" width="4" height="6" rx="1" fill="#d7aa4d" />
        <circle cx="3" cy="3" r="1.5" fill="#1a1d23" />
      </g>

      {/* Horse 3 — trailing */}
      <g transform="translate(48, 80) scale(0.7)" opacity="0.55">
        <path d="M0 8 C2 0,6 -2,10 0 L14 -4 L13 1 C16 0,18 2,16 6 L15 8 L10 10 L6 10 Z" fill="#64748b" />
        <rect x="2" y="10" width="4" height="6" rx="1" fill="#64748b" />
        <rect x="10" y="10" width="4" height="6" rx="1" fill="#64748b" />
        <circle cx="3" cy="3" r="1.5" fill="#1a1d23" />
      </g>

      {/* Start/Finish label */}
      <text x="160" y="176" textAnchor="middle" fontSize="9" fill="#64748b" fontFamily="sans-serif">XUẤT PHÁT / VỀ ĐÍCH</text>
    </svg>
  );
}

//Sort arrow component
function SortArrow({ dir }) {
  if (!dir) return <span className="rd-sort-icon">&#8645;</span>;
  return <span className="rd-sort-icon">{dir === "asc" ? "▲" : "▼"}</span>;
}

/* ==================================================================
   Page Component
   ================================================================== */
export default function RefereeDashboardPage() {
  const navigate = useNavigate();
  const [assignments, setAssignments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [sortKey, setSortKey] = useState("assignedAt");
  const [sortDir, setSortDir] = useState("desc");

  //Fetch API data on mount
  useEffect(() => {
    let alive = true;
    const load = async () => {
      try {
        const data = await getMyAssignments();
        if (!alive) return;
        const list = Array.isArray(data?.data) ? data.data : Array.isArray(data) ? data : [];
        setAssignments(list);
      } catch (e) {
        if (!alive) return;
        setError(e.message);
        setAssignments([]);
      } finally {
        if (alive) setLoading(false);
      }
    };
    load();
    return () => { alive = false; };
  }, []);

  //KPI counts derived from assignments
  const {
    totalAssignments,
    confirmedCount,
    completedCount,
    pendingCount,
    healthCheckCount,
    infractionCount,
    infractionSpark,
  } = useMemo(() => {
    const total = assignments.length;
    const confirmed = assignments.filter(a => a.status === "Confirmed").length;
    const completed = assignments.filter(a => a.status === "Completed").length;
    const pending = assignments.filter(a => a.status === "Assigned").length;
    return {
      totalAssignments: total,
      confirmedCount: confirmed,
      completedCount: completed,
      pendingCount: pending,
      healthCheckCount: 0,
      infractionCount: 0,
      infractionSpark: [],
    };
  }, [assignments]);

  //Sorting handler
  const handleSort = useCallback((key) => {
    setSortKey(prev => {
      if (prev === key) {
        setSortDir(d => (d === "asc" ? "desc" : "asc"));
        return prev;
      }
      setSortDir("asc");
      return key;
    });
  }, []);

  //Filtered and sorted assignments for display
  const displayed = useMemo(() => {
    const term = searchTerm.toLowerCase().trim();
    let items = term
      ? assignments.filter(a =>
          (a.raceName ?? "").toLowerCase().includes(term) ||
          (a.role ?? "").toLowerCase().includes(term) ||
          (a.status ?? "").toLowerCase().includes(term)
        )
      : assignments;

    items = [...items].sort((x, y) => {
      let a, b;
      switch (sortKey) {
        case "raceName": a = x.raceName ?? ""; b = y.raceName ?? ""; break;
        case "role":     a = x.role ?? "";     b = y.role ?? "";     break;
        case "status":   a = x.status ?? "";   b = y.status ?? "";   break;
        default:         a = x.assignedAt ?? ""; b = y.assignedAt ?? ""; break;
      }
      if (typeof a === "string") {
        return sortDir === "asc" ? a.localeCompare(b) : b.localeCompare(a);
      }
      return sortDir === "asc" ? (a > b ? 1 : -1) : (a > b ? -1 : 1);
    });
    return items;
  }, [assignments, searchTerm, sortKey, sortDir]);

  //Status pill mapping
  const pillClass = (s) => STATUS_PILL[s] || "rd-pill--Pending";

  //Donut segments
  const donutPct = useMemo(() => {
    const total = assignments.length || 1;
    const conf = (confirmedCount / total) * 100;
    const comp = (completedCount / total) * 100;
    const pend = (pendingCount / total) * 100;
    const rej = 100 - conf - comp - pend;
    return { conf, comp, pend, rej: Math.max(rej, 0) };
  }, [assignments, confirmedCount, completedCount, pendingCount]);

  const donutGradient = `conic-gradient(
    var(--rd-gold-dim) 0deg ${donutPct.conf * 3.6}deg,
    var(--rd-green) ${donutPct.conf * 3.6}deg ${(donutPct.conf + donutPct.comp) * 3.6}deg,
    var(--rd-blue) ${(donutPct.conf + donutPct.comp) * 3.6}deg ${(donutPct.conf + donutPct.comp + donutPct.pend) * 3.6}deg,
    rgba(0,0,0,0.06) ${(donutPct.conf + donutPct.comp + donutPct.pend) * 3.6}deg 360deg
  )`;

  //Bar chart data
  const barData = [
    { label: "T1", value: 0 },
    { label: "T2", value: 0 },
    { label: "T3", value: 0 },
    { label: "T4", value: 0 },
  ];
  const maxBar = Math.max(...barData.map(b => b.value), 1);

  return (
    <div className="rd-wrap">
      <div className="rd-container">
        {/* ════════ Top Bar ════════ */}
        <header className="rd-topbar">
          <div>
            <h1>Bảng điều khiển</h1>
            <p className="rd-topbar-sub">
              Quản lý phân công trận đấu và duy trì tiêu chuẩn liêm chính cao nhất.
            </p>
          </div>
          <div className="rd-topbar-badge">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
            </svg>
            {new Date().toLocaleDateString("vi-VN", { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
          </div>
        </header>

        {/* ════════ Compact KPI Bar ════════ */}
        <div className="rd-kpi-bar">
          {/* — KPI 1: Total Assignments + mini bars — */}
          <div className="rd-kpi-card">
            <span className="rd-kpi-label">Tổng phân công</span>
            <div className="rd-kpi-value">{totalAssignments}</div>
            <div className="rd-mini-bars">
              <div className="rd-mini-bar-item">
                <span>Đã xác nhận</span>
                <div className="rd-mini-bar-label">
                  <span>{confirmedCount}</span>
                  <span>{totalAssignments ? Math.round((confirmedCount / totalAssignments) * 100) : 0}%</span>
                </div>
                <div className="rd-mini-bar-track">
                  <div className="rd-mini-bar-fill rd-mini-bar-fill--gold" style={{ width: `${totalAssignments ? (confirmedCount / totalAssignments) * 100 : 0}%` }} />
                </div>
              </div>
              <div className="rd-mini-bar-item">
                <span>Hoàn thành</span>
                <div className="rd-mini-bar-label">
                  <span>{completedCount}</span>
                  <span>{totalAssignments ? Math.round((completedCount / totalAssignments) * 100) : 0}%</span>
                </div>
                <div className="rd-mini-bar-track">
                  <div className="rd-mini-bar-fill rd-mini-bar-fill--green" style={{ width: `${totalAssignments ? (completedCount / totalAssignments) * 100 : 0}%` }} />
                </div>
              </div>
            </div>
          </div>

          {/* — KPI 2: Active Health Checks — */}
          <div className="rd-kpi-card">
            <span className="rd-kpi-label">Kiểm tra sức khoẻ</span>
            <div className="rd-kpi-value">{healthCheckCount}</div>
            <div className="rd-kpi-row">
              <span style={{ fontSize: 12, color: "#64748b" }}>Đang chờ xử lý</span>
            </div>
          </div>

          {/* — KPI 3: Recent Infractions + sparkline — */}
          <div className="rd-kpi-card">
            <span className="rd-kpi-label">Vi phạm gần đây</span>
            <div className="rd-kpi-value">{infractionCount}</div>
            <div className="rd-sparkline">
              {infractionSpark.map((h, i) => (
                <div key={i} className="rd-spark-bar" style={{ height: `${(h / Math.max(...infractionSpark, 1)) * 100}%` }} />
              ))}
            </div>
          </div>
        </div>

        {/* ════════ Racetrack + Assignments Table ════════ */}
        <div className="rd-content-grid">
          {/* — Racetrack Widget — */}
          <div className="rd-track-card">
            <div className="rd-track-header">
              <h3>Đường đua</h3>
              <span className="rd-track-status">Đang hoạt động</span>
            </div>
            <div className="rd-track-svg-wrap">
              <RacetrackSVG />
            </div>
          </div>

          {/* — Assignments Table — */}
          <div className="rd-table-card">
            <div className="rd-table-header">
              <h3>Phân công</h3>
              <div className="rd-search">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
                <input
                  type="text"
                  placeholder="Tìm kiếm..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>

            <div className="rd-table-wrap">
              {loading ? (
                <div className="rd-loading-dots">
                  <span /><span /><span />
                </div>
              ) : displayed.length === 0 ? (
                <div className="rd-table-empty">
                  {searchTerm ? "Không tìm thấy kết quả phù hợp." : "Chưa có phân công nào."}
                </div>
              ) : (
                <table className="rd-table">
                  <thead>
                    <tr>
                      <th onClick={() => handleSort("raceName")} className={sortKey === "raceName" ? "rd-sort-active" : ""}>
                        Cuộc đua <SortArrow dir={sortKey === "raceName" ? sortDir : null} />
                      </th>
                      <th onClick={() => handleSort("role")} className={sortKey === "role" ? "rd-sort-active" : ""}>
                        Vai trò <SortArrow dir={sortKey === "role" ? sortDir : null} />
                      </th>
                      <th onClick={() => handleSort("assignedAt")} className={sortKey === "assignedAt" ? "rd-sort-active" : ""}>
                        Ngày <SortArrow dir={sortKey === "assignedAt" ? sortDir : null} />
                      </th>
                      <th onClick={() => handleSort("status")} className={sortKey === "status" ? "rd-sort-active" : ""}>
                        Trạng thái <SortArrow dir={sortKey === "status" ? sortDir : null} />
                      </th>
                      <th style={{ width: 70 }}> </th>
                    </tr>
                  </thead>
                  <tbody>
                    {displayed.map((a) => (
                      <tr key={a.id}>
                        <td style={{ fontWeight: 500 }}>{a.raceName || "Cuộc đua"}</td>
                        <td className="rd-cell-role">{a.role || "Trọng tài"}</td>
                        <td className="rd-cell-date">{fmtDate(a.assignedAt)}</td>
                        <td>
                          <span className={`rd-pill ${pillClass(a.status)}`}>
                            {a.status === "Confirmed" ? "Đã xác nhận" :
                             a.status === "Completed" ? "Hoàn thành" :
                             a.status === "Assigned" ? "Đã phân công" :
                             a.status === "Rejected" || a.status === "Cancelled" ? "Từ chối" : a.status}
                          </span>
                        </td>
                        <td>
                          <button className="rd-btn-detail">Chi tiết</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

        {/* ════════ Analytics Insight Section ════════ */}
        <div className="rd-analytics">
          {/* — Donut Chart: Report Breakdown — */}
          <div className="rd-analytics-card">
            <h3>Phân tích báo cáo</h3>
            <div className="rd-donut-wrap">
              <div className="rd-donut" style={{ background: donutGradient }}>
                <div className="rd-donut-hole">
                  <strong>{assignments.length}</strong>
                  <span>Tổng</span>
                </div>
              </div>
              <div className="rd-donut-legend">
                <div className="rd-donut-legend-item">
                  <span className="rd-dot rd-dot--gold" />
                  Đã xác nhận
                  <span className="rd-lbl">{confirmedCount}</span>
                </div>
                <div className="rd-donut-legend-item">
                  <span className="rd-dot rd-dot--green" />
                  Hoàn thành
                  <span className="rd-lbl">{completedCount}</span>
                </div>
                <div className="rd-donut-legend-item">
                  <span className="rd-dot rd-dot--blue" />
                  Đã phân công
                  <span className="rd-lbl">{pendingCount}</span>
                </div>
                <div className="rd-donut-legend-item">
                  <span className="rd-dot" style={{ background: "rgba(0,0,0,0.06)" }} />
                  Khác
                  <span className="rd-lbl">{Math.max(0, assignments.length - confirmedCount - completedCount - pendingCount)}</span>
                </div>
              </div>
            </div>
          </div>

          {/* — Small Bar Chart: Infractions over time — */}
          <div className="rd-analytics-card">
            <h3>Vi phạm theo tháng</h3>
            <div className="rd-bar-chart">
              {barData.map((b) => (
                <div key={b.label} className="rd-bar-col">
                  <span className="rd-bar-value">{b.value}</span>
                  <div className="rd-bar" style={{ height: `${(b.value / maxBar) * 100}%` }} />
                  <span className="rd-bar-label">{b.label}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* ════════ Error / Info banners ════════ */}
        {error && assignments.length === 0 && (
          <div className="rd-error">{error}</div>
        )}
        {error && assignments.length > 0 && (
          <div className="rd-info">Đã xảy ra lỗi khi tải một phần dữ liệu.</div>
        )}
      </div>
    </div>
  );
}
