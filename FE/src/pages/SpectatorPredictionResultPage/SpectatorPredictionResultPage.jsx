import { useEffect, useMemo, useState } from "react";
import { getMyPredictions } from "../../services/spectatorApi";
import "./SpectatorPredictionResultPage.css";

const formatDate = (v) =>
  v ? new Date(v).toLocaleDateString("vi-VN", { dateStyle: "medium" }) : "Chưa xác định";

function SpectatorPredictionResultPage() {
  const [predictions, setPredictions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getMyPredictions()
      .then((d) =>
        setPredictions(Array.isArray(d?.data) ? d.data : Array.isArray(d) ? d : [])
      )
      .catch((e) => setError(e.message || "Không thể tải kết quả dự đoán."))
      .finally(() => setLoading(false));
  }, []);

  const stats = useMemo(() => {
    const total = predictions.length;
    const won = predictions.filter((p) => (p.status ?? p.Status) === "Won").length;
    const lost = predictions.filter((p) => (p.status ?? p.Status) === "Lost").length;
    const pending = predictions.filter((p) => (p.status ?? p.Status) === "Pending").length;
    const winRate = total > 0 ? Math.round((won / total) * 100) : 0;
    const totalPayout = predictions.reduce(
      (sum, p) => sum + Number(p.payoutAmount ?? p.PayoutAmount ?? 0),
      0
    );
    return { total, won, lost, pending, winRate, totalPayout };
  }, [predictions]);

  return (
    <div className="pr-page">
      {/* ---- Hero ---- */}
      <section className="pr-hero">
        <span className="pr-eyebrow">Kết quả dự đoán</span>
        <h1>Kết quả dự đoán</h1>
        <p>Theo dõi dự đoán cuộc đua, trạng thái cược, lịch sử thanh toán và ngày kết quả.</p>
      </section>

      {/* ---- Stats bar ---- */}
      <div className="pr-stats">
        <div className="pr-stat-card">
          <span className="pr-stat-card__label">Tổng dự đoán</span>
          <strong className="pr-stat-card__value">{stats.total}</strong>
        </div>
        <div className="pr-stat-card pr-stat-card--won">
          <span className="pr-stat-card__label">Đã thắng</span>
          <strong className="pr-stat-card__value">{stats.won}</strong>
        </div>
        <div className="pr-stat-card pr-stat-card--lost">
          <span className="pr-stat-card__label">Đã thua</span>
          <strong className="pr-stat-card__value">{stats.lost}</strong>
        </div>
        <div className="pr-stat-card pr-stat-card--pending">
          <span className="pr-stat-card__label">Đang chờ</span>
          <strong className="pr-stat-card__value">{stats.pending}</strong>
        </div>
        <div className="pr-stat-card">
          <span className="pr-stat-card__label">Tỷ lệ thắng</span>
          <strong className="pr-stat-card__value">{stats.winRate}%</strong>
        </div>
        <div className="pr-stat-card pr-stat-card--payout">
          <span className="pr-stat-card__label">Tổng thanh toán</span>
          <strong className="pr-stat-card__value">{stats.totalPayout.toLocaleString()}đ</strong>
        </div>
      </div>

      {error && <div className="pr-error">{error}</div>}

      {/* ---- Content ---- */}
      {loading ? (
        <div className="pr-empty">
          <h4>Đang tải kết quả dự đoán</h4>
          <p>Vui lòng đợi trong giây lát.</p>
        </div>
      ) : predictions.length === 0 ? (
        <div className="pr-empty">
          <h4>Chưa có dự đoán</h4>
          <p>Đặt dự đoán đầu tiên của bạn để xem kết quả tại đây.</p>
        </div>
      ) : (
        <div className="pr-grid">
          {predictions.map((p) => {
            const id = p.id ?? p.Id;
            const status = (p.status ?? p.Status ?? "Pending").toString();
            const statusLower = status.toLowerCase();
            const isWon = statusLower === "won";
            const isLost = statusLower === "lost";
            const isPending = statusLower === "pending";

            const statusLabel =
              statusLower === "won" ? "Thắng" : statusLower === "lost" ? "Thua" : "Đang chờ";

            const statusIcon =
              isWon ? (
                null
              ) : isLost ? (
                null
              ) : (
                null
              );

            return (
              <article key={id} className={`pr-card pr-card--${statusLower}`}>
                {/* Status badge */}
                <div className={`pr-card__status pr-card__status--${statusLower}`}>
                  {statusIcon}
                  <span>{statusLabel}</span>
                </div>

                {/* Main info */}
                <div className="pr-card__body">
                  <h3>{p.raceName ?? p.RaceName ?? "Cuộc đua"}</h3>
                  <div className="pr-card__details">
                    <span className="pr-card__detail">
                      Ngựa: <strong>{p.predictedHorseName ?? p.PredictedHorseName ?? "—"}</strong>
                    </span>
                    <span className="pr-card__detail">
                      {formatDate(p.createdAt ?? p.CreatedAt)}
                    </span>
                  </div>
                </div>

                {/* Financials */}
                <div className="pr-card__financials">
                  <div className="pr-card__fin-item">
                    <span>Tiền cược</span>
                    <strong>{(p.betAmount ?? p.BetAmount ?? 0).toLocaleString()}đ</strong>
                  </div>
                  <div className={`pr-card__fin-item ${isWon ? "pr-card__fin-item--won" : ""}`}>
                    <span>Thanh toán</span>
                    <strong>
                      {isPending
                        ? "Đang chờ"
                        : `${(p.payoutAmount ?? p.PayoutAmount ?? 0).toLocaleString()}đ`}
                    </strong>
                  </div>
                  <div className="pr-card__fin-item">
                    <span>Tỷ lệ cược</span>
                    <strong>{p.odds ?? p.Odds ?? "—"}</strong>
                  </div>
                </div>

                {/* Result accent line for won/lost */}
                <div
                  className={`pr-card__accent ${isWon ? "pr-card__accent--won" : ""} ${isLost ? "pr-card__accent--lost" : ""} ${isPending ? "pr-card__accent--pending" : ""}`}
                  aria-hidden="true"
                />
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default SpectatorPredictionResultPage;
