import { useEffect, useMemo, useState } from "react";
import { unwrapResponseData } from "../../services/authRoleUtils";
import {
  createPrediction,
  getActiveTournaments,
  getRace,
  getRaces,
  getTournaments,
} from "../../services/spectatorApi";
import { getRaceEntries } from "../../services/refereeApi";
import { getBalance } from "../../services/walletApi";
import "./SpectatorPredictionFormPage.css";

const getStatusMessage = (status) => {
  switch (status) {
    case "scheduled":
    case "registrationopen":
    case "registrationclosed": return "Dự đoán đã đóng trong vòng 5 phút trước giờ đua.";
    case "inprogress": return "Cuộc đua đang diễn ra, đã khóa cược.";
    case "finished": return "Cuộc đua đã kết thúc, không thể đặt cược.";
    case "cancelled": return "Cuộc đua đã bị hủy.";
    default: return "Cuộc đua đã khóa — không thể đặt cược.";
  }
};

const formatCountdown = (value) => {
  if (!value) return "--:--";
  const target = new Date(value);
  if (Number.isNaN(target.getTime())) return "--:--";
  const diff = target.getTime() - Date.now();
  if (diff <= 0) return "Đã bắt đầu";

  const totalSeconds = Math.floor(diff / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  if (days > 0) return `${days}d ${hours}h`;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
};

const formatDateTime = (value) => {
  if (!value) return "Chưa xác định";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa xác định";
  return new Intl.DateTimeFormat("vi-VN", {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
};

const BETTING_CLOSE_BEFORE_MS = 5 * 60 * 1000;
const BETTABLE_STATUSES = new Set(["scheduled", "registrationopen", "registrationclosed"]);

const canBetOnRace = (status, scheduledAt) => {
  if (!BETTABLE_STATUSES.has(status)) return false;
  const startTime = new Date(scheduledAt).getTime();
  return Number.isFinite(startTime) && startTime - Date.now() >= BETTING_CLOSE_BEFORE_MS;
};

function SpectatorPredictionFormPage() {
  const [tournaments, setTournaments] = useState([]);
  const [races, setRaces] = useState([]);
  const [selectedTournament, setSelectedTournament] = useState("");
  const [selectedRace, setSelectedRace] = useState("");
  const [selectedHorseId, setSelectedHorseId] = useState(null);
  const [betAmount, setBetAmount] = useState("");
  const [raceDetail, setRaceDetail] = useState(null);
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [submitError, setSubmitError] = useState("");
  const [walletBalance, setWalletBalance] = useState(null);
  const [now, setNow] = useState(Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    getBalance()
      .then((d) => {
        const b = d?.data ?? d;
        setWalletBalance(b?.balance ?? b?.Balance ?? 0);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadData = async () => {
      setIsLoading(true);
      setErrorMessage("");

      try {
        const [tournamentsResponse, racesResponse] = await Promise.all([
          getTournaments().catch(() => getActiveTournaments()),
          getRaces(),
        ]);
        let tournamentPayload = unwrapResponseData(tournamentsResponse);
        const racesPayload = unwrapResponseData(racesResponse);

        let tournamentItems = Array.isArray(tournamentPayload) ? tournamentPayload : [];
        const raceItems = Array.isArray(racesPayload) ? racesPayload : [];

        if (tournamentItems.length === 0) {
          try {
            const fallbackRes = await getActiveTournaments();
            const fallbackData = unwrapResponseData(fallbackRes);
            if (Array.isArray(fallbackData) && fallbackData.length > 0) {
              tournamentItems = fallbackData;
            }
          } catch { /* ignore fallback error */ }
        }

        if (!cancelled) {
          setTournaments(tournamentItems);
          setRaces(raceItems);
          setSelectedTournament("");
        }
      } catch (error) {
        if (!cancelled) {
          setErrorMessage(error.message || "Không thể tải dữ liệu dự đoán.");
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    loadData();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadRaceDetail = async () => {
      if (!selectedRace) {
        setRaceDetail(null);
        return;
      }

      setIsLoading(true);
      setSubmitError("");

      try {
        const [raceResponse, entriesResponse] = await Promise.all([
          getRace(selectedRace),
          getRaceEntries(selectedRace),
        ]);
        const payload = unwrapResponseData(raceResponse);
        const entriesList = Array.isArray(entriesResponse)
          ? entriesResponse
          : entriesResponse?.data ?? [];
        if (!cancelled) {
          setRaceDetail({ ...(payload ?? {}), entries: entriesList });
          setSelectedHorseId(null);
        }
      } catch (error) {
        if (!cancelled) {
          setRaceDetail(null);
          setSubmitError(error.message || "Không thể tải chi tiết cuộc đua.");
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    loadRaceDetail();
    return () => { cancelled = true; };
  }, [selectedRace]);

  const activeTournaments = useMemo(() => {
    // 1. Tạo Map lưu trữ tất cả thông tin giải đấu theo ID (chuẩn hóa chữ thường)
    const tournamentMap = new Map();
    tournaments.forEach((t) => {
      const id = String(t?.id ?? t?.Id ?? "").toLowerCase().trim();
      if (id) tournamentMap.set(id, t);
    });

    // 2. Lấy danh sách TournamentID của những cuộc đua ĐANG hiển thị (Scheduled / InProgress)
    const activeTournamentIds = new Set(
      races
        .filter((race) => {
          const status = (race?.status ?? race?.Status ?? "").toLowerCase().trim();
          return status !== "finished" && status !== "cancelled";
        })
        .map((race) => String(race?.tournamentId ?? race?.TournamentId ?? "").toLowerCase().trim())
        .filter(Boolean)
    );

    // 3. CHỈ hiển thị các giải đấu thực sự có cuộc đua đang diễn ra hoặc sắp diễn ra
    const result = [];
    const addedIds = new Set();

    activeTournamentIds.forEach((tid) => {
      if (addedIds.has(tid)) return;
      if (tournamentMap.has(tid)) {
        result.push(tournamentMap.get(tid));
      } else {
        const race = races.find((r) => String(r?.tournamentId ?? r?.TournamentId ?? "").toLowerCase().trim() === tid);
        const fallbackName = race?.tournamentName ?? race?.TournamentName ?? "Giải đấu";
        result.push({ id: tid, name: fallbackName });
      }
      addedIds.add(tid);
    });

    return result;
  }, [tournaments, races]);

  const raceOptions = useMemo(() => {
    const selTid = String(selectedTournament ?? "").toLowerCase().trim();
    return races
      .filter((race) => {
        const tid = String(race?.tournamentId ?? race?.TournamentId ?? "").toLowerCase().trim();
        if (selTid && tid !== selTid) return false;
        // Ẩn cuộc đua đã kết thúc (finished) hoặc đã hủy (cancelled). Giữ lại inprogress và scheduled.
        const status = (race?.status ?? race?.Status ?? "").toLowerCase().trim();
        if (status === "finished" || status === "cancelled") return false;
        return true;
      })
      .map((race) => {
        const id = race?.id ?? race?.Id;
        const name = race?.name ?? race?.Name ?? "Cuộc đua";
        const scheduledAt = race?.scheduledAt ?? race?.ScheduledAt;
        const status = (race?.status ?? race?.Status ?? "").toLowerCase().trim();
        return {
          id,
          name,
          time: formatDateTime(scheduledAt),
          countdown: formatCountdown(scheduledAt),
          status,
          canBet: canBetOnRace(status, scheduledAt),
        };
      });
  }, [races, selectedTournament, now]);

  useEffect(() => {
    if (raceOptions.length === 0) {
      setSelectedRace("");
      return;
    }

    if (!raceOptions.some((race) => race.id === selectedRace)) {
      const nextRace = raceOptions.find((race) => race.canBet) ?? raceOptions[0];
      setSelectedRace(nextRace.id);
    }
  }, [raceOptions, selectedRace]);

  const selectedRaceDetails = raceOptions.find((r) => r.id === selectedRace);

  const horseOptions = useMemo(() => {
    const entries = raceDetail?.entries ?? [];
    const mapped = entries.map((entry) => ({
      id: entry.horseId ?? entry.HorseId,
      name: entry.horseName ?? entry.HorseName ?? "Không xác định",
      jockey: entry.jockeyName ?? entry.JockeyName ?? "Chưa xác định",
      winRate: entry.horseWinRate ?? entry.HorseWinRate ?? 0,
      jockeyWinRate: entry.jockeyWinRate ?? entry.JockeyWinRate ?? 0,
      probabilityPercent: entry.probabilityPercent ?? entry.ProbabilityPercent ?? 0,
      odds: entry.odds ?? entry.Odds ?? 1.0,
    }));

    const highestProb = mapped.length > 0 ? Math.max(...mapped.map((h) => h.probabilityPercent)) : 0;
    const highestOdds = mapped.length > 0 ? Math.max(...mapped.map((h) => h.odds)) : 0;

    return mapped.map((h) => ({
      ...h,
      isFavorite: highestProb > 0 && h.probabilityPercent === highestProb,
      isUnderdog: highestOdds > 1.0 && h.odds === highestOdds && (!highestProb || h.probabilityPercent !== highestProb),
    }));
  }, [raceDetail]);

  const selectedHorse = horseOptions.find((h) => h.id === selectedHorseId);

  const handleSubmit = (event) => {
    event.preventDefault();
    setSubmitError("");
    const bet = Number(betAmount);
    if (!Number.isFinite(bet) || bet <= 0) {
      setSubmitError("Số tiền cược phải lớn hơn 0.");
      return;
    }
    if (walletBalance !== null && bet > walletBalance) {
      setSubmitError("Số dư không đủ để đặt cược.");
      return;
    }
    if (selectedHorseId) setShowConfirmation(true);
  };

  const handleConfirm = async () => {
    if (!selectedRace || !selectedHorseId) return;

    const bet = parseFloat(betAmount) || 0;
    if (walletBalance !== null && bet > walletBalance) {
      setSubmitError("Số dư không đủ để đặt cược.");
      return;
    }

    setIsSubmitting(true);
    setSubmitError("");

    try {
      await createPrediction({
        raceId: selectedRace,
        predictedHorseId: selectedHorseId,
        betAmount: bet,
      });
      // Refresh wallet balance after bet
      try {
        const bal = await getBalance();
        const b = bal?.data ?? bal;
        setWalletBalance(b?.balance ?? b?.Balance ?? 0);
      } catch { /* ignore */ }
      setShowConfirmation(false);
      setBetAmount("");
      setSelectedHorseId(null);
    } catch (error) {
      setSubmitError(error.message || "Không thể gửi dự đoán.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const tournamentName = useMemo(() => {
    const race = races.find((r) => (r.id ?? r.Id) === selectedRace);
    const tid = race?.tournamentId ?? race?.TournamentId ?? selectedTournament;
    const t = tournaments.find((item) => (item.id ?? item.Id) === tid);
    return [t?.name ?? t?.Name].filter(Boolean)[0];
  }, [races, selectedRace, selectedTournament, tournaments]);

  return (
    <div className="pf-page">
      {/* ---- Hero ---- */}
      <section className="pf-hero">
        <div className="pf-hero__text">
          <span className="pf-eyebrow">Dự đoán cuộc đua</span>
          <h1>Phiếu dự đoán</h1>
          <p>Chọn cuộc đua sắp tới, chọn người thắng và xem lại dự đoán trước khi gửi.</p>
        </div>
        {selectedRaceDetails && (
          <div className="pf-hero__countdown">
            <span className="pf-hero__countdown-label">Đếm ngược cuộc đua</span>
            <strong className="pf-hero__countdown-value">
              {selectedRaceDetails.countdown}
            </strong>
            <span className="pf-hero__countdown-meta">
              {selectedRaceDetails.name} &middot; {selectedRaceDetails.time}
            </span>
          </div>
        )}
      </section>

      {errorMessage && (
        <div className="pf-error-banner">{errorMessage}</div>
      )}

      {/* ---- Selects ---- */}
      <div className="pf-selects">
        <div className="pf-field">
          <label htmlFor="pf-tournament" className="pf-label">Giải đấu</label>
          <select
            id="pf-tournament"
            className="pf-select"
            value={selectedTournament}
            onChange={(e) => setSelectedTournament(e.target.value)}
          >
            <option value="">Tất cả giải đấu</option>
            {activeTournaments.map((t) => (
              <option key={t.id ?? t.Id} value={t.id ?? t.Id}>
                {t.name ?? t.Name}
              </option>
            ))}
          </select>
        </div>
        <div className="pf-field">
          <label htmlFor="pf-race" className="pf-label">Cuộc đua</label>
          <select
            id="pf-race"
            className="pf-select"
            value={selectedRace}
            onChange={(e) => {
              setSelectedRace(e.target.value);
              // Đồng bộ giải đấu theo race được chọn để hiển thị đúng thông tin
              const race = races.find((r) => (r.id ?? r.Id) === e.target.value);
              const tid = race?.tournamentId ?? race?.TournamentId;
              if (tid) setSelectedTournament(tid);
            }}
          >
            {raceOptions.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name} {!r.canBet ? " (Không thể cược)" : ""} — {r.time}
              </option>
            ))}
          </select>
          {selectedRaceDetails && !selectedRaceDetails.canBet && (
            <div className="pf-status-warning">
              <span className="pf-status-warning__icon">🔒</span>
              <p>{getStatusMessage(selectedRaceDetails.status)}</p>
            </div>
          )}
        </div>
      </div>

      {/* ---- Horse grid ---- */}
      <section className="pf-horses-section">
        <div className="pf-section-header">
          <h2>Chọn ngựa</h2>
          <p>{selectedRaceDetails?.canBet ? "Nhấn vào thẻ ngựa để chốt dự đoán của bạn." : "Cuộc đua đã khóa — không thể đặt cược."}</p>
        </div>

        {selectedRaceDetails && !selectedRaceDetails.canBet ? (
          <div className="pf-empty" style={{ border: "1px solid rgba(239,68,68,0.2)", background: "rgba(239,68,68,0.04)", borderRadius: 14 }}>
            <h4 style={{ color: "#c41e1e" }}>Đã khóa cược</h4>
            <p>Cuộc đua đang diễn ra hoặc đã kết thúc. Chỉ có thể cược vào cuộc đua sắp diễn ra.</p>
          </div>
        ) : isLoading ? (
          <div className="pf-empty">
            <h4>Đang tải danh sách ngựa</h4>
            <p>Vui lòng đợi trong giây lát.</p>
          </div>
        ) : horseOptions.length === 0 ? (
          <div className="pf-empty">
            <h4>Không có ngựa</h4>
            <p>Chọn cuộc đua khác để xem danh sách ngựa tham gia.</p>
          </div>
        ) : (
          <div className="pf-horse-grid">
            {horseOptions.map((horse) => {
              const active = selectedHorseId === horse.id;
              return (
                <button
                  key={horse.id}
                  type="button"
                  className={`pf-horse-card${active ? " pf-horse-card--active" : ""}`}
                  onClick={() => setSelectedHorseId(horse.id)}
                >
                  <span className="pf-horse-card__radio" aria-hidden="true" />
                  <div className="pf-horse-card__body">
                    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                      <h3 style={{ margin: 0 }}>{horse.name}</h3>
                      {horse.isFavorite && (
                        <span style={{ fontSize: 10, fontWeight: 700, padding: "2px 8px", borderRadius: 999, background: "#f59e0b", color: "#fff" }}>
                          🌟 Ứng viên số 1
                        </span>
                      )}
                      {horse.isUnderdog && (
                        <span style={{ fontSize: 10, fontWeight: 700, padding: "2px 8px", borderRadius: 999, background: "#8b5cf6", color: "#fff" }}>
                          💥 Cửa ăn lớn
                        </span>
                      )}
                    </div>
                    <p className="pf-horse-card__jockey">
                      Kỵ sĩ: {horse.jockey}
                    </p>
                  </div>
                  <div className="pf-horse-card__stats">
                    <div className="pf-horse-stat">
                      <span>Tỷ lệ thắng Ngựa</span>
                      <strong>{horse.winRate}%</strong>
                    </div>
                    {horse.probabilityPercent > 0 && (
                      <div className="pf-horse-stat">
                        <span>Xác suất thắng</span>
                        <strong style={{ color: "#d97706" }}>{horse.probabilityPercent}%</strong>
                      </div>
                    )}
                    <div className="pf-horse-stat">
                      <span>Tỷ lệ cược</span>
                      <strong>{horse.odds}x</strong>
                    </div>
                  </div>
                  <div className="pf-horse-card__form">
                    <span>Phong độ gần đây</span>
                    <p>{horse.form}</p>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </section>

      {/* ---- Bet + Submit ---- */}
      <form className="pf-action-bar" onSubmit={handleSubmit}>
        <div className="pf-field pf-field--amount">
          <label htmlFor="pf-bet" className="pf-label">
            Số tiền cược
            {walletBalance !== null && (
              <span style={{ fontSize: 12, fontWeight: 400, color: "#657086", marginLeft: 8 }}>
                (Số dư: <strong style={{ color: walletBalance >= (parseFloat(betAmount) || 0) ? "#1a7d1a" : "#c41e1e" }}>{Number(walletBalance).toLocaleString()} điểm</strong>)
              </span>
            )}
          </label>
          <div className="pf-amount-input-wrap">
            <span className="pf-amount-currency">đ</span>
            <input
              id="pf-bet"
              className="pf-input"
              type="number"
              min="1"
              step="1"
              required
              placeholder="50"
              value={betAmount}
              onChange={(e) => setBetAmount(e.target.value)}
              disabled={!selectedRaceDetails?.canBet}
            />
          </div>
          <div className="pf-quick-bets" style={{ display: "flex", gap: 6, marginTop: 8, flexWrap: "wrap" }}>
            {[10, 50, 100, 500].map((amt) => (
              <button
                key={amt}
                type="button"
                style={{ padding: "4px 10px", fontSize: 12, borderRadius: 6, border: "1px solid var(--hr-border, #ccc)", background: "rgba(255,255,255,0.08)", color: "var(--hr-text)", cursor: "pointer", fontWeight: 600 }}
                onClick={() => setBetAmount((prev) => String((parseFloat(prev) || 0) + amt))}
                disabled={!selectedRaceDetails?.canBet}
              >
                +{amt}
              </button>
            ))}
            {walletBalance > 0 && (
              <button
                type="button"
                style={{ padding: "4px 10px", fontSize: 12, borderRadius: 6, border: "1px solid #d97706", background: "rgba(217,119,6,0.15)", color: "#d97706", cursor: "pointer", fontWeight: 700 }}
                onClick={() => setBetAmount(String(walletBalance))}
                disabled={!selectedRaceDetails?.canBet}
              >
                Tất cả ({walletBalance})
              </button>
            )}
          </div>
          {selectedHorse && (parseFloat(betAmount) || 0) > 0 && (
            <div style={{ marginTop: 8, padding: "8px 12px", borderRadius: 8, background: "rgba(16,185,129,0.1)", border: "1px solid rgba(16,185,129,0.3)", color: "#10b981", fontSize: 13, fontWeight: 600 }}>
              💰 Thưởng dự kiến nếu thắng: <strong>{((parseFloat(betAmount) || 0) * selectedHorse.odds).toLocaleString(undefined, { maximumFractionDigits: 2 })} điểm</strong> (Odds {selectedHorse.odds}x)
            </div>
          )}
        </div>
        <button
          type="submit"
          className="pf-btn-primary"
          disabled={!selectedHorseId || isSubmitting || !selectedRaceDetails?.canBet}
        >
          {selectedRaceDetails && !selectedRaceDetails.canBet ? "Đã khóa cược" : isSubmitting ? "Đang gửi..." : "Gửi dự đoán"}
        </button>
      </form>

      {submitError && <div className="pf-error-banner">{submitError}</div>}

      {/* ---- Race info card ---- */}
      <div className="pf-info-card">
        <div className="pf-info-card__header">
          <span>Thông tin cuộc đua</span>
        </div>
        <div className="pf-info-card__grid">
          <div className="pf-info-item">
            <span>Đường đua</span>
            <strong>{raceDetail?.location ?? raceDetail?.Location ?? "--"}</strong>
          </div>
          <div className="pf-info-item">
            <span>Ngựa đã chọn</span>
            <strong className={selectedHorse ? "pf-info-item--active" : ""}>
              {selectedHorse?.name || "Chưa chọn"}
            </strong>
          </div>
          <div className="pf-info-item">
            <span>Tỷ lệ cược</span>
            <strong>{selectedHorse?.odds || "--"}</strong>
          </div>
          <div className="pf-info-item pf-info-item--rules">
            <span>Quy tắc</span>
            <p>Dự đoán bị khóa 5 phút trước khi cuộc đua bắt đầu. Phần thưởng được tính từ tỷ lệ trực tiếp.</p>
          </div>
        </div>
      </div>

      {/* ---- Confirmation Modal ---- */}
      {showConfirmation && (
        <div className="pf-modal-overlay" role="dialog" aria-modal="true" aria-labelledby="pf-modal-title">
          <div className="pf-modal">
            <div className="pf-modal__header">
              <div>
                <span className="pf-modal__badge">Dự đoán đã sẵn sàng</span>
                <h3 id="pf-modal-title">Xác nhận dự đoán</h3>
                <p>Xem lại lựa chọn trước khi gửi.</p>
              </div>
              <button
                type="button"
                className="pf-modal__close"
                onClick={() => setShowConfirmation(false)}
                aria-label="Đóng"
              >
              </button>
            </div>
            <div className="pf-modal__body">
              <div className="pf-modal__row">
                <span>Giải đấu</span>
                <strong>{tournamentName}</strong>
              </div>
              <div className="pf-modal__row">
                <span>Cuộc đua</span>
                <strong>{selectedRaceDetails?.name}</strong>
              </div>
              <div className="pf-modal__row">
                <span>Ngựa</span>
                <strong>{selectedHorse?.name}</strong>
              </div>
              <div className="pf-modal__row">
                <span>Tỷ lệ cược</span>
                <strong>{selectedHorse?.odds}</strong>
              </div>
              <div className="pf-modal__row">
                <span>Số tiền cược</span>
                <strong className="pf-modal__amount">{parseFloat(betAmount) || 0}đ</strong>
              </div>
              {submitError && <div className="pf-modal__error">{submitError}</div>}
            </div>
            <div className="pf-modal__actions">
              <button
                type="button"
                className="pf-btn-primary pf-btn-primary--full"
                onClick={handleConfirm}
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang xác nhận..." : "Xác nhận dự đoán"}
              </button>
              <button
                type="button"
                className="pf-btn-ghost"
                onClick={() => setShowConfirmation(false)}
              >
                Chỉnh sửa lựa chọn
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default SpectatorPredictionFormPage;
