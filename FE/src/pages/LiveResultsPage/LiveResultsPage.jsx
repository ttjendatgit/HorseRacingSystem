import { useEffect, useState } from "react";
import { getLiveRanking, getRaces } from "../../services/spectatorApi";
import "./LiveResultsPage.css";

function LiveResultsPage() {
  const [races, setRaces] = useState([]);
  const [selectedId, setSelectedId] = useState("");
  const [ranking, setRanking] = useState(null);

  useEffect(() => {
    getRaces()
      .then((d) => {
        const list = Array.isArray(d) ? d : [];
        setRaces(list);
        const finished = list.filter((r) => (r.status ?? r.Status) === "Finished");
        if (finished.length) setSelectedId(finished[0].id ?? finished[0].Id);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!selectedId) {
      setRanking(null);
      return;
    }
    getLiveRanking(selectedId)
      .then((d) => setRanking(d?.data ?? d))
      .catch(() => setRanking(null));
  }, [selectedId]);

  const rankings = ranking?.rankings ?? ranking?.Rankings ?? ranking?.positions ?? [];
  const resultStatus = (
    ranking?.resultStatus ?? ranking?.ResultStatus ?? ""
  ).toLowerCase();
  const isOfficial = resultStatus === "official";

  return (
    <div className="live-results-page">
      <section className="page-header results-hero">
        <span className="pill">Kết Quả Trực Tiếp</span>
        <h1>Kết Quả Trực Tiếp</h1>
        <p>Xem lại bảng xếp hạng cuộc đua đã hoàn thành, người chiến thắng, kỵ sĩ và thời gian ghi nhận.</p>
      </section>

      <div className="results-toolbar">
        <label htmlFor="race-result-select">Cuộc đua</label>
        <select
          id="race-result-select"
          value={selectedId}
          onChange={(e) => setSelectedId(e.target.value)}
        >
          <option value="">Chọn cuộc đua</option>
          {races.map((r) => (
            <option key={r.id ?? r.Id} value={r.id ?? r.Id}>
              {r.name ?? r.Name} ({r.status ?? r.Status})
            </option>
          ))}
        </select>
      </div>

      {!ranking ? (
        <p className="empty-state">Chọn một cuộc đua đã hoàn thành để xem kết quả.</p>
      ) : (
        <div className="result-panel">
          <h2>{ranking.raceName ?? ranking.race?.name ?? "Kết Quả Cuộc Đua"}</h2>
          {resultStatus && !isOfficial && (
            <p className="empty-state" style={{ color: "#b45309", fontWeight: 600 }}>
              ⏳ Kết quả tạm thời — đang chờ admin duyệt thành chính thức. Thứ hạng bên dưới chưa phải kết quả cuối cùng.
            </p>
          )}
          {rankings.length > 0 ? (
            <table className="results-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Ngựa</th>
                  <th>Kỵ sĩ</th>
                  <th>Xác suất thắng</th>
                  <th>Odds ban đầu</th>
                  <th>Thời gian</th>
                </tr>
              </thead>
              <tbody>
                {rankings.map((p, i) => (
                  <tr key={`${p.horseName ?? "horse"}-${i}`}>
                    <td>{p.position ?? i + 1}</td>
                    <td>{isOfficial && p.won ? "🏆 " : ""}{p.horseName ?? "-"}</td>
                    <td>{p.jockeyName ?? "-"}</td>
                    <td>{(p.probabilityPercent ?? p.ProbabilityPercent ?? 0) > 0 ? `${p.probabilityPercent ?? p.ProbabilityPercent}%` : "-"}</td>
                    <td><strong style={{ color: "#d97706" }}>{(p.odds ?? p.Odds ?? 1.0)}x</strong></td>
                    <td>{p.time ?? "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="winner-card">
              {isOfficial ? "Người chiến thắng: " : "Ngựa dẫn đầu (tạm thời): "}
              <strong>{ranking.winningHorseName ?? ranking.winningHorse?.name ?? "Chưa xác định"}</strong>
            </p>
          )}
        </div>
      )}
    </div>
  );
}

export default LiveResultsPage;
