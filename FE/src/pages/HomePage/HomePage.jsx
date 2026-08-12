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
      {/* ── Hero Banner ── */}
      <section className="hero-banner">
        <div className="hero-banner__bg">
          <img src={heroImage} alt="" />
          <div className="hero-banner__overlay" />
        </div>
        <div className="hero-banner__content">
          <span className="hero-banner__badge">🏆 Nền Tảng Đua Ngựa Hàng Đầu</span>
          <h1>Chinh Phục Đường Đua<br />Cùng RaceMaster</h1>
          <p>
            Nền tảng toàn diện cho Chủ Ngựa, Kỵ Sĩ và Khán Giả — quản lý giải đấu,
            theo dõi kết quả trực tiếp và kết nối cộng đồng đua ngựa chuyên nghiệp.
          </p>
          <div className="hero-banner__actions">
            <Link to="/register" className="hero-btn hero-btn--primary">Tham Gia Ngay</Link>
            <Link to="/tournaments" className="hero-btn hero-btn--outline">Khám Phá Giải Đấu</Link>
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
          <span className="section-tag">Giải Đấu</span>
          <h2>Giải Đấu Nổi Bật</h2>
          <p>Những giải đấu danh giá nhất đang chờ đón bạn và chiến mã của mình.</p>
        </div>
        <div className="tournaments-grid">
          {tournaments.length === 0 ? (
            <p className="muted">Chưa có giải đấu nào.</p>
          ) : tournaments.slice(0, 3).map((t) => {
            const statusKey = (t.statusName ?? t.StatusName ?? "draft").toLowerCase();
            const statusLabel = TOURNAMENT_STATUS[statusKey] ?? t.statusName ?? "Đang mở";
            const raceCount = t.raceCount ?? t.RaceCount ?? t.stats?.raceCount ?? 0;
            return (
              <div key={t.id ?? t.Id} className="tournament-card">
                <div className="tournament-card__header">
                  <span className={`tournament-status ${statusKey === "ongoing" || statusKey === "started" ? "tournament-status--live" : "tournament-status--upcoming"}`}>
                    {statusLabel}
                  </span>
                  <span className="tournament-category">{t.startDate ? new Date(t.startDate).toLocaleDateString("vi-VN") : "—"}</span>
                </div>
                <h3>{t.name ?? t.Name}</h3>
                <div className="tournament-card__meta">
                  <span>🏁 {raceCount} cuộc đua</span>
                </div>
                <p className="tournament-card__desc">{t.description ?? t.Description ?? "Không có mô tả."}</p>
                <Link to="/tournaments" className="tournament-card__link">Xem chi tiết →</Link>
              </div>
            );
          })}
        </div>
      </section>

      {/* ── Bộ sưu tập ảnh (Marquee) ── */}
      <section className="marquee-section">
        <div className="section-header">
          <span className="section-tag">Thư Viện</span>
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
          <span className="section-tag">Bảng Xếp Hạng</span>
          <h2>Top Kỵ Sĩ & Chủ Ngựa</h2>
          <p>Những cá nhân xuất sắc nhất trên đường đua mùa giải này.</p>
        </div>
        <div className="leader-grid">
          {/* Kỵ Sĩ */}
          <div className="leader-panel">
            <h3>🏇 Top Kỵ Sĩ</h3>
            <div className="leader-list">
              {topJockeys.length === 0 ? (
                <p className="muted">Chưa có dữ liệu.</p>
              ) : topJockeys.map((j, idx) => (
                <div key={j.id ?? j.name} className="leader-card">
                  <span className="leader-card__rank">#{idx + 1}</span>
                  <div className="leader-card__info">
                    <strong>{j.name}</strong>
                    <span>{j.totalRaces ?? 0} cuộc đua · {j.wins ?? 0} thắng · Tỷ lệ {j.winRate ?? 0}%</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
          {/* Chủ Ngựa */}
          <div className="leader-panel">
            <h3>🐎 Top Chủ Ngựa</h3>
            <div className="leader-list">
              {topOwners.length === 0 ? (
                <p className="muted">Chưa có dữ liệu.</p>
              ) : topOwners.map((o, idx) => (
                <div key={o.name} className="leader-card">
                  <span className="leader-card__rank">#{idx + 1}</span>
                  <div className="leader-card__info">
                    <strong>{o.name}</strong>
                    <span>{o.horses} ngựa · {o.wins} thắng · {o.entries} lượt đua</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ── Kết quả thi đấu gần đây ── */}
      <section className="results-section">
        <div className="section-header">
          <span className="section-tag">Kết Quả</span>
          <h2>Kết Quả Thi Đấu Gần Đây</h2>
          <p>Cập nhật kết quả mới nhất từ các cuộc đua đã hoàn thành.</p>
        </div>
        {recentRaces.length === 0 ? (
          <p className="muted">Chưa có cuộc đua nào hoàn thành.</p>
        ) : (
          <div className="results-table-wrap">
            <table className="results-table">
              <thead>
                <tr>
                  <th>Cuộc đua</th>
                  <th>Địa điểm</th>
                  <th>Cự ly</th>
                  <th>Thời gian</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {recentRaces.map((r) => (
                  <tr key={r.id ?? r.Id}>
                    <td className="results-race-name">{r.name ?? r.Name}</td>
                    <td>{r.location ?? r.Location ?? "—"}</td>
                    <td>{r.distance ?? r.Distance ?? "—"}m</td>
                    <td className="results-time">{formatTime(r.scheduledAt ?? r.ScheduledAt)}</td>
                    <td>
                      <Link to="/live-results" className="results-more-link">Kết quả →</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Link to="/live-results" className="results-more">Xem tất cả kết quả →</Link>
          </div>
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
