import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getOwnerTournaments } from "../../services/ownerApi";
import "../OwnerSharedLayout.css";
import "./OwnerTournamentListPage.css";

const statusFilters = ["Tất cả", "Đang diễn ra", "Mở", "Đã đóng"];

const formatDate = (value) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Chưa xác định"
    : new Intl.DateTimeFormat("vi-VN", { month: "short", day: "numeric", year: "numeric" }).format(date);
};

const getTournamentStatus = (tournament) => {
  if (!(tournament?.isActive ?? tournament?.IsActive)) return "Đã đóng";
  const start = new Date(tournament?.startDate ?? tournament?.StartDate);
  const end = new Date(tournament?.endDate ?? tournament?.EndDate);
  const now = new Date();
  return !Number.isNaN(start.getTime()) && !Number.isNaN(end.getTime()) && now >= start && now <= end
    ? "Đang diễn ra" : "Mở";
};

const mapTournament = (tournament) => ({
  id: tournament?.id ?? tournament?.Id,
  name: tournament?.name ?? tournament?.Name ?? "Giải đấu",
  description: tournament?.description ?? tournament?.Description ?? "Không có mô tả.",
  startDate: tournament?.startDate ?? tournament?.StartDate,
  endDate: tournament?.endDate ?? tournament?.EndDate,
  dates: `${formatDate(tournament?.startDate ?? tournament?.StartDate)} - ${formatDate(tournament?.endDate ?? tournament?.EndDate)}`,
  status: getTournamentStatus(tournament),
  raceCount: tournament?.raceCount ?? tournament?.RaceCount ?? 0,
  roundCount: tournament?.roundCount ?? tournament?.RoundCount ?? 0,
  prizePool: tournament?.prizePool ?? tournament?.PrizePool ?? 0,
  venue: tournament?.venue ?? tournament?.Venue ?? "",
  surfaceType: tournament?.surfaceType ?? tournament?.SurfaceType ?? "",
});

