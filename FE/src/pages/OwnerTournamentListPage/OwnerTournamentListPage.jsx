import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getOwnerTournaments } from "../../services/ownerApi";
import { getPrizesByTournament } from "../../services/managementApi";
import { getTournamentRegistrationState, getTournamentRegistrationTone, selectUpcomingTournament } from "../../utils/tournamentRegistration";
import { OWNER_REQUIREMENT_LABELS } from "../../utils/ownerDemoDisplay";
import { isJockeyRole } from "../../services/authRoleUtils";
import { apiToVNDisplay } from "../../utils/vnDateTime";
import { RaceButton, RaceModalShell } from "../../components/ui/RaceUi";
import PrizeBreakdown from "../../components/PrizeBreakdown";
import "../../components/PrizeBreakdown.css";
import "../OwnerSharedLayout.css";
import "./OwnerTournamentListPage.css";

// Kept in sync with the labels getTournamentRegistrationState can actually produce for a
// Published/Ongoing/Finished/Cancelled Tournament — see tournamentRegistration.test.js's
// "owner tournament list status filter labels" suite, which pins this against that source of truth.
const statusFilters = ["Tất cả", "Mở đăng ký", "Đã đủ số lượng tham gia", "Đã đóng đăng ký", "Đã kết thúc đăng ký", "Giải đã kết thúc", "Giải đã hủy"];

const formatDate = (value) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Chưa xác định"
    : new Intl.DateTimeFormat("vi-VN", { month: "short", day: "numeric", year: "numeric" }).format(date);
};

const mapTournament = (tournament) => {
  const registrationState = getTournamentRegistrationState(tournament);
  return {
    id: tournament?.id ?? tournament?.Id,
    name: tournament?.name ?? tournament?.Name ?? "Giải đấu",
    description: tournament?.description ?? tournament?.Description ?? "Không có mô tả.",
    startDate: tournament?.startDate ?? tournament?.StartDate,
    endDate: tournament?.endDate ?? tournament?.EndDate,
    dates: `${formatDate(tournament?.startDate ?? tournament?.StartDate)} - ${formatDate(tournament?.endDate ?? tournament?.EndDate)}`,
    registrationDeadline: tournament?.registrationDeadline ?? tournament?.RegistrationDeadline,
    status: registrationState.label,
    statusKey: registrationState.key,
    canRegister: registrationState.canRegister,
    raceCount: tournament?.raceCount ?? tournament?.RaceCount ?? 0,
    roundCount: tournament?.roundCount ?? tournament?.RoundCount ?? 0,
    prizePool: tournament?.prizePool ?? tournament?.PrizePool ?? 0,
    venue: tournament?.venue ?? tournament?.Venue ?? "",
    surfaceType: tournament?.surfaceType ?? tournament?.SurfaceType ?? "",
    maxParticipants: tournament?.maxParticipants ?? tournament?.MaxParticipants ?? null,
    approvedRegistrationCount: tournament?.approvedRegistrationCount ?? tournament?.ApprovedRegistrationCount ?? 0,
  };
};

