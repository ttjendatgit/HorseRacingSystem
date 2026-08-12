import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  formatJockeyDate,
  getJockeyAssignedRaces,
  getJockeyInvitations,
  normalizeInvitationStatus,
} from "../../services/jockeyApi";
import { getProfile } from "../../services/authApi";
import "./JockeyDashboardPage.css";

function JockeyDashboardPage() {
  const [races, setRaces] = useState([]);
  const [invitations, setInvitations] = useState([]);
  const [profile, setProfile] = useState(null);
  const [, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    let cancelled = false;

    const loadDashboard = async () => {
      try {
        setLoading(true);
        const [raceData, invitationData, profileData] = await Promise.all([
          getJockeyAssignedRaces(),
          getJockeyInvitations(),
          getProfile().then(d => d?.data ?? d).catch(() => null),
        ]);

        if (!cancelled) {
          setRaces(raceData);
          setInvitations(invitationData);
          setProfile(profileData);
          setErrorMessage("");
        }
      } catch (error) {
        if (!cancelled) {
          setErrorMessage(error.message || "Không thể tải dữ liệu.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    loadDashboard();
    return () => { cancelled = true; };
  }, []);

  const sortedRaces = useMemo(
    () => [...races].sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()),
    [races],
  );

  const nextRace = sortedRaces[0];
  const totalRacesNum = races.length;
  const confirmedRaces = races.filter(r => r.jockeyConfirmed).length;
  const pendingCount = invitations.filter(i => normalizeInvitationStatus(i.status).toLowerCase() === "pending").length;
  const winRate = profile?.winRate ?? profile?.WinRate ?? 0;
  const rank = profile?.rank ?? profile?.Rank ?? null;

  return (
    <div className="jockey-dashboard">
      {/* Hero Banner */}
      <section className="jd-hero" style={{ backgroundImage: "url('/src/assets/racing.png')" }}>
        <div className="jd-hero__overlay" />
        <div className="jd-hero__content">
          <div>
            <span className="pill" style={{ background: "rgba(215,170,77,0.2)", color: "#f2d28b" }}>Kỵ sĩ</span>
            <h1>Bảng điều khiển</h1>
            <p>Theo dõi lịch đua, quản lý lời mời và kiểm tra thành tích của bạn.</p>
          </div>
          <div className="jd-hero__stats">
            <div>
              <span>Cuộc đua</span>
              <strong>{totalRacesNum}</strong>
            </div>
            <div>
              <span>Đã xác nhận</span>
              <strong>{confirmedRaces}</strong>
            </div>
            <div>
              <span>Tỉ lệ thắng</span>
              <strong>{winRate}%</strong>
            </div>
            <div>
              <span>Lời mời</span>
              <strong>{pendingCount}</strong>
            </div>
          </div>
        </div>
      </section>

      {errorMessage && <p className="jd-error">{errorMessage}</p>}

      {/* Quick Actions + Next Race */}
      <div className="jd-cols">
        <div className="jd-card">
          <h3>Cuộc đua tiếp theo</h3>
          {nextRace ? (
            <div className="jd-next-race">
              <h4>{nextRace.title}</h4>
              <p className="jd-race-meta">
                {formatJockeyDate(nextRace.scheduledAt, "Chưa lên lịch")}
                {nextRace.location ? ` · ${nextRace.location}` : ""}
              </p>
              <p className="jd-race-meta">
                Ngựa: <strong>{nextRace.horseName || "Chưa phân công"}</strong>
              </p>
              <span className={`jd-badge ${nextRace.jockeyConfirmed ? "jd-badge--ok" : "jd-badge--warn"}`}>
                {nextRace.jockeyConfirmed ? "Đã xác nhận" : "Chờ xác nhận"}
              </span>
            </div>
          ) : (
            <p className="muted">Chưa có cuộc đua nào được phân công.</p>
          )}
          <div className="jd-actions">
            <Link to="/jockey/schedule" className="jd-btn">Lịch đua</Link>
            <Link to="/jockey/invitations" className="jd-btn jd-btn--outline">Lời mời</Link>
          </div>
        </div>

        {/* Upcoming Races */}
        <div className="jd-card jd-card--wide">
          <h3>Lịch sắp tới</h3>
          {sortedRaces.slice(0, 4).length > 0 ? (
            <div className="jd-race-list">
              {sortedRaces.slice(0, 4).map(race => (
                <div key={race.id} className="jd-race-row">
                  <div>
                    <strong>{race.title}</strong>
                    <p>{race.tournamentName || ""}{race.horseName ? ` · ${race.horseName}` : ""}</p>
                  </div>
                  <div className="jd-race-right">
                    <span className="jd-date">{formatJockeyDate(race.scheduledAt, "")}</span>
                    <span className={`jd-badge ${race.jockeyConfirmed ? "jd-badge--ok" : "jd-badge--warn"}`}>
                      {race.jockeyConfirmed ? "Đã xác nhận" : "Chờ"}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="muted">Chưa có cuộc đua nào.</p>
          )}
        </div>
      </div>

      {/* Invitations + Stats */}
      <div className="jd-cols">
        <div className="jd-card">
          <h3>Lời mời đang chờ</h3>
          {invitations.filter(i => i.status === "Pending" || i.status === "pending").length > 0 ? (
            <div className="jd-invite-list">
              {invitations.filter(i => i.status === "Pending" || i.status === "pending").slice(0, 3).map(inv => (
                <div key={inv.id} className="jd-invite-row">
                  <div>
                    <strong>{inv.raceName}</strong>
                    <p>Ngựa: {inv.horseName}</p>
                  </div>
                  <Link to={`/jockey/invitations/${inv.id}`} className="jd-btn jd-btn--sm">Xem</Link>
                </div>
              ))}
            </div>
          ) : (
            <p className="muted">Không có lời mời đang chờ.</p>
          )}
          <Link to="/jockey/invitations" className="jd-link">Xem tất cả lời mời →</Link>
        </div>

        <div className="jd-card">
          <h3>Thành tích</h3>
          <div className="jd-stats">
            <div className="jd-stat">
              <span>Hạng</span>
              <strong>#{rank ?? "--"}</strong>
            </div>
            <div className="jd-stat">
              <span>Tỉ lệ thắng</span>
              <strong>{winRate}%</strong>
            </div>
            <div className="jd-stat">
              <span>Tổng số cuộc đua</span>
              <strong>{totalRacesNum}</strong>
            </div>
            <div className="jd-stat">
              <span>Đã xác nhận</span>
              <strong>{confirmedRaces}</strong>
            </div>
            <div className="jd-stat">
              <span>Đang chờ</span>
              <strong>{pendingCount}</strong>
            </div>
          </div>
          <Link to="/jockey/performance" className="jd-link">Xem chi tiết →</Link>
        </div>
      </div>
    </div>
  );
}

export default JockeyDashboardPage;
