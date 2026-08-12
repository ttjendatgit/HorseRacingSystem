import { useEffect, useState, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { deleteAllNotifications, getNotifications, getUnreadCount, markNotificationRead } from "../../services/notificationApi";

export default function NotificationBell() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [notifs, setNotifs] = useState([]);
  const [unread, setUnread] = useState(0);
  const [deletingAll, setDeletingAll] = useState(false);
  const ref = useRef(null);

  const load = useCallback(() => {
    getNotifications().then((d) => setNotifs(Array.isArray(d) ? d.slice(0, 8) : [])).catch(() => {});
    getUnreadCount().then((d) => setUnread(typeof d === "number" ? d : d?.count ?? d?.Count ?? 0)).catch(() => {});
  }, []);

  useEffect(() => {
    load();
    const intervalId = window.setInterval(load, 15000);
    window.addEventListener("focus", load);
    return () => {
      window.clearInterval(intervalId);
      window.removeEventListener("focus", load);
    };
  }, [load]);

  useEffect(() => {
    if (!open) return;
    const cb = () => setOpen(false);
    document.addEventListener("click", cb);
    return () => document.removeEventListener("click", cb);
  }, [open]);

  const handleMark = async (id) => {
    try { await markNotificationRead(id); load(); } catch { /* ignore */ }
  };

  const handleNotificationClick = async (notification) => {
    const id = notification.id ?? notification.Id;
    const isRead = notification.isRead ?? notification.IsRead ?? false;
    const actionUrl = notification.actionUrl ?? notification.ActionUrl;
    if (!isRead) await handleMark(id);
    setOpen(false);
    if (actionUrl) navigate(actionUrl);
  };

  const handleDeleteAll = async (event) => {
    event.stopPropagation();
    if (!window.confirm("Bạn có chắc muốn xóa tất cả thông báo?")) return;

    setDeletingAll(true);
    try {
      await deleteAllNotifications();
      setNotifs([]);
      setUnread(0);
    } catch {
      window.alert("Không thể xóa tất cả thông báo. Vui lòng thử lại.");
    } finally {
      setDeletingAll(false);
    }
  };

  return (
    <div style={{ position: "relative" }} ref={ref} onClick={(e) => { e.stopPropagation(); setOpen(!open); }}>
      <button className="ah-notif" onClick={() => {}}>
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5"><path d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/></svg>
        {unread > 0 && <span className="ah-notif-badge">{unread > 9 ? "9+" : unread}</span>}
      </button>

      {open && (
        <div style={{
          position: "absolute", right: 0, top: "calc(100% + 8px)", zIndex: 100,
          width: 340, background: "#1a1511", borderRadius: 10,
          border: "1px solid rgba(184,134,59,0.18)", boxShadow: "0 16px 40px rgba(0,0,0,0.35)",
          padding: 16,
        }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12, paddingBottom: 10, borderBottom: "1px solid rgba(238,229,212,0.08)" }}>
            <strong style={{ fontSize: 14, color: "#eee5d4" }}>Thông báo</strong>
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              {notifs.length > 0 && (
                <button
                  type="button"
                  disabled={deletingAll}
                  onClick={handleDeleteAll}
                  style={{
                    border: 0, background: "transparent", padding: 0, cursor: deletingAll ? "wait" : "pointer",
                    color: "#c9695a", fontSize: 11, fontWeight: 600, opacity: deletingAll ? 0.6 : 1,
                  }}
                >
                  {deletingAll ? "Đang xóa..." : "Xóa tất cả"}
                </button>
              )}
              <span style={{ fontSize: 11, color: "#aa9d8a" }}>{unread} chưa đọc</span>
            </div>
          </div>
          {notifs.length === 0 ? (
            <p style={{ textAlign: "center", color: "#aa9d8a", fontSize: 13, padding: "20px 0", margin: 0 }}>Không có thông báo</p>
          ) : (
            <div style={{ display: "grid", gap: 4 }}>
              {notifs.map((n) => {
                const id = n.id ?? n.Id;
                const isRead = n.isRead ?? n.IsRead ?? false;
                return (
                  <div key={id} role="button" tabIndex={0} onClick={() => handleNotificationClick(n)}
                    onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); handleNotificationClick(n); } }}
                    style={{
                      padding: "10px 12px", borderRadius: 8, cursor: "pointer", fontSize: 13,
                      background: isRead ? "transparent" : "rgba(184,134,59,0.07)",
                      borderLeft: isRead ? "2px solid transparent" : "2px solid #b8863b",
                      transition: "background 0.15s",
                    }}
                    onMouseEnter={(e) => e.target.style.background = "rgba(238,229,212,0.04)"}
                    onMouseLeave={(e) => e.target.style.background = isRead ? "transparent" : "rgba(184,134,59,0.07)"}
                  >
                    <strong style={{ display: "block", color: "#eee5d4", marginBottom: 2 }}>{n.title ?? n.Title ?? "Thông báo"}</strong>
                    <span style={{ color: "#aa9d8a", fontSize: 12 }}>{n.message ?? n.Message ?? n.content ?? n.Content ?? ""}</span>
                    <span style={{ display: "block", color: "#aa9d8a", fontSize: 10, marginTop: 4 }}>
                      {n.createdAt ?? n.CreatedAt ? new Date(n.createdAt ?? n.CreatedAt).toLocaleDateString() : ""}
                    </span>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
