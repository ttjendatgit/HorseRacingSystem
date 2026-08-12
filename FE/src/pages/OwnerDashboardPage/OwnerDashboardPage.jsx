import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getOwnerProfile, getOwnerTournaments, getOwnerEntries, getOwnerPerformance } from "../../services/ownerApi";
import "./OwnerDashboardPage.css";

function OwnerDashboardPage() {
  const navigate = useNavigate();
  const [owner, setOwner] = useState(null);
  const [tournaments, setTournaments] = useState([]);
  const [entries, setEntries] = useState([]);
  const [performance, setPerformance] = useState(null);
  const [profileError, setProfileError] = useState("");

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try { const p = await getOwnerProfile(); if (!cancelled) setOwner(p); } catch (e) { if (!cancelled) setProfileError(e.message); }
      try { const p = await getOwnerTournaments(); if (!cancelled) setTournaments(Array.isArray(p) ? p.slice(0, 4) : []); } catch { if (!cancelled) setTournaments([]); }
      try { const p = await getOwnerEntries(); if (!cancelled) setEntries(Array.isArray(p) ? p : []); } catch { if (!cancelled) setEntries([]); }
      try { const p = await getOwnerPerformance(); if (!cancelled) setPerformance(p?.data ?? p); } catch { /* ignore */ }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  const upcomingEntries = useMemo(() => entries.filter(e => (e.finishPosition ?? e.FinishPosition) == null), [entries]);
  const pastEntries = useMemo(() => entries.filter(e => (e.finishPosition ?? e.FinishPosition) != null), [entries]);
  const pendingCount = useMemo(() => entries.filter(e => !(e.ownerConfirmed ?? e.OwnerConfirmed)).length, [entries]);
  const confirmedCount = useMemo(() => entries.filter(e => e.ownerConfirmed ?? e.OwnerConfirmed).length, [entries]);
  const wins = pastEntries.filter(e => (e.finishPosition ?? e.FinishPosition) === 1).length;
  const winRate = entries.length > 0 ? Math.round((wins / entries.length) * 100) : 0;

  const getEntryField = (entry, camel, pascal) => entry[camel] ?? entry[pascal];

  const horseCount = owner?.horseCount ?? 0;
  const ownerName = owner?.fullName || "Chủ ngựa";

  return (
    <div className="od-wrap">
      <div className="od-container">
        {/* Topbar */}
        <header className="od-topbar">
          <div>
            <h1>Chào mừng trở lại, {ownerName}</h1>
            <p className="od-topbar-sub">{upcomingEntries.length} cuộc đua sắp diễn ra · {pendingCount} chờ xác nhận</p>
          </div>
          <div className="od-topbar-actions">
            <button className="od-btn od-btn--primary" onClick={() => navigate("/owner/horses/new")}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 5v14M5 12h14"/></svg>
              Thêm ngựa
            </button>
            <button className="od-btn od-btn--outline" onClick={() => navigate("/owner/register-tournament")}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 6v6m0 0v6m0-6h6m-6 0H6"/></svg>
              Đăng ký giải
            </button>
          </div>
        </header>

        {profileError && <div className="od-error">{profileError}</div>}

        {/* Primary — Hero Stats */}
        <section className="od-primary-grid">
          <div className="od-hero-card">
            <div className="od-hero-card__content">
              <span className="od-label">Tổng số ngựa</span>
              <strong className="od-hero-value">{horseCount}</strong>
              <span className="od-trend od-trend--up">{confirmedCount} đã xác nhận tham gia</span>
            </div>
            <div className="od-hero-card__ring">
              <svg width="80" height="80" viewBox="0 0 80 80">
                <circle cx="40" cy="40" r="34" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="6" />
                <circle cx="40" cy="40" r="34" fill="none" stroke="#f2d28b" strokeWidth="6" strokeDasharray={`${(horseCount / 10) * 214} 214`} strokeLinecap="round" transform="rotate(-90 40 40)" />
              </svg>
              <span className="od-ring-label">{horseCount}/10</span>
            </div>
          </div>
          <div className="od-hero-card">
            <div className="od-hero-card__content">
              <span className="od-label">Tỉ lệ thắng</span>
              <strong className="od-hero-value">{winRate}%</strong>
              <span className="od-trend od-trend--up">{wins} chiến thắng</span>
            </div>
            <div className="od-hero-card__bar">
              <div className="od-bar-track">
                <div className="od-bar-fill" style={{ width: `${winRate}%` }} />
              </div>
            </div>
          </div>
          <div className="od-hero-card">
            <div className="od-hero-card__content">
              <span className="od-label">Cuộc đua sắp tới</span>
              <strong className="od-hero-value">{upcomingEntries.length}</strong>
              <span className="od-trend">{pendingCount} chưa xác nhận</span>
            </div>
          </div>
          <div className="od-hero-card">
            <div className="od-hero-card__content">
              <span className="od-label">Đã hoàn thành</span>
              <strong className="od-hero-value">{pastEntries.length}</strong>
              <span className="od-trend od-trend--up">{Math.round((pastEntries.length / (entries.length || 1)) * 100)}% tổng số</span>
            </div>
          </div>
        </section>

        {/* Dominant — Performance Chart + Horse Stats */}
        {performance && (
          <section className="od-dominant">
            <div className="od-chart-card">
              <h3>Hiệu suất theo tuần</h3>
              <div className="od-chart">
                {(performance.weeklyPerformance ?? []).map((w) => {
                  const max = Math.max(...(performance.weeklyPerformance ?? []).map(w => w.races), 1);
                  return (
                    <div key={w.week} className="od-chart-col">
                      <span className="od-chart-bar" style={{ height: `${(w.races / max) * 100}%` }}>
                        <span className="od-chart-val">{w.wins}</span>
                      </span>
                      <span className="od-chart-label">{w.week}</span>
                    </div>
                  );
                })}
              </div>
            </div>
            <div className="od-horse-stats">
              <h3>Hiệu suất ngựa</h3>
              <div className="od-horse-list">
                {(performance.horseStats ?? []).slice(0, 4).map(h => (
                  <div key={h.horseId ?? h.horseName} className="od-horse-item">
                    <div className="od-horse-info">
                      <strong>{h.horseName}</strong>
                      <span>{h.totalRaces} đua · {h.wins} thắng</span>
                    </div>
                    <div className="od-horse-bar-wrap">
                      <div className="od-horse-bar">
                        <div className="od-horse-fill" style={{ width: `${h.winRate}%` }} />
                      </div>
                      <span className="od-horse-rate">{h.winRate}%</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </section>
        )}

        {/* Secondary — 2 Columns */}
        <div className="od-cols">
          <div className="od-card">
            <div className="od-card__header">
              <h3>Cuộc đua sắp tới</h3>
              <span className="od-count">{upcomingEntries.length}</span>
            </div>
            <div className="od-card__body">
              {upcomingEntries.length === 0 ? (
                <p className="od-muted">Chưa có cuộc đua nào.</p>
              ) : (
                <div className="od-feed">
                  {upcomingEntries.slice(0, 5).map(entry => {
                    const hName = getEntryField(entry, "horseName", "HorseName");
                    const rName = getEntryField(entry, "raceName", "RaceName");
                    const confirmed = getEntryField(entry, "ownerConfirmed", "OwnerConfirmed");
                    return (
                      <div key={getEntryField(entry, "entryId", "EntryId")} className="od-feed-item">
                        <div className={`od-feed-dot ${confirmed ? "od-feed-dot--green" : "od-feed-dot--amber"}`} />
                        <div className="od-feed-content">
                          <strong>{rName ?? "Cuộc đua"}</strong>
                          <span>{hName ?? "Ngựa"} · {confirmed ? "Đã xác nhận" : "Chờ xác nhận"}</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          <div className="od-card">
            <div className="od-card__header">
              <h3>Giải đấu</h3>
              <span className="od-count">{tournaments.length}</span>
            </div>
            <div className="od-card__body">
              {tournaments.length === 0 ? (
                <p className="od-muted">Chưa tham gia giải đấu nào.</p>
              ) : (
                <div className="od-feed">
                  {tournaments.map(t => (
                    <div key={t.id ?? t.Id} className="od-feed-item">
                      <div className={`od-feed-dot ${(t.isActive ?? t.IsActive) ? "od-feed-dot--green" : "od-feed-dot--gray"}`} />
                      <div className="od-feed-content">
                        <strong>{t.name ?? t.Name}</strong>
                        <span>{t.raceCount ?? t.RaceCount ?? 0} cuộc đua</span>
                      </div>
                      <span className={`od-chip ${(t.isActive ?? t.IsActive) ? "od-chip--active" : "od-chip--closed"}`}>
                        {(t.isActive ?? t.IsActive) ? "Mở" : "Đã đóng"}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Tertiary — Activity */}
        <div className="od-cols">
          <div className="od-card">
            <div className="od-card__header">
              <h3>Kết quả gần đây</h3>
            </div>
            <div className="od-card__body">
              {pastEntries.length === 0 ? (
                <p className="od-muted">Chưa có kết quả.</p>
              ) : (
                <div className="od-feed">
                  {pastEntries.slice(0, 5).map(entry => {
                    const hName = getEntryField(entry, "horseName", "HorseName");
                    const rName = getEntryField(entry, "raceName", "RaceName");
                    const pos = getEntryField(entry, "finishPosition", "FinishPosition");
                    const posLabel = pos === 1 ? "Vô địch" : pos === 2 ? "Hạng 2" : pos === 3 ? "Hạng 3" : `#${pos}`;
                    const posClass = pos === 1 ? "win" : pos === 2 ? "place" : pos === 3 ? "show" : "";
                    return (
                      <div key={getEntryField(entry, "entryId", "EntryId")} className="od-feed-item">
                        <div className={"od-pos-badge " + (posClass ? "od-pos-badge--" + posClass : "")}>
                          {pos === 1 ? "1st" : pos === 2 ? "2nd" : pos === 3 ? "3rd" : "#" + pos}
                        </div>
                        <div className="od-feed-content">
                          <strong>{hName ?? "Ngựa"}</strong>
                          <span>{rName ?? "Cuộc đua"} · {posLabel}</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          <div className="od-card">
            <div className="od-card__header">
              <h3>Hoạt động gần đây</h3>
            </div>
            <div className="od-card__body">
              {entries.length === 0 ? (
                <p className="od-muted">Chưa có hoạt động.</p>
              ) : (
                <div className="od-feed">
                  {entries.slice(0, 6).map(entry => {
                    const hName = getEntryField(entry, "horseName", "HorseName");
                    const confirmed = getEntryField(entry, "ownerConfirmed", "OwnerConfirmed");
                    const pos = getEntryField(entry, "finishPosition", "FinishPosition");
                    return (
                      <div key={getEntryField(entry, "entryId", "EntryId")} className="od-feed-item">
                        <div className={`od-feed-dot ${pos === 1 ? "od-feed-dot--gold" : confirmed ? "od-feed-dot--green" : "od-feed-dot--amber"}`} />
                        <div className="od-feed-content">
                          <strong>{hName ?? "Ngựa"}</strong>
                          <span>{confirmed ? "Đã xác nhận" : "Chờ xác nhận"}{pos != null ? ` · #${pos}` : ""}</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Bottom — Race Calendar */}
        <div className="od-cols">
          <div className="od-card">
            <div className="od-card__header">
              <h3>Lịch đua</h3>
            </div>
            <div className="od-card__body">
              <div className="od-timeline">
                <div className="od-timeline-item">
                  <span className="od-timeline-date">Hôm nay</span>
                  <div className="od-timeline-bar">
                    <div className="od-timeline-dot od-timeline-dot--active" />
                    <div className="od-timeline-line" />
                  </div>
                  <div className="od-timeline-content">
                    {upcomingEntries.length > 0 ? (
                      <strong>{upcomingEntries[0]?.raceName ?? "Cuộc đua"}</strong>
                    ) : (
                      <span className="od-muted" style={{textAlign:"left",padding:0}}>Không có lịch</span>
                    )}
                  </div>
                </div>
                <div className="od-timeline-item">
                  <span className="od-timeline-date">Tuần này</span>
                  <div className="od-timeline-bar">
                    <div className="od-timeline-dot" />
                    <div className="od-timeline-line" />
                  </div>
                  <div className="od-timeline-content">
                    <span className="od-muted" style={{textAlign:"left",padding:0}}>{upcomingEntries.length} cuộc đua · {pendingCount} chờ xác nhận</span>
                  </div>
                </div>
                <div className="od-timeline-item">
                  <span className="od-timeline-date">Sắp tới</span>
                  <div className="od-timeline-bar">
                    <div className="od-timeline-dot" />
                  </div>
                  <div className="od-timeline-content">
                    <span className="od-muted" style={{textAlign:"left",padding:0}}>{tournaments.length} giải đấu đang mở</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="od-card">
            <div className="od-card__header">
              <h3>Điều kiện ngựa</h3>
            </div>
            <div className="od-card__body">
              {entries.length === 0 ? (
                <p className="od-muted">Chưa có dữ liệu.</p>
              ) : (
                <div className="od-horse-list">
                  {entries
                    .reduce((acc, e) => {
                      const name = getEntryField(e, "horseName", "HorseName");
                      if (!acc.find(h => h.name === name)) acc.push({ name, entries: [] });
                      const h = acc.find(h => h.name === name);
                      h.entries.push(e);
                      return acc;
                    }, [])
                    .slice(0, 3)
                    .map(h => {
                      const total = h.entries.length;
                      const wins = h.entries.filter(e => (e.finishPosition ?? e.FinishPosition) === 1).length;
                      const speed = Math.min(95, 50 + wins * 10);
                      const stamina = Math.min(95, 40 + (total - wins) * 5);
                      return (
                        <div key={h.name} className="od-horse-item" style={{marginBottom:8}}>
                          <div className="od-horse-info">
                            <strong>{h.name}</strong>
                            <span>{wins}/{total} thắng</span>
                          </div>
                          <div className="od-cond-row">
                            <span className="od-cond-label">Tốc độ</span>
                            <div className="od-horse-bar"><div className="od-horse-fill od-horse-fill--gold" style={{width:speed+"%"}} /></div>
                            <span className="od-cond-val">{speed}%</span>
                          </div>
                          <div className="od-cond-row">
                            <span className="od-cond-label">Sức bền</span>
                            <div className="od-horse-bar"><div className="od-horse-fill" style={{width:stamina+"%"}} /></div>
                            <span className="od-cond-val">{stamina}%</span>
                          </div>
                        </div>
                      );
                    })}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default OwnerDashboardPage;
