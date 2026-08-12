import { useEffect, useMemo, useState } from "react";
import { unwrapResponseData } from "../../services/authRoleUtils";
import { getTournaments } from "../../services/spectatorApi";
import heroBg from "../../assets/racing.png";
import "./SpectatorTournamentListPage.css";

const CATEGORY_LABELS = {
  "grade 1": "Grade 1",
  "grade 2": "Grade 2",
  "grade 3": "Grade 3",
  listed: "Listed",
  handicap: "Handicap",
  maiden: "Maiden",
  "group 1": "Group 1",
  "group 2": "Group 2",
  "group 3": "Group 3",
};

const PAGE_SIZE = 9;

const formatDate = (value) => {
  if (!value) return "Chưa xác định";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa xác định";
  return new Intl.DateTimeFormat("vi-VN", {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(date);
};

const formatPrizePool = (value) => {
  if (value == null || value === 0) return "Đang cập nhật";
  const num = Number(value);
  if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M VNĐ`;
  if (num >= 1_000) return `${(num / 1_000).toFixed(0)}K VNĐ`;
  return `${num.toLocaleString("vi-VN")} VNĐ`;
};

const getTournamentStatus = (t) => {
  const isActive = t.isActive ?? t.IsActive ?? true;
  const start = t.startDate ?? t.StartDate;
  const end = t.endDate ?? t.EndDate;

  if (!isActive || (end && new Date(end) < new Date())) return "completed";

  if (start && new Date(start) > new Date()) return "upcoming";

  return "active";
};

const STATUS_FILTERS = [
  { value: "all", label: "Tất cả" },
  { value: "active", label: "Đang diễn ra" },
  { value: "upcoming", label: "Sắp diễn ra" },
  { value: "completed", label: "Đã kết thúc" },
];

const mapTournament = (t) => ({
  id: t?.id ?? t?.Id,
  name: t?.name ?? t?.Name ?? "Giải đấu",
  description: t?.description ?? t?.Description ?? "",
  category: t?.category ?? t?.Category ?? "",
  venue: t?.venue ?? t?.Venue ?? "Địa điểm chưa xác định",
  roundCount: t?.roundCount ?? t?.RoundCount ?? 0,
  raceCount: t?.raceCount ?? t?.RaceCount ?? 0,
  prizePool: t?.prizePool ?? t?.PrizePool ?? 0,
  startDate: t?.startDate ?? t?.StartDate,
  endDate: t?.endDate ?? t?.EndDate,
  isActive: t?.isActive ?? t?.IsActive ?? true,
  status: getTournamentStatus(t),
});

const STATUS_META = {
  active: { label: "Đang diễn ra", className: "badge--live" },
  upcoming: { label: "Sắp diễn ra", className: "badge--upcoming" },
  completed: { label: "Đã kết thúc", className: "badge--completed" },
};

function SpectatorTournamentListPage() {
  const [tournaments, setTournaments] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const [detailTournament, setDetailTournament] = useState(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setIsLoading(true);
      setErrorMessage("");
      try {
        const response = await getTournaments();
        const payload = unwrapResponseData(response);
        const items = Array.isArray(payload) ? payload.map(mapTournament) : [];
        if (!cancelled) setTournaments(items);
      } catch (err) {
        if (!cancelled) {
          setErrorMessage(err.message || "Không thể tải danh sách giải đấu.");
          setTournaments([]);
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    load();
    return () => { cancelled = true; };
  }, []);

  const filtered = useMemo(() => {
    return tournaments.filter((t) => {
      const matchStatus = statusFilter === "all" || t.status === statusFilter;
      const keyword = query.toLowerCase();
      const matchQuery =
        !keyword ||
        t.name.toLowerCase().includes(keyword) ||
        t.venue.toLowerCase().includes(keyword) ||
        (t.category && t.category.toLowerCase().includes(keyword));
      return matchStatus && matchQuery;
    });
  }, [tournaments, query, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(currentPage, totalPages);
  const pageItems = filtered.slice(
    (safePage - 1) * PAGE_SIZE,
    safePage * PAGE_SIZE,
  );

  // reset page on filter change
  useEffect(() => setCurrentPage(1), [query, statusFilter]);

  const handlePrevPage = () =>
    setCurrentPage((p) => Math.max(1, p - 1));
  const handleNextPage = () =>
    setCurrentPage((p) => Math.min(totalPages, p + 1));

  const renderPageNumbers = () => {
    const pages = [];
    const maxVisible = 5;
    let start = Math.max(1, safePage - Math.floor(maxVisible / 2));
    let end = Math.min(totalPages, start + maxVisible - 1);
    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  };

  return (
    <div className="stl-page">
      {/* ── Hero Banner ── */}
      <section className="stl-hero" style={{ backgroundImage: `url(${heroBg})` }}>
        <div className="stl-hero__content">
          <span className="stl-eyebrow">Khán giả</span>
          <h1 className="stl-title">Giải đấu</h1>
          <p className="stl-subtitle">
            Khám phá các giải đua đang diễn ra, sắp tới và đã kết thúc.
          </p>
          <div className="stl-header__stats">
            <div className="stl-stat-pill">
              <span className="stl-stat-pill__value">{tournaments.length}</span>
              <span className="stl-stat-pill__label">Tổng giải đấu</span>
            </div>
            <div className="stl-stat-pill">
              <span className="stl-stat-pill__value">
                {tournaments.filter((t) => t.status === "active").length}
              </span>
              <span className="stl-stat-pill__label">Đang diễn ra</span>
            </div>
          </div>
        </div>
      </section>

      {/* ── Filters ── */}
      <section className="stl-filters">
        <div className="stl-search">
          <input
            className="stl-search__input"
            type="text"
            placeholder="Tìm theo tên giải đấu, địa điểm..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </div>
        <div className="stl-status-tabs">
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.value}
              className={`stl-status-tab${
                statusFilter === f.value ? " stl-status-tab--active" : ""
              }`}
              onClick={() => setStatusFilter(f.value)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </section>

      {/* ── Content ── */}
      <section className="stl-content">
        {isLoading ? (
          <div className="stl-empty">
            <h3>Đang tải giải đấu</h3>
            <p>Vui lòng đợi trong giây lát...</p>
          </div>
        ) : errorMessage ? (
          <div className="stl-empty stl-empty--error">
            <h3>Không thể tải giải đấu</h3>
            <p>{errorMessage}</p>
            <button
              className="stl-btn stl-btn--primary"
              onClick={() => window.location.reload()}
            >
              Thử lại
            </button>
          </div>
        ) : pageItems.length === 0 ? (
          <div className="stl-empty">
            <h3>Không tìm thấy giải đấu</h3>
            <p>Thử thay đổi từ khóa hoặc bộ lọc trạng thái.</p>
            <button
              className="stl-btn stl-btn--ghost"
              onClick={() => {
                setQuery("");
                setStatusFilter("all");
              }}
            >
              Xóa bộ lọc
            </button>
          </div>
        ) : (
          <div className="stl-grid">
            {pageItems.map((t) => {
              const meta = STATUS_META[t.status] || STATUS_META.upcoming;
              return (
                <article key={t.id} className="stl-card">
                  <div
                    className="stl-card__banner"
                    style={
                      (t.imageUrl ?? t.ImageUrl)
                        ? { backgroundImage: `url(${t.imageUrl ?? t.ImageUrl})`, backgroundSize: "cover", backgroundPosition: "center" }
                        : {}
                    }
                  >
                    <span className={`stl-badge ${meta.className}`}>
                      {meta.label}
                    </span>
                    {t.category && (
                      <span className="stl-badge stl-badge--category">
                        {CATEGORY_LABELS[t.category.toLowerCase()] ||
                          t.category}
                      </span>
                    )}
                  </div>
                  <div className="stl-card__body">
                    <h3 className="stl-card__title">{t.name}</h3>
                    <div className="stl-card__venue">
                      <span>{t.venue}</span>
                    </div>
                    {t.description && (
                      <p className="stl-card__desc">{t.description}</p>
                    )}
                    <div className="stl-card__stats">
                      <div className="stl-card__stat">
                        <span className="stl-card__stat-value">
                          {t.roundCount}
                        </span>
                        <span className="stl-card__stat-label">Vòng đua</span>
                      </div>
                      <div className="stl-card__stat">
                        <span className="stl-card__stat-value">
                          {t.raceCount}
                        </span>
                        <span className="stl-card__stat-label">Cuộc đua</span>
                      </div>
                      <div className="stl-card__stat">
                        <span className="stl-card__stat-value">
                          {formatPrizePool(t.prizePool)}
                        </span>
                        <span className="stl-card__stat-label">Giải thưởng</span>
                      </div>
                    </div>
                    <div className="stl-card__dates">
                      <span>
                        {formatDate(t.startDate)} – {formatDate(t.endDate)}
                      </span>
                    </div>
                  </div>
                  <div className="stl-card__actions">
                    <button
                      className="stl-btn stl-btn--ghost"
                      onClick={() => setDetailTournament(t)}
                    >
                      Xem chi tiết
                    </button>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>

      {/* ── Pagination ── */}
      {!isLoading && !errorMessage && totalPages > 1 && (
        <nav className="stl-pagination">
          <button
            className="stl-btn stl-btn--ghost"
            onClick={handlePrevPage}
            disabled={safePage === 1}
          >
            Trước
          </button>
          <div className="stl-pagination__numbers">
            {renderPageNumbers().map((p) => (
              <button
                key={p}
                className={`stl-pagination__num${
                  p === safePage ? " stl-pagination__num--active" : ""
                }`}
                onClick={() => setCurrentPage(p)}
              >
                {p}
              </button>
            ))}
          </div>
          <button
            className="stl-btn stl-btn--ghost"
            onClick={handleNextPage}
            disabled={safePage === totalPages}
          >
            Tiếp
          </button>
        </nav>
      )}

      {/* ── Detail Modal ── */}
      {detailTournament && (
        <div
          className="stl-modal-overlay"
          role="dialog"
          aria-modal="true"
          onClick={(e) => {
            if (e.target === e.currentTarget) setDetailTournament(null);
          }}
        >
          <div className="stl-modal">
            <div className="stl-modal__header">
              <div>
                <span
                  className={`stl-badge ${
                    STATUS_META[detailTournament.status]?.className || ""
                  }`}
                >
                  {STATUS_META[detailTournament.status]?.label || ""}
                </span>
                <h2>{detailTournament.name}</h2>
              </div>
              <button
                className="stl-modal__close"
                onClick={() => setDetailTournament(null)}
                aria-label="Đóng"
              >
              </button>
            </div>
            <div className="stl-modal__grid">
              <div className="stl-modal__field">
                <span className="stl-modal__label">Địa điểm</span>
                <span className="stl-modal__value">{detailTournament.venue}</span>
              </div>
              <div className="stl-modal__field">
                <span className="stl-modal__label">Hạng mục</span>
                <span className="stl-modal__value">
                  {detailTournament.category || "Chưa xác định"}
                </span>
              </div>
              <div className="stl-modal__field">
                <span className="stl-modal__label">Số vòng đua</span>
                <span className="stl-modal__value">{detailTournament.roundCount}</span>
              </div>
              <div className="stl-modal__field">
                <span className="stl-modal__label">Tổng cuộc đua</span>
                <span className="stl-modal__value">{detailTournament.raceCount}</span>
              </div>
              <div className="stl-modal__field">
                <span className="stl-modal__label">Giải thưởng</span>
                <span className="stl-modal__value">
                  {formatPrizePool(detailTournament.prizePool)}
                </span>
              </div>
              <div className="stl-modal__field">
                <span className="stl-modal__label">Thời gian</span>
                <span className="stl-modal__value">
                  {formatDate(detailTournament.startDate)} –{" "}
                  {formatDate(detailTournament.endDate)}
                </span>
              </div>
            </div>
            {detailTournament.description && (
              <div className="stl-modal__desc">
                <span className="stl-modal__label">Mô tả</span>
                <p>{detailTournament.description}</p>
              </div>
            )}
            <div className="stl-modal__actions">
              <button
                className="stl-btn stl-btn--primary"
                onClick={() => setDetailTournament(null)}
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

export default SpectatorTournamentListPage;