function OwnerTournamentListPage() {
  // Task B Final Correction §5: Register-to-Tournament is Owner-only — hidden for Jockey (UX
  // only; backend [Authorize(Roles="HorseOwner")] on POST /api/tournament-registrations is authoritative).
  const canRegisterAsOwner = !isJockeyRole();
  const navigate = useNavigate();
  const [tournaments, setTournaments] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("Tất cả");
  const [activeTournament, setActiveTournament] = useState(null);
  const [activePrizes, setActivePrizes] = useState([]);

  // PRIZE-V1.1 PART 8/9: Owner (and Jockey, which reuses this same page — see canRegisterAsOwner
  // above) currently see only PrizePool in this modal, not the rank breakdown — fetched lazily
  // when a Tournament is opened. A Draft Tournament's Prize breakdown is hidden server-side
  // (404), so a failed fetch here just leaves the section empty rather than erroring the modal.
  useEffect(() => {
    if (!activeTournament) { setActivePrizes([]); return; }
    let cancelled = false;
    getPrizesByTournament(activeTournament.id)
      .then((data) => { if (!cancelled) setActivePrizes(Array.isArray(data) ? data : []); })
      .catch(() => { if (!cancelled) setActivePrizes([]); });
    return () => { cancelled = true; };
  }, [activeTournament]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const payload = await getOwnerTournaments();
        if (!cancelled) setTournaments(Array.isArray(payload) ? payload.map(mapTournament) : []);
      } catch (error) {
        if (!cancelled) setErrorMessage(error.message || "Không thể tải danh sách giải đấu.");
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  const filteredTournaments = useMemo(() =>
    tournaments.filter(t =>
      (status === "Tất cả" || t.status.toLowerCase() === status.toLowerCase()) &&
      t.name.toLowerCase().includes(query.toLowerCase())
    ), [query, status, tournaments]);

  const totalOpen = tournaments.filter(t => t.canRegister).length;
  const totalClosed = tournaments.filter(t => !t.canRegister).length;

  // Sourced from the FULL Tournament list (never the search/status-filtered one) — this widget
  // answers "what's coming up next" regardless of what the Owner currently has typed/selected in
  // the filters above it, and must never surface a Finished/Cancelled Tournament (§1 fix).
  const upcomingTournament = useMemo(() => selectUpcomingTournament(tournaments), [tournaments]);

  const goToRegister = (tournamentId) => navigate(`/owner/register-tournament?tournamentId=${tournamentId}`);

  return (
    <div className="owner-page otl-page">
      <section className="page-header">
        <h1>Giải đấu</h1>
        <p>Tìm giải đấu đang mở đăng ký, xem thông tin chi tiết và cơ cấu giải thưởng trước khi đăng ký ngựa.</p>
      </section>

      <div className="otl-summary">
        <div className="otl-stat"><span>Tổng số</span><strong>{tournaments.length}</strong></div>
        <div className="otl-stat"><span>Đang mở đăng ký</span><strong>{totalOpen}</strong></div>
        <div className="otl-stat"><span>Đã đóng</span><strong>{totalClosed}</strong></div>
      </div>

      {/* Controls */}
      <div className="otl-toolbar">
        <div className="otl-field">
          <label htmlFor="otl-search" className="otl-field-label">Tìm kiếm</label>
          <div className="otl-search">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="11" cy="11" r="8" /><path d="m21 21-4.35-4.35" /></svg>
            <input id="otl-search" type="text" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Tìm theo tên giải đấu" />
          </div>
        </div>
        <div className="otl-field">
          <label htmlFor="otl-status" className="otl-field-label">Trạng thái</label>
          <select id="otl-status" className="otl-select" value={status} onChange={(e) => setStatus(e.target.value)}>
            {statusFilters.map(f => <option key={f} value={f}>{f}</option>)}
          </select>
        </div>
      </div>

      {/* Main */}
      <div className="otl-layout">
        <div className="otl-main">
          {isLoading ? (
            <div className="otl-empty"><h3>Đang tải</h3><p>Đang tải danh sách giải đấu...</p></div>
          ) : errorMessage ? (
            <div className="otl-empty"><h3>Lỗi</h3><p>{errorMessage}</p></div>
          ) : filteredTournaments.length === 0 ? (
            <div className="otl-empty"><h3>Không tìm thấy giải đấu</h3><p>Thử từ khóa hoặc trạng thái khác.</p></div>
          ) : (
            <div className="otl-grid">
              {filteredTournaments.map((t) => (
                <article key={t.id} className="otl-card">
                  <div className="otl-card__top">
                    <h3>{t.name}</h3>
                    <span className={`otl-badge otl-badge--${getTournamentRegistrationTone(t.statusKey)}`}>{t.status}</span>
                  </div>
                  <p className="otl-card__desc">{t.description}</p>
                  <div className="otl-card__meta">
                    <span>{t.dates}</span>
                    <span>{t.raceCount} cuộc đua</span>
                    {t.roundCount ? <span>{t.roundCount} vòng</span> : null}
                    {t.venue ? <span>{t.venue}</span> : null}
                  </div>
                  <div className="otl-card__footer">
                    <span className="otl-card__prize">Quỹ thưởng {(t.prizePool || 0).toLocaleString("vi-VN")}đ</span>
                    <div className="otl-card__actions">
                      <button type="button" className="ghost-button" onClick={() => setActiveTournament(t)}>Chi tiết</button>
                      {t.canRegister && canRegisterAsOwner && (
                        <button type="button" className="primary-button" onClick={() => goToRegister(t.id)}>Đăng ký</button>
                      )}
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>

        <aside className="otl-sidebar">
          <div className="otl-widget">
            <h4>Giải đấu sắp tới</h4>
            {upcomingTournament ? (
              <div className="otl-widget__race">
                <h5>{upcomingTournament.name}</h5>
                <p>{upcomingTournament.dates}</p>
                <p className="otl-widget__muted">{upcomingTournament.raceCount} cuộc đua</p>
              </div>
            ) : (
              <div className="otl-widget__empty">
                <p>Chưa có giải đấu sắp tới.</p>
                <p className="otl-widget__muted">Hãy quay lại sau khi có giải đấu mới được công bố.</p>
              </div>
            )}
          </div>
          <div className="otl-widget">
            <h4>Thông tin</h4>
            <p className="otl-widget__muted">
              Đăng ký ngựa của bạn tham gia các giải đấu để tranh tài và giành giải thưởng theo cơ cấu đã công bố.
            </p>
          </div>
        </aside>
      </div>

      {/* Detail modal */}
      {activeTournament && (
        <RaceModalShell
          title={activeTournament.name}
          description={activeTournament.description}
          onClose={() => setActiveTournament(null)}
          footer={<>
            <RaceButton variant="ghost" onClick={() => setActiveTournament(null)}>Đóng</RaceButton>
            {activeTournament.canRegister && canRegisterAsOwner && (
              <RaceButton onClick={() => { const id = activeTournament.id; setActiveTournament(null); goToRegister(id); }}>
                Đăng ký ngay
              </RaceButton>
            )}
          </>}
        >
          <div className="otl-modal-status">
            <span className={`otl-badge otl-badge--${getTournamentRegistrationTone(activeTournament.statusKey)}`}>{activeTournament.status}</span>
          </div>
          <div className="otl-modal-grid">
            <div><span>Thời gian</span><strong>{activeTournament.dates}</strong></div>
            <div><span>Hạn đăng ký</span><strong>{apiToVNDisplay(activeTournament.registrationDeadline) || "Chưa thiết lập"}</strong></div>
            <div><span>Cuộc đua</span><strong>{activeTournament.raceCount}</strong></div>
            <div><span>Vòng đấu</span><strong>{activeTournament.roundCount}</strong></div>
            <div>
              <span>Sức chứa</span>
              <strong>{activeTournament.maxParticipants ? `${activeTournament.approvedRegistrationCount}/${activeTournament.maxParticipants} ngựa` : "Không giới hạn"}</strong>
            </div>
            <div><span>Địa điểm</span><strong>{activeTournament.venue || "--"}</strong></div>
            <div><span>Loại mặt đường</span><strong>{activeTournament.surfaceType || "--"}</strong></div>
            <div><span>Quỹ thưởng</span><strong className="otl-modal-prize">{(activeTournament.prizePool || 0).toLocaleString("vi-VN")}đ</strong></div>
          </div>

          {activePrizes.length > 0 && <PrizeBreakdown prizes={activePrizes} />}

          <div className="otl-requirements">
            <h4>Điều kiện đăng ký</h4>
            <ul>
              {OWNER_REQUIREMENT_LABELS.slice(0, 4).map((label) => (
                <li key={label}>{label}</li>
              ))}
            </ul>
            <p>Hệ thống kiểm tra trùng đăng ký và lịch giải khi Chủ ngựa gửi yêu cầu.</p>
          </div>
        </RaceModalShell>
      )}
    </div>
  );
}

export default OwnerTournamentListPage;
