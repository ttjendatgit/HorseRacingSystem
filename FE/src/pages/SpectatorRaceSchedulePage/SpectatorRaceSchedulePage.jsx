import { useEffect, useMemo, useState, useRef, useCallback } from "react";
import { unwrapResponseData } from "../../services/authRoleUtils";
import { getRace, getRaces } from "../../services/spectatorApi";
import "./SpectatorRaceSchedulePage.css";

/* ── Helpers ── */
const formatDate = (value) => {
  if (!value) return "Chưa xác định";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "Chưa xác định";
  return new Intl.DateTimeFormat("vi-VN", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(d);
};

const formatShortDate = (value) => {
  if (!value) return "Chưa xác định";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "Chưa xác định";
  return new Intl.DateTimeFormat("vi-VN", {
    month: "short",
    day: "numeric",
    year: "numeric",
  }).format(d);
};

const formatTime = (value) => {
  if (!value) return "";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "";
  return new Intl.DateTimeFormat("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(d);
};

const formatCountdown = (target) => {
  if (!target) return "";
  const t = new Date(target);
  if (Number.isNaN(t.getTime())) return "";
  const diff = t.getTime() - Date.now();
  if (diff <= 0) return "Đã bắt đầu";
  const totalSec = Math.floor(diff / 1000);
  const d = Math.floor(totalSec / 86400);
  const h = Math.floor((totalSec % 86400) / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  if (d > 0) return `${d} ngày ${h} giờ`;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
};

// RaceStatus — event lifecycle only (see BE Enums.cs). RegistrationOpen/
// RegistrationClosed are transitional compatibility values not shown here
// since this page doesn't currently distinguish the registration window.
const STATUS_META = {
  Scheduled: { label: "Đã lên lịch", className: "srs-badge--scheduled" },
  RegistrationOpen: { label: "Mở đăng ký", className: "srs-badge--scheduled" },
  RegistrationClosed: { label: "Đóng đăng ký", className: "srs-badge--scheduled" },
  InProgress: { label: "Đang diễn ra", className: "srs-badge--live" },
  Finished: { label: "Đã kết thúc", className: "srs-badge--completed" },
  Canceled: { label: "Đã hủy", className: "srs-badge--canceled" },
  Cancelled: { label: "Đã hủy", className: "srs-badge--canceled" },
};

const getStatusMeta = (status) => {
  const s = String(status || "");
  for (const [key, meta] of Object.entries(STATUS_META)) {
    if (s.toLowerCase() === key.toLowerCase()) return meta;
  }
  return STATUS_META.Scheduled;
};

/* ── Map race ── */
const mapRace = (r) => {
  const scheduledAt = r?.scheduledAt ?? r?.ScheduledAt;
  const status = r?.status ?? r?.Status ?? "Scheduled";
  return {
    id: r?.id ?? r?.Id,
    name: r?.name ?? r?.Name ?? "Cuộc đua",
    location: r?.location ?? r?.Location ?? "Địa điểm chưa xác định",
    distance: r?.distance ?? r?.Distance ?? 0,
    scheduledAt,
    status,
    entriesCount: r?.entriesCount ?? r?.EntriesCount ?? 0,
    countdown: formatCountdown(scheduledAt),
    time: formatTime(scheduledAt),
    date: formatShortDate(scheduledAt),
    fullDate: formatDate(scheduledAt),
  };
};

/* ── Component ── */
function SpectatorRaceSchedulePage() {
  const [races, setRaces] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");
  const [detailRace, setDetailRace] = useState(null);
  const [now, setNow] = useState(() => Date.now());
  const intervalRef = useRef(null);

  /* Tick every second for countdowns */
  useEffect(() => {
    intervalRef.current = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(intervalRef.current);
  }, []);

  /* Recompute countdowns */
  const racesWithCountdown = useMemo(() => {
    return races.map((r) => ({
      ...r,
      countdown: formatCountdown(r.scheduledAt),
    }));
  }, [races, now]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setIsLoading(true);
      setErrorMessage("");
      try {
        const response = await getRaces();
        const payload = unwrapResponseData(response);
        const items = Array.isArray(payload) ? payload.map(mapRace) : [];
        if (!cancelled) setRaces(items);
      } catch (err) {
        if (!cancelled) {
          setErrorMessage(err.message || "Không thể tải lịch đua.");
          setRaces([]);
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  /* Group races by date */
  const groupedDays = useMemo(() => {
    const map = new Map();
    for (const r of racesWithCountdown) {
      const key = r.date;
      if (!map.has(key)) map.set(key, []);
      map.get(key).push(r);
    }
    const entries = Array.from(map.entries());
    entries.sort(
      (a, b) => new Date(a[1][0].scheduledAt) - new Date(b[1][0].scheduledAt),
    );
    return entries;
  }, [racesWithCountdown]);

  /* Stats */
  const stats = useMemo(() => {
    const live = races.filter(
      (r) => r.status.toLowerCase() === "inprogress",
    ).length;
    const upcoming = races.filter(
      (r) => new Date(r.scheduledAt) > new Date(),
    );
    const nextRace = upcoming.sort(
      (a, b) => new Date(a.scheduledAt) - new Date(b.scheduledAt),
    )[0];
    return {
      liveCount: live,
      upcomingCount: upcoming.length,
      totalCount: races.length,
      nextRaceName: nextRace?.name ?? "Không có",
      nextCountdown: nextRace ? formatCountdown(nextRace.scheduledAt) : "—",
    };
  }, [racesWithCountdown, now]);

  /* Open race detail */
  const handleOpenDetail = useCallback(async (race) => {
    setDetailRace({ ...race, loading: true, horses: [], error: "" });
    try {
      const response = await getRace(race.id);
      const detail = unwrapResponseData(response);
      const entries = detail?.entries ?? detail?.Entries ?? [];
      const horses = entries.map((e) => ({
        id: e?.horseId ?? e?.HorseId,
        name: e?.horse?.name ?? e?.Horse?.Name ?? "Không xác định",
        jockeyName:
          e?.jockey?.user?.fullName ??
          e?.Jockey?.User?.FullName ??
          "Chưa xác định",
        gate: e?.gate ?? e?.Gate ?? e?.status ?? e?.Status ?? "—",
      }));
      setDetailRace({
        ...race,
        name: detail?.name ?? detail?.Name ?? race.name,
        location: detail?.location ?? detail?.Location ?? race.location,
        distance: detail?.distance ?? detail?.Distance ?? race.distance,
        status: detail?.status ?? detail?.Status ?? race.status,
        horses,
        loading: false,
        error: "",
      });
    } catch (err) {
      setDetailRace({
        ...race,
        loading: false,
        error: err.message || "Không thể tải chi tiết cuộc đua.",
        horses: [],
      });
    }
  }, []);

  return (
    <div className="srs-page">
      {/* ── Header ── */}
      <header className="srs-header">
        <span className="srs-eyebrow">Khán giả</span>
        <h1 className="srs-title">Lịch đua</h1>
        <p className="srs-subtitle">
          Theo dõi dòng thời gian các cuộc đua, đếm ngược đến giờ xuất phát.
        </p>
      </header>

      {/* ── Highlight stats ── */}
      <section className="srs-highlights">
        <div className="srs-highlight srs-highlight--live">
          <div>
            <span className="srs-highlight__value">{stats.liveCount}</span>
            <span className="srs-highlight__label">Đang trực tiếp</span>
          </div>
        </div>
        <div className="srs-highlight srs-highlight--upcoming">
          <div>
            <span className="srs-highlight__value">{stats.upcomingCount}</span>
            <span className="srs-highlight__label">Sắp diễn ra</span>
          </div>
        </div>
        <div className="srs-highlight srs-highlight--countdown">
          <div>
            <span className="srs-highlight__label">Đếm ngược cuộc đua tiếp theo</span>
            <span className="srs-highlight__countdown">{stats.nextCountdown}</span>
            <span className="srs-highlight__racename">{stats.nextRaceName}</span>
          </div>
        </div>
      </section>

      {/* ── Content ── */}
      <section className="srs-content">
        {isLoading ? (
          <div className="srs-empty">
            <h3>Đang tải lịch đua</h3>
            <p>Vui lòng đợi trong giây lát...</p>
          </div>
        ) : errorMessage ? (
          <div className="srs-empty srs-empty--error">
            <h3>Không thể tải lịch đua</h3>
            <p>{errorMessage}</p>
            <button
              className="srs-btn srs-btn--primary"
              onClick={() => window.location.reload()}
            >
              Thử lại
            </button>
          </div>
        ) : groupedDays.length === 0 ? (
          <div className="srs-empty">
            <h3>Chưa có cuộc đua nào</h3>
            <p>Lịch đua sẽ được cập nhật khi có cuộc đua mới.</p>
          </div>
        ) : (
          <div className="srs-timeline">
            {groupedDays.map(([date, dayRaces]) => (
              <div key={date} className="srs-day">
                <div className="srs-day__marker">
                  <div className="srs-day__dot" />
                  <div className="srs-day__line" />
                </div>
                <div className="srs-day__content">
                  <div className="srs-day__header">
                    <h2 className="srs-day__date">{date}</h2>
                    <span className="srs-day__count">
                      {dayRaces.length} cuộc đua
                    </span>
                  </div>
                  <div className="srs-day__cards">
                    {dayRaces.map((race) => {
                      const meta = getStatusMeta(race.status);
                      const isLive =
                        race.status.toLowerCase() === "inprogress";
                      return (
                        <article
                          key={race.id}
                          className={`srs-race-card${
                            isLive ? " srs-race-card--live" : ""
                          }`}
                        >
                          <div className="srs-race-card__top">
                            <div className="srs-race-card__info">
                              <span className="srs-race-card__time">
                                {formatTime(race.scheduledAt)}
                              </span>
                              <h3 className="srs-race-card__name">
                                {race.name}
                              </h3>
                              <div className="srs-race-card__meta">
                                <span>
                                  {race.location}
                                </span>
                                {race.distance > 0 && (
                                  <span>
                                    {race.distance}m
                                  </span>
                                )}
                              </div>
                            </div>
                            <div className="srs-race-card__badges">
                              <span className={`srs-badge ${meta.className}`}>
                                {isLive && (
                                  <span className="srs-live-dot" aria-hidden="true" />
                                )}
                                {meta.label}
                              </span>
                              <span className="srs-countdown">
                                {race.countdown}
                              </span>
                            </div>
                          </div>
                          <div className="srs-race-card__action">
                            <button
                              className="srs-btn srs-btn--ghost"
                              onClick={() => handleOpenDetail(race)}
                            >
                              Xem chi tiết
                            </button>
                          </div>
                        </article>
                      );
                    })}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* ── Detail Modal ── */}
      {detailRace && (
        <div
          className="srs-modal-overlay"
          role="dialog"
          aria-modal="true"
          onClick={(e) => {
            if (e.target === e.currentTarget) setDetailRace(null);
          }}
        >
          <div className="srs-modal">
            <div className="srs-modal__header">
              <div>
                <span
                  className={`srs-badge ${
                    getStatusMeta(detailRace.status).className
                  }`}
                >
                  {getStatusMeta(detailRace.status).label}
                </span>
                <h2>{detailRace.name}</h2>
              </div>
              <button
                className="srs-modal__close"
                onClick={() => setDetailRace(null)}
                aria-label="Đóng"
              ></button>
            </div>
            <div className="srs-modal__grid">
              <div className="srs-modal__field">
                <span className="srs-modal__label">Địa điểm</span>
                <span className="srs-modal__value">{detailRace.location}</span>
              </div>
              <div className="srs-modal__field">
                <span className="srs-modal__label">Cự ly</span>
                <span className="srs-modal__value">
                  {detailRace.distance > 0
                    ? `${detailRace.distance.toLocaleString("vi-VN")}m`
                    : "Chưa xác định"}
                </span>
              </div>
              <div className="srs-modal__field">
                <span className="srs-modal__label">Giờ đua</span>
                <span className="srs-modal__value">
                  {formatTime(detailRace.scheduledAt)} —{" "}
                  {formatShortDate(detailRace.scheduledAt)}
                </span>
              </div>
              <div className="srs-modal__field">
                <span className="srs-modal__label">Đếm ngược</span>
                <span className="srs-modal__value">
                  {detailRace.countdown || "Đang cập nhật"}
                </span>
              </div>
            </div>

            {/* Participants */}
            <div className="srs-modal__section">
              <h3 className="srs-modal__subtitle">Ngựa tham gia</h3>
              {detailRace.loading ? (
                <p className="srs-modal__note">Đang tải danh sách...</p>
              ) : detailRace.error ? (
                <p className="srs-modal__note srs-modal__note--error">
                  {detailRace.error}
                </p>
              ) : detailRace.horses && detailRace.horses.length > 0 ? (
                <div className="srs-participant-grid">
                  {detailRace.horses.map((h) => (
                    <div key={h.id} className="srs-participant">
                      <strong>{h.name}</strong>
                      <span className="srs-participant__jockey">
                        {h.jockeyName}
                      </span>
                      <span className="srs-participant__gate">
                        Cổng {h.gate}
                      </span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="srs-modal__note">
                  Danh sách ngựa sẽ hiển thị gần giờ xuất phát.
                </p>
              )}
            </div>

            <div className="srs-modal__actions">
              <button
                className="srs-btn srs-btn--primary"
                onClick={() => setDetailRace(null)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default SpectatorRaceSchedulePage;
