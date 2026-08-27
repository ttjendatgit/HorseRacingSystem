import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getTournaments } from "../../services/spectatorApi";
import "./TournamentListPage.css";

function TournamentListPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("all");

  useEffect(() => {
    getTournaments()
      .then((d) => {
        const list = Array.isArray(d) ? d : [];
        list.sort((a, b) => (b.prizePool ?? b.PrizePool ?? 0) - (a.prizePool ?? a.PrizePool ?? 0));
        setItems(list);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const filteredItems = statusFilter === "all"
    ? items
    : items.filter((t) => (t.status ?? t.Status)?.toString().toLowerCase() === statusFilter);

  const statusCounts = {
    all: items.length,
    draft: items.filter((t) => (t.status ?? t.Status) === 0).length,
    published: items.filter((t) => (t.status ?? t.Status) === 1).length,
    ongoing: items.filter((t) => (t.status ?? t.Status) === 2).length,
    finished: items.filter((t) => (t.status ?? t.Status) === 3).length,
    cancelled: items.filter((t) => (t.status ?? t.Status) === 4).length,
  };

  return (
    <div className="tournament-list-page">
      <div className="tournament-layout">
        <aside className="tournament-sidebar">
          <div className="tournament-sidebar__header">
            <span className="pill">Danh Sách Giải Đấu</span>
            <h3>Duyệt sự kiện</h3>
            <p className="muted">Khám phá các mùa giải, vòng đấu và quy mô cuộc đua.</p>
          </div>
          <div className="tournament-sidebar__card">
            <p className="muted">Đang diễn ra</p>
            <h4>{statusCounts.ongoing}</h4>
            <span>{items.length} tổng số giải đấu</span>
          </div>

          {/* Status Filter */}
          <div style={{ marginTop: 16 }}>
            <p className="muted" style={{ marginBottom: 8, fontSize: 12 }}>Lọc theo trạng thái</p>
            {Object.entries(statusCounts).map(([key, count]) => (
              <button
                key={key}
                onClick={() => setStatusFilter(key)}
                style={{
                  display: "block",
                  width: "100%",
                  padding: "8px 12px",
                  marginBottom: 4,
                  borderRadius: 8,
                  border: "none",
                  background: statusFilter === key ? "rgba(143,100,32,0.15)" : "transparent",
                  color: statusFilter === key ? "#8f6420" : "#657086",
                  cursor: "pointer",
                  textAlign: "left",
                  fontSize: 13,
                  fontWeight: statusFilter === key ? 600 : 400,
                }}
              >
                {statusLabel(key)} ({count})
              </button>
            ))}
          </div>
        </aside>

        <div className="tournament-content">
          <section className="page-header tournament-hero">
            <h1>Giải Đấu</h1>
            <p>
              Duyệt tất cả các giải đấu đua ngựa, mùa giải đang hoạt động và quy mô cuộc đua.
            </p>
          </section>

          {loading ? (
            <div className="empty-state">
              <h3>Đang tải giải đấu</h3>
              <p>Đang lấy lịch đua và thông tin cuộc đua mới nhất.</p>
            </div>
          ) : filteredItems.length === 0 ? (
            <div className="empty-state">
              <h3>Không tìm thấy giải đấu</h3>
              <p>Hiện không có giải đấu nào để hiển thị.</p>
            </div>
          ) : (
            <div className="tournament-list">
              {filteredItems.map((t) => {
                const status = t.status ?? t.Status;
                const stats = t.stats ?? t.Stats;
                return (
                  <article key={t.id ?? t.Id} className="tournament-card">
                    <div
                      className="tournament-banner"
                      style={{
                        position: "relative",
                        ...(t.imageUrl ?? t.ImageUrl
                          ? { backgroundImage: `url(${t.imageUrl ?? t.ImageUrl})`, backgroundSize: "cover", backgroundPosition: "center" }
                          : {}),
                      }}
                    >
                      <span
                        className="status-pill"
                        style={{
                          background: statusBg(status),
                          color: statusColor(status),
                        }}
                      >
                        {statusLabel_(status)}
                      </span>
                      {(t.imageUrl ?? t.ImageUrl) && (
                        <span title="Có ảnh bìa" style={{
                          position: "absolute", left: 12, top: 12,
                          background: "rgba(0,0,0,0.5)", color: "#fff",
                          borderRadius: 6, padding: "2px 8px", fontSize: 11,
                          display: "inline-flex", alignItems: "center", gap: 4
                        }}>🖼</span>
                      )}
                    </div>
                    <div className="tournament-body">
                      <div>
                        <h3>{t.name ?? t.Name}</h3>
                        <p>
                          {t.description ??
                            t.Description ??
                            "Không có mô tả."}
                        </p>
                      </div>
                      <div className="tournament-meta">
                        <div>
                          <span>Vòng đấu</span>
                          <strong>{t.roundCount ?? t.RoundCount ?? 0}</strong>
                        </div>
                        <div>
                          <span>Cuộc đua</span>
                          <strong>{stats?.raceCount ?? t.raceCount ?? t.RaceCount ?? 0}</strong>
                        </div>
                        <div>
                          <span>Ngựa</span>
                          <strong>{stats?.horseCount ?? 0}</strong>
                        </div>
                        <div>
                          <span>Kỵ sĩ</span>
                          <strong>{stats?.jockeyCount ?? 0}</strong>
                        </div>
                        {stats?.daysRemaining != null && (
                          <div>
                            <span>Còn lại</span>
                            <strong>{stats.daysRemaining} ngày</strong>
                          </div>
                        )}
                      </div>
                    </div>
                    <div className="tournament-actions">
                      <Link
                        className="ghost-button"
                        to={`/tournaments/${t.id ?? t.Id}`}
                      >
                        Xem chi tiết
                      </Link>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function statusLabel(key) {
  const map = {
    all: "Tất cả",
    draft: "Bản nháp",
    published: "Đã công bố",
    ongoing: "Đang diễn ra",
    finished: "Đã kết thúc",
    cancelled: "Đã hủy",
  };
  return map[key] || key;
}

function statusBg(s) {
  const m = {
    0: "rgba(100,116,139,0.1)",  // Draft
    1: "rgba(37,99,235,0.1)",    // Published
    2: "rgba(245,158,11,0.1)",   // Ongoing
    3: "rgba(16,185,129,0.1)",   // Finished
    4: "rgba(239,68,68,0.1)",    // Cancelled
  };
  return m[s] || "rgba(100,116,139,0.1)";
}

function statusColor(s) {
  const m = {
    0: "#64748b",  // Draft
    1: "#2563eb",  // Published
    2: "#f59e0b",  // Ongoing
    3: "#10b981",  // Finished
    4: "#ef4444",  // Cancelled
  };
  return m[s] || "#64748b";
}

function statusLabel_(s) {
  const m = {
    0: "Bản nháp",
    1: "Đã công bố",
    2: "Đang diễn ra",
    3: "Đã kết thúc",
    4: "Đã hủy",
  };
  return m[s] || "Không xác định";
}

export default TournamentListPage;
