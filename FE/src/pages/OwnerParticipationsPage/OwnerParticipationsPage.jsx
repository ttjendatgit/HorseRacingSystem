import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getMyTournamentRegistrations, getMyRaceEntries } from "../../services/ownerHorseApi";
import { apiToVNDisplay } from "../../utils/vnDateTime";
import "../OwnerSharedLayout.css";
import "./OwnerParticipationsPage.css";

const registrationStatusLabel = (status) => {
  if (status === "Approved") return "Đã duyệt";
  if (status === "Rejected") return "Từ chối";
  if (status === "Withdrawn") return "Đã rút";
  return "Chờ duyệt";
};

// Task C1 §3: bucketing is driven ONLY by the Tournament's own lifecycle status (Published /
// Ongoing / Finished) plus the registration's own status — never by inferring or fabricating a
// stage from anything else.
const bucketOf = (registration) => {
  const t = registration.tournamentStatus;
  const s = registration.statusRaw;
  if (t === "Published" && (s === "Pending" || s === "Approved")) return "upcoming";
  if (t === "Ongoing") return "ongoing";
  if (t === "Finished") return "finished";
  return "other";
};

function OwnerParticipationsPage() {
  const [registrations, setRegistrations] = useState([]);
  const [entries, setEntries] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let isMounted = true;
    const load = async () => {
      setIsLoading(true);
      setError("");
      try {
        const [regData, entryData] = await Promise.all([
          getMyTournamentRegistrations(),
          getMyRaceEntries(),
        ]);
        if (!isMounted) return;
        setRegistrations(
          (Array.isArray(regData) ? regData : []).map((r) => {
            const status = r.status ?? r.Status ?? "Pending";
            return {
              id: r.id ?? r.Id,
              horseId: r.horseId ?? r.HorseId,
              horseName: r.horseName ?? r.HorseName ?? "Không rõ",
              tournamentId: r.tournamentId ?? r.TournamentId,
              tournamentName: r.tournamentName ?? r.TournamentName ?? "Giải đấu",
              tournamentStatus: r.tournamentStatus ?? r.TournamentStatus ?? "",
              tournamentStartDate: r.tournamentStartDate ?? r.TournamentStartDate,
              tournamentEndDate: r.tournamentEndDate ?? r.TournamentEndDate,
              statusRaw: status,
              statusLabel: registrationStatusLabel(status),
            };
          }),
        );
        setEntries(
          (Array.isArray(entryData) ? entryData : []).map((e) => ({
            entryId: e.entryId ?? e.EntryId,
            horseId: e.horseId ?? e.HorseId,
            tournamentId: e.tournamentId ?? e.TournamentId,
            raceId: e.raceId ?? e.RaceId,
            raceName: e.raceName ?? e.RaceName ?? "Cuộc đua",
            roundNumber: e.roundNumber ?? e.RoundNumber,
            roundName: e.roundName ?? e.RoundName,
            scheduledAt: e.scheduledAt ?? e.ScheduledAt,
            raceStatus: e.raceStatus ?? e.RaceStatus ?? "",
            jockeyName: e.jockeyName ?? e.JockeyName ?? "",
            finishPosition: e.finishPosition ?? e.FinishPosition ?? null,
          })),
        );
      } catch (err) {
        if (isMounted) setError(err?.message || "Không thể tải danh sách tham gia.");
      } finally {
        if (isMounted) setIsLoading(false);
      }
    };
    load();
    return () => {
      isMounted = false;
    };
  }, []);

  const entriesFor = (tournamentId, horseId) =>
    entries.filter(
      (e) => String(e.tournamentId) === String(tournamentId) && String(e.horseId) === String(horseId),
    );

  const groups = useMemo(() => {
    const result = { upcoming: [], ongoing: [], finished: [], other: [] };
    registrations.forEach((registration) => {
      result[bucketOf(registration)].push(registration);
    });
    return result;
  }, [registrations]);

  const renderRaceEntry = (entry) => (
    <div key={entry.entryId} className="op-entry">
      <div className="op-entry-row">
        <span>Cuộc đua</span>
        <strong>{entry.raceName}</strong>
      </div>
      {entry.roundNumber != null || entry.roundName ? (
        <div className="op-entry-row">
          <span>Vòng đấu</span>
          <strong>{entry.roundName || `Vòng ${entry.roundNumber}`}</strong>
        </div>
      ) : null}
      <div className="op-entry-row">
        <span>Lịch thi đấu</span>
        <strong>{apiToVNDisplay(entry.scheduledAt) || "Chưa xếp lịch"}</strong>
      </div>
      <div className="op-entry-row">
        <span>Kỵ sĩ hiện tại</span>
        <strong>{entry.jockeyName || "Chưa có kỵ sĩ"}</strong>
      </div>
      {/* Task C1 §3: never fabricate a rank — FinishPosition is only populated for horses with a
          real recorded result. Everyone else gets an honest placeholder. */}
      <div className="op-entry-row">
        <span>Kết quả</span>
        <strong>
          {entry.finishPosition != null ? `Hạng ${entry.finishPosition}` : "Chưa có thứ hạng đầy đủ"}
        </strong>
      </div>
    </div>
  );

  const renderRegistrationCard = (registration, { showEntries }) => {
    const relatedEntries = showEntries
      ? entriesFor(registration.tournamentId, registration.horseId)
      : [];
    return (
      <article key={registration.id} className="op-card">
        <div className="op-card-header">
          <div>
            <span>Giải đấu</span>
            <strong>{registration.tournamentName}</strong>
          </div>
          <div>
            <span>Ngựa</span>
            <strong>{registration.horseName}</strong>
          </div>
          <div>
            <span>Trạng thái đăng ký</span>
            <strong
              className={`registration-status-pill registration-status-pill--${registration.statusLabel
                .toLowerCase()
                .replace(/\s+/g, "-")}`}
            >
              {registration.statusLabel}
            </strong>
          </div>
        </div>
        <div className="op-card-dates">
          {apiToVNDisplay(registration.tournamentStartDate) || "Chưa xác định"} -{" "}
          {apiToVNDisplay(registration.tournamentEndDate) || "Chưa xác định"}
        </div>
        {showEntries ? (
          relatedEntries.length > 0 ? (
            <div className="op-entries">{relatedEntries.map(renderRaceEntry)}</div>
          ) : (
            <p className="muted">Chưa được phân công vào cuộc đua nào trong giải này.</p>
          )
        ) : null}
      </article>
    );
  };

  const sections = [
    {
      key: "upcoming",
      title: "Sắp diễn ra",
      hint: "Giải đấu đã công bố, đăng ký đang chờ duyệt hoặc đã được duyệt.",
      items: groups.upcoming,
      showEntries: false,
    },
    {
      key: "ongoing",
      title: "Đang diễn ra",
      hint: "Giải đấu đang diễn ra — cuộc đua được phân công (nếu có) hiển thị bên dưới.",
      items: groups.ongoing,
      showEntries: true,
    },
    {
      key: "finished",
      title: "Đã kết thúc",
      hint: "Giải đấu đã kết thúc — chỉ hiển thị dữ liệu thực tế đã ghi nhận.",
      items: groups.finished,
      showEntries: true,
    },
    {
      key: "other",
      title: "Khác",
      hint: "Đăng ký bị từ chối, đã rút, hoặc thuộc giải đấu ở trạng thái khác.",
      items: groups.other,
      showEntries: false,
    },
  ];

  return (
    <div className="owner-page owner-participations">
      <div className="owner-content">
        <section className="page-header">
          <h1>Tham gia của tôi</h1>
          <p>Theo dõi tất cả giải đấu bạn đã đăng ký, theo từng giai đoạn.</p>
        </section>

        {error ? <p className="form-error">{error}</p> : null}

        {isLoading ? (
          <p className="muted">Đang tải...</p>
        ) : registrations.length === 0 ? (
          <div className="op-empty">
            <p>Bạn chưa đăng ký giải đấu nào.</p>
            <Link className="primary-button" to="/owner/register-tournament">
              Đăng ký giải đấu
            </Link>
          </div>
        ) : (
          sections.map((section) => (
            <section key={section.key} className="op-section">
              <div className="section-heading">
                <h2>
                  {section.title} <span className="op-count">({section.items.length})</span>
                </h2>
                <p>{section.hint}</p>
              </div>
              {section.items.length === 0 ? (
                <p className="muted">Không có mục nào.</p>
              ) : (
                <div className="op-grid">
                  {section.items.map((registration) =>
                    renderRegistrationCard(registration, { showEntries: section.showEntries }),
                  )}
                </div>
              )}
            </section>
          ))
        )}
      </div>
    </div>
  );
}

export default OwnerParticipationsPage;
