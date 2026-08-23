import { useEffect, useMemo, useState } from "react";
import { NavLink, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  assignHorseToRace,
  cancelRace,
  createRound,
  createTournament,
  deleteTournament,
  endRace,
  generateNextRound,
  getAdminDashboard,
  getAdminUser,
  getAdminUsers,
  getAdminTournaments,
  getOwnerHorse,
  getOwnerHorses,
  getTournamentApprovedHorses,
  getTournamentRaces,
  getTournamentRounds,
  approveRaceResult,
  rejectRaceResult,
  getPendingRaceEntries,
  approveRaceEntry,
  rejectRaceEntry,
  setUserActive,
  startRace,
  updateOwnerHorseStatus,
  updateRound,
  updateTournament,
} from "../../services/adminApi";
import { getAvailableJockeys } from "../../services/jockeyApi";
import { resolveApiUrl } from "../../services/apiClient";
import { request } from "../../services/apiClient";
import { canEditTournamentStructure, canHardDeleteTournament, getTournamentLifecycleLabel, isFinalRound } from "../../utils/tournamentRegistration";
import { getPlacementLabel, getRankedEntries } from "../../utils/raceResultDisplay";
import { groupJockeysByApprovalStatus } from "../../utils/jockeyAdminReview";
import JockeyReviewModal from "../../components/JockeyReviewModal";
import {
  PrizeManagement,
  ProtestManagement,
  TransferManagement,
  ContractManagement,
  InjuryManagement,
} from "./AdminOperations";
import { AuditLogViewer, NotificationManager } from "./AdminAudit";
import TournamentForm from "../../components/TournamentForm";
import RaceForm from "../../components/RaceForm";
import RaceResultsPage from "./pages/RaceResultsPage";
import HorseManagementPage from "./pages/HorseManagementPage";
import RefereeManagementPage from "./pages/RefereeManagementPage";
import TournamentDetail from "./pages/TournamentDetail";
import PredictionsManagementPage from "./pages/PredictionsManagementPage";
import { apiToVNInput, apiToVNDisplay, apiToVNDate, apiToUtcDate, vnInputToApiUtc, vnNowInput } from "../../utils/vnDateTime";
import "./AdminPage.css";

function AdminHorseImage({ imageUrl, name, className = "" }) {
  const [hasError, setHasError] = useState(false);
  const resolvedUrl = resolveApiUrl(imageUrl);
  const initial = String(name || "H").trim().slice(0, 1).toUpperCase();

  useEffect(() => {
    setHasError(false);
  }, [resolvedUrl]);

  return (
    <div className={`admin-horse-image ${className}`.trim()}>
      {resolvedUrl && !hasError ? (
        <img
          src={resolvedUrl}
          alt={name ? `${name} ngựa` : "Ngựa"}
          onError={() => setHasError(true)}
        />
      ) : (
        <div className="admin-horse-image__fallback" aria-label="Không có ảnh ngựa">
          <span>{initial}</span>
          <small>Không có ảnh</small>
        </div>
      )}
    </div>
  );
}

const navGroups = [
  { label: "Dashboard", items: [{ to: "/admin", label: "Tổng quan", icon: "M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" }] },
  { label: "Users", items: [
    { to: "/admin/users", label: "Người dùng", icon: "M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197m13.5-9a2.5 2.5 0 11-5 0 2.5 2.5 0 015 0z" },
    { to: "/admin/registrations", label: "Đăng ký", icon: "M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" },
    { to: "/admin/roles", label: "Vai trò", icon: "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" },
  ] },
  { label: "Tournaments", items: [
    { to: "/admin/tournaments", label: "Giải đấu", icon: "M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z" },
    { to: "/admin/rounds", label: "Vòng đấu", icon: "M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4" },
    { to: "/admin/races", label: "Cuộc đua", icon: "M13 10V3L4 14h7v7l9-11h-7z" },
    { to: "/admin/race-results", label: "Kết quả", icon: "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" },
    { to: "/admin/referee-assign", label: "Phân công trọng tài", icon: "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" },
  ] },
  { label: "Management", items: [
    { to: "/admin/horses", label: "Ngựa", icon: "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" },
    { to: "/admin/referees", label: "Trọng tài", icon: "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" },
  ] },
  { label: "Operations", items: [
    { to: "/admin/prizes", label: "Tiền thưởng", icon: "M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" },
    { to: "/admin/protests", label: "Khiếu nại", icon: "M3 21l4-4V5a2 2 0 012-2h6a2 2 0 012 2v12l4 4" },
    { to: "/admin/transfers", label: "Chuyển nhượng", icon: "M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" },
    { to: "/admin/contracts", label: "Hợp đồng", icon: "M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" },
    { to: "/admin/injuries", label: "Chấn thương", icon: "M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" },
  ] },
  { label: "System", items: [
    { to: "/admin/audit", label: "Nhật ký", icon: "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" },
    { to: "/admin/notifications", label: "Thông báo", icon: "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" },
  ] },
  { label: "Finance", items: [
    { to: "/admin/predictions", label: "Dự đoán", icon: "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" },
    { to: "/admin/withdrawals", label: "Rút tiền", icon: "M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" },
  ] },
];

// Vietnam-timezone policy (Asia/Ho_Chi_Minh, UTC+7) — see FE/src/utils/vnDateTime.js. The backend
// serializes every Tournament/Round/Race (and other) DateTime as a naive UTC instant (no Z/offset,
// Npgsql legacy-timestamp mode — see BE/Program.cs), so display/input conversion always goes
// through that shared utility rather than new Date(value) + the browser's own local timezone.
const formatDate = (value) => (value ? apiToVNDate(value) : "-");

const formatDateTime = (value) => (value ? apiToVNDisplay(value) : "-");

const inputDate = (days = 0) => vnNowInput(days);

const isGuid = (value) =>
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value,
  );

const canOwnHorses = (role) => {
  const normalizedRole = String(role ?? "").replace(/[_\s-]/g, "").toLowerCase();
  return normalizedRole === "horseowner" || normalizedRole === "jockey";
};

