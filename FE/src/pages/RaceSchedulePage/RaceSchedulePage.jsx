import { useEffect, useState } from "react";
import { getRaces } from "../../services/spectatorApi";
import "./RaceSchedulePage.css";

const fDate = (v) =>
  v
    ? new Date(v).toLocaleString("vi-VN", {
        dateStyle: "medium",
        timeStyle: "short",
      })
    : "Chưa xác định";

const statusLabel = (s) => {
  const m = { scheduled: "Đã lên lịch", registrationopen: "Chuẩn bị", registrationclosed: "Chuẩn bị", inprogress: "Đang diễn ra", finished: "Đã kết thúc", cancelled: "Đã hủy" };
  return m[(s ?? "").toLowerCase()] ?? s ?? "Đã lên lịch";
};

function RaceSchedulePage() {
  const [races, setRaces] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getRaces()
      .then((d) => setRaces(Array.isArray(d) ? d : []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="race-schedule-page">
      <section className="page-header schedule-hero">
        <span className="pill">Lịch Đua</span>
        <h1>Lịch Đua</h1>
        <p>Các cuộc đua sắp tới và đã qua với trạng thái, địa điểm, cự ly và thời gian bắt đầu.</p>
      </section>

      {loading ? (
        <p className="empty-state">Đang tải danh sách đua...</p>
      ) : races.length === 0 ? (
        <p className="empty-state">Không có cuộc đua nào được lên lịch.</p>
      ) : (
        <div className="race-grid">
          {races.map((r) => (
            <div key={r.id ?? r.Id} className="race-card">
              <div>
                <span className="badge">{statusLabel(r.status ?? r.Status)}</span>
                <h3>{r.name ?? r.Name}</h3>
              </div>
              <div className="race-meta">
                <span>{r.location ?? r.Location ?? "Chưa xác định"}</span>
                <span>{r.distance ?? r.Distance ?? "-"}m</span>
                <strong>{fDate(r.scheduledAt ?? r.ScheduledAt)}</strong>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default RaceSchedulePage;
