import { useEffect, useMemo, useState } from "react";
import {
  formatJockeyDate,
  getJockeyAssignedRaces,
  getJockeyPendingEntries,
  confirmJockeyEntry,
  declineJockeyEntry,
} from "../../services/jockeyApi";
import "./JockeySchedulePage.css";

function DetailBlock({ icon, label, value }) {
  return (
    <div className="js-block">
      <span className="js-block-icon">{icon}</span>
      <div>
        <span className="js-block-label">{label}</span>
        <strong className="js-block-value">{value ?? "--"}</strong>
      </div>
    </div>
  );
}

export default function JockeySchedulePage() {
  const [races, setRaces] = useState([]);
  const [pendingEntries, setPendingEntries] = useState([]);
  const [pendingLoading, setPendingLoading] = useState(false);
  const [loading, setLoading] = useState(true);
  const [selectedRace, setSelectedRace] = useState(null);
  const [detailMode, setDetailMode] = useState("race");
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [showTimeline, setShowTimeline] = useState(false);

  const loadPending = async () => {
    setPendingLoading(true);
    try {
      const data = await getJockeyPendingEntries();
      setPendingEntries(Array.isArray(data) ? data : []);
    } catch {
      setPendingEntries([]);
    } finally {
      setPendingLoading(false);
    }
  };

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        setLoading(true);
        const data = await getJockeyAssignedRaces();
        if (!cancelled) { setRaces(data); setSelectedRace(data[0] ?? null); }
      } catch {
        if (!cancelled) { setRaces([]); setSelectedRace(null); }
      } finally { if (!cancelled) setLoading(false); }
    };
    load();
    loadPending();
    return () => { cancelled = true; };
  }, []);

  const handleConfirmEntry = async (entryId) => {
    try {
      await confirmJockeyEntry(entryId);
      await loadPending();
      const data = await getJockeyAssignedRaces();
      setRaces(data);
      setSelectedRace((prev) => {
        if (prev && (prev.id ?? prev.entryId) === entryId) return data.find(r => (r.id ?? r.entryId) === entryId) ?? prev;
        return prev;
      });
    } catch (err) {
      alert(err?.message ?? "Không thể xác nhận.");
    }
  };

  const handleDeclineEntry = async (entryId) => {
    if (!window.confirm("Từ chối tham gia cuộc đua này?")) return;
    try {
      await declineJockeyEntry(entryId);
      await loadPending();
      const data = await getJockeyAssignedRaces();
      setRaces(data);
    } catch (err) {
      alert(err?.message ?? "Không thể từ chối.");
    }
  };

  const sortedRaces = useMemo(() =>
    [...races].sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()),
    [races]);

  const filteredRaces = useMemo(() => {
    let items = sortedRaces;
    if (search.trim()) items = items.filter(r => (r.title || "").toLowerCase().includes(search.toLowerCase()) || (r.horseName || "").toLowerCase().includes(search.toLowerCase()));
    if (filter === "upcoming") items = items.filter(r => new Date(r.scheduledAt) > new Date());
    else if (filter === "completed") items = items.filter(r => new Date(r.scheduledAt) <= new Date());
    return items;
  }, [sortedRaces, search, filter]);

  const calendarGroups = useMemo(() =>
    sortedRaces.reduce((acc, race) => {
      const d = new Date(race.scheduledAt);
      const key = Number.isNaN(d.getTime()) ? "unknown" : new Intl.DateTimeFormat("vi-VN", { month: "short", day: "2-digit" }).format(d);
      if (!acc[key]) acc[key] = [];
      acc[key].push(race);
      return acc;
    }, {}),
  [sortedRaces]);

  const todayCount = sortedRaces.filter(r => { const d = new Date(r.scheduledAt); const t = new Date(); return d.getDate() === t.getDate() && d.getMonth() === t.getMonth() && d.getFullYear() === t.getFullYear(); }).length;
  const upcomingCount = sortedRaces.filter(r => new Date(r.scheduledAt) > new Date()).length;

  return (
    <div className="js-page">
      {/* Header */}
      <div className="js-header">
        <h1>Lịch đua</h1>
        <p className="js-sub">Theo dõi các cuộc đua đã được phân công.</p>
        <div className="js-summary">
          <span className="js-sum-chip"><strong>{sortedRaces.length}</strong> Đã phân công</span>
          <span className="js-sum-chip"><strong>{upcomingCount}</strong> Sắp diễn ra</span>
          <span className="js-sum-chip"><strong>{todayCount}</strong> Hôm nay</span>
        </div>
      </div>

      {/* Pending confirmations */}
      {pendingLoading ? null : pendingEntries.length > 0 && (
        <div className="js-pending">
          <div className="js-pending__header">
            <h3>Chờ bạn xác nhận tham gia ({pendingEntries.length})</h3>
            <span>Cuộc đua chỉ bắt đầu khi kỵ sĩ xác nhận</span>
          </div>
          <div className="js-pending__list">
            {pendingEntries.map((p) => (
              <div key={p.entryId ?? p.EntryId} className="js-pending__item">
                <div>
                  <strong>{p.raceName ?? p.RaceName ?? "Cuộc đua"}</strong>
                  <span>
                    {p.horseName ?? p.HorseName ?? "Ngựa"}
                    {p.tournamentName ? ` · ${p.tournamentName}` : ""}
                    {p.scheduledAt ? ` · ${formatJockeyDate(p.scheduledAt, "")}` : ""}
                  </span>
                </div>
                <div style={{ display: "flex", gap: 8 }}>
                  <button className="js-btn js-btn--primary" onClick={() => handleConfirmEntry(p.entryId ?? p.EntryId)}>Xác nhận</button>
                  <button className="js-btn js-btn--outline" onClick={() => handleDeclineEntry(p.entryId ?? p.EntryId)}>Từ chối</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Toolbar */}
      <div className="js-toolbar">
        <div className="js-search">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
          <input placeholder="Tìm cuộc đua..." value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        <div className="js-filter-group">
          {["all", "upcoming", "completed"].map(f => (
            <button key={f} className={`js-filter-btn ${filter === f ? "js-filter-btn--active" : ""}`} onClick={() => setFilter(f)}>
              {f === "all" ? "Tất cả" : f === "upcoming" ? "Sắp tới" : "Đã qua"}
            </button>
          ))}
        </div>
        <button className={`js-pill ${showTimeline ? "js-pill--active" : ""}`} onClick={() => setShowTimeline(!showTimeline)}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
          Timeline
        </button>
      </div>

      {/* Content */}
      {loading ? (
        <div className="js-loading"><div className="js-skeleton"/><div className="js-skeleton"/><div className="js-skeleton"/></div>
      ) : filteredRaces.length === 0 ? (
        <div className="js-empty">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="rgba(242,210,139,0.3)" strokeWidth="1"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
          <h3>Không có cuộc đua</h3>
          <p>Chưa có cuộc đua nào được phân công.</p>
        </div>
      ) : (
        <>
          <div className="js-layout">
            {/* Left — Race List */}
            <div className="js-list">
              {filteredRaces.map(race => (
                <div key={race.id} className={`js-card ${selectedRace?.id === race.id ? "js-card--active" : ""}`} onClick={() => { setSelectedRace(race); setDetailMode("race"); }}>
                  <div className="js-card__top">
                    <div className="js-card__icon">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#8f6420" strokeWidth="1.5"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
                    </div>
                    <div>
                      <h3>{race.title}</h3>
                      <p className="js-card__sub">{race.horseName}{race.tournamentName ? ` · ${race.tournamentName}` : ""}</p>
                    </div>
                    <span className={`js-badge ${race.jockeyConfirmed ? "js-badge--ok" : "js-badge--warn"}`}>
                      {race.jockeyConfirmed ? "Đã xác nhận" : "Chờ"}
                    </span>
                  </div>
                  <div className="js-card__meta">
                    <span>{formatJockeyDate(race.scheduledAt, "")}</span>
                    {race.location && <span>{race.location}</span>}
                    {race.distance && <span>{race.distance}m</span>}
                  </div>
                </div>
              ))}
            </div>

            {/* Right — Detail */}
            <div className="js-detail">
              {selectedRace ? (
                <>
                  <div className="js-detail-tabs">
                    <button className={`js-dt ${detailMode === "race" ? "js-dt--active" : ""}`} onClick={() => setDetailMode("race")}>Cuộc đua</button>
                    <button className={`js-dt ${detailMode === "horse" ? "js-dt--active" : ""}`} onClick={() => setDetailMode("horse")}>Ngựa</button>
                  </div>

                  {detailMode === "race" ? (
                    <div>
                      <h2 className="js-detail-title">{selectedRace.title}</h2>
                      <p className="js-detail-sub">{selectedRace.tournamentName}</p>
                      <div className="js-blocks">
                        <DetailBlock icon="" label="Thời gian" value={formatJockeyDate(selectedRace.scheduledAt, "")} />
                        <DetailBlock icon="" label="Đường đua" value={selectedRace.location} />
                        <DetailBlock icon="" label="Cự ly" value={selectedRace.distance ? `${selectedRace.distance}m` : ""} />
                        <DetailBlock icon="" label="Số người" value={selectedRace.maxParticipants} />
                        <DetailBlock icon="" label="Trạng thái" value={selectedRace.status} />
                        <DetailBlock icon="" label="Chủ ngựa" value={selectedRace.ownerConfirmed ? "Đã xác nhận" : "Chờ"} />
                      </div>
                    </div>
                  ) : (
                    <div>
                      <h2 className="js-detail-title">{selectedRace.horseName}</h2>
                      <p className="js-detail-sub">Hồ sơ ngựa được phân công</p>
                      <div className="js-blocks">
                        <DetailBlock icon="" label="Giống" value={selectedRace.horseBreed} />
                        <DetailBlock icon="" label="Giới tính" value={selectedRace.horseGender} />
                        <DetailBlock icon="" label="Tuổi" value={selectedRace.horseAge} />
                        <DetailBlock icon="" label="Cân nặng" value={selectedRace.horseWeight} />
                        <DetailBlock icon="" label="Chiều cao" value={selectedRace.horseHeight} />
                        <DetailBlock icon="" label="Sự nghiệp" value={`${selectedRace.horseTotalWins || 0} thắng / ${selectedRace.horseTotalRaces || 0} đua`} />
                      </div>
                    </div>
                  )}

                  <div className="js-detail-actions">
                    {!selectedRace.jockeyConfirmed && (
                      <button className="js-btn js-btn--primary" onClick={() => handleConfirmEntry(selectedRace.id ?? selectedRace.entryId)}>
                        Xác nhận tham gia cuộc đua
                      </button>
                    )}
                    <button className="js-btn js-btn--outline" onClick={() => setDetailMode(detailMode === "race" ? "horse" : "race")}>
                      {detailMode === "race" ? "Xem hồ sơ ngựa" : "Xem thông tin đua"}
                    </button>
                  </div>
                </>
              ) : (
                <p className="js-detail-empty">Chọn một cuộc đua để xem chi tiết.</p>
              )}
            </div>
          </div>

          {/* Timeline */}
          {showTimeline && (
            <div className="js-timeline">
              <h3>Lịch đua</h3>
              <div className="js-tl-list">
                {Object.entries(calendarGroups).map(([date, items]) => (
                  <div key={date} className="js-tl-day">
                    <div className="js-tl-line">
                      <span className="js-tl-dot" />
                      <div className="js-tl-bar" />
                    </div>
                    <div className="js-tl-content">
                      <span className="js-tl-date">{date}</span>
                      {items.map(race => (
                        <div key={race.id} className="js-tl-race" onClick={() => { setSelectedRace(race); setDetailMode("race"); }}>
                          <div className="js-tl-race__icon">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#8f6420" strokeWidth="1.5"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
                          </div>
                          <div>
                            <strong>{race.title}</strong>
                            <span>{race.horseName} · {race.location} · {formatJockeyDate(race.scheduledAt, "")}</span>
                          </div>
                          <span className={`js-badge ${race.jockeyConfirmed ? "js-badge--ok" : "js-badge--warn"}`}>
                            {race.jockeyConfirmed ? "Đã xác nhận" : "Chờ"}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
