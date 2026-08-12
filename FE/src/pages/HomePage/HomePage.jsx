import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getActiveTournaments, getRaces } from "../../services/spectatorApi";
import { request } from "../../services/apiClient";
import heroImage from "../../assets/racing.png";
import homeOneImage from "../../assets/home1.png";
import homeTwoImage from "../../assets/home2.png";
import jockeyImage from "../../assets/Jockey.png";
import "./HomePage.css";

const MARQUEE_IMAGES = [heroImage, homeOneImage, homeTwoImage, jockeyImage];

const TOURNAMENT_STATUS = {
  draft: "Bản nháp",
  published: "Đã công bố",
  ongoing: "Đang diễn ra",
  started: "Đang diễn ra",
  finished: "Đã kết thúc",
  cancelled: "Đã hủy",
};

function HomePage() {
  const [tournaments, setTournaments] = useState([]);
  const [races, setRaces] = useState([]);
  const [topJockeys, setTopJockeys] = useState([]);
  const [topOwners, setTopOwners] = useState([]);
  const [jockeyCount, setJockeyCount] = useState(0);
  const [ownerCount, setOwnerCount] = useState(0);
  const [recentRaces, setRecentRaces] = useState([]);

  const formatTime = (value) =>
    value
      ? new Date(value).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" })
      : "Chưa xác định";

  useEffect(() => {
    Promise.all([
      getActiveTournaments().catch(() => []),
      getRaces().catch(() => []),
      request("/api/leaderboard/jockeys").catch(() => []),
      request("/api/leaderboard/horses").catch(() => []),
    ]).then(([t, r, j, h]) => {
      const tournamentList = Array.isArray(t) ? t : [];
      const raceList = Array.isArray(r) ? r : [];
      const jockeyList = Array.isArray(j) ? j : [];
      const horseList = Array.isArray(h) ? h : [];

      setTournaments(tournamentList);
      setRaces(raceList);
      setJockeyCount(jockeyList.length);
      setTopJockeys(jockeyList.slice(0, 3));

      // Gộp xếp hạng ngựa theo chủ sở hữu
      const ownerMap = new Map();
      horseList.forEach((horse) => {
        const name = horse.ownerName || "Chưa xác định";
        if (!ownerMap.has(name)) ownerMap.set(name, { name, horses: 0, wins: 0, entries: 0 });
        const o = ownerMap.get(name);
        o.horses += 1;
        o.wins += Number(horse.wins || 0);
        o.entries += Number(horse.totalRaces || 0);
      });
      setOwnerCount(ownerMap.size);
      setTopOwners([...ownerMap.values()].sort((a, b) => b.wins - a.wins).slice(0, 3));

      const finished = raceList
        .filter((x) => (x.status ?? x.Status) === "Finished")
        .sort((a, b) => new Date(b.scheduledAt ?? b.ScheduledAt ?? 0) - new Date(a.scheduledAt ?? a.ScheduledAt ?? 0))
        .slice(0, 5);
      setRecentRaces(finished);
    });
  }, []);

  const stats = [
    { value: tournaments.length, label: "Giải đấu đang hoạt động" },
    { value: races.length, label: "Cuộc đua" },
    { value: jockeyCount, label: "Kỵ sĩ" },
    { value: ownerCount, label: "Chủ ngựa" },
  ];

  return (
    <div className="home-page">
      {/* ── Cinematic Hero ── */}
      <section className="legacy-hero">
        <div className="legacy-hero__background" aria-hidden="true" />

        <div className="legacy-hero__shade" aria-hidden="true" />

        <div className="legacy-hero__inner">
          <div className="legacy-hero__copy">
            <h1 className="legacy-hero__title">
              Điều hành cả
              <br />
              mùa giải
              <br />
              trong <em>một</em>
              <br />
              <em>nền tảng</em>
            </h1>

            <div className="legacy-hero__eyebrow">
              <span />
              PHOTO FINISH
            </div>

            <p className="legacy-hero__description">
              Quản lý lịch thi đấu, đăng ký ngựa và jockey, chấm kết quả và
              công bố bảng xếp hạng — tất cả theo thời gian thực, cho mọi vai
              trò từ ban tổ chức đến khán giả.
            </p>

            <div className="legacy-hero__actions">
              <Link to="/tournaments" className="legacy-hero__primary">
                Khám phá giải đấu
              </Link>

              <Link to="/live-results" className="legacy-hero__secondary">
                Xem kết quả →
              </Link>
            </div>
          </div>

          <div className="legacy-hero__visual">
            <article className="legacy-race-card">
              <img
                src="/images/home-legacy/RaceCard.jpg"
                alt=""
                className="legacy-race-card__image"
              />
              <div className="legacy-race-card__scrim" aria-hidden="true" />

              <div className="legacy-race-card__content">
                <span className="legacy-race-card__eyebrow">Sắp diễn ra</span>
                <h2 className="legacy-race-card__title">Những cuộc đua đang chờ bạn</h2>
                <Link to="/tournaments" className="legacy-race-card__cta">
                  <span>Xem lịch</span>
                  <span className="legacy-race-card__arrow" aria-hidden="true">→</span>
                </Link>
              </div>
            </article>

            <article className="legacy-ranking-card">
              <div className="legacy-ranking-card__media">
                <img
                  src="/images/home-legacy/RankingCard.jpg"
                  alt=""
                />
              </div>

              <div className="legacy-ranking-card__content">
                <span className="legacy-ranking-card__eyebrow">Bảng xếp hạng</span>
                <Link to="/live-results" className="legacy-ranking-card__row">
                  <span className="legacy-ranking-card__rank">01</span>
                  <span className="legacy-ranking-card__label">Dẫn đầu mùa giải</span>
                  <span className="legacy-ranking-card__arrow" aria-hidden="true">→</span>
                </Link>
              </div>
            </article>
          </div>
        </div>
      </section>

      {/* ── Thống kê hệ thống ── */}
      <section className="stats-bar">
        <div className="stats-bar__grid">
          {stats.map((s) => (
            <div key={s.label} className="stat-item">
              <span className="stat-item__value">{s.value}</span>
              <span className="stat-item__label">{s.label}</span>
            </div>
          ))}
        </div>
      </section>

      {/* ── Giải đấu nổi bật ── */}
      <section className="tournaments-section">
        <div className="section-header">
          <span className="section-eyebrow">Giải Đấu</span>
          <h2>Giải Đấu Nổi Bật</h2>
          <p>Những giải đấu danh giá nhất đang chờ đón bạn và chiến mã của mình.</p>
        </div>
        <div className="tournaments-list">
          {tournaments.length === 0 ? (
            <p className="muted">Chưa có giải đấu nào.</p>
          ) : tournaments.slice(0, 3).map((t) => {
            const statusKey = (t.statusName ?? t.StatusName ?? "draft").toLowerCase();
            const statusLabel = TOURNAMENT_STATUS[statusKey] ?? t.statusName ?? "Đang mở";
            const raceCount = t.raceCount ?? t.RaceCount ?? t.stats?.raceCount ?? 0;
            // Presentation-only date breakdown derived from the same startDate value.
            const startDate = t.startDate ? new Date(t.startDate) : null;
            const dateDay = startDate ? String(startDate.getDate()).padStart(2, "0") : "—";
            const dateMonth = startDate ? startDate.toLocaleDateString("vi-VN", { month: "short" }) : "";
            const dateYear = startDate ? startDate.getFullYear() : "";
            return (
              <article key={t.id ?? t.Id} className="tournament-feature">
                <div className="tournament-feature__date">
                  <span className="tournament-feature__day">{dateDay}</span>
                  <span className="tournament-feature__month">{dateMonth}</span>
                  <span className="tournament-feature__year">{dateYear || "—"}</span>
                  <span
                    className={`tournament-feature__status ${
                      statusKey === "ongoing" || statusKey === "started"
                        ? "tournament-feature__status--live"
                        : "tournament-feature__status--upcoming"
                    }`}
                  >
                    {statusLabel}
                  </span>
                </div>

                <div className="tournament-feature__body">
                  <h3 className="tournament-feature__title">{t.name ?? t.Name}</h3>
                  <p className="tournament-feature__desc">{t.description ?? t.Description ?? "Không có mô tả."}</p>
                  <span className="tournament-feature__races">{String(raceCount).padStart(2, "0")} cuộc đua</span>
                  <Link to="/tournaments" className="tournament-feature__cta">
                    <span>Khám phá giải đấu</span>
                    <span className="tournament-feature__arrow" aria-hidden="true">→</span>
                  </Link>
                </div>

                <div className="tournament-feature__image" aria-hidden="true" />
              </article>
            );
          })}
        </div>
      </section>

      {/* ── Bộ sưu tập ảnh (Marquee) ── */}
      <section className="marquee-section">
        <div className="section-header">
          <span className="section-eyebrow">Thư Viện</span>
          <h2>Khoảnh Khắc Đường Đua</h2>
        </div>
        <div className="marquee-track">
          <div className="marquee-slide">
            {[...MARQUEE_IMAGES, ...MARQUEE_IMAGES].map((img, i) => (
              <div key={i} className="marquee-item">
                <img src={img} alt="" />
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── Top Kỵ Sĩ & Top Chủ Ngựa ── */}
      <section className="leader-section">
        <div className="section-header">
          <span className="section-eyebrow">Bảng Xếp Hạng</span>
          <h2>Top Kỵ Sĩ & Chủ Ngựa</h2>
          <p>Những cá nhân xuất sắc nhất trên đường đua mùa giải này.</p>
        </div>
        <div className="standings">
          {/* Kỵ Sĩ */}
          <div className="standings__column">
            <h3 className="standings__heading">Top Kỵ Sĩ</h3>
            {topJockeys.length === 0 ? (
              <p className="muted">Chưa có dữ liệu.</p>
            ) : (
              <div className="standings__list">
                {topJockeys.map((j, idx) => (
                  <div key={j.id ?? j.name} className="standings__row">
                    <span className="standings__rank">{String(idx + 1).padStart(2, "0")}</span>
                    <span className="standings__name">{j.name}</span>
                    <span className="standings__stat">
                      {j.totalRaces ?? 0} cuộc đua · {j.wins ?? 0} thắng · {j.winRate ?? 0}%
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
          {/* Chủ Ngựa */}
          <div className="standings__column">
            <h3 className="standings__heading">Top Chủ Ngựa</h3>
            {topOwners.length === 0 ? (
              <p className="muted">Chưa có dữ liệu.</p>
            ) : (
              <div className="standings__list">
                {topOwners.map((o, idx) => (
                  <div key={o.name} className="standings__row">
                    <span className="standings__rank">{String(idx + 1).padStart(2, "0")}</span>
                    <span className="standings__name">{o.name}</span>
                    <span className="standings__stat">
                      {o.horses} ngựa · {o.wins} thắng · {o.entries} lượt đua
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </section>

      {/* ── Kết quả thi đấu gần đây ── */}
      <section className="results-section">
        <div className="section-header">
          <span className="section-eyebrow">Kết Quả</span>
          <h2>Kết Quả Thi Đấu Gần Đây</h2>
          <p>Cập nhật kết quả mới nhất từ các cuộc đua đã hoàn thành.</p>
        </div>
        {recentRaces.length === 0 ? (
          <p className="muted">Chưa có cuộc đua nào hoàn thành.</p>
        ) : (
          <>
            <div className="results-list">
              {recentRaces.map((r) => (
                <article key={r.id ?? r.Id} className="results-row">
                  <div className="results-row__main">
                    <h3 className="results-row__title">{r.name ?? r.Name}</h3>
                    <p className="results-row__meta">
                      {r.location ?? r.Location ?? "—"} · {r.distance ?? r.Distance ?? "—"}m · {formatTime(r.scheduledAt ?? r.ScheduledAt)}
                    </p>
                  </div>
                  <Link to="/live-results" className="results-row__link">Kết quả →</Link>
                </article>
              ))}
            </div>
            <Link to="/live-results" className="results-more">Xem tất cả kết quả →</Link>
          </>
        )}
      </section>

      {/* ── CTA Đăng ký ── */}
      <section className="register-cta">
        <div className="register-cta__card">
          <h2>Bắt Đầu Hành Trình Của Bạn</h2>
          <p>Đăng ký ngay hôm nay để tham gia vào thế giới đua ngựa chuyên nghiệp.</p>
          <div className="register-cta__buttons">
            <Link to="/register/horse-owner" className="hero-btn hero-btn--primary">Đăng Ký Chủ Ngựa</Link>
            <Link to="/register/jockey" className="hero-btn hero-btn--primary">Đăng Ký Kỵ Sĩ</Link>
            <Link to="/register" className="hero-btn hero-btn--outline">Đăng Ký Khán Giả</Link>
          </div>
        </div>
      </section>
    </div>
  );
}

export default HomePage;
