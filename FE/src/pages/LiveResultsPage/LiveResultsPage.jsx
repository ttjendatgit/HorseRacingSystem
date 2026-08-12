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
          {rankings.length > 0 ? (
            <table className="results-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Ngựa</th>
                  <th>Kỵ sĩ</th>
                  <th>Thời gian</th>
                </tr>
              </thead>
              <tbody>
                {rankings.map((p, i) => (
                  <tr key={`${p.horseName ?? "horse"}-${i}`}>
                    <td>{p.position ?? i + 1}</td>
                    <td>{p.horseName ?? "-"}</td>
                    <td>{p.jockeyName ?? "-"}</td>
                    <td>{p.time ?? "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="winner-card">
              Người chiến thắng: <strong>{ranking.winningHorseName ?? ranking.winningHorse?.name ?? "Chưa xác định"}</strong>
            </p>
          )}
        </div>
      )}
    </div>
  );
}

export default LiveResultsPage;