function AdminShell({ children }) {
  const location = useLocation();
  const [expanded, setExpanded] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsedGroups, setCollapsedGroups] = useState({ "Operations": true });

  const toggleGroup = (label) => {
    setCollapsedGroups(prev => ({ ...prev, [label]: !prev[label] }));
  };

  useEffect(() => { setMobileOpen(false); }, [location.pathname]);

  return (
    <div className="ad-layout">
      {/* Mobile sidebar toggle */}
      <button className={`ad-mobile-toggle ${mobileOpen ? "ad-mobile-toggle--open" : ""}`} onClick={() => setMobileOpen(!mobileOpen)}>
        <span /><span /><span />
      </button>

      {/* Mini Sidebar */}
      <aside className={`ad-sidebar ${expanded ? "ad-sidebar--exp" : ""} ${mobileOpen ? "ad-sidebar--mobile-open" : ""}`}
        onMouseEnter={() => setExpanded(true)}
        onMouseLeave={() => setExpanded(false)}
      >
        <div className="ad-sidebar__logo">
          <img src="/logo.png" alt="RaceMaster" />
          {expanded && <span className="ad-sidebar__title">RaceMaster</span>}
        </div>
        <nav className="ad-sidebar__nav">
          {navGroups.map((group) => {
            const isCollapsed = collapsedGroups[group.label] || false;
            return (
              <div key={group.label} className="ad-nav-group">
                <button
                  className="ad-nav-group__header"
                  onClick={(e) => { e.preventDefault(); toggleGroup(group.label); }}
                  title={group.label}
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d={group.items[0].icon} />
                  </svg>
                  {expanded && (
                    <>
                      <span className="ad-nav-group__label">{group.label}</span>
                      <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor" style={{ marginLeft: "auto", transform: isCollapsed ? "rotate(-90deg)" : "rotate(0)", transition: "transform 0.15s" }}>
                        <path d="M4 2l4 4-4 4" />
                      </svg>
                    </>
                  )}
                </button>
                {(!expanded || !isCollapsed) && (
                  <div className="ad-nav-group__items">
                    {group.items.map((item) => {
                      const isActive = item.to === "/admin" ? location.pathname === "/admin" : location.pathname.startsWith(item.to);
                      return (
                        <NavLink key={item.to} to={item.to} end={item.end} className={`ad-nav-link ${isActive ? "ad-nav-link--active" : ""}`}>
                          {!expanded ? (
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                              <path d={item.icon} />
                            </svg>
                          ) : (
                            <span className="ad-nav-label">{item.label}</span>
                          )}
                          {!expanded && (
                            <span className="ad-nav-link__tooltip">{item.label}</span>
                          )}
                        </NavLink>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </nav>
      </aside>

      {/* Main */}
      <div className="ad-main">
        <div className="ad-content">{children}</div>
      </div>
    </div>
  );
}

function PageTitle({ eyebrow, title, description, action }) {
  return (
    <section className="admin-title">
      <div>
        <span>{eyebrow}</span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </section>
  );
}

function Notice({ message, error }) {
  return message ? <p className={error ? "admin-notice admin-notice--error" : "admin-notice"}>{message}</p> : null;
}

function Dashboard() {
  const navigate = useNavigate();
  const [data, setData] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    getAdminDashboard().then(setData).catch((err) => setError(err.message));
  }, []);

  const stats = [
    { label: "Tổng người dùng", value: data?.totalUsers ?? data?.TotalUsers ?? "-", trend: "+12%", icon: "M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197", color: "#10b981" },
    { label: "Giải đấu", value: data?.activeTournaments ?? data?.ActiveTournaments ?? "-", trend: "+3", icon: "M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z", color: "#f2d28b" },
    { label: "Trực tiếp", value: data?.ongoingRaces ?? data?.OngoingRaces ?? "-", trend: "live", icon: "M13 10V3L4 14h7v7l9-11h-7z", color: "#ef4444" },
    { label: "Sắp tới", value: data?.upcomingRaces ?? data?.UpcomingRaces ?? "-", trend: "+2 hôm nay", icon: "M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z", color: "#6366f1" },
  ];

  const recentActivities = (() => {
    const list = data?.recentActivity ?? data?.RecentActivity;
    if (!list || !Array.isArray(list) || list.length === 0) return [];
    return list.map(a => ({
      action: a.action ?? a.Action ?? "",
      subject: a.subject ?? a.Subject ?? "",
      time: a.createdAt ?? a.CreatedAt ? formatDate(a.createdAt ?? a.CreatedAt) : "",
      type: "registration"
    }));
  })();

  const pendingItems = [
    { label: "Đăng ký chờ duyệt", count: data?.pendingRegistrations ?? data?.PendingRegistrations ?? 0, priority: "high", path: "/admin/registrations" },
    { label: "Phân công trọng tài", count: data?.totalReferees ?? data?.TotalReferees ?? 0, priority: "medium", path: "/admin/referee-assign" },
  ];

  return (
    <>
      {/* Hero */}
      <div className="ad-hero">
        <div>
          <span className="pill" style={{ background: "rgba(215,170,77,0.15)", color: "#f2d28b" }}>Bảng điều khiển</span>
          <h1>Tổng quan hệ thống</h1>
          <p>Giám sát hoạt động nền tảng và duy trì vận hành đua.</p>
        </div>
        <div className="ad-hero-right">
          <div className="ad-hero-status">
            <span className="ad-dot ad-dot--green" />
            <span>Hệ thống hoạt động bình thường</span>
          </div>
          <div className="ad-quick-actions">
            <button className="ad-qa-btn" onClick={() => navigate("/admin/tournaments")}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 5v14M5 12h14"/></svg>
              Tạo giải đấu
            </button>
            <button className="ad-qa-btn" onClick={() => navigate("/admin/registrations")}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M16 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="8.5" cy="7" r="4"/><path d="M20 8v6M23 11h-6"/></svg>
              Duyệt đăng ký
            </button>
          </div>
        </div>
      </div>
      {error && <Notice message={error} error />}

      {/* KPI Cards */}
      <section className="ad-kpis">
        {stats.map((s) => (
          <div key={s.label} className="ad-kpi">
            <div className="ad-kpi__icon" style={{ background: `${s.color}15`, color: s.color }}>
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5"><path d={s.icon} /></svg>
            </div>
            <div className="ad-kpi__info">
              <span className="ad-kpi__label">{s.label}</span>
              <strong className="ad-kpi__value">{s.value}</strong>
              <span className={`ad-kpi__trend ${s.trend === "live" ? "ad-kpi__trend--live" : ""}`}>
                {s.trend === "live" ? "● Đang diễn ra" : s.trend}
              </span>
            </div>
            {/* Mini sparkline */}
            <div className="ad-kpi__spark">
              {[3,4,2,5,3,4,5].map((h, i) => (
                <span key={i} className="ad-spark-bar" style={{ height: `${h*5+4}px`, opacity: 0.4 + h*0.1 }} />
              ))}
            </div>
          </div>
        ))}
      </section>

      {/* Charts row */}
      <section className="ad-charts">
        <div className="ad-card ad-card--chart">
          <h3>Tăng trưởng người dùng</h3>
          <div className="ad-area-chart">
            <svg viewBox="0 0 300 100" className="ad-area-svg">
              <defs>
                <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#f2d28b" stopOpacity="0.3" />
                  <stop offset="100%" stopColor="#f2d28b" stopOpacity="0" />
                </linearGradient>
              </defs>
              <polyline fill="url(#areaGrad)" points="0,80 30,60 60,70 90,40 120,50 150,30 180,35 210,20 240,25 270,15 300,10 300,100 0,100" />
              <polyline fill="none" stroke="#f2d28b" strokeWidth="2" points="0,80 30,60 60,70 90,40 120,50 150,30 180,35 210,20 240,25 270,15 300,10" />
            </svg>
            <div className="ad-chart-labels">
              <span>T2</span><span>T4</span><span>T6</span><span>T8</span><span>T10</span><span>T12</span>
            </div>
          </div>
        </div>
        <div className="ad-card ad-card--chart">
          <h3>Phân bố giải đấu</h3>
          <div className="ad-donut">
            <div className="ad-donut-ring">
              <div className="ad-donut-hole"><strong>{data?.activeTournaments ?? data?.ActiveTournaments ?? 0}</strong><span>Hoạt động</span></div>
            </div>
            <div className="ad-donut-legend">
              <div><span style={{background:"#f2d28b"}} /><label>Đang mở</label><strong>{data?.activeTournaments ?? data?.ActiveTournaments ?? 0}</strong></div>
              <div><span style={{background:"#94a3b8"}} /><label>Đã đóng</label><strong>{data?.upcomingRaces ?? data?.UpcomingRaces ?? 0}</strong></div>
              <div><span style={{background:"#10b981"}} /><label>Sắp diễn ra</label><strong>8</strong></div>
            </div>
          </div>
        </div>
      </section>

      {/* 2-column: Live Races + Approval Queue */}
      <section className="ad-grid-cols">
        <div className="ad-card">
          <div className="ad-card__header">
            <h3>Cuộc đua trực tiếp & sắp tới</h3>
          </div>
          <div className="ad-card__body">
            {(() => {
              const activeRaces = data?.activeRaces ?? data?.ActiveRaces;
              if (!activeRaces || !Array.isArray(activeRaces) || activeRaces.length === 0) {
                return <p style={{ color: "var(--hr-muted)", padding: 20, textAlign: "center" }}>Không có cuộc đua nào đang diễn ra hoặc sắp tới.</p>;
              }
              return activeRaces.map((r, i) => {
                const status = (r.status ?? r.Status ?? "").toLowerCase();
                const isLive = status === "inprogress";
                return (
                  <div key={r.id ?? r.Id ?? i} className="ad-race-item">
                    <div className={`ad-race-dot ${isLive ? "ad-race-dot--live" : "ad-race-dot--upcoming"}`} />
                    <div>
                      <strong>{r.name ?? r.Name}</strong>
                      <span>{r.entryCount ?? r.EntryCount ?? 0} ngựa tham gia · {formatDate(r.scheduledAt ?? r.ScheduledAt)}</span>
                    </div>
                    <span className={`ad-chip ${isLive ? "ad-chip--live" : "ad-chip--upcoming"}`}>{isLive ? "Đang đua" : "Sắp tới"}</span>
                  </div>
                );
              });
            })()}
          </div>
        </div>

        <div className="ad-card">
          <div className="ad-card__header">
            <h3>Hành động chờ xử lý</h3>
            <span className="ad-count">{pendingItems.reduce((s, i) => s + (i.count || 0), 0)}</span>
          </div>
          <div className="ad-card__body">
            {pendingItems.map((item) => {
              const priorities = { high: { color: "#ef4444", bg: "rgba(239,68,68,0.06)", bar: "#ef4444" }, medium: { color: "#f59e0b", bg: "rgba(245,158,11,0.06)", bar: "#f59e0b" }, low: { color: "var(--hr-muted)", bg: "rgba(238,229,212,0.05)", bar: "var(--hr-muted)" } };
              const p = priorities[item.priority];
              return (
                <div key={item.label} className="ad-action-card" style={{ borderLeftColor: p.bar }}>
                  <div className="ad-action-card__top">
                    <div className="ad-action-card__info">
                      <strong>{item.label}</strong>
                      <span>{item.count} yêu cầu đang chờ</span>
                    </div>
                    <div className="ad-action-card__count" style={{ color: p.color }}>{item.count}</div>
                  </div>
                  <div className="ad-action-card__bar"><div style={{ width: `${Math.min((item.count||0)*20, 100)}%`, background: p.bar }} /></div>
                  <div className="ad-action-card__actions">
                    <button className="ad-btn-approve" onClick={() => navigate(item.path)}>Xem chi tiết</button>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* Activity Feed */}
      <section className="ad-card">
        <div className="ad-card__header">
          <h3>Hoạt động gần đây</h3>
        </div>
        <div className="ad-card__body">
          <div className="ad-feed">
            {recentActivities.length === 0 ? (
              <p style={{ color: "var(--hr-muted)", textAlign: "center", padding: 20 }}>Chưa có hoạt động nào.</p>
            ) : recentActivities.map((a, i) => (
              <div key={i} className="ad-feed-item">
                <div className="ad-feed-dot ad-feed-dot--green" />
                <div className="ad-feed-content">
                  <strong>{a.action}: {a.subject}</strong>
                  <span>{a.time}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}

function UserList() {
  const [users, setUsers] = useState([]);
  const [query, setQuery] = useState("");
  const [message, setMessage] = useState("");
  const navigate = useNavigate();

  const load = () => getAdminUsers().then((items) => setUsers(Array.isArray(items) ? items : [])).catch((err) => setMessage(err.message));
  useEffect(() => {
    load();
  }, []);

  const filtered = useMemo(() => users.filter((user) =>
    `${user.fullName ?? user.FullName ?? ""} ${user.email ?? user.Email ?? ""} ${user.role ?? user.Role ?? ""}`.toLowerCase().includes(query.toLowerCase())
  ), [query, users]);

  const toggle = async (user) => {
    const id = user.id ?? user.Id;
    const active = user.isActive ?? user.IsActive;
    try {
      await setUserActive(id, !active);
      setMessage(`Người dùng ${active ? "đã khóa" : "đã kích hoạt lại"} thành công.`);
      load();
    } catch (err) {
      setMessage(err.message);
    }
  };

  return (
    <>
      <PageTitle eyebrow="Quản lý người dùng" title="Danh sách người dùng" description="Tìm kiếm tài khoản, xem chi tiết và kiểm soát quyền truy cập." />
      <div className="admin-toolbar"><input placeholder="Tìm kiếm người dùng, email hoặc vai trò..." value={query} onChange={(e) => setQuery(e.target.value)} /><span>{filtered.length} người dùng</span></div>
      <Notice message={message} />
      <div className="admin-table-wrap">
        <table className="admin-table"><thead><tr><th>Người dùng</th><th>Vai trò</th><th>Tham gia</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>{filtered.map((user) => {
            const id = user.id ?? user.Id;
            const active = user.isActive ?? user.IsActive;
            return <tr key={id}>
              <td><strong>{user.fullName ?? user.FullName ?? "Người dùng chưa đặt tên"}</strong><small>{user.email ?? user.Email}</small></td>
              <td>{user.role ?? user.Role}</td><td>{formatDate(user.createdAt ?? user.CreatedAt)}</td>
              <td><span className={active ? "status status--active" : "status status--inactive"}>{active ? "Hoạt động" : "Đã khóa"}</span></td>
              <td><div className="admin-actions"><button onClick={() => navigate(`/admin/users/${id}`)}>Chi tiết</button><button onClick={() => toggle(user)}>{active ? "Khóa" : "Mở khóa"}</button></div></td>
            </tr>;
          })}</tbody>
        </table>
      </div>
    </>
  );
}

function UserDetail() {
  const { id } = useParams();
  const [user, setUser] = useState(null);
  const [horses, setHorses] = useState([]);
  const [horsesLoading, setHorsesLoading] = useState(false);
  const [horsesError, setHorsesError] = useState("");
  const [message, setMessage] = useState("");
  const navigate = useNavigate();

  useEffect(() => {
    let cancelled = false;

    getAdminUser(id)
      .then(async (userData) => {
        if (cancelled) return;

        setUser(userData);
        const userRole = userData?.role ?? userData?.Role;
        const horseCount = userData?.horseCount ?? userData?.HorseCount ?? 0;
        if (canOwnHorses(userRole) || horseCount > 0) {
          setHorsesLoading(true);
          setHorsesError("");
          try {
            const horseData = await getOwnerHorses(id);
            if (!cancelled) setHorses(Array.isArray(horseData) ? horseData : []);
          } catch (err) {
            if (!cancelled) setHorsesError(err.message);
          } finally {
            if (!cancelled) setHorsesLoading(false);
          }
        } else {
          setHorses([]);
        }
      })
      .catch((err) => {
        if (!cancelled) setMessage(err.message);
      });

    return () => {
      cancelled = true;
    };
  }, [id]);
  const active = user?.isActive ?? user?.IsActive;
  const role = user?.role ?? user?.Role;
  const horseCount = user?.horseCount ?? user?.HorseCount ?? 0;
  const showHorseManagement = canOwnHorses(role) || horseCount > 0;

  const toggle = async () => {
    try {
      await setUserActive(id, !active);
      setUser(await getAdminUser(id));
      setMessage(`Tài khoản ${active ? "đã khóa" : "đã kích hoạt lại"} thành công.`);
    } catch (err) { setMessage(err.message); }
  };

  const changeHorseStatus = async (horse, status) => {
    let note = null;
    if (status === "Rejected") {
      note = window.prompt("Nhập lý do từ chối:");
      if (!note?.trim()) return;
    }

    try {
      await updateOwnerHorseStatus(id, horse.id ?? horse.Id, { status, note });
      setMessage(`${horse.name ?? horse.Name} đã đổi thành ${status}.`);
      const horseData = await getOwnerHorses(id);
      setHorses(Array.isArray(horseData) ? horseData : []);
    } catch (err) {
      setMessage(err.message);
    }
  };

  return (
    <>
      <PageTitle eyebrow="Quản lý người dùng" title="Chi tiết người dùng" description="Xem thông tin tài khoản và trạng thái truy cập." action={<button className="ghost-button" onClick={() => navigate("/admin/users")}>Quay lại danh sách</button>} />
      <Notice message={message} />
      <article className="admin-profile">
        <div className="admin-profile__avatar">{(user?.fullName ?? user?.FullName ?? "U").slice(0, 1)}</div>
        <div><span className={active ? "status status--active" : "status status--inactive"}>{active ? "Hoạt động" : "Đã khóa"}</span><h2>{user?.fullName ?? user?.FullName ?? "Đang tải..."}</h2><p>{user?.email ?? user?.Email}</p></div>
        <button className="primary-button" onClick={toggle} disabled={!user}>{active ? "Khóa người dùng" : "Mở khóa người dùng"}</button>
      </article>
      <section className="admin-detail-grid">
        <div><span>Vai trò</span><strong>{role ?? "-"}</strong></div>
        <div><span>Ngày tạo</span><strong>{formatDate(user?.createdAt ?? user?.CreatedAt)}</strong></div>
        <div><span>Ngựa đã đăng ký</span><strong>{horseCount}</strong></div>
        <div><span>ID người dùng</span><strong>{id}</strong></div>
      </section>
      {showHorseManagement && <>
        <div className="section-heading">
          <h2>Ngựa của chủ sở hữu</h2>
          <p>Xem và thay đổi trạng thái phê duyệt cho từng con ngựa.</p>
        </div>
        {horsesLoading && <p className="admin-muted-note">Đang tải danh sách ngựa...</p>}
        {horsesError && <p className="admin-notice admin-notice--error">Không thể tải danh sách ngựa: {horsesError}</p>}
        {!horsesLoading && !horsesError && horses.length === 0 && <p className="admin-muted-note">Người dùng chưa có ngựa để duyệt.</p>}
        <section className="admin-horse-grid">
          {horses.map((horse) => {
            const status = horse.approvalStatus ?? horse.ApprovalStatus;
            const horseName = horse.name ?? horse.Name ?? "Ngựa chưa đặt tên";
            return <article key={horse.id ?? horse.Id} className="admin-horse-card">
              <div className="admin-horse-card__media">
                <AdminHorseImage
                  imageUrl={horse.imageUrl ?? horse.ImageUrl}
                  name={horseName}
                />
                <span className={`status status--${status?.toLowerCase()}`}>{status}</span>
              </div>
              <div className="admin-horse-card__body">
                <div className="admin-horse-card__heading">
                  <div>
                    <h3>{horseName}</h3>
                    <p>{horse.breed ?? horse.Breed ?? "Giống không xác định"} · {horse.gender ?? horse.Gender ?? "Giới tính không xác định"} · Tuổi {horse.age ?? horse.Age}</p>
                  </div>
                  <button
                    className="admin-horse-card__detail"
                    onClick={() => navigate(`/admin/users/${id}/horses/${horse.id ?? horse.Id}`)}
                  >
                    Xem chi tiết
                  </button>
                </div>
                <div className="admin-horse-card__stats">
                  <div><span>Số cuộc đua</span><strong>{horse.totalRaces ?? horse.TotalRaces ?? 0}</strong></div>
                  <div><span>Thắng</span><strong>{horse.totalWins ?? horse.TotalWins ?? 0}</strong></div>
                  <div><span>Tỷ lệ thắng</span><strong>{horse.totalRaces ?? horse.TotalRaces ? `${Math.round(((horse.totalWins ?? horse.TotalWins ?? 0) / (horse.totalRaces ?? horse.TotalRaces)) * 100)}%` : "0%"}</strong></div>
                </div>
                {(horse.approvalNote ?? horse.ApprovalNote) && <p className="admin-horse-card__note">{horse.approvalNote ?? horse.ApprovalNote}</p>}
              </div>
              <div className="admin-actions admin-horse-card__actions">
                {["Pending", "Approved", "Rejected"].map((nextStatus) => <button className={`admin-horse-status-button admin-horse-status-button--${nextStatus.toLowerCase()}`} key={nextStatus} disabled={status === nextStatus} onClick={() => changeHorseStatus(horse, nextStatus)}>{nextStatus}</button>)}
              </div>
            </article>;
          })}
        </section>
      </>}
    </>
  );
}

function HorseDetail() {
  const { userId, horseId } = useParams();
  const [horse, setHorse] = useState(null);
  const [message, setMessage] = useState("");
  const navigate = useNavigate();

  useEffect(() => {
    let cancelled = false;
    getOwnerHorse(userId, horseId)
      .then((data) => {
        if (!cancelled) setHorse(data);
      })
      .catch((err) => {
        if (!cancelled) setMessage(err.message);
      });
    return () => {
      cancelled = true;
    };
  }, [horseId, userId]);

  const value = (camel, pascal, fallback = "-") =>
    horse?.[camel] ?? horse?.[pascal] ?? fallback;
  const status = value("approvalStatus", "ApprovalStatus");

  return (
    <>
      <PageTitle
        eyebrow="Quản lý ngựa"
        title="Chi tiết ngựa"
        description="Xem thông tin đầy đủ về ngựa và dữ liệu sở hữu."
        action={<button className="ghost-button" onClick={() => navigate(`/admin/users/${userId}`)}>Quay lại chủ sở hữu</button>}
      />
      <Notice message={message} error />
      {horse && <section className="admin-horse-detail">
        <article className="admin-horse-detail__hero">
          <AdminHorseImage
            className="admin-horse-detail__image"
            imageUrl={value("imageUrl", "ImageUrl", "")}
            name={value("name", "Name")}
          />
          <div>
            <span className={`status status--${status.toLowerCase()}`}>{status}</span>
            <h2>{value("name", "Name")}</h2>
            <p>{value("breed", "Breed", "Giống không xác định")} · {value("gender", "Gender", "Giới tính không xác định")} · {value("color", "Color", "Màu không xác định")}</p>
          </div>
        </article>
        <section className="admin-horse-detail__grid">
          <div><span>Chủ sở hữu</span><strong>{value("ownerName", "OwnerName")}</strong></div>
          <div><span>Tuổi</span><strong>{value("age", "Age")}</strong></div>
          <div><span>Ngày sinh</span><strong>{formatDate(value("dateOfBirth", "DateOfBirth", null))}</strong></div>
          <div><span>Cân nặng</span><strong>{value("weight", "Weight")} kg</strong></div>
          <div><span>Chiều cao</span><strong>{value("height", "Height")} cm</strong></div>
          <div><span>Tổng số cuộc đua</span><strong>{value("totalRaces", "TotalRaces", 0)}</strong></div>
          <div><span>Tổng số thắng</span><strong>{value("totalWins", "TotalWins", 0)}</strong></div>
          <div><span>Tỷ lệ thắng</span><strong>{value("totalRaces", "TotalRaces", 0) ? `${Math.round((value("totalWins", "TotalWins", 0) / value("totalRaces", "TotalRaces", 0)) * 100)}%` : "0%"}</strong></div>
          <div><span>ID ngựa</span><strong>{horseId}</strong></div>
          <div><span>ID chủ sở hữu</span><strong>{value("ownerId", "OwnerId")}</strong></div>
        </section>
        {(value("approvalNote", "ApprovalNote", "")) && <article className="admin-horse-detail__note"><span>Ghi chú phê duyệt</span><p>{value("approvalNote", "ApprovalNote")}</p></article>}
      </section>}
    </>
  );
}

const JOCKEY_TAB_EMPTY_MESSAGE = {
  pending: "Không có kỵ sĩ nào đang chờ duyệt.",
  approved: "Chưa có kỵ sĩ nào được duyệt.",
  rejected: "Chưa có kỵ sĩ nào bị từ chối.",
  all: "Không tìm thấy tài khoản kỵ sĩ nào.",
};

function Roles() {
  const [jockeys, setJockeys] = useState([]);
  const [message, setMessage] = useState("");
  const [reviewJockeyId, setReviewJockeyId] = useState(null);
  const [activeTab, setActiveTab] = useState("pending");

  const loadJockeys = () =>
    getAvailableJockeys()
      .then(setJockeys)
      .catch((err) => setMessage(err.message));

  useEffect(() => {
    loadJockeys();
  }, []);

  // J-ADMIN-REVIEW: Approve/Reject now happen inside JockeyReviewModal, not directly on a table
  // row — this table is a triage view (who needs a decision, filtered by tab), not the decision
  // UI itself. Default tab is Pending so the verification queue is what Admin sees first.
  const groups = groupJockeysByApprovalStatus(jockeys);
  const TABS = [
    { key: "pending", label: "Chờ duyệt", list: groups.pending },
    { key: "approved", label: "Đã duyệt", list: groups.approved },
    { key: "rejected", label: "Từ chối", list: groups.rejected },
    { key: "all", label: "Tất cả", list: groups.all },
  ];
  const activeList = TABS.find((t) => t.key === activeTab)?.list ?? [];

  const closeReview = () => setReviewJockeyId(null);
  const onReviewChanged = () => {
    setMessage("Đã cập nhật trạng thái kỵ sĩ.");
    loadJockeys();
  };

  return (
    <>
      <Notice message={message} />

      <section className="admin-panel">
        <div className="admin-panel__heading">
          <span>Xác minh hồ sơ</span>
          <h2>Quản lý kỵ sĩ</h2>
        </div>

        <div className="admin-stat-grid">
          <div className="admin-stat-card">
            <p>Chờ duyệt</p>
            <h3>{groups.pending.length}</h3>
          </div>
          <div className="admin-stat-card">
            <p>Đã duyệt</p>
            <h3>{groups.approved.length}</h3>
          </div>
          <div className="admin-stat-card">
            <p>Từ chối</p>
            <h3>{groups.rejected.length}</h3>
          </div>
        </div>

        <div className="jrx-tabs" role="tablist" aria-label="Lọc kỵ sĩ theo trạng thái phê duyệt">
          {TABS.map((tab) => (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={activeTab === tab.key}
              className={`jrx-tab${activeTab === tab.key ? " jrx-tab--active" : ""}`}
              onClick={() => setActiveTab(tab.key)}
            >
              {tab.label}
              <span className="jrx-tab__count">{tab.list.length}</span>
            </button>
          ))}
        </div>

        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Kỵ sĩ</th>
                <th>Giấy phép</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {activeList.map((jockey) => {
                const status = jockey.approvalStatusName || "Không xác định";
                return (
                  <tr key={jockey.id}>
                    <td>
                      <div className="jrx-cell-name">
                        <strong>{jockey.fullName}</strong>
                        <small>{jockey.email}</small>
                      </div>
                    </td>
                    <td>{jockey.licenseNumber || "—"}</td>
                    <td>
                      <span className={`status status--${status.toLowerCase()}`}>{status}</span>
                    </td>
                    <td>
                      <div className="admin-actions">
                        <button onClick={() => setReviewJockeyId(jockey.id)}>Xem hồ sơ</button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {activeList.length === 0 ? (
                <tr>
                  <td colSpan="4">{JOCKEY_TAB_EMPTY_MESSAGE[activeTab]}</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>

      {reviewJockeyId && (
        <JockeyReviewModal jockeyId={reviewJockeyId} onClose={closeReview} onChanged={onReviewChanged} />
      )}
    </>
  );
}

function TournamentManagement() {
  const [items, setItems] = useState([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState("");
  const [editingStatus, setEditingStatus] = useState(null);
  const [message, setMessage] = useState("");
  const [uploading, setUploading] = useState(false);
  const [selectedT, setSelectedT] = useState(null);
  const [form, setForm] = useState({ name: "", description: "", venue: "", startDate: inputDate(7), endDate: inputDate(14), prizePool: 0, imageUrl: "", minParticipants: 3, maxParticipants: 10, maxRounds: 1 });
  // V0.1 micro-fix: MaxRounds may only change while Draft — Published/Ongoing/Finished/Cancelled
  // must ALL lock it, not just Published. Single source of truth for both the field's
  // disabled state (JSX below) and whether it's included in the update payload (submit below).
  const isDraft = canEditTournamentStructure(editingStatus);
  const load = () => getAdminTournaments().then((data) => setItems(Array.isArray(data) ? data : [])).catch((err) => setMessage(err.message));
  useEffect(() => {
    load();
  }, []);

  const handleUpload = async (file) => {
    if (!file) return;
    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const res = await request("/api/auth/upload-document", { method: "POST", body: formData });
      const d = res?.data ?? res;
      setForm((prev) => ({ ...prev, imageUrl: d?.url ?? "" }));
    } catch (e) {
      setMessage("Tải ảnh thất bại: " + (e.message ?? ""));
    }
    setUploading(false);
  };

  const submit = async (event) => {
    event.preventDefault();
    try {
      // Phase4B: when editing a Published tournament, omit immutable fields from the payload
      // so the BE never sees them. Draft edits send everything.
      const isPublished = editingId && editingStatus === 1; // TournamentStatus.Published == 1
      const payload = {
        name: form.name,
        description: form.description,
        imageUrl: form.imageUrl,
      };
      if (!isPublished) {
        payload.startDate = vnInputToApiUtc(form.startDate);
        payload.endDate = vnInputToApiUtc(form.endDate);
        payload.minParticipants = Number(form.minParticipants);
        payload.maxParticipants = Number(form.maxParticipants);
      }
      // V0.1 micro-fix: MaxRounds is structural (drives V0 Final identity) and is locked for
      // EVERY non-Draft status (Published, Ongoing, Finished, Cancelled) — not just Published,
      // unlike the isPublished-gated fields above (that existing scope is unchanged here).
      if (isDraft) {
        payload.maxRounds = Number(form.maxRounds);
      }
      if (editingId) await updateTournament(editingId, payload);
      else {
        payload.startDate = vnInputToApiUtc(form.startDate);
        payload.endDate = vnInputToApiUtc(form.endDate);
        payload.minParticipants = Number(form.minParticipants);
        payload.maxParticipants = Number(form.maxParticipants);
        payload.maxRounds = Number(form.maxRounds);
        await createTournament(payload);
      }
      setMessage(`Giải đấu ${editingId ? "đã cập nhật" : "đã tạo"} thành công.`);
      setShowForm(false); setEditingId(""); setEditingStatus(null); load();
    } catch (err) { setMessage(err.message); }
  };
  const edit = (item) => {
    setEditingId(item.id ?? item.Id);
    setEditingStatus(item.status ?? item.Status ?? null);
    setForm({
      name: item.name ?? item.Name ?? "",
      description: item.description ?? item.Description ?? "",
      startDate: apiToVNInput(item.startDate ?? item.StartDate),
      endDate: apiToVNInput(item.endDate ?? item.EndDate),
      imageUrl: item.imageUrl ?? item.ImageUrl ?? "",
      minParticipants: item.minParticipants ?? item.MinParticipants ?? 3,
      maxParticipants: item.maxParticipants ?? item.MaxParticipants ?? 10,
      maxRounds: item.maxRounds ?? item.MaxRounds ?? 1,
    });
    setShowForm(true);
  };
  const remove = async (id) => {
    if (!window.confirm("Xóa giải đấu này?")) return;
    try { await deleteTournament(id); setMessage("Đã xóa giải đấu."); load(); } catch (err) { setMessage(err.message); }
  };

  const viewT = async (item) => {
    setSelectedT(item);
  };

  // TournamentStatus.Cancelled backend enum value (BE/Models/Enums.cs) — NextTransitionDto.Status
  // serializes as the raw int (no global string enum converter).
  const CANCELLED_STATUS = 4;

  const changeStatus = async (id, newStatus) => {
    try {
      const body = { newStatus };
      if (newStatus === CANCELLED_STATUS) {
        const reason = window.prompt("Nhập lý do hủy giải đấu:");
        if (reason === null) return; // dismissed — do not call the API
        if (!reason.trim()) { setMessage("Lý do hủy giải đấu không được để trống."); return; }
        body.reason = reason.trim();
      }
      await request(`/api/tournaments/${id}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      setMessage("Đã cập nhật trạng thái giải đấu.");
      load();
    } catch (err) { setMessage(err.message); }
  };

  return (
    <>
      <PageTitle eyebrow="Quản lý giải đấu" title="Giải đấu" description="Tạo giải đấu và điều phối vòng đấu, cuộc đua." action={<button className="primary-button" onClick={() => { setEditingId(""); setShowForm(true); }}>Tạo giải đấu</button>} />
      <Notice message={message} />
      {showForm && !editingId && (
        <TournamentForm
          onClose={() => setShowForm(false)}
          onSuccess={() => {
            setShowForm(false);
            setMessage("Giải đấu đã tạo thành công.");
            load();
          }}
        />
      )}
      {showForm && editingId && <form className="admin-form" onSubmit={submit}>
        {editingStatus === 1 && <p style={{ color: "var(--hr-gold-soft)", fontSize: 13, marginBottom: 8 }}>⚠ Giải đấu đã công bố — chỉ có thể sửa Tên, Mô tả và Ảnh bìa.</p>}
        <input placeholder="Tên giải đấu" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        <input placeholder="Mô tả" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
          Thời gian bắt đầu *
          <input type="datetime-local" required value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} min={inputDate(0)} disabled={editingStatus === 1} />
        </label>
        <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
          Thời gian kết thúc *
          <input type="datetime-local" required value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} min={inputDate(0)} disabled={editingStatus === 1} />
        </label>
        {editingStatus !== 1 && <p style={{ margin: "-8px 0 8px", fontSize: 12, color: "var(--hr-muted)" }}>Giải đấu có thể bắt đầu và kết thúc trong cùng một ngày, miễn thời gian kết thúc sau thời gian bắt đầu.</p>}
        <input type="number" placeholder="Số người tham gia tối thiểu" required min="3" value={form.minParticipants} onChange={(e) => setForm({ ...form, minParticipants: e.target.value })} disabled={editingStatus === 1} />
        <input type="number" placeholder="Số người tham gia tối đa" required min="1" value={form.maxParticipants} onChange={(e) => setForm({ ...form, maxParticipants: e.target.value })} disabled={editingStatus === 1} />
        <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
          Số vòng đấu *
          <input type="number" required min="1" step="1" value={form.maxRounds} onChange={(e) => setForm({ ...form, maxRounds: e.target.value })} disabled={!isDraft} />
        </label>
        <label style={{ fontSize: 13, color: "var(--hr-muted)" }}>Ảnh bìa giải đấu (tỉ lệ 3:1, đề xuất 1200×400px):
          <input type="file" accept="image/*" onChange={(e) => { const f = e.target.files?.[0]; if (f) handleUpload(f); }} style={{ display: "block", marginTop: 4 }} />
          {uploading ? <span style={{ color: "var(--hr-gold-soft)", fontSize: 12 }}>Đang tải ảnh...</span> : null}
        </label>
        {form.imageUrl && <img src={form.imageUrl} alt="preview" style={{ width: 120, borderRadius: 8, marginTop: 4 }} />}
        <button className="primary-button" disabled={uploading}>Lưu giải đấu</button>
      </form>}
      <section className="admin-card-grid admin-tournament-grid">{items.map((item) => {
        const id = item.id ?? item.Id;
        const lifecycleStatus = (item.statusName ?? item.StatusName ?? item.status ?? item.Status ?? "").toString().toLowerCase();
        const lifecycleClass = lifecycleStatus === "published" || lifecycleStatus === "ongoing" || lifecycleStatus === "1" || lifecycleStatus === "2" ? "status--active" : "status--inactive";
        return <article key={id} className="admin-tournament-card" role="button" tabIndex={0} style={{ position: "relative", overflow: "hidden", cursor:"pointer" }} onClick={() => viewT(item)} onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); viewT(item); } }}>
          {item.imageUrl ?? item.ImageUrl ? <div style={{ position: "absolute", inset: 0, backgroundImage: `url(${(item.imageUrl ?? item.ImageUrl)})`, backgroundSize: "cover", backgroundPosition: "center", opacity: 0.15, pointerEvents: "none" }} /> : null}
          <div style={{ position: "relative", zIndex: 1 }}><span className={`status ${lifecycleClass}`}>{getTournamentLifecycleLabel(item)}</span><h3>{item.name ?? item.Name}</h3><p>{item.description ?? item.Description ?? "Không có mô tả"}</p></div><dl style={{ position: "relative", zIndex: 1 }}><div><dt>Bắt đầu</dt><dd>{formatDateTime(item.startDate ?? item.StartDate)}</dd></div><div><dt>Kết thúc</dt><dd>{formatDateTime(item.endDate ?? item.EndDate)}</dd></div><div><dt>Vòng đấu</dt><dd>{item.roundCount ?? item.RoundCount ?? 0}</dd></div><div><dt>Cuộc đua</dt><dd>{item.raceCount ?? item.RaceCount ?? 0}</dd></div></dl><div className="admin-actions" style={{ position: "relative", zIndex: 1 }}>
            {(item.nextTransitions ?? item.NextTransitions ?? []).map((t) => (
              <button
                key={t.status}
                style={t.isPrimary
                  ? { background: "rgba(112,139,104,0.16)", color: "var(--hr-success)", border: "1px solid rgba(112,139,104,0.35)" }
                  : { background: "transparent", color: "var(--hr-text)", border: "1px solid var(--hr-border-soft)" }}
                onClick={(e) => { e.stopPropagation(); changeStatus(id, t.status); }}
              >
                {t.label}
              </button>
            ))}
            <button onClick={() => edit(item)}>Sửa</button>
            {/* T-D1: hard delete only ever allowed for Draft (backend-enforced, 409 otherwise) —
                once Published/Ongoing/Finished/Cancelled, Tournament history must be preserved; the
                Cancel action above (nextTransitions) is how Admin retires a Published/Ongoing one. */}
            {canHardDeleteTournament(item) && <button className="admin-danger" onClick={() => remove(id)}>Xóa</button>}
          </div></article>;
      })}</section>
      {selectedT && (
        <TournamentDetail
          t={selectedT}
          onBack={() => setSelectedT(null)}
          setMessage={setMessage}
          getTournamentRaces={getTournamentRaces}
          getTournamentRounds={getTournamentRounds}
        />
      )}
    </>
  );
}

function ScheduleManagement({ type }) {
  const location = useLocation();
  const preselectTournamentId = new URLSearchParams(location.search).get("tournamentId") || "";
  const [tournaments, setTournaments] = useState([]);
  const [selected, setSelected] = useState("");
  const [items, setItems] = useState([]);
  const [approvedHorses, setApprovedHorses] = useState([]);
  const [message, setMessage] = useState("");
  const [assignment, setAssignment] = useState({ raceId: "", horseId: "" });
  const [expandedRaceId, setExpandedRaceId] = useState(null);
  const [raceEntries, setRaceEntries] = useState([]);
  const [raceReferees, setRaceReferees] = useState([]);
  const [raceViolations, setRaceViolations] = useState([]);
  const [resolvingViolation, setResolvingViolation] = useState(null);
  const [penaltyText, setPenaltyText] = useState("");
  const [raceResult, setRaceResult] = useState(null);
  const [raceReport, setRaceReport] = useState(null);
  const [assignedHorseIds, setAssignedHorseIds] = useState(new Set());
  const [busyHorseIdsAll, setBusyHorseIdsAll] = useState(new Set());
  const [showRaceForm, setShowRaceForm] = useState(false);
  const [assignmentsByRace, setAssignmentsByRace] = useState(new Map());

  const refreshBusyHorses = async () => {
    try {
      const res = await request("/api/races/management/busy-horses");
      const ids = Array.isArray(res) ? res : res?.data ?? [];
      setBusyHorseIdsAll(new Set(ids));
    } catch { /* non-critical */ }
  };

  const refreshRefereeAssignments = async () => {
    try {
      const res = await request("/api/referees/assignments");
      const list = Array.isArray(res?.data ?? res) ? (res?.data ?? res) : [];
      const map = new Map();
      list.forEach((a) => {
        const raceId = a.raceId ?? a.RaceId;
        if (!raceId) return;
        if (!map.has(raceId)) map.set(raceId, []);
        map.get(raceId).push(a);
      });
      setAssignmentsByRace(map);
    } catch { /* non-critical */ }
  };

  useEffect(() => { refreshBusyHorses(); }, [type]);
  useEffect(() => { refreshRefereeAssignments(); }, [type]);

  useEffect(() => {
    if (!assignment.raceId) { setAssignedHorseIds(new Set()); return; }
    request(`/api/referees/race/${assignment.raceId}/entries`)
      .then(d => {
        const entries = Array.isArray(d) ? d : d?.data ?? [];
        setAssignedHorseIds(new Set(entries.map(e => e.horseId ?? e.HorseId)));
      })
      .catch(() => setAssignedHorseIds(new Set()));
  }, [assignment.raceId]);

  const VIOLATION_LABELS = { 1: "Hành vi nguy hiểm", 2: "Xuất phát sai", 3: "Can thiệp", 4: "Phúc lợi động vật", 5: "Vi phạm thiết bị", 6: "Khác" };
  // Race progress (RaceStatus) — event lifecycle only. RegistrationOpen/
  // RegistrationClosed are transitional compatibility values (see BE Enums.cs).
  const raceStatusLabel = {
    scheduled: "Sắp diễn ra",
    registrationopen: "Chuẩn bị",
    registrationclosed: "Chuẩn bị",
    inprogress: "Đang đua",
    finished: "Đã kết thúc",
    cancelled: "Đã hủy",
  };

  // Result status (RaceResultStatus) — separate concern from race progress.
  const resultStatusLabel = {
    provisional: "Tạm thời (chờ duyệt)",
    official: "Chính thức",
  };

  // Race creation lives exclusively in RaceForm.jsx (the single canonical Race payload builder) —
  // this page only builds Round create/update payloads (same form for both, per Phase5B).
  const defaultRoundForm = { name: "", roundNumber: 1, scheduledStartDate: inputDate(7), scheduledEndDate: inputDate(8), description: "", advanceCount: "" };
  const [form, setForm] = useState(defaultRoundForm);
  const [editingRoundId, setEditingRoundId] = useState("");

  useEffect(() => {
    getAdminTournaments().then((data) => {
      const list = Array.isArray(data) ? data : [];
      setTournaments(list);
      // Deep-link from Tournament Detail's "+ Tạo vòng đấu" (?tournamentId=...) preselects the
      // Tournament so the Admin doesn't have to find it again in the dropdown — falls back to the
      // first Tournament when the id is absent or no longer matches.
      const preselected = preselectTournamentId && list.some((item) => (item.id ?? item.Id) === preselectTournamentId)
        ? preselectTournamentId
        : (list[0]?.id ?? list[0]?.Id ?? "");
      setSelected(preselected);
    }).catch((err) => setMessage(err.message));
  }, []);
  useEffect(() => {
    if (type !== "race") return;
    if (!selected) { setApprovedHorses([]); return; }

    // Task B Correction 2 §5: this dropdown ("Chọn ngựa đã được phê duyệt") must reflect
    // Tournament-registration approval (TournamentHorseRegistration.Status == Approved for the
    // SELECTED Tournament), not the global Horse.ApprovalStatus pool across every Owner. A Horse
    // with no registration, or a Pending/Rejected/Withdrawn one, must never be assignable here.
    const loadAssignmentOptions = async () => {
      try {
        const horses = await getTournamentApprovedHorses(selected);
        setApprovedHorses(Array.isArray(horses) ? horses : []);
      } catch (err) {
        setMessage(err.message);
      }
    };

    loadAssignmentOptions();
  }, [type, selected]);
  useEffect(() => {
    if (!selected) return;
    const fetcher = type === "round" ? getTournamentRounds : getTournamentRaces;
    fetcher(selected).then((data) => setItems(Array.isArray(data) ? data : [])).catch((err) => setMessage(err.message));
  }, [selected, type]);

  const visibleHorses = approvedHorses;

  const selectHorse = (horseId) => {
    setAssignment((current) => ({
      ...current,
      horseId,
    }));
  };

  const startEditRound = (round) => {
    setEditingRoundId(round.id ?? round.Id);
    // Nullish coalescing only — AdvanceCount = 0 is a valid, meaningful value (Final Round)
    // and must round-trip through the form unchanged, never collapsing to "".
    const advanceCount = round.advanceCount ?? round.AdvanceCount;
    setForm({
      name: round.name ?? round.Name ?? "",
      roundNumber: round.roundNumber ?? round.RoundNumber ?? 1,
      scheduledStartDate: apiToVNInput(round.scheduledStartDate ?? round.ScheduledStartDate),
      scheduledEndDate: apiToVNInput(round.scheduledEndDate ?? round.ScheduledEndDate),
      description: round.description ?? round.Description ?? "",
      advanceCount: advanceCount === null || advanceCount === undefined ? "" : advanceCount,
    });
  };

  const cancelEditRound = () => {
    setEditingRoundId("");
    setForm(defaultRoundForm);
  };

  // Q1: server is the sole source of truth for readiness — this only sends the current Round's
  // id, never Horse IDs, rankings, or target assignments.
  const generateNextRoundForRound = async (roundId) => {
    try {
      const result = await generateNextRound(roundId);
      const data = result?.data ?? result?.Data ?? {};
      const generated = data.generatedEntries ?? data.GeneratedEntries;
      setMessage(generated != null ? `Đã tạo vòng tiếp theo: ${generated} ngựa đủ điều kiện.` : "Đã tạo vòng tiếp theo thành công.");
      setItems(await getTournamentRounds(selected));
    } catch (err) { setMessage(err.message); }
  };

  const submit = async (event) => {
    event.preventDefault();
    // Phase5B fix: friendly client-side containment check before hitting the API — the backend
    // stays authoritative (same rules re-checked there), this just avoids a round-trip for the
    // obvious case of a Round scheduled outside its Tournament's window. Vietnam-timezone policy
    // (Asia/Ho_Chi_Minh) — see FE/src/utils/vnDateTime.js.
    const roundStartUtc = vnInputToApiUtc(form.scheduledStartDate);
    const roundEndUtc = vnInputToApiUtc(form.scheduledEndDate);
    const roundStart = apiToUtcDate(roundStartUtc);
    const roundEnd = apiToUtcDate(roundEndUtc);
    if (selectedTournament) {
      const tStart = apiToUtcDate(selectedTournament.startDate ?? selectedTournament.StartDate);
      const tEnd = apiToUtcDate(selectedTournament.endDate ?? selectedTournament.EndDate);
      if (roundStart < tStart) {
        setMessage("Thời gian bắt đầu Vòng đấu không được trước thời gian bắt đầu Giải đấu.");
        return;
      }
      if (roundEnd > tEnd) {
        setMessage("Thời gian kết thúc Vòng đấu không được sau thời gian kết thúc Giải đấu.");
        return;
      }
    }
    if (roundStart >= roundEnd) {
      setMessage("Thời gian bắt đầu Vòng đấu phải trước thời gian kết thúc.");
      return;
    }
    try {
      const payload = {
        ...form,
        scheduledStartDate: roundStartUtc,
        scheduledEndDate: roundEndUtc,
        advanceCount: form.advanceCount === "" ? null : Number(form.advanceCount),
      };
      if (editingRoundId) {
        await updateRound(editingRoundId, payload);
        setMessage("Vòng đấu đã cập nhật thành công.");
        setEditingRoundId("");
        setForm(defaultRoundForm);
      } else {
        await createRound(selected, payload);
        setMessage("Vòng đấu đã tạo thành công.");
      }
      setItems(await getTournamentRounds(selected));
    } catch (err) { setMessage(err.message); }
  };

  const assignHorse = async (event) => {
    event.preventDefault();
    const horseId = assignment.horseId.trim();

    if (!isGuid(horseId)) {
      setMessage("ID ngựa phải là GUID hợp lệ.");
      return;
    }

    try {
      await assignHorseToRace(assignment.raceId, { horseId });
      setMessage("Đã phân công ngựa vào cuộc đua thành công.");
      setAssignment({ raceId: "", horseId: "" });
      setItems(await getTournamentRaces(selected));
      refreshBusyHorses();
    } catch (err) { setMessage(err.message); }
  };

  const submitResolveViolation = async (violationId) => {
    if (!penaltyText.trim()) {
      alert("Vui lòng nhập hình phạt (ví dụ: Trừ 50% thưởng, Cấm thi đấu...)");
      return;
    }
    try {
      await request(`/api/admin/violations/${violationId}/resolve`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ penalty: penaltyText.trim() }),
      });
      setMessage("Đã xử lý vi phạm thành công.");
      setResolvingViolation(null);
      setPenaltyText("");
      const violRes = await request(`/api/referees/race/${expandedRaceId}/violations`);
      setRaceViolations(Array.isArray(violRes) ? violRes : violRes?.data ?? []);
    } catch (err) {
      setMessage("Lỗi xử lý vi phạm: " + err.message);
    }
  };

  const handleRaceAction = async (raceId, action) => {
    const labels = { start: "bắt đầu", end: "kết thúc", cancel: "hủy", approve: "duyệt kết quả", reject: "từ chối kết quả" };
    if (action === "approve") {
      // Warn if the provisional winner has an unresolved violation, so Admin
      // doesn't pay out a prediction for a horse that may end up disqualified.
      // Matched by HorseId (authoritative), not HorseName.
      const winnerHorseId = raceResult?.winningHorseId ?? raceResult?.WinningHorseId;
      const isWinnerViolated = winnerHorseId != null && raceViolations.some(v => {
        const violationHorseId = v.horseId ?? v.HorseId;
        const penalty = v.penalty ?? v.Penalty;
        return violationHorseId === winnerHorseId && !(penalty && penalty.trim());
      });
      const confirmMsg = isWinnerViolated
        ? "⚠️ CẢNH BÁO: Ngựa thắng cuộc đang có VI PHẠM chưa xử lý. Bạn có chắc chắn muốn duyệt kết quả và trả thưởng cho con ngựa này không?"
        : "Duyệt kết quả này thành chính thức (Official)? Dự đoán sẽ được thanh toán ngay sau khi duyệt.";
      if (!window.confirm(confirmMsg)) return;
      try {
        await approveRaceResult(raceId);
        setMessage("Kết quả đã chính thức (Official). Dự đoán đã được thanh toán.");
        setItems(await getTournamentRaces(selected));
        refreshBusyHorses();
      } catch (err) { setMessage(err.message); }
      return;
    }
    if (action === "reject") {
      const reason = window.prompt("Lý do từ chối kết quả:");
      if (!reason) return;
      try {
        await rejectRaceResult(raceId, reason);
        setMessage("Kết quả tạm thời đã bị từ chối. Trọng tài cần nộp lại.");
        setItems(await getTournamentRaces(selected));
        refreshBusyHorses();
      } catch (err) { setMessage(err.message); }
      return;
    }
    if (action === "end") {
      if (!window.confirm("Kết thúc cuộc đua? Thao tác này chỉ đánh dấu cuộc đua đã diễn ra xong — trọng tài sẽ nộp kết quả sau đó.")) return;
    } else if (!window.confirm(`${labels[action].charAt(0).toUpperCase() + labels[action].slice(1)} cuộc đua này?`)) return;
    try {
      if (action === "start") await startRace(raceId);
      else if (action === "end") await endRace(raceId);
      else if (action === "cancel") await cancelRace(raceId);
      setMessage(`Cuộc đua đã ${labels[action]} thành công.`);
      setItems(await getTournamentRaces(selected));
      refreshBusyHorses();
    } catch (err) { setMessage(err.message); }
  };

  const title = type === "round" ? "Quản lý vòng đấu" : "Quản lý cuộc đua & lên lịch";
  const selectedTournament = tournaments.find((t) => (t.id ?? t.Id) === selected);
  // Structural Round edit ("Sửa") is only exposed while the parent Tournament is Draft — Phase5
  // locks Round/Race structural mutation to Draft-only, so the action must never be offered once
  // the Tournament is Published/Ongoing/Finished/Cancelled, even though the backend already rejects it.
  const isDraftTournament = (selectedTournament?.statusName ?? selectedTournament?.StatusName) === "Draft";
  return (
    <>
      <PageTitle
        eyebrow="Quản lý giải đấu"
        title={title}
        description={type === "round" ? "Xây dựng giai đoạn giải đấu và xác định khung thời gian." : "Sắp xếp cuộc đua, đặt lịch và chuẩn bị phân công ngựa."}
        action={type === "race" ? <button className="primary-button" onClick={() => setShowRaceForm(true)}>+ Tạo cuộc đua</button> : null}
      />
      <Notice message={message} />
      {showRaceForm && (
        <RaceForm
          tournamentId={selected}
          tournamentName={selectedTournament?.name ?? selectedTournament?.Name}
          tournamentStartDate={selectedTournament?.startDate ?? selectedTournament?.StartDate}
          tournamentEndDate={selectedTournament?.endDate ?? selectedTournament?.EndDate}
          tournamentRegistrationDeadline={selectedTournament?.registrationDeadline ?? selectedTournament?.RegistrationDeadline}
          onClose={() => setShowRaceForm(false)}
          onSuccess={async () => {
            setShowRaceForm(false);
            setMessage("Cuộc đua đã tạo thành công. Lời mời trọng tài đã được gửi.");
            setItems(await getTournamentRaces(selected));
            refreshBusyHorses();
          }}
        />
      )}
      <div className="admin-select-row"><label>Giải đấu<select className="admin-select" value={selected} onChange={(e) => { setSelected(e.target.value); cancelEditRound(); }}>{tournaments.map((item) => <option key={item.id ?? item.Id} value={item.id ?? item.Id}>{item.name ?? item.Name}</option>)}</select></label></div>
      {/* Round creation stays dedicated here — Race creation/edit lives exclusively in the
          RaceForm modal above (single canonical Race payload builder, Phase5B consolidation). */}
      {type === "round" && selectedTournament && (
        <div style={{ padding: "10px 14px", borderRadius: 10, border: "1px solid var(--hr-border-soft)", background: "var(--hr-surface-2)", marginBottom: 12, fontSize: 13 }}>
          <div><span style={{ color: "var(--hr-muted)" }}>Giải đấu: </span><strong style={{ color: "var(--hr-paper)" }}>{selectedTournament.name ?? selectedTournament.Name}</strong></div>
          <div><span style={{ color: "var(--hr-muted)" }}>Thời gian giải: </span><strong style={{ color: "var(--hr-paper)" }}>{formatDateTime(selectedTournament.startDate ?? selectedTournament.StartDate)} → {formatDateTime(selectedTournament.endDate ?? selectedTournament.EndDate)}</strong></div>
          <p style={{ margin: "4px 0 0", fontSize: 12, color: "var(--hr-muted)" }}>Vòng đấu phải nằm hoàn toàn trong thời gian của Giải đấu.</p>
        </div>
      )}
      {type === "round" && (
        <form className="admin-form" onSubmit={submit}>
          {editingRoundId && <p style={{ color: "var(--hr-gold-soft)", fontSize: 13, marginBottom: 8 }}>✎ Đang sửa vòng đấu.</p>}
          <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
            Tên vòng đấu *
            <input placeholder="Ví dụ: Vòng loại, Bán kết, Chung kết." required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </label>
          <p style={{ margin: "-8px 0 8px", fontSize: 12, color: "var(--hr-muted)" }}>Ví dụ: Vòng loại, Bán kết, Chung kết.</p>

          <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
            Số thứ tự vòng *
            <input type="number" min="1" required value={form.roundNumber} onChange={(e) => setForm({ ...form, roundNumber: Number(e.target.value) })} />
          </label>
          <p style={{ margin: "-8px 0 8px", fontSize: 12, color: "var(--hr-muted)" }}>Thứ tự vòng trong giải đấu, bắt đầu từ 1.</p>

          <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
            Thời gian bắt đầu *
            <input type="datetime-local" required value={form.scheduledStartDate} onChange={(e) => setForm({ ...form, scheduledStartDate: e.target.value })}
              min={selectedTournament ? apiToVNInput(selectedTournament.startDate ?? selectedTournament.StartDate) : inputDate(0)}
              max={selectedTournament ? apiToVNInput(selectedTournament.endDate ?? selectedTournament.EndDate) : undefined} />
          </label>
          <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
            Thời gian kết thúc *
            <input type="datetime-local" required value={form.scheduledEndDate} onChange={(e) => setForm({ ...form, scheduledEndDate: e.target.value })}
              min={selectedTournament ? apiToVNInput(selectedTournament.startDate ?? selectedTournament.StartDate) : inputDate(0)}
              max={selectedTournament ? apiToVNInput(selectedTournament.endDate ?? selectedTournament.EndDate) : undefined} />
          </label>

          <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
            Số ngựa đi tiếp *
            <input type="number" min="0" placeholder="Số ngựa đi tiếp" value={form.advanceCount} onChange={(e) => setForm({ ...form, advanceCount: e.target.value })} />
          </label>
          <p style={{ margin: "-8px 0 8px", fontSize: 12, color: "var(--hr-muted)" }}>Nhập 0 nếu đây là Vòng chung kết. (AdvanceCount)</p>

          <div style={{ display: "flex", gap: 8 }}>
            <button className="primary-button" disabled={!selected}>{editingRoundId ? "Lưu vòng đấu" : "Tạo vòng đấu"}</button>
            {editingRoundId && <button type="button" className="ghost-button" onClick={cancelEditRound}>Hủy</button>}
          </div>
        </form>
      )}
      {type === "race" && <form className="admin-form" onSubmit={assignHorse}>
        <select className="admin-select" required value={assignment.raceId} onChange={(e) => setAssignment({ ...assignment, raceId: e.target.value })}>
          <option value="">Chọn cuộc đua để phân công ngựa</option>
          {items.map((item) => <option key={item.id ?? item.Id} value={item.id ?? item.Id}>{item.name ?? item.Name}</option>)}
        </select>
        {(() => {
          const selectedRace = items.find((item) => (item.id ?? item.Id) === assignment.raceId);
          const selectedRoundNumber = selectedRace ? (selectedRace.roundNumber ?? selectedRace.RoundNumber) : null;
          // Q1-UX: manual Horse->Race assignment is only valid for RoundNumber == 1 — the backend
          // already rejects it for Round2+ (those RaceEntries can only come from
          // POST rounds/{roundId}/generate-next). Hide the ENTIRE Horse-selection flow rather than
          // filtering eliminated horses, so no horse — including a genuine qualifier — is ever
          // manually assignable to Round2+.
          if (assignment.raceId && selectedRoundNumber != null && selectedRoundNumber > 1) {
            return (
              <p style={{margin:"8px 0",padding:"10px 14px",borderRadius:10,background:"rgba(112,139,104,0.1)",border:"1px solid rgba(112,139,104,0.25)",color:"var(--hr-success)",fontSize:13}}>
                Ngựa ở vòng này được xác định tự động từ kết quả chính thức của vòng trước.<br />
                Không thể phân công ngựa thủ công.
              </p>
            );
          }
          return (
            <>
              <select className="admin-select" required value={assignment.horseId} onChange={(e) => selectHorse(e.target.value)}>
                <option value="">Chọn ngựa đã được duyệt đăng ký giải đấu</option>
                {visibleHorses.map((horse) => {
                  const horseId = horse.id ?? horse.Id;
                  const isInThisRace = assignedHorseIds.has(horseId);
                  const isBusyElsewhere = busyHorseIdsAll.has(horseId) && !isInThisRace;
                  const isDisabled = isInThisRace || isBusyElsewhere;
                  const label = isInThisRace ? " [Đã thêm]" : isBusyElsewhere ? " [Đã đăng ký cuộc đua khác]" : "";
                  return <option key={horseId} value={horseId} disabled={isDisabled} style={{color: isDisabled ? "var(--hr-muted)" : "inherit"}}>
                    {horse.name ?? horse.Name}{label}
                  </option>;
                })}
              </select>
              <button className="primary-button" disabled={!assignment.raceId || !assignment.horseId}>Phân công ngựa vào cuộc đua</button>
            </>
          );
        })()}
      </form>}

      <section className="admin-card-grid">{items.map((item) => {
        const itemId = item.id ?? item.Id;
        const itemStatus = (item.status ?? item.Status ?? "").toLowerCase();
        const itemResultStatus = (item.resultStatus ?? item.ResultStatus ?? "").toLowerCase();

        if (type === "round") {
          const roundNumber = item.roundNumber ?? item.RoundNumber;
          // Nullish coalescing only — AdvanceCount = 0 is a real, meaningful value and must
          // never be treated as "not set".
          const advanceCount = item.advanceCount ?? item.AdvanceCount;
          // V0/V0.1: Final is defined by RoundNumber === Tournament.MaxRounds, not AdvanceCount
          // alone — Draft data may temporarily hold AdvanceCount=0 on a non-final round.
          const isFinal = isFinalRound(item, selectedTournament);
          return (
            <article key={itemId} className="admin-simple-card">
              <span className="badge">#{roundNumber}</span>
              {isFinal && (
                <span className="badge" style={{ marginLeft: 4, background: "rgba(184,134,59,0.16)", color: "var(--hr-gold-soft)" }}>Vòng chung kết</span>
              )}
              <h3>{item.name ?? item.Name}</h3>
              <div style={{ fontSize: 13, color: "var(--hr-text)", marginTop: 4, display: "grid", gap: 2 }}>
                <span>Bắt đầu: {formatDateTime(item.scheduledStartDate ?? item.ScheduledStartDate)}</span>
                <span>Kết thúc: {formatDateTime(item.scheduledEndDate ?? item.ScheduledEndDate)}</span>
                <span>Số ngựa đi tiếp: {advanceCount ?? "Chưa thiết lập"}</span>
              </div>
              {isDraftTournament && (
                <div className="admin-actions" style={{ marginTop: 8 }}>
                  <button onClick={() => startEditRound(item)}>Sửa</button>
                </div>
              )}
              {/* Q1: only offered for a non-final Round of a non-Draft Tournament — the backend
                  remains authoritative on actual readiness (every source Race Finished with an
                  Official result); this button just avoids offering it somewhere it can never work. */}
              {!isDraftTournament && !isFinal && (
                <div className="admin-actions" style={{ marginTop: 8 }}>
                  <button onClick={() => generateNextRoundForRound(itemId)}>Tạo vòng tiếp theo</button>
                </div>
              )}
            </article>
          );
        }

        return <article key={itemId} className="admin-simple-card" style={{cursor:"pointer"}} onClick={async () => {
          if (type !== "race") return;
          if (expandedRaceId === itemId) { setExpandedRaceId(null); return; }
          setExpandedRaceId(itemId);
          try {
            const [entriesRes, refsRes, violRes, resultRes, reportRes] = await Promise.all([
              request(`/api/referees/race/${itemId}/entries`),
              request(`/api/referees/race/${itemId}/assignments`),
              request(`/api/referees/race/${itemId}/violations`),
              request(`/api/races/${itemId}/result`).catch(() => null),
              request(`/api/referees/race/${itemId}/report`).catch(() => null),
            ]);
            setRaceEntries(Array.isArray(entriesRes) ? entriesRes : entriesRes?.data ?? []);
            const refs = Array.isArray(refsRes) ? refsRes : refsRes?.data ?? [];
            setRaceReferees(Array.isArray(refs) ? refs : []);
            const viols = Array.isArray(violRes) ? violRes : violRes?.data ?? [];
            setRaceViolations(Array.isArray(viols) ? viols : []);
            setRaceResult(resultRes?.data ?? resultRes ?? null);
            setRaceReport(reportRes?.data ?? reportRes ?? null);
          } catch { setRaceEntries([]); setRaceReferees([]); setRaceViolations([]); setRaceResult(null); setRaceReport(null); }
        }}>
          <span className="badge">{raceStatusLabel[itemStatus] ?? item.status ?? item.Status}</span>
          {itemResultStatus && (
            <span className="badge" style={{marginLeft:4,background:itemResultStatus==="official"?"rgba(112,139,104,0.16)":"rgba(185,138,69,0.16)",color:itemResultStatus==="official"?"var(--hr-success)":"var(--hr-warning)"}}>
              {resultStatusLabel[itemResultStatus] ?? itemResultStatus}
            </span>
          )}
          <h3>{item.name ?? item.Name}</h3>
          <p>{formatDate(item.scheduledAt ?? item.ScheduledAt)}</p>
          <small>{item.entriesCount ?? item.EntriesCount ?? 0} ngựa đã phân công</small>
          {type === "race" && (() => {
            const refAssigns = assignmentsByRace.get(itemId) ?? [];
            const confirmedReferees = refAssigns.filter(a => (a.status ?? a.Status) === "Confirmed").length;
            const canStart = confirmedReferees >= 1;
            return (
              <div className="admin-actions admin-race-actions">
                {itemStatus !== "inprogress" && itemStatus !== "finished" && itemStatus !== "cancelled" && (
                  <>
                    <button onClick={() => handleRaceAction(itemId, "start")} disabled={!canStart} title={canStart ? "" : "Chờ trọng tài chấp nhận lời mời"}>
                      Bắt đầu
                    </button>
                    {!canStart && (
                      <span style={{ fontSize: 12, color: "var(--hr-muted)", alignSelf: "center" }}>
                        {refAssigns.length === 0
                          ? "Chưa có trọng tài - hãy thêm trọng tài trước khi bắt đầu"
                          : `Chờ trọng tài xác nhận (${confirmedReferees}/${refAssigns.length})`}
                      </span>
                    )}
                  </>
                )}
                {itemStatus === "inprogress" && (
                  <button style={{ background: "rgba(112,139,104,0.16)", color: "var(--hr-success)", border: "1px solid rgba(112,139,104,0.35)" }} onClick={() => handleRaceAction(itemId, "end")}>
                    Kết thúc cuộc đua
                  </button>
                )}
                {itemStatus === "finished" && !itemResultStatus && (
                  <span style={{ fontSize: 12, color: "var(--hr-muted)", alignSelf: "center" }}>Đã kết thúc — chờ trọng tài nộp kết quả.</span>
                )}
                {itemStatus === "finished" && itemResultStatus === "provisional" && (
                  <>
                    <button style={{ background: "rgba(112,139,104,0.16)", color: "var(--hr-success)", border: "1px solid rgba(112,139,104,0.35)" }} onClick={() => handleRaceAction(itemId, "approve")}>
                      Duyệt KQ
                    </button>
                    <button style={{ background: "rgba(201,105,90,0.16)", color: "var(--hr-danger)", border: "1px solid rgba(201,105,90,0.35)" }} onClick={() => handleRaceAction(itemId, "reject")}>
                      Từ chối
                    </button>
                  </>
                )}
                {itemStatus === "finished" && itemResultStatus === "official" && (
                  <span style={{ fontSize: 12, color: "var(--hr-success)", fontWeight: 600, alignSelf: "center" }}>✓ Chính thức</span>
                )}
                {/* Backend cancellation gate (RaceManagementService.CancelRaceAsync) only
                    allows Scheduled or InProgress — RegistrationOpen/RegistrationClosed/
                    Finished/Cancelled must not show this action. */}
                {(itemStatus === "scheduled" || itemStatus === "inprogress") && (
                  <button className="admin-danger" onClick={() => handleRaceAction(itemId, "cancel")}>
                    Hủy
                  </button>
                )}
              </div>
            );
          })()}
          {type === "race" && expandedRaceId === itemId && (
            <div style={{marginTop:12,padding:12,borderTop:"1px solid var(--hr-border-soft)"}} onClick={e => e.stopPropagation()}>
              <h4 style={{fontSize:14,margin:"0 0 8px",color:"var(--hr-paper)"}}>Ngựa tham gia</h4>
              {raceEntries.length === 0 ? (
                <p style={{color:"var(--hr-muted)",fontSize:13}}>Chưa có ngựa nào được phân công.</p>
              ) : (
                <table style={{width:"100%",fontSize:13,borderCollapse:"collapse"}}>
                  <thead><tr>
                    <th style={th}>Ngựa</th><th style={th}>Kỵ sĩ</th><th style={th}>Tỉ lệ cược</th>
                  </tr></thead>
                  <tbody>{raceEntries.map(e => (
                    <tr key={e.entryId ?? e.EntryId}>
                      <td style={td}>{e.horseName ?? e.HorseName}</td>
                      <td style={td}>{e.jockeyName ?? e.JockeyName ?? "Chưa có"}</td>
                      <td style={td}>{(e.odds ?? e.Odds ?? 1).toFixed(2)}x</td>
                    </tr>
                  ))}</tbody>
                </table>
              )}
              {raceReferees.length > 0 && (
                <div style={{marginTop:12}}>
                  <h4 style={{fontSize:14,margin:"0 0 8px",color:"var(--hr-paper)"}}>Trọng tài</h4>
                  {raceReferees.map(r => {
                    const st = r.status ?? r.Status;
                    return (
                      <span key={r.id ?? r.Id} style={{
                        display:"inline-block",margin:"0 8px 4px 0",padding:"4px 12px",
                        borderRadius:8,fontSize:12,fontWeight:600,
                        background:st==="Confirmed"?"rgba(112,139,104,.16)":st==="Assigned"?"rgba(185,138,69,.16)":"rgba(238,229,212,.06)",
                        color:st==="Confirmed"?"var(--hr-success)":st==="Assigned"?"var(--hr-warning)":"var(--hr-muted)"
                      }}>
                        {r.refereeName ?? r.RefereeName} — {r.role==="Chief Referee"?"Trọng tài trưởng":"Trợ lý"}
                      </span>
                    );
                  })}
                </div>
              )}
              {raceViolations.length > 0 && (
                <div style={{marginTop:12}}>
                  <h4 style={{fontSize:14,margin:"0 0 8px",color:"var(--hr-danger)"}}>Vi phạm ({raceViolations.length})</h4>
                  {raceViolations.map(v => {
                    const vId = v.id ?? v.Id;
                    const penalty = v.penalty ?? v.Penalty;
                    const isResolved = Boolean(penalty && penalty.trim());
                    return (
                      <div key={vId} style={{padding:"8px 12px",marginBottom:6,borderRadius:8,background:"rgba(201,105,90,0.12)",border:"1px solid rgba(201,105,90,0.3)",fontSize:12}}>
                        <strong style={{color:"var(--hr-danger)"}}>{VIOLATION_LABELS[v.violationType ?? v.ViolationType] ?? "Vi phạm"}</strong>
                        <span style={{color:"var(--hr-muted)",marginLeft:8}}>— {v.horseName ?? v.HorseName} — {v.refereeName ?? v.RefereeName}</span>
                        <p style={{margin:"4px 0 0",color:"var(--hr-text)"}}>{v.description ?? v.Description}</p>
                        {isResolved ? (
                          <div style={{marginTop:6}}>
                            <span style={{fontSize:11,fontWeight:700,color:"var(--hr-success)"}}>✓ Đã xử lý</span>
                            <p style={{margin:"2px 0 0",color:"var(--hr-text)"}}>Hình phạt: {penalty}</p>
                          </div>
                        ) : resolvingViolation === vId ? (
                          <div style={{marginTop:6,display:"flex",gap:6,alignItems:"center",flexWrap:"wrap"}} onClick={e => e.stopPropagation()}>
                            <input
                              type="text"
                              value={penaltyText}
                              onChange={e => setPenaltyText(e.target.value)}
                              placeholder="Nhập hình phạt..."
                              style={{flex:1,minWidth:160,padding:"4px 8px",borderRadius:6,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)",color:"var(--hr-text)",fontSize:12}}
                            />
                            <button style={{padding:"4px 10px",fontSize:11,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:"rgba(112,139,104,0.16)",color:"var(--hr-success)",cursor:"pointer",fontWeight:600}} onClick={() => submitResolveViolation(vId)}>Lưu</button>
                            <button style={{padding:"4px 10px",fontSize:11,borderRadius:6,border:"1px solid var(--hr-border-soft)",background:"transparent",color:"var(--hr-text)",cursor:"pointer"}} onClick={() => { setResolvingViolation(null); setPenaltyText(""); }}>Hủy</button>
                          </div>
                        ) : (
                          <button
                            style={{marginTop:6,padding:"4px 10px",fontSize:11,borderRadius:6,border:"1px solid rgba(201,105,90,.4)",background:"transparent",color:"var(--hr-danger)",cursor:"pointer",fontWeight:600}}
                            onClick={e => { e.stopPropagation(); setResolvingViolation(vId); setPenaltyText(""); }}
                          >
                            Xử lý phạt
                          </button>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
              {itemStatus === "finished" && raceResult && (() => {
                const resultStatusVal = (raceResult.resultStatus ?? raceResult.ResultStatus ?? "").toLowerCase();
                const isOfficial = resultStatusVal === "official";
                const winnerHorseId = raceResult.winningHorseId ?? raceResult.WinningHorseId;
                const winnerEntry = raceEntries.find(e => (e.horseId ?? e.HorseId) === winnerHorseId);

                // Q1-UX: full ranking, sourced from RaceResultResponse.Rankings — the backend's
                // own parse of the canonical RaceResult.RankingsJson (Q1's qualification
                // authority), never RaceEntry.FinishPosition. Only rendered once Official —
                // Provisional keeps the existing winner-only summary below (a full "Đi tiếp/Bị
                // loại" ranking must never be presented before Admin approval finalizes it).
                const rankings = raceResult.rankings ?? raceResult.Rankings ?? [];
                const rankedEntries = isOfficial
                  ? getRankedEntries(rankings).map((r) => {
                      const horseId = r.horseId ?? r.HorseId;
                      const entry = raceEntries.find((e) => (e.horseId ?? e.HorseId) === horseId);
                      return {
                        position: r.position ?? r.Position,
                        horseId,
                        horseName: r.horseName ?? r.HorseName ?? entry?.horseName ?? entry?.HorseName,
                        jockeyName: entry?.jockeyName ?? entry?.JockeyName,
                      };
                    })
                  : [];
                const isFinal = isFinalRound(item, selectedTournament);
                const qualificationSlots = item.qualificationSlots ?? item.QualificationSlots;

                return (
                  <div style={{marginTop:12,padding:"10px 14px",borderRadius:10,background:isOfficial?"rgba(112,139,104,0.1)":"rgba(185,138,69,0.1)",border:`1px solid ${isOfficial?"rgba(112,139,104,0.25)":"rgba(185,138,69,0.3)"}`}}>
                    <h4 style={{fontSize:14,margin:"0 0 6px",color:isOfficial?"var(--hr-success)":"var(--hr-warning)"}}>
                      {isOfficial ? "Kết quả chính thức" : "Kết quả tạm thời (chưa duyệt)"}
                    </h4>
                    {isOfficial && rankedEntries.length > 0 ? (
                      <div style={{display:"grid",gap:4}}>
                        {rankedEntries.map((r) => {
                          const label = getPlacementLabel({ position: r.position, isFinal, qualificationSlots });
                          const color = label === "Bị loại" ? "var(--hr-danger)" : (label === "Đi tiếp" || isFinal) ? "var(--hr-success)" : "var(--hr-muted)";
                          const icon = label === "Đi tiếp" ? "✓ " : label === "Bị loại" ? "✕ " : "";
                          return (
                            <p key={r.horseId} style={{margin:0,fontSize:13,color:"var(--hr-paper)"}}>
                              {r.position === 1 ? "🏆" : `#${r.position}`} <strong>{r.horseName ?? "Chưa xác định"}</strong>
                              {r.jockeyName ? <span> — Kỵ sĩ: {r.jockeyName}</span> : null}
                              {label ? <span style={{marginLeft:8,fontWeight:600,color}}>{icon}{label}</span> : null}
                            </p>
                          );
                        })}
                      </div>
                    ) : (
                      // Legacy safety: Official but no usable Rankings (pre-R0 data), or still
                      // Provisional — winner-only fallback rather than a blank result.
                      <p style={{margin:0,fontSize:13,color:"var(--hr-paper)"}}>
                        {isOfficial ? "🏆" : "⏳"} <strong>{winnerEntry?.horseName ?? winnerEntry?.HorseName ?? "Chưa xác định"}</strong>
                        {winnerEntry?.jockeyName ?? winnerEntry?.JockeyName ? <span> — Kỵ sĩ: {winnerEntry?.jockeyName ?? winnerEntry?.JockeyName}</span> : null}
                      </p>
                    )}
                    {raceResult.notes ?? raceResult.Notes ? <p style={{margin:"6px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Ghi chú: {raceResult.notes ?? raceResult.Notes}</p> : null}
                    {(raceResult.rejectedReason ?? raceResult.RejectedReason) ? <p style={{margin:"4px 0 0",fontSize:12,color:"var(--hr-danger)"}}>Đã bị từ chối trước đó: {raceResult.rejectedReason ?? raceResult.RejectedReason}</p> : null}
                  </div>
                );
              })()}
              {raceReport && (
                <div style={{marginTop:12,padding:"10px 14px",borderRadius:10,background:"rgba(139,92,246,0.1)",border:"1px solid rgba(139,92,246,0.25)"}}>
                  <h4 style={{fontSize:14,margin:"0 0 6px",color:"#c4b5fd"}}>📋 Báo cáo trọng tài</h4>
                  <p style={{margin:0,fontSize:13,color:"var(--hr-text)"}}>{raceReport.details ?? raceReport.Details ?? "—"}</p>
                  {(raceReport.incidents ?? raceReport.Incidents) && (
                    <p style={{margin:"6px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Sự cố: {raceReport.incidents ?? raceReport.Incidents}</p>
                  )}
                  <span style={{display:"block",marginTop:6,fontSize:11,color:"var(--hr-muted)"}}>
                    {raceReport.refereeName ?? raceReport.RefereeName ?? "Trọng tài"}
                    {raceReport.completedAt ?? raceReport.CompletedAt ? ` · ${new Date(raceReport.completedAt ?? raceReport.CompletedAt).toLocaleString("vi-VN")}` : ""}
                  </span>
                </div>
              )}
            </div>
          )}
        </article>;
      })}</section>
    </>
  );
}

function RegistrationManagement() {
  const [entryItems, setEntryItems] = useState([]);
  const [query, setQuery] = useState("");
  const [message, setMessage] = useState("");

  const load = () =>
    getPendingRaceEntries()
      .then((data) => setEntryItems(Array.isArray(data) ? data : []))
      .catch((err) => setMessage(err.message));

  useEffect(() => { load(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const filteredEntries = useMemo(() =>
    entryItems.filter((item) => {
      const search = `${item.horseName ?? item.HorseName ?? ""} ${item.ownerName ?? item.OwnerName ?? ""} ${item.jockeyName ?? item.JockeyName ?? ""} ${item.tournamentName ?? item.TournamentName ?? ""} ${item.raceName ?? item.RaceName ?? ""}`.toLowerCase();
      return search.includes(query.toLowerCase());
    }),
  [query, entryItems]);

  const approveEntry = async (entry) => {
    const id = entry.entryId ?? entry.EntryId;
    try {
      await approveRaceEntry(id);
      setMessage("Đã phê duyệt đăng ký ngựa vào cuộc đua.");
      load();
    } catch (err) { setMessage(err.message); }
  };

  const rejectEntry = async (entry) => {
    const id = entry.entryId ?? entry.EntryId;
    const reason = window.prompt("Lý do từ chối (tùy chọn):");
    if (reason === null) return;
    try {
      await rejectRaceEntry(id, reason || "Bị từ chối bởi admin");
      setMessage("Đã từ chối đăng ký.");
      load();
    } catch (err) { setMessage(err.message); }
  };

  return (
    <>
      <PageTitle eyebrow="Quản lý giải đấu" title="Phê duyệt đăng ký" description="Xem xét và phê duyệt đăng ký ngựa vào cuộc đua." />
      <div className="admin-toolbar">
        <input placeholder="Tìm kiếm theo ngựa, chủ ngựa, kỵ sĩ hoặc giải đấu..." value={query} onChange={(e) => setQuery(e.target.value)} />
        <span>{filteredEntries.length} đăng ký</span>
      </div>
      <Notice message={message} />

      <div className="admin-table-wrap">
          <table className="admin-table">
            <thead><tr><th>Ngựa</th><th>Chủ ngựa</th><th>Kỵ sĩ</th><th>Giải đấu</th><th>Cuộc đua</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>
              {filteredEntries.map((item) => {
                const id = item.entryId ?? item.EntryId;
                return (
                  <tr key={id}>
                    <td><strong>{item.horseName ?? item.HorseName ?? "N/A"}</strong></td>
                    <td>{item.ownerName ?? item.OwnerName ?? "-"}</td>
                    <td>{item.jockeyName ?? item.JockeyName ?? "Chưa có"}</td>
                    <td>{item.tournamentName ?? item.TournamentName ?? "-"}</td>
                    <td>{item.raceName ?? item.RaceName ?? "-"}</td>
                    <td><span className="status status--pending">Chờ duyệt</span></td>
                    <td>
                      <div className="admin-actions">
                        <button onClick={() => approveEntry(item)}>Phê duyệt</button>
                        <button className="admin-danger" onClick={() => rejectEntry(item)}>Từ chối</button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {filteredEntries.length === 0 && (
                <tr><td colSpan={7}>Không có đăng ký ngựa nào đang chờ duyệt.</td></tr>
              )}
            </tbody>
          </table>
      </div>
    </>
  );
}

function AdminPage() {
  const location = useLocation();
  let content = <Dashboard />;
  if (location.pathname === "/admin/users") content = <UserList />;
  else if (location.pathname === "/admin/registrations") content = <RegistrationManagement />;
  else if (location.pathname.includes("/horses/")) content = <HorseDetail />;
  else if (location.pathname.startsWith("/admin/users/")) content = <UserDetail />;
  else if (location.pathname === "/admin/roles") content = <Roles />;
  else if (location.pathname === "/admin/tournaments") content = <TournamentManagement />;
  else if (location.pathname === "/admin/race-results") content = <RaceResultsPage />;
  else if (location.pathname === "/admin/horses") content = <HorseManagementPage />;
  else if (location.pathname === "/admin/referees") content = <RefereeManagementPage />;
  else if (location.pathname === "/admin/referee-assign") content = <RefereeAssignmentManagement />;
  else if (location.pathname === "/admin/rounds") content = <ScheduleManagement type="round" />;
  else if (location.pathname === "/admin/races") content = <ScheduleManagement type="race" />;
  else if (location.pathname === "/admin/prizes") content = <PrizeManagement />;
  else if (location.pathname === "/admin/protests") content = <ProtestManagement />;
  else if (location.pathname === "/admin/transfers") content = <TransferManagement />;
  else if (location.pathname === "/admin/contracts") content = <ContractManagement />;
  else if (location.pathname === "/admin/injuries") content = <InjuryManagement />;
  else if (location.pathname === "/admin/audit") content = <AuditLogViewer />;
  else if (location.pathname === "/admin/notifications") content = <NotificationManager />;
  else if (location.pathname === "/admin/withdrawals") content = <WithdrawalManagement />;
  else if (location.pathname === "/admin/predictions") content = <PredictionsManagementPage />;

  return <AdminShell>{content}</AdminShell>;
}

export default AdminPage;

/* ─── Referee Assignment Management ─── */
function RefereeAssignmentManagement() {
  const [assignments, setAssignments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  const loadAssignments = () => {
    setLoading(true);
    request("/api/referees/assignments")
      .then(d => {
        const list = Array.isArray(d?.data ?? d) ? (d?.data ?? d) : [];
        setAssignments(list);
      })
      .catch(err => setMessage(err.message))
      .finally(() => setLoading(false));
  };

  useEffect(() => { loadAssignments(); }, []);

  const statusLabels = { "Assigned": "Đã phân công", "Confirmed": "Đã xác nhận", "Completed": "Hoàn thành", "Cancelled": "Đã hủy" };
  const roleLabels = { "Chief Referee": "Trọng tài trưởng", "Assistant": "Trợ lý" };

  const sorted = [...assignments].sort((a, b) =>
    (a.tournamentName ?? a.TournamentName ?? "").localeCompare(b.tournamentName ?? b.TournamentName ?? "") ||
    (a.refereeName ?? a.RefereeName ?? "").localeCompare(b.refereeName ?? b.RefereeName ?? ""));

  return (
    <>
      <PageTitle eyebrow="Giải đấu" title="Phân công trọng tài" description="Danh sách trọng tài được phân công cho từng giải đấu." />
      <Notice message={message} />
      <div className="admin-toolbar"><span>{assignments.length} phân công</span></div>
      <div style={{ overflowX: "auto", border: "1px solid var(--hr-border-soft)", borderRadius: 16 }}>
        <table style={{ width: "100%", borderCollapse: "collapse", background: "var(--hr-surface)" }}>
          <thead>
            <tr>
              <th style={th}>Trọng tài</th>
              <th style={th}>Giải đấu</th>
              <th style={th}>Vòng</th>
              <th style={th}>Cuộc đua</th>
              <th style={th}>Vai trò</th>
              <th style={th}>Trạng thái</th>
              <th style={th}>Ngày phân công</th>
              <th style={th}>Ghi chú</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={8} style={{ padding: 20, textAlign: "center", color: "var(--hr-muted)" }}>Đang tải...</td></tr>
            ) : sorted.length === 0 ? (
              <tr><td colSpan={8} style={{ padding: 20, textAlign: "center", color: "var(--hr-muted)" }}>Chưa có phân công nào.</td></tr>
            ) : sorted.map(a => {
              const st = a.status ?? a.Status;
              const stColor = st === "Confirmed" ? "var(--hr-success)" : st === "Completed" ? "#a5b4fc" : st === "Cancelled" ? "var(--hr-danger)" : "var(--hr-warning)";
              const stBg = st === "Confirmed" ? "rgba(112,139,104,.16)" : st === "Completed" ? "rgba(99,102,241,.16)" : st === "Cancelled" ? "rgba(201,105,90,.16)" : "rgba(185,138,69,.16)";
              return (
                <tr key={a.id ?? a.Id}>
                  <td style={td}><strong>{a.refereeName ?? a.RefereeName ?? "-"}</strong></td>
                  <td style={td}>{a.tournamentName ?? a.TournamentName ?? "-"}</td>
                  <td style={td}>{a.roundName ?? a.RoundName ?? "-"}</td>
                  <td style={td}>{a.raceName ?? a.RaceName ?? "-"}</td>
                  <td style={td}>{roleLabels[a.role] ?? a.role ?? "-"}</td>
                  <td style={td}><span style={{ padding: "4px 10px", borderRadius: 999, fontSize: 12, fontWeight: 600, background: stBg, color: stColor }}>{statusLabels[st] ?? st}</span></td>
                  <td style={td}>{a.assignedAt ? formatDate(a.assignedAt) : "-"}</td>
                  <td style={td}>{a.notes ?? a.Notes ?? "-"}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}

const th = { padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" };
const td = { padding: 15, borderBottom: "1px solid var(--hr-border-soft)", fontSize: 13, color: "var(--hr-text)" };

/* ─── Withdrawal Management ─── */

function WithdrawalManagement() {
  const [list, setList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(null);

  const fetchList = async () => {
    setLoading(true);
    try {
      const res = await request("/api/withdrawal/admin/pending");
      const d = res?.data ?? res;
      setList(Array.isArray(d) ? d : []);
    } catch { /* ignore */ }
    setLoading(false);
  };

  useEffect(() => { fetchList(); }, []);

  const handleProcess = async (id, status) => {
    setProcessing(id);
    try {
      await request("/api/withdrawal/admin/process", {
        method: "POST",
        body: JSON.stringify({ withdrawalId: id, status }),
      });
      fetchList();
    } catch (e) {
      alert(e?.message ?? "Xử lý thất bại.");
    }
    setProcessing(null);
  };

  return (
    <div>
      <PageTitle eyebrow="Tài chính" title="Quản lý rút tiền" description="Duyệt yêu cầu rút tiền từ người dùng." />

      {loading ? (
        <p style={{ color: "var(--hr-muted)" }}>Đang tải...</p>
      ) : list.length === 0 ? (
        <div style={{ textAlign: "center", padding: "40px 0", color: "var(--hr-muted)" }}>
          <p>Không có yêu cầu rút tiền nào đang chờ.</p>
        </div>
      ) : (
        <div style={{ overflowX: "auto", border: "1px solid var(--hr-border-soft)", borderRadius: 16 }}>
          <table style={{ width: "100%", borderCollapse: "collapse", background: "var(--hr-surface)" }}>
            <thead>
              <tr>
                <th style={{ padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" }}>Người dùng</th>
                <th style={{ padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" }}>Ngân hàng</th>
                <th style={{ padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" }}>Số tài khoản</th>
                <th style={{ padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" }}>Số tiền</th>
                <th style={{ padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" }}>Ngày yêu cầu</th>
                <th style={{ padding: 15, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {list.map((w) => (
                <tr key={w.id ?? w.Id}>
                  <td style={{ padding: 15, borderBottom: "1px solid var(--hr-border-soft)", fontSize: 13, color: "var(--hr-text)" }}>{w.userName ?? w.UserName ?? "-"}</td>
                  <td style={{ padding: 15, borderBottom: "1px solid var(--hr-border-soft)", fontSize: 13, color: "var(--hr-text)" }}>{w.bankName ?? w.BankName ?? "-"}</td>
                  <td style={{ padding: 15, borderBottom: "1px solid var(--hr-border-soft)", fontSize: 13, color: "var(--hr-text)" }}>{w.accountNumber ?? w.AccountNumber ?? "-"}</td>
                  <td style={{ padding: 15, borderBottom: "1px solid var(--hr-border-soft)", fontSize: 13, color: "var(--hr-text)" }}><strong>{(w.amount ?? w.Amount ?? 0).toLocaleString()} điểm</strong></td>
                  <td style={{ padding: 15, borderBottom: "1px solid var(--hr-border-soft)", fontSize: 13, color: "var(--hr-text)" }}>{w.createdAt ? new Date(w.createdAt).toLocaleDateString() : "-"}</td>
                  <td style={{ display: "flex", gap: 8 }}>
                    <button
                      style={{ padding: "6px 14px", borderRadius: 8, fontSize: 13, fontWeight: 600, border: "none", background: "#1a7d1a", color: "#fff", cursor: "pointer" }}
                      disabled={processing === (w.id ?? w.Id)}
                      onClick={() => handleProcess(w.id ?? w.Id, "completed")}
                    >
                      {processing === (w.id ?? w.Id) ? "..." : "Đã chuyển tiền"}
                    </button>
                    <button
                      style={{ padding: "6px 14px", borderRadius: 8, fontSize: 13, fontWeight: 600, border: "none", background: "#c41e1e", color: "#fff", cursor: "pointer" }}
                      disabled={processing === (w.id ?? w.Id)}
                      onClick={() => handleProcess(w.id ?? w.Id, "rejected")}
                    >
                      Từ chối
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
