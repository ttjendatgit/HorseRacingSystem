import { useEffect, useMemo, useState } from "react";
import { getMyPredictions } from "../../services/spectatorApi";
import "./SpectatorRewardNotificationsPage.css";

const formatDate = (v) =>
  v ? new Date(v).toLocaleDateString("vi-VN", { dateStyle: "medium" }) : "Chưa xác định";

const formatCurrency = (v) => {
  const n = Number(v ?? 0);
  return n.toLocaleString("vi-VN");
};

function SpectatorRewardNotificationsPage() {
  const [predictions, setPredictions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getMyPredictions()
      .then((d) =>
        setPredictions(Array.isArray(d?.data) ? d.data : Array.isArray(d) ? d : [])
      )
      .catch((e) => setError(e.message || "Không thể tải thông báo phần thưởng."))
      .finally(() => setLoading(false));
  }, []);

  const wonPredictions = useMemo(
    () => predictions.filter((p) => (p.status ?? p.Status) === "Won"),
    [predictions]
  );

  const totalPayout = useMemo(
    () =>
      wonPredictions.reduce(
        (sum, p) => sum + Number(p.payoutAmount ?? p.PayoutAmount ?? 0),
        0
      ),
    [wonPredictions]
  );

  return (
    <div className="prw-page">
      {/* ---- Hero banner ---- */}
      <section className="prw-hero">
        <div className="prw-hero__text">
          <span className="prw-eyebrow">Thông báo phần thưởng</span>
          <h1>Phần thưởng của bạn</h1>
          <p>Tiền thắng dự đoán, phần thưởng đã thanh toán và lịch sử nhận thưởng.</p>
        </div>
        <div className="prw-hero__payout">
          <span className="prw-hero__payout-label">Tổng tiền thắng</span>
          <strong className="prw-hero__payout-value">
            ${formatCurrency(totalPayout)}
          </strong>
          <div className="prw-hero__payout-meta">
            <div className="prw-hero__meta-item">
              <span>Dự đoán thắng</span>
              <strong>{wonPredictions.length}</strong>
            </div>
            <div className="prw-hero__meta-item">
              <span>Tổng dự đoán</span>
              <strong>{predictions.length}</strong>
            </div>
          </div>
        </div>
      </section>

      {error && <div className="prw-error">{error}</div>}

      {/* ---- Content ---- */}
      {loading ? (
        <div className="prw-empty">
          <h4>Đang tải thông báo phần thưởng</h4>
          <p>Vui lòng đợi trong giây lát.</p>
        </div>
      ) : wonPredictions.length === 0 ? (
        <div className="prw-empty">
          <h4>Chưa có phần thưởng</h4>
          <p>Tiếp tục dự đoán và giành chiến thắng để nhận thưởng.</p>
        </div>
      ) : (
        <div className="prw-grid">
          {wonPredictions.map((p) => {
            const id = p.id ?? p.Id;
            const payout = Number(p.payoutAmount ?? p.PayoutAmount ?? 0);
            const settledDate =
              p.settledAt ?? p.SettledAt ?? p.createdAt ?? p.CreatedAt;

            return (
              <article key={id} className="prw-card">
                {/* Gold badge */}

                {/* Card body */}
                <div className="prw-card__body">
                  <div className="prw-card__header">
                    <span className={`prw-card__status`}>Đã thắng</span>
                    <span className="prw-card__date">
                      {formatDate(settledDate)}
                    </span>
                  </div>
                  <h3>{p.raceName ?? p.RaceName ?? "Cuộc đua"}</h3>
                  <p className="prw-card__horse">
                    Ngựa dự đoán: <strong>{p.predictedHorseName ?? p.PredictedHorseName ?? "—"}</strong>
                  </p>
                  <p className="prw-card__desc">
                    Phần thưởng dự đoán thắng đã được thanh toán.
                  </p>
                </div>

                {/* Payout amount */}
                <div className="prw-card__payout">
                  <span className="prw-card__payout-label">Thanh toán</span>
                  <strong className="prw-card__payout-value">
                    ${formatCurrency(payout)}
                  </strong>
                  <span className="prw-card__payout-sub">Đã nhận</span>
                </div>
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default SpectatorRewardNotificationsPage;
