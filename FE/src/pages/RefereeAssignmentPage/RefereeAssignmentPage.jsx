import { useCallback, useEffect, useMemo, useState } from "react";
import { getMyAssignments, respondToRefereeAssignment } from "../../services/refereeAssignmentApi";
import "./RefereeAssignmentPage.css";

/* ── Status config ── */
const statusConfig = {
  Assigned: { label: "Chờ xử lý", className: "ra-badge--pending" },
  Pending: { label: "Chờ xử lý", className: "ra-badge--pending" },
  Confirmed: { label: "Đã xác nhận", className: "ra-badge--confirmed" },
  Accepted: { label: "Đã xác nhận", className: "ra-badge--confirmed" },
  Completed: { label: "Hoàn thành", className: "ra-badge--completed" },
  Rejected: { label: "Đã từ chối", className: "ra-badge--rejected" },
  Declined: { label: "Đã từ chối", className: "ra-badge--rejected" },
  Cancelled: { label: "Đã từ chối", className: "ra-badge--rejected" },
};

/* ── Helpers ── */
function fmtDate(value) {
  if (!value) return "Chưa xác định";
  try {
    return new Date(value).toLocaleDateString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  } catch {
    return "Chưa xác định";
  }
}

function fmtTime(value) {
  if (!value) return "";
  try {
    return new Date(value).toLocaleTimeString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return "";
  }
}

function isPending(status) {
  return status === "Assigned" || status === "Pending";
}

function isActionable(status) {
  return isPending(status);
}

