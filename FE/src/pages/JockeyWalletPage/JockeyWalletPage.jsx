import { useCallback, useEffect, useState } from "react";
import { getBalance } from "../../services/walletApi";
import { getJockeyPrizeHistory } from "../../services/jockeyApi";
import { formatVndCurrency } from "../../utils/prizeAllocation";
import "../OwnerWalletPage/OwnerWalletPage.css";

const formatDate = (v) =>
  v ? new Date(v).toLocaleDateString("vi-VN", { dateStyle: "medium" }) : "—";

function WalletIcon() {
  return (
    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
      <rect x="2" y="6" width="20" height="14" rx="2.5" />
      <path d="M2 10.5h20" />
      <circle cx="17" cy="15" r="1.4" fill="currentColor" stroke="none" />
    </svg>
  );
}

function TrophyIcon() {
  return (
    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
      <path d="M8 21h8M12 17v4M7 4h10v4a5 5 0 0 1-10 0V4Z" />
      <path d="M17 5h2.5a2.5 2.5 0 0 1 0 5H17M7 5H4.5a2.5 2.5 0 0 0 0 5H7" />
    </svg>
  );
}

function JockeyWalletPage() {
  const [balance, setBalance] = useState(null);
  const [balanceLoading, setBalanceLoading] = useState(true);
  const [balanceError, setBalanceError] = useState("");

  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [historyError, setHistoryError] = useState("");

  const loadBalance = useCallback(() => {
    setBalanceLoading(true);
    setBalanceError("");
    getBalance()
      .then((d) => {
        const b = d?.data ?? d;
        setBalance(Number(b?.balance ?? b?.Balance ?? 0));
      })
      .catch((e) => setBalanceError(e.message || "Không thể tải số dư ví."))
      .finally(() => setBalanceLoading(false));
  }, []);

  const loadHistory = useCallback(() => {
    setHistoryLoading(true);
    setHistoryError("");
    getJockeyPrizeHistory()
      .then((list) => setHistory(Array.isArray(list) ? list : []))
      .catch((e) => setHistoryError(e.message || "Không thể tải lịch sử nhận thưởng."))
      .finally(() => setHistoryLoading(false));
  }, []);

  useEffect(() => {
    loadBalance();
    loadHistory();
  }, [loadBalance, loadHistory]);

  return (
    <div className="ow-page">
      <div className="ow-topbar">
        <div>
          <h1>Ví của tôi</h1>
          <p className="ow-topbar-sub">Số dư và phần thưởng được chia theo tỉ lệ trên lời mời kỵ sĩ.</p>
        </div>
      </div>

      <section className="ow-hero" aria-label="Số dư ví hiện tại">
        <div className="ow-hero__content">
          <span className="ow-label">Số dư ví</span>
          {balanceLoading ? (
            <strong className="ow-hero__value">Đang tải…</strong>
          ) : balanceError ? (
            <strong className="ow-hero__value ow-hero__value--error">Không thể tải số dư</strong>
          ) : (
            <strong className="ow-hero__value">{formatVndCurrency(balance)}</strong>
          )}
          <p className="ow-hero__sub">Tiền được cộng khi Admin trao thưởng và lời mời có tỉ lệ chia &gt; 0%.</p>
        </div>
        <div className="ow-hero__icon">
          <WalletIcon />
        </div>
      </section>

      {balanceError && (
        <div className="ow-error" role="alert">
          <span>{balanceError}</span>
          <button type="button" className="ow-btn ow-btn--outline" onClick={loadBalance}>
            Thử lại
          </button>
        </div>
      )}

      <section className="ow-history" aria-label="Lịch sử nhận thưởng">
        <div className="ow-history__head">
          <h2>Lịch sử nhận thưởng</h2>
        </div>

        {historyError && (
          <div className="ow-error" role="alert">
            <span>{historyError}</span>
            <button type="button" className="ow-btn ow-btn--outline" onClick={loadHistory}>
              Thử lại
            </button>
          </div>
        )}

        {historyLoading ? (
          <div className="ow-empty">
            <p>Đang tải lịch sử nhận thưởng…</p>
          </div>
        ) : history.length === 0 && !historyError ? (
          <div className="ow-empty">
            <div className="ow-empty__icon">
              <TrophyIcon />
            </div>
            <h3>Chưa có giải thưởng nào</h3>
            <p>Phần thưởng chỉ xuất hiện sau khi Admin trao giải và lời mời của bạn có tỉ lệ chia.</p>
          </div>
        ) : (
          <ul className="ow-list">
            {history.map((h, i) => {
              const tournamentId = h.tournamentId ?? h.TournamentId ?? i;
              const tournamentName = h.tournamentName ?? h.TournamentName ?? "Giải đấu";
              const position = h.position ?? h.Position;
              const horseName = h.horseName ?? h.HorseName ?? "—";
              const amount = h.jockeyAmount ?? h.JockeyAmount ?? 0;
              const share = h.jockeySharePercentage ?? h.JockeySharePercentage;
              const distributedAt = h.distributedAt ?? h.DistributedAt;

              return (
                <li key={`${tournamentId}-${position}-${i}`} className="ow-card hover-lift">
                  <div className="ow-card__badge" aria-label={`Hạng ${position ?? "?"}`}>
                    Hạng {position ?? "?"}
                  </div>
                  <div className="ow-card__body">
                    <h3>{tournamentName}</h3>
                    <p className="ow-card__horse">
                      Ngựa thi đấu: <strong>{horseName}</strong>
                      {share != null ? ` · Chia ${share}%` : ""}
                    </p>
                    <time className="ow-card__date" dateTime={distributedAt || undefined}>
                      {formatDate(distributedAt)}
                    </time>
                  </div>
                  <div className="ow-card__amount">
                    <span>Đã nhận</span>
                    <strong>{formatVndCurrency(amount)}</strong>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </div>
  );
}

export default JockeyWalletPage;
