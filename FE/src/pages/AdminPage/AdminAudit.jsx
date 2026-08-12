import { useEffect, useState } from "react";
import { request } from "../../services/apiClient";

const fDate = (v) => v ? new Date(v).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" }) : "-";
const PAGE_SIZE = 20;

export function AuditLogViewer() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState("");
  const [page, setPage] = useState(0);
  const [total, setTotal] = useState(0);

  const load = async (limit = 500) => {
    setLoading(true);
    try {
      const data = await request(`/api/auditlogs/latest/${limit}`);
      const list = Array.isArray(data) ? data : data?.data ?? [];
      setItems(list);
      setTotal(list.length);
    } catch (e) { setMsg(e.message); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const pageItems = items.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
  const maxPage = Math.max(0, Math.ceil(total / PAGE_SIZE) - 1);

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <h2>Nhật ký kiểm toán</h2>
          <p style={{ color: "#657086", marginBottom: 16 }}>Theo dõi tất cả hành động và thay đổi hệ thống.</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <span style={{ color: "#657086", fontSize: 13 }}>{total} bản ghi</span>
          <button disabled={page === 0} onClick={() => setPage(page - 1)}>Trước</button>
          <span style={{ color: "#e7c678" }}>{page + 1}/{maxPage + 1}</span>
          <button disabled={page >= maxPage} onClick={() => setPage(page + 1)}>Sau</button>
        </div>
      </div>
      {msg && <p className="admin-notice admin-notice--error">{msg}</p>}
      {loading ? <p>Đang tải...</p> : (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead><tr><th>Thời gian</th><th>Hành động</th><th>Đối tượng</th><th>Mô tả</th><th>IP</th></tr></thead>
            <tbody>
              {pageItems.map((log) => {
                const id = log.id ?? log.Id;
                const entId = typeof (log.entityId ?? log.EntityId) === "string" ? (log.entityId ?? log.EntityId).slice(0, 8) : "-";
                return (
                  <tr key={id}>
                    <td>{fDate(log.createdAt ?? log.CreatedAt)}</td>
                    <td><span className="badge">{log.action ?? log.Action}</span></td>
                    <td>{log.entityType ?? log.EntityType} #{entId}</td>
                    <td>{log.description ?? log.Description ?? "-"}</td>
                    <td>{log.ipAddress ?? log.IpAddress ?? "-"}</td>
                  </tr>
                );
              })}
              {pageItems.length === 0 && <tr><td colSpan={5}>Không tìm thấy nhật ký kiểm toán nào.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function NotificationManager() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState("");
  const [page, setPage] = useState(0);
  const PAGE = 20;

  const load = async () => {
    setLoading(true);
    try {
      const data = await request("/api/notifications/filter", { method: "POST", body: JSON.stringify({}) });
      const list = Array.isArray(data) ? data : data?.data ?? [];
      setItems(list);
    } catch (e) { setMsg(e.message); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const pageItems = items.slice(page * PAGE, (page + 1) * PAGE);
  const maxPage = Math.max(0, Math.ceil(items.length / PAGE) - 1);

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <h2>Thông báo</h2>
          <p style={{ color: "#657086", marginBottom: 16 }}>Xem và quản lý thông báo hệ thống.</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <span style={{ color: "#657086", fontSize: 13 }}>{items.length} bản ghi</span>
          <button disabled={page === 0} onClick={() => setPage(page - 1)}>Trước</button>
          <span style={{ color: "#e7c678" }}>{page + 1}/{maxPage + 1}</span>
          <button disabled={page >= maxPage} onClick={() => setPage(page + 1)}>Sau</button>
        </div>
      </div>
      {msg && <p className="admin-notice admin-notice--error">{msg}</p>}
      {loading ? <p>Đang tải...</p> : (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead><tr><th>Thời gian</th><th>Người dùng</th><th>Tiêu đề</th><th>Loại</th><th>Danh mục</th><th>Đã đọc</th></tr></thead>
            <tbody>
              {pageItems.map((n) => (
                <tr key={n.id ?? n.Id}>
                  <td>{fDate(n.createdAt ?? n.CreatedAt)}</td>
                  <td>{n.userId ?? n.UserId}</td>
                  <td>{n.title ?? n.Title}</td>
                  <td><span className="badge">{n.type ?? n.Type}</span></td>
                  <td>{n.category ?? n.Category}</td>
                  <td><span className={`status status--${(n.isRead ?? n.IsRead) ? "active" : "inactive"}`}>{(n.isRead ?? n.IsRead) ? "Đã đọc" : "Chưa đọc"}</span></td>
                </tr>
              ))}
              {pageItems.length === 0 && <tr><td colSpan={6}>Không tìm thấy thông báo nào.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