export default function RefereeAssignmentPage() {
  const [assignments, setAssignments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(null); // assignmentId being acted on
  const [error, setError] = useState("");
  const [toast, setToast] = useState(null);

  /* ── Load assignments ── */
  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const data = await getMyAssignments();
        if (!cancelled) {
          const list = Array.isArray(data?.data)
            ? data.data
            : Array.isArray(data)
              ? data
              : [];
          setAssignments(list);
        }
      } catch (e) {
        if (!cancelled) {
          setError(e.message);
          setAssignments([]);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => {
      cancelled = true;
    };
  }, []);

  /* ── Show toast helper ── */
  const showToast = useCallback((message) => {
    setToast(message);
    setTimeout(() => setToast(null), 3500);
  }, []);

  /* ── Respond to assignment ── */
  const handleRespond = useCallback(
    async (assignmentId, accept) => {
      setActionLoading(assignmentId);
      try {
        const responseStr = accept ? "Accept" : "Reject";
        await respondToRefereeAssignment(assignmentId, responseStr);
        // Update local state optimistically
        setAssignments((prev) =>
          prev.map((a) =>
            a.id === assignmentId
              ? { ...a, status: accept ? "Confirmed" : "Cancelled" }
              : a
          )
        );
        showToast(accept ? "Đã xác nhận phân công" : "Đã từ chối phân công");
      } catch {
        showToast("Không thể cập nhật. Vui lòng thử lại.");
      } finally {
        setActionLoading(null);
      }
    },
    [showToast]
  );

  /* ── KPI computations ── */
  const { total, confirmed, pending } = useMemo(() => {
    const t = assignments.length;
    const c = assignments.filter(
      (a) => a.status === "Confirmed" || a.status === "Accepted"
    ).length;
    const p = assignments.filter(
      (a) => a.status === "Assigned" || a.status === "Pending"
    ).length;
    return { total: t, confirmed: c, pending: p };
  }, [assignments]);

  /* ── Render card action buttons ── */
  const renderActions = (a) => {
    if (!isActionable(a.status)) return null;
    const busy = actionLoading === a.id;
    return (
      <div className="ra-card__actions">
        <button
          className="ra-btn ra-btn--accept"
          disabled={busy}
          onClick={() => handleRespond(a.id, true)}
        >
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <polyline points="20 6 9 17 4 12" />
          </svg>
          Xác nhận
        </button>
        <button
          className="ra-btn ra-btn--reject"
          disabled={busy}
          onClick={() => handleRespond(a.id, false)}
        >
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
          Từ chối
        </button>
      </div>
    );
  };

  /* ── Render card ── */
  const renderCard = (a) => {
    const cfg = statusConfig[a.status] || {
      label: a.status || "Không xác định",
      className: "ra-badge--pending",
    };
    const borderClass =
      a.status === "Confirmed" || a.status === "Accepted"
        ? "ra-card--confirmed"
        : a.status === "Completed"
          ? "ra-card--completed"
          : a.status === "Rejected" || a.status === "Declined" || a.status === "Cancelled"
            ? "ra-card--rejected"
            : "ra-card--pending";

    return (
      <div key={a.id} className={`ra-card ${borderClass}`}>
        {/* Top row: title + badge */}
        <div className="ra-card__top">
          <h3 className="ra-card__title">
            {a.raceName || "Cuộc đua"}
          </h3>
          <span className={`ra-badge ${cfg.className}`}>{cfg.label}</span>
        </div>

        {/* Role */}
        {a.role && (
          <div className="ra-card__role">
            Vai trò: <strong>{a.role}</strong>
          </div>
        )}

        {/* Details row */}
        <div className="ra-card__details">
          {a.raceDate && (
            <div className="ra-card__detail">
              <svg
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
              </svg>
              <span>
                {fmtDate(a.raceDate)} {fmtTime(a.raceDate) && `- ${fmtTime(a.raceDate)}`}
              </span>
            </div>
          )}
          {a.location && (
            <div className="ra-card__detail">
              <svg
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                <circle cx="12" cy="10" r="3" />
              </svg>
              <span>{a.location}</span>
            </div>
          )}
          {a.assignedAt && (
            <div className="ra-card__detail">
              <svg
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              </svg>
              <span>Phân công: {fmtDate(a.assignedAt)}</span>
            </div>
          )}
        </div>

        {/* Action buttons */}
        {renderActions(a)}
      </div>
    );
  };

  /* ── Main render ── */
  return (
    <div className="ra-wrap">
      <div className="ra-container">
        {/* ── Header ── */}
        <header className="ra-topbar">
          <div className="ra-topbar__left">
            <h1>Phân công</h1>
            <p className="ra-topbar__sub">
              Xem xét và phản hồi phân công trọng tài từ ban tổ chức giải đấu.
            </p>
          </div>
        </header>

        {/* ── Summary Chips ── */}
        <div className="ra-chips">
          <div className="ra-chip">
            <strong>{total}</strong>
            <span>Tổng phân công</span>
          </div>
          <div className="ra-chip">
            <strong>{confirmed}</strong>
            <span>Đã xác nhận</span>
          </div>
          <div className="ra-chip">
            <strong>{pending}</strong>
            <span>Chờ xử lý</span>
          </div>
        </div>

        {/* ── KPI Dark Cards ── */}
        <div className="ra-kpis">
          <div className="ra-kpi">
            <span className="ra-kpi__label">Tổng số</span>
            <strong className="ra-kpi__value">{total}</strong>
            <span className="ra-kpi__trend">
              {confirmed} đã xác nhận &middot; {pending} chờ
            </span>
          </div>
          <div className="ra-kpi">
            <span className="ra-kpi__label">Đã xác nhận</span>
            <strong className="ra-kpi__value">{confirmed}</strong>
            <span className="ra-kpi__trend">
              {total > 0
                ? Math.round((confirmed / total) * 100) + "% tổng số"
                : "Chưa có dữ liệu"}
            </span>
          </div>
          <div className="ra-kpi">
            <span className="ra-kpi__label">Chờ xử lý</span>
            <strong className="ra-kpi__value">{pending}</strong>
            <span className="ra-kpi__trend">
              Cần phản hồi ngay
            </span>
          </div>
        </div>

        {/* ── Assignment List ── */}
        {loading ? (
          <div className="ra-loading">
            <div className="ra-skeleton" />
            <div className="ra-skeleton" />
            <div className="ra-skeleton" />
          </div>
        ) : assignments.length === 0 ? (
          <div className="ra-empty">
            <div className="ra-empty__icon">
              <svg
                width="24"
                height="24"
                viewBox="0 0 24 24"
                fill="none"
                stroke="#94a3b8"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              </svg>
            </div>
            <h3>Chưa có phân công</h3>
            <p>Bạn chưa được phân công trận đấu nào. Vui lòng quay lại sau.</p>
          </div>
        ) : (
          <div className="ra-list">
            {assignments.map(renderCard)}
          </div>
        )}

        {/* ── Error banner ── */}
        {error && (
          <div className="ra-info">
            Không thể tải dữ liệu từ máy chủ: {error}
          </div>
        )}

        {/* ── Toast ── */}
        {toast && <div className="ra-toast">{toast}</div>}
      </div>
    </div>
  );
}
