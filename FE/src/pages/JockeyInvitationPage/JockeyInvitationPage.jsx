import { useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import {
  formatJockeyDate,
  getJockeyInvitations,
  normalizeInvitationStatus,
  respondJockeyInvitation,
  withdrawJockeyInvitation,
} from "../../services/jockeyApi";
import "./JockeyInvitationPage.css";

function JockeyInvitationPage() {
  const location = useLocation();
  const [invitations, setInvitations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadingId, setLoadingId] = useState(null);
  const [message, setMessage] = useState("");
  const [activeTab, setActiveTab] = useState(location.state?.focusTab ?? "all");
  const [search, setSearch] = useState("");

  const loadInvitations = async () => {
    try { setLoading(true); const d = await getJockeyInvitations(); setInvitations(d); setMessage(""); }
    catch (e) { setMessage(e.message || "Không thể tải lời mời."); }
    finally { setLoading(false); }
  };

  useEffect(() => {
    if (location.state?.focusTab) {
      setActiveTab(location.state.focusTab);
    }
  }, [location.state?.focusTab]);

  useEffect(() => { loadInvitations(); }, []);

  const getBucket = (value) => {
    const normalized = normalizeInvitationStatus(value).toLowerCase();
    if (normalized.includes("pending") || normalized === "1") return "pending";
    if (normalized.includes("accept") || normalized.includes("confirm") || normalized === "2") return "accepted";
    if (normalized.includes("decline") || normalized.includes("reject") || normalized === "3") return "declined";
    if (normalized.includes("withdraw") || normalized === "4") return "withdrawn";
    return "unknown";
  };

  const pending = useMemo(() => invitations.filter(i => getBucket(i.status) === "pending"), [invitations]);
  const accepted = useMemo(() => invitations.filter(i => getBucket(i.status) === "accepted"), [invitations]);
  const declined = useMemo(() => invitations.filter(i => getBucket(i.status) === "declined"), [invitations]);
  const withdrawn = useMemo(() => invitations.filter(i => getBucket(i.status) === "withdrawn"), [invitations]);

  const filtered = useMemo(() => {
    let items = invitations;
    if (activeTab === "pending") items = pending;
    else if (activeTab === "accepted") items = accepted;
    else if (activeTab === "declined") items = declined;
    else if (activeTab === "withdrawn") items = withdrawn;
    if (search.trim()) items = items.filter(i => (i.raceName || "").toLowerCase().includes(search.toLowerCase()) || (i.horseName || "").toLowerCase().includes(search.toLowerCase()) || (i.ownerName || "").toLowerCase().includes(search.toLowerCase()));
    return items;
  }, [activeTab, invitations, pending, accepted, declined, withdrawn, search]);

  const handleWithdraw = async (id) => {
    const reason = window.prompt("Vui lòng nhập lý do xin rút khỏi cuộc đua:");
    if (reason === null) return;
    if (reason.trim().length < 3) {
      setMessage("Lý do xin rút phải có ít nhất 3 ký tự.");
      return;
    }
    setLoadingId(id);
    try {
      await withdrawJockeyInvitation(id, reason.trim());
      setInvitations((current) => current.map((item) =>
        item.id === id ? { ...item, status: "Withdrawn", responseNote: reason.trim() } : item));
      setActiveTab("withdrawn");
      setMessage("Đã xin rút khỏi cuộc đua và thông báo cho chủ ngựa.");
    } catch (e) { setMessage(e.message || "Không thể xin rút khỏi cuộc đua."); }
    finally { setLoadingId(null); }
  };

  const handleResponse = async (id, accept) => {
    setLoadingId(id);
    try {
      await respondJockeyInvitation(id, accept);
      setInvitations((current) =>
        current.map((item) =>
          item.id === id ? { ...item, status: accept ? "Accepted" : "Declined", normalizedStatus: accept ? "Accepted" : "Declined" } : item,
        ),
      );
      setActiveTab(accept ? "accepted" : "declined");
      setMessage(accept ? "Đã chấp nhận lời mời." : "Đã từ chối lời mời.");
    } catch (e) { setMessage(e.message || "Lỗi."); }
    finally { setLoadingId(null); }
  };

  const statusMeta = (s) => {
    const bucket = getBucket(s);
    if (bucket === "pending") return { label: "Chờ duyệt", cls: "pending" };
    if (bucket === "accepted") return { label: "Đã chấp nhận", cls: "accepted" };
    if (bucket === "declined") return { label: "Đã từ chối", cls: "declined" };
    if (bucket === "withdrawn") return { label: "Đã xin rút", cls: "withdrawn" };
    return { label: s || "Không rõ", cls: "" };
  };

  const TABS = [
    { key: "all", label: "Tất cả" },
    { key: "pending", label: "Chờ duyệt", count: pending.length },
    { key: "accepted", label: "Đã chấp nhận", count: accepted.length },
    { key: "declined", label: "Đã từ chối", count: declined.length },
    { key: "withdrawn", label: "Đã xin rút", count: withdrawn.length },
  ];

  return (
    <div className="ji-page">
      {/* Header */}
      <div className="ji-header">
        <div>
          <h1>Lời mời đua</h1>
          <p className="ji-sub">Theo dõi và phản hồi các lời mời từ chủ ngựa.</p>
        </div>
      </div>

      {/* Chips */}
      <div className="ji-chips">
        <span className={`ji-chip ${activeTab === "pending" ? "ji-chip--pending" : ""}`} onClick={() => setActiveTab("pending")}>
          <span className="ji-chip-dot ji-chip-dot--pending" /> Chờ duyệt <strong>{pending.length}</strong>
        </span>
        <span className={`ji-chip ${activeTab === "accepted" ? "ji-chip--accepted" : ""}`} onClick={() => setActiveTab("accepted")}>
          <span className="ji-chip-dot ji-chip-dot--accepted" /> Đã chấp nhận <strong>{accepted.length}</strong>
        </span>
        <span className={`ji-chip ${activeTab === "declined" ? "ji-chip--declined" : ""}`} onClick={() => setActiveTab("declined")}>
          <span className="ji-chip-dot ji-chip-dot--declined" /> Đã từ chối <strong>{declined.length}</strong>
        </span>
      </div>

      {/* Toolbar */}
      <div className="ji-toolbar">
        <div className="ji-search">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
          <input placeholder="Tìm lời mời..." value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        <button className="ji-btn-icon" onClick={loadInvitations} disabled={loading} title="Làm mới">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M1 4v6h6M23 20v-6h-6"/><path d="M20.49 9A9 9 0 005.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 013.51 15"/></svg>
        </button>
        <div className="ji-tabs">
          {TABS.map(t => (
            <button key={t.key} className={`ji-tab ${activeTab === t.key ? "ji-tab--active" : ""}`} onClick={() => setActiveTab(t.key)}>
              {t.label}{t.count !== undefined && <span className="ji-tab-num">{t.count}</span>}
            </button>
          ))}
        </div>
      </div>

      {message && <div className="ji-msg">{message}</div>}

      {/* Content */}
      {loading ? (
        <div className="ji-loading">
          <div className="ji-skeleton" />
          <div className="ji-skeleton" />
          <div className="ji-skeleton" />
        </div>
      ) : filtered.length === 0 ? (
        <div className="ji-empty">
          <div className="ji-empty-visual">
            <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="rgba(242,210,139,0.3)" strokeWidth="1"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
          </div>
          <h3>Chưa có lời mời nào</h3>
          <p>Khi chủ ngựa gửi lời mời tham gia cuộc đua, chúng sẽ xuất hiện tại đây.</p>
          <Link to="/jockey/schedule" className="ji-btn ji-btn--primary" style={{marginTop:12}}>Xem lịch đua</Link>
        </div>
      ) : (
        <div className="ji-list">
          {filtered.map(inv => {
            const s = statusMeta(inv.status);
            const canRespond = getBucket(inv.status) === "pending";
            const canWithdraw = getBucket(inv.status) === "accepted";
            return (
              <div key={inv.id} className={`ji-card ji-card--${s.cls}`}>
                <div className="ji-card__main">
                  <div className="ji-card__left">
                    <div className="ji-card__icon">
                      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#8f6420" strokeWidth="1.5"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
                    </div>
                    <div>
                      <h3>{inv.raceName}</h3>
                      <p className="ji-card__meta">
                        {inv.horseName && <>Ngựa: <strong>{inv.horseName}</strong> · </>}
                        {inv.ownerName && <>Chủ ngựa: <strong>{inv.ownerName}</strong> · </>}
                        {inv.scheduledAt ? formatJockeyDate(inv.scheduledAt) : ""}
                      </p>
                    </div>
                  </div>
                  <div className="ji-card__right">
                    <span className={`ji-badge ji-badge--${s.cls}`}>{s.label}</span>
                  </div>
                </div>
                <div className="ji-card__extra">
                  {inv.tournamentName && <span>Giải: {inv.tournamentName}</span>}
                  {inv.location && <span> · {inv.location}</span>}
                </div>
                <div className="ji-card__actions">
                  {canRespond && <button className="ji-btn ji-btn--ghost" onClick={() => handleResponse(inv.id, false)} disabled={loadingId !== null}>
                    Từ chối
                  </button>}
                  {canRespond && <button className="ji-btn ji-btn--primary" onClick={() => handleResponse(inv.id, true)} disabled={loadingId !== null}>
                    {loadingId === inv.id ? "..." : "Chấp nhận"}
                  </button>}
                  {canWithdraw && <button className="ji-btn ji-btn--danger" onClick={() => handleWithdraw(inv.id)} disabled={loadingId !== null}>
                    {loadingId === inv.id ? "..." : "Xin rút"}
                  </button>}
                  <Link to={`/jockey/invitations/${inv.id}`} className="ji-btn ji-btn--outline">Chi tiết</Link>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default JockeyInvitationPage;
