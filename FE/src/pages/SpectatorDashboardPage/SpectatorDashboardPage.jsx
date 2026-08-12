import { useEffect, useState } from "react";
import { getActiveTournaments, getRaces, getMyPredictions } from "../../services/spectatorApi";
import "./SpectatorDashboardPage.css";

const formatRaceTime = (value) =>
  value
    ? new Date(value).toLocaleString("vi-VN", {
        dateStyle: "medium",
        timeStyle: "short",
      })
    : "Chưa xác định";

const getField = (obj, camel, pascal) => obj[camel] ?? obj[pascal];

function SpectatorDashboardPage() {
  const [tournaments, setTournaments] = useState([]);
  const [races, setRaces] = useState([]);
  const [predictionsToday, setPredictionsToday] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      getActiveTournaments().catch(() => []),
      getRaces().catch(() => []),
      getMyPredictions().catch(() => null),
    ])
      .then(([t, r, p]) => {
        setTournaments(Array.isArray(t) ? t : []);
        setRaces(Array.isArray(r) ? r : []);
        if (p && Array.isArray(p)) {
          const today = new Date().toDateString();
          const count = p.filter((pred) => {
            const createdAt = pred.createdAt ?? pred.CreatedAt;
            return createdAt && new Date(createdAt).toDateString() === today;
          }).length;
          setPredictionsToday(count);
        } else {
          setPredictionsToday(0);
        }
      })
      .finally(() => setLoading(false));
  }, []);

  const liveRaces = races.filter(
    (r) => (getField(r, "status", "Status") ?? "").toLowerCase() === "inprogress"
  );
  const upcomingRaces = races.filter(
    (r) => (getField(r, "status", "Status") ?? "").toLowerCase() === "scheduled"
  );

  // --- Loading state ---
  if (loading) {
    return (
      <div className="sd-page">
        <div className="sd-loading">
          <div className="sd-loading-spinner" />
          <p>Đang tải bảng điều khiển...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="sd-page">
      {/* ── Hero header ── */}
      <header className="sd-hero">
        <div className="sd-hero-text">
          <span className="sd-hero-badge">Khán giả</span>
          <h1>Bảng điều khiển</h1>
          <p>Theo dõi giải đấu, cuộc đua trực tiếp và dự đoán trong một nơi.</p>
        </div>
      </header>

      {/* ── Stats bar ── */}
      <div className="sd-stats">
        <div className="sd-stat-card">

          {/* sd-stat-icon removed */}
          <div className="sd-stat-body">
            <span className="sd-stat-value">{tournaments.length}</span>
            <span className="sd-stat-label">Giải đấu đang hoạt động</span>
          </div>
        </div>

        <div className="sd-stat-card">
          {/* sd-stat-icon removed */}
          <div className="sd-stat-body">
            <span className="sd-stat-value">{liveRaces.length}</span>
            <span className="sd-stat-label">Cuộc đua trực tiếp</span>
          </div>
        </div>

        <div className="sd-stat-card">
          {/* sd-stat-icon removed */}
          <div className="sd-stat-body">
            <span className="sd-stat-value">{upcomingRaces.length}</span>
            <span className="sd-stat-label">Cuộc đua sắp tới</span>
          </div>
        </div>

        <div className="sd-stat-card">
          {/* sd-stat-icon removed */}
          <div className="sd-stat-body">
            <span className="sd-stat-value">
              {predictionsToday !== null ? predictionsToday : "-"}
            </span>
            <span className="sd-stat-label">Dự đoán hôm nay</span>
          </div>
        </div>
      </div>

      {/* ── Live Now section ── */}
      <section className="sd-section">
        <div className="sd-section-header">
          <h2>Đang trực tiếp</h2>
          {liveRaces.length > 0 && (
            <span className="sd-live-dot" aria-label="Đang phát trực tiếp">
              <span className="sd-live-dot-pulse" />
              TRỰC TIẾP
            </span>
          )}
        </div>

        {liveRaces.length === 0 ? (
          <div className="sd-empty">

<p>Không có cuộc đua trực tiếp nào.</p>
            <span>Hãy quay lại sau để theo dõi các cuộc đua đang diễn ra.</span>
          </div>
        ) : (
          <div className="sd-grid sd-grid--3col">
            {liveRaces.map((r) => (
              <article key={getField(r, "id", "Id")} className="sd-card sd-card--live">
                <div className="sd-card-accent" />
                <div className="sd-card-body">
                  <div className="sd-card-badge sd-card-badge--live">Đang diễn ra</div>
                  <h3>{getField(r, "name", "Name")}</h3>
                  <p className="sd-card-location">

{getField(r, "location", "Location") || "Địa điểm chưa xác định"}
                  </p>
                  <div className="sd-card-footer">
                    <span className="sd-card-distance">
                      {getField(r, "distance", "Distance") ? `${getField(r, "distance", "Distance")}m` : "-"}
                    </span>
                    <span className="sd-card-time">{formatRaceTime(getField(r, "scheduledAt", "ScheduledAt"))}</span>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      {/* ── Active Tournaments section ── */}
      <section className="sd-section">
        <div className="sd-section-header">
          <h2>Giải đấu đang hoạt động</h2>
          <span className="sd-section-count">{tournaments.length} giải đấu</span>
        </div>

        {tournaments.length === 0 ? (
          <div className="sd-empty">

<p>Không có giải đấu nào đang hoạt động.</p>
            <span>Các giải đấu sẽ xuất hiện ở đây khi được kích hoạt.</span>
          </div>
        ) : (
          <div className="sd-grid sd-grid--3col">
            {tournaments.map((t) => (
              <article key={getField(t, "id", "Id")} className="sd-card">
                <div className="sd-card-body">
                  <div className="sd-card-badge">Đang hoạt động</div>
                  <h3>{getField(t, "name", "Name")}</h3>
                  <p className="sd-card-desc">
                    {getField(t, "description", "Description") || "Không có mô tả."}
                  </p>
                  <div className="sd-card-meta">
                    <div className="sd-meta-item">
                      <span className="sd-meta-value">{getField(t, "roundCount", "RoundCount") ?? 0}</span>
                      <span className="sd-meta-label">Vòng đấu</span>
                    </div>
                    <div className="sd-meta-item">
                      <span className="sd-meta-value">{getField(t, "raceCount", "RaceCount") ?? 0}</span>
                      <span className="sd-meta-label">Cuộc đua</span>
                    </div>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      {/* ── Upcoming Races section ── */}
      <section className="sd-section">
        <div className="sd-section-header">
          <h2>Cuộc đua sắp tới</h2>
          <span className="sd-section-count">{upcomingRaces.length} cuộc đua</span>
        </div>

        {upcomingRaces.length === 0 ? (
          <div className="sd-empty">

<p>Không có cuộc đua nào được lên lịch.</p>
            <span>Lịch đua sẽ được cập nhật khi có cuộc đua mới.</span>
          </div>
        ) : (
          <div className="sd-table-wrapper">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>Tên cuộc đua</th>
                  <th>Địa điểm</th>
                  <th>Cự ly</th>
                  <th>Trạng thái</th>
                  <th>Thời gian</th>
                </tr>
              </thead>
              <tbody>
                {upcomingRaces.map((r) => (
                  <tr key={getField(r, "id", "Id")}>
                    <td className="sd-table-name">{getField(r, "name", "Name")}</td>
                    <td>{getField(r, "location", "Location") || "-"}</td>
                    <td>
                      {getField(r, "distance", "Distance")
                        ? `${getField(r, "distance", "Distance")}m`
                        : "-"}
                    </td>
                    <td>
                      <span className="sd-table-badge sd-table-badge--scheduled">
                        Đã lên lịch
                      </span>
                    </td>
                    <td className="sd-table-time">
                      {formatRaceTime(getField(r, "scheduledAt", "ScheduledAt"))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

export default SpectatorDashboardPage;