function OwnerTournamentListPage() {
  const navigate = useNavigate();
  const [tournaments, setTournaments] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("Tất cả");
  const [activeTournament, setActiveTournament] = useState(null);

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

  const totalOpen = tournaments.filter(t => t.status === "Mở" || t.status === "Đang diễn ra").length;
  const totalClosed = tournaments.filter(t => t.status === "Đã đóng").length;

  const statusClass = (s) => {
    if (s === "Đang diễn ra") return "live";
    if (s === "Mở") return "open";
    return "closed";
  };

  return (
    <div className="otl-page">
      {/* Hero */}
      <section className="otl-hero">
        <div className="otl-hero__overlay" />
        <div className="otl-hero__content">
          <div>
            <span className="pill" style={{ background: "rgba(215,170,77,0.2)", color: "#f2d28b" }}>Giải đấu</span>
            <h1>Giải đấu của chủ ngựa</h1>
            <p>Tìm kiếm giải đấu và lên kế hoạch đăng ký.</p>
          </div>
          <div className="otl-hero__stats">
            <div><span>Tổng số</span><strong>{tournaments.length}</strong></div>
            <div><span>Đang mở</span><strong>{totalOpen}</strong></div>
            <div><span>Đã đóng</span><strong>{totalClosed}</strong></div>
          </div>
        </div>
      </section>

      {/* Filters */}
      <section className="otl-filters">
        <div className="filter-group">
          <label className="label-required">Tìm kiếm</label>
          <input className="form-input" type="text" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Tìm theo tên giải đấu" />
        </div>
        <div className="filter-group">
          <label className="label-required">Trạng thái</label>
          <select className="form-select" value={status} onChange={(e) => setStatus(e.target.value)}>
            {statusFilters.map(f => <option key={f} value={f}>{f}</option>)}
          </select>
        </div>
      </section>

      {/* Layout 2 cột */}
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
                  <div
                    className="otl-card__banner"
                    style={
                      (t.imageUrl ?? t.ImageUrl)
                        ? { backgroundImage: `url(${t.imageUrl ?? t.ImageUrl})`, backgroundSize: "cover", backgroundPosition: "center" }
                        : {}
                    }
                  >
                    <div className="otl-card__banner-top">
                      <span className={`otl-badge otl-badge--${statusClass(t.status)}`}>{t.status}</span>
                      <div className="otl-card__banner-right">
                        {t.status !== "Đã đóng" && (
                          <span className="otl-countdown">{t.raceCount} cuộc đua</span>
                        )}
                      </div>
                    </div>
                    <div className="otl-card__banner-info">
                      <h4>Giải đấu</h4>
                      <p>{t.name}</p>
                    </div>
                    {t.status === "Đang diễn ra" && (
                      <div className="otl-card__progress">
                        <div className="otl-card__progress-fill" style={{ width: "60%" }} />
                      </div>
                    )}
                  </div>
                  <div className="otl-card__body">
                    <div>
                      <h3>{t.name}</h3>
                      <p>{t.description}</p>
                      <span className="otl-card__dates">{t.dates}</span>
                    </div>
                    <div className="otl-card__stats">
                      <div><span>Ngựa</span><strong>{t.raceCount}</strong></div>
                      <div><span>Vòng</span><strong>{t.roundCount}</strong></div>
                      <div><span>Giải thưởng</span><strong>{(t.prizePool || 0).toLocaleString()}đ</strong></div>
                      <div><span>Địa điểm</span><strong>{t.venue || "--"}</strong></div>
                    </div>
                    <div className="otl-card__actions">
                      <button className="otl-btn otl-btn--outline" onClick={() => setActiveTournament(t)}>Chi tiết</button>
                      <button className="otl-btn otl-btn--primary" onClick={() => navigate("/owner/register-tournament")}>Đăng ký</button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>

        {/* Sidebar */}
        <aside className="otl-sidebar">
          {filteredTournaments.length > 0 && (
            <div className="otl-widget">
              <h4>Giải đấu sắp tới</h4>
              <div className="otl-widget__race">
                <h5>{filteredTournaments[0].name}</h5>
                <p>{filteredTournaments[0].dates}</p>
                <p className="muted">{filteredTournaments[0].raceCount} cuộc đua</p>
              </div>
            </div>
          )}
          <div className="otl-widget">
            <h4>Thông tin</h4>
            <p style={{ fontSize: 13, color: "#5B6475", margin: 0, lineHeight: 1.6 }}>
              Đăng ký ngựa của bạn tham gia các giải đấu để tranh tài và giành giải thưởng hấp dẫn.
            </p>
          </div>
        </aside>
      </div>

      {/* Modal */}
      {activeTournament && (
        <div className="otl-modal-overlay" onClick={() => setActiveTournament(null)}>
          <div className="otl-modal" onClick={(e) => e.stopPropagation()}>
            <div className="otl-modal__header">
              <div>
                <span className={`otl-badge otl-badge--${statusClass(activeTournament.status)}`}>{activeTournament.status}</span>
                <h3>{activeTournament.name}</h3>
                <p className="muted" style={{ fontSize: 13, color: "#5B6475", margin: 0 }}>{activeTournament.description}</p>
              </div>
            </div>
            <div className="otl-modal__body">
              <div><h4>Ngày</h4><p>{activeTournament.dates}</p></div>
              <div><h4>Cuộc đua</h4><p>{activeTournament.raceCount}</p></div>
              <div><h4>Vòng</h4><p>{activeTournament.roundCount}</p></div>
              <div><h4>Giải thưởng</h4><p>{(activeTournament.prizePool || 0).toLocaleString()}đ</p></div>
              <div><h4>Địa điểm</h4><p>{activeTournament.venue || "--"}</p></div>
              <div><h4>Loại mặt đường</h4><p>{activeTournament.surfaceType || "--"}</p></div>
            </div>
            <div className="otl-modal__actions">
              <button className="otl-btn otl-btn--outline" onClick={() => setActiveTournament(null)}>Đóng</button>
              <button className="otl-btn otl-btn--primary" onClick={() => { setActiveTournament(null); navigate("/owner/register-tournament"); }}>Đăng ký ngay</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default OwnerTournamentListPage;
