import { useEffect, useMemo, useState } from "react";
import {
  getMyHorses,
  getMyRaceEntries,
  registerHorseForRace,
} from "../../services/ownerHorseApi";
import { getOwnerTournaments } from "../../services/ownerApi";
import { getTournamentRaces } from "../../services/adminApi";
import "../OwnerSharedLayout.css";
import "./OwnerTournamentRegisterPage.css";

const formatDate = (value) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Chưa xác định"
    : new Intl.DateTimeFormat("vi-VN", {
        month: "long",
        day: "numeric",
        year: "numeric",
      }).format(date);
};

const mapTournament = (tournament) => ({
  id: tournament?.id ?? tournament?.Id,
  name: tournament?.name ?? tournament?.Name ?? "Giải đấu",
  status: (tournament?.isActive ?? tournament?.IsActive) ? "Mở" : "Đã đóng",
  description:
    tournament?.description ?? tournament?.Description ?? "Không có mô tả.",
  date: formatDate(tournament?.startDate ?? tournament?.StartDate),
  raceCount: tournament?.raceCount ?? tournament?.RaceCount ?? 0,
});

function OwnerTournamentRegisterPage() {
  const [tournaments, setTournaments] = useState([]);
  const [horses, setHorses] = useState([]);
  const [races, setRaces] = useState([]);
  const [selectedHorseId, setSelectedHorseId] = useState("");
  const [selectedTournamentId, setSelectedTournamentId] = useState("");
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [showConfirm, setShowConfirm] = useState(false);
  const [registrations, setRegistrations] = useState([]);
  const [isHorseLoading, setIsHorseLoading] = useState(true);
  const [horseError, setHorseError] = useState("");
  const [isTournamentLoading, setIsTournamentLoading] = useState(true);
  const [tournamentError, setTournamentError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [msg, setMsg] = useState("");

  const loadRaces = async () => {
    if (!selectedTournamentId) {
      setRaces([]);
      return;
    }
    try {
      const data = await getTournamentRaces(selectedTournamentId);
      setRaces(Array.isArray(data) ? data : []);
    } catch {
      setRaces([]);
    }
  };

  useEffect(() => {
    loadRaces();
  }, [selectedTournamentId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    let isMounted = true;

    const fetchOwnerData = async () => {
      setIsHorseLoading(true);
      setIsTournamentLoading(true);
      setHorseError("");
      setTournamentError("");
      setMsg("");

      try {
        const data = await getMyHorses();
        const approvedHorses = (Array.isArray(data) ? data : []).filter(
          (horse) =>
            horse.approvalStatus === 2 || horse.approvalStatus === "Approved",
        );

        if (isMounted) {
          setHorses(approvedHorses);
          setSelectedHorseId(approvedHorses[0]?.id ?? "");
        }
      } catch (err) {
        if (isMounted) {
          setHorses([]);
          setSelectedHorseId("");
          setHorseError(err?.message || "Không thể tải danh sách ngựa đã duyệt.");
        }
      } finally {
        if (isMounted) setIsHorseLoading(false);
      }

      try {
        const data = await getOwnerTournaments();
        const openTournaments = (Array.isArray(data) ? data : [])
          .map(mapTournament)
          .filter((tournament) => tournament.status === "Mở");

        if (isMounted) {
          setTournaments(openTournaments);
          setSelectedTournamentId(openTournaments[0]?.id ?? "");
        }
      } catch (err) {
        if (isMounted) {
          setTournaments([]);
          setSelectedTournamentId("");
          setTournamentError(err?.message || "Không thể tải giải đấu đang mở.");
        }
      } finally {
        if (isMounted) setIsTournamentLoading(false);
      }

      try {
        const data = await getMyRaceEntries();
        if (isMounted) {
          const list = Array.isArray(data) ? data : [];
          const parsedRegistrations = list.map((entry) => ({
            id: entry.entryId ?? entry.EntryId ?? entry.id ?? Date.now(),
            horseId: entry.horseId ?? entry.HorseId,
            raceId: entry.raceId ?? entry.RaceId,
            horse: entry.horseName ?? entry.HorseName ?? "Không rõ",
            tournament:
              entry.tournamentName ??
              entry.TournamentName ??
              entry.tournament ??
              "Giải đấu",
            race: entry.raceName ?? entry.RaceName ?? "Cuộc đua",
            status:
              (entry.status ?? entry.Status ?? "Pending") === "Approved"
                ? "Đã duyệt"
                : (entry.status ?? entry.Status ?? "Pending") === "Rejected"
                  ? "Từ chối"
                  : "Chờ duyệt",
            submitted:
              entry.submittedDate ??
              entry.SubmittedDate ??
              new Date().toISOString().slice(0, 10),
          }));
          setRegistrations(parsedRegistrations);
        }
      } catch {
        if (isMounted) setRegistrations([]);
      }
    };

    fetchOwnerData();
    return () => {
      isMounted = false;
    };
  }, []);

  const selectedHorse = useMemo(
    () => horses.find((horse) => horse.id === selectedHorseId),
    [horses, selectedHorseId],
  );

  const selectedTournament = useMemo(
    () => tournaments.find((tournament) => tournament.id === selectedTournamentId),
    [selectedTournamentId, tournaments],
  );

  const hasExistingRegistration = useMemo(() => {
    if (!selectedHorseId || !selectedRaceId) return false;
    return registrations.some(
      (registration) =>
        String(registration.horseId) === String(selectedHorseId) &&
        String(registration.raceId) === String(selectedRaceId),
    );
  }, [registrations, selectedHorseId, selectedRaceId]);

  const ownerAlreadyInRace = useMemo(() => {
    if (!selectedRaceId) return false;
    return registrations.some(
      (registration) =>
        String(registration.raceId) === String(selectedRaceId),
    );
  }, [registrations, selectedRaceId]);

  const eligibilityChecks = [
    {
      label: "Duyệt ngựa",
      value: selectedHorse ? "Đã duyệt" : "Không có ngựa được chọn",
      tone: selectedHorse ? "success" : "warning",
    },
    {
      label: "Yêu cầu tuổi",
      value: selectedHorse?.age >= 3 ? "Đủ điều kiện" : "Quá trẻ",
      tone: selectedHorse?.age >= 3 ? "success" : "warning",
    },
    {
      label: "Thành tích đua",
      value: selectedHorse
        ? `${selectedHorse.totalWins ?? 0} trận thắng / ${selectedHorse.totalRaces ?? 0} cuộc đua`
        : "-",
      tone: selectedHorse ? "success" : "warning",
    },
    {
      label: "Cuộc đua giải đấu",
      value: selectedTournament
        ? `${selectedTournament.raceCount} cuộc đua đã lên lịch`
        : "Không có giải đấu được chọn",
      tone: selectedTournament ? "success" : "warning",
    },
  ];

  const handleSubmitRegistration = async () => {
    if (!selectedHorse || !selectedTournament || !selectedRaceId) return;
    setIsSubmitting(true);
    setMsg("");
    try {
      await registerHorseForRace(selectedHorse.id, selectedRaceId, {});
      setRegistrations((current) => [
        {
          id: Date.now(),
          horseId: selectedHorse.id,
          raceId: selectedRaceId,
          horse: selectedHorse.name,
          tournament: selectedTournament.name,
          race:
            races.find((r) => (r.id ?? r.Id) === selectedRaceId)?.name ??
            selectedRaceId,
          status: "Chờ duyệt",
          submitted: new Date().toISOString().slice(0, 10),
        },
        ...current,
      ]);
      setMsg("Đăng ký đã được gửi thành công! Admin sẽ duyệt ngựa của bạn.");
    } catch (err) {
      if (err.message?.includes("already registered")) {
        setMsg(
          "Ngựa này đã được đăng ký cho cuộc đua này. Vui lòng chọn ngựa khác hoặc cuộc đua khác.",
        );
      } else {
        setMsg("Lỗi: " + err.message);
      }
    } finally {
      setIsSubmitting(false);
      setShowConfirm(false);
    }
  };

  return (
    <div className="owner-page owner-tournament-register">
      <div>
        <div className="owner-content">
          <section className="page-header">
            <h1>Đăng ký ngựa vào cuộc đua</h1>
            <p>Chọn ngựa, kiểm tra điều kiện và theo dõi từng yêu cầu.</p>
          </section>

          <section className="register-grid">
            <form className="register-form">
              <div className="register-form__heading">
                <span className="pill">Đăng ký mới</span>
                <h2>Đăng ký cuộc đua</h2>
                <p>Chỉ những ngựa đã được duyệt mới có thể đăng ký.</p>
              </div>
              <div className="form-field">
                <label className="label-required" htmlFor="select-horse">
                  Chọn ngựa
                </label>
                <select
                  id="select-horse"
                  className="form-select"
                  value={selectedHorseId}
                  onChange={(event) => setSelectedHorseId(event.target.value)}
                  disabled={isHorseLoading || horses.length === 0}
                >
                  {isHorseLoading ? (
                    <option value="">
                      Đang tải danh sách ngựa đã duyệt...
                    </option>
                  ) : horses.length === 0 ? (
                    <option value="">Không có ngựa đã duyệt nào</option>
                  ) : null}
                  {horses.map((horse) => (
                    <option key={horse.id} value={horse.id}>
                      {horse.name} · Đã duyệt · {horse.age ?? "-"} tuổi
                    </option>
                  ))}
                </select>
                {horseError ? <p className="form-error">{horseError}</p> : null}
              </div>
              <div className="form-field">
                <label className="label-required" htmlFor="select-tournament">
                  Chọn giải đấu
                </label>
                <select
                  id="select-tournament"
                  className="form-select"
                  value={selectedTournamentId}
                  onChange={(event) =>
                    setSelectedTournamentId(event.target.value)
                  }
                  disabled={isTournamentLoading || tournaments.length === 0}
                >
                  {isTournamentLoading ? (
                    <option value="">Đang tải giải đấu đang mở...</option>
                  ) : tournaments.length === 0 ? (
                    <option value="">Không có giải đấu đang mở nào</option>
                  ) : null}
                  {tournaments.map((tournament) => (
                    <option key={tournament.id} value={tournament.id}>
                      {tournament.name} · {tournament.status}
                    </option>
                  ))}
                </select>
                {tournamentError ? (
                  <p className="form-error">{tournamentError}</p>
                ) : null}
              </div>
              <div className="form-field">
                <label className="label-required" htmlFor="select-race">
                  Chọn cuộc đua
                </label>
                <select
                  id="select-race"
                  className="form-select"
                  value={selectedRaceId}
                  onChange={(event) => setSelectedRaceId(event.target.value)}
                  disabled={!selectedTournamentId || races.length === 0}
                >
                  {!selectedTournamentId ? (
                    <option value="">Chọn giải đấu trước</option>
                  ) : races.length === 0 ? (
                    <option value="">Không có cuộc đua nào</option>
                  ) : (
                    <option value="">-- Chọn cuộc đua --</option>
                  )}
                  {races.map((race) => (
                    <option key={race.id ?? race.Id} value={race.id ?? race.Id}>
                      {race.name ?? race.Name} ·{" "}
                      {race.status ?? race.Status ?? "Đã lên lịch"}
                    </option>
                  ))}
                </select>
              </div>
              <div className="selection-summary">
                <div>
                  <span>Ngựa</span>
                  <strong>
                    {selectedHorse?.name ?? "Không có ngựa đã duyệt"}
                  </strong>
                  <p>
                    {selectedHorse
                      ? `${selectedHorse.age ?? "-"} tuổi · ${selectedHorse.totalWins ?? 0} trận thắng / ${selectedHorse.totalRaces ?? 0} cuộc đua`
                      : "Cần có ngựa đã được duyệt."}
                  </p>
                </div>
                <div>
                  <span>Giải đấu</span>
                  <strong>
                    {selectedTournament?.name ?? "Không có giải đấu đang mở"}
                  </strong>
                  <p>
                    {selectedTournament
                      ? `${selectedTournament.description} · ${selectedTournament.date}`
                      : "Cần có giải đấu đang mở."}
                  </p>
                </div>
              </div>
              <div className="register-actions">
                {msg && (
                  <p
                    className={
                      msg.startsWith("Lỗi") ? "form-error" : "form-success"
                    }
                  >
                    {msg}
                  </p>
                )}
                <button
                  className="primary-button"
                  type="button"
                  onClick={() => setShowConfirm(true)}
                  disabled={
                    !selectedHorse ||
                    !selectedTournament ||
                    !selectedRaceId ||
                    isSubmitting ||
                    hasExistingRegistration ||
                    ownerAlreadyInRace
                  }
                >
                  {isSubmitting
                    ? "Đang gửi..."
                    : hasExistingRegistration
                      ? "Đã đăng ký"
                      : ownerAlreadyInRace
                        ? "Đã có ngựa tham gia"
                        : "Xem lại đăng ký"}
                </button>
                {hasExistingRegistration ? (
                  <p className="form-error">
                    Ngựa này đã được đăng ký cho cuộc đua đã chọn.
                  </p>
                ) : ownerAlreadyInRace ? (
                  <p className="form-error">
                    Bạn chỉ có thể đăng ký 1 con ngựa vào 1 cuộc đua. Ngựa khác của bạn đã được đăng ký.
                  </p>
                ) : null}
              </div>
            </form>

            <div className="eligibility-card">
              <div className="section-heading">
                <h2>Kiểm tra điều kiện</h2>
                <p>Xem trước cho lựa chọn hiện tại.</p>
              </div>
              <div className="eligibility-list">
                {eligibilityChecks.map((check) => (
                  <div
                    key={check.label}
                    className={`eligibility-item eligibility-item--${check.tone}`}
                  >
                    <span>{check.label}</span>
                    <strong>{check.value}</strong>
                  </div>
                ))}
              </div>
              <div className="eligibility-note">
                <h4>Nhắc nhở</h4>
                <p className="muted">
                  Xác nhận lịch đua và tình trạng kỵ sĩ trước khi gửi.
                </p>
              </div>
            </div>
          </section>

          <section className="registration-status">
            <div className="section-heading">
              <h2>Trạng thái đăng ký</h2>
              <p>Theo dõi yêu cầu cuộc đua đã gửi từ chuồng ngựa.</p>
            </div>
            <div className="registration-table">
              {registrations.length === 0 ? (
                <p className="muted">Chưa có đăng ký nào.</p>
              ) : (
                registrations.map((registration) => (
                  <article key={registration.id} className="registration-row">
                    <div>
                      <span>Ngựa</span>
                      <strong>{registration.horse}</strong>
                    </div>
                    <div>
                      <span>Giải đấu</span>
                      <strong>{registration.tournament}</strong>
                    </div>
                    <div>
                      <span>Cuộc đua</span>
                      <strong>{registration.race}</strong>
                    </div>
                    <div>
                      <span>Đã gửi</span>
                      <strong>{registration.submitted}</strong>
                    </div>
                    <div>
                      <span>Trạng thái</span>
                      <strong
                        className={`registration-status-pill registration-status-pill--${registration.status
                          .toLowerCase()
                          .replace(/\s+/g, "-")}`}
                      >
                        {registration.status}
                      </strong>
                    </div>
                  </article>
                ))
              )}
            </div>
          </section>
        </div>
      </div>

      {showConfirm ? (
        <div
          className="modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-labelledby="register-modal-title"
        >
          <div className="owner-modal">
            <div className="modal-header">
              <div>
                <span className="badge">Sẵn sàng gửi</span>
                <h3 id="register-modal-title">Xác nhận đăng ký</h3>
                <p className="muted">Xem lại trước khi gửi đăng ký.</p>
              </div>
              <button
                className="ghost-button"
                onClick={() => setShowConfirm(false)}
              >
                Đóng
              </button>
            </div>
            <div className="modal-body">
              <div>
                <h4>Ngựa</h4>
                <p>{selectedHorse?.name}</p>
              </div>
              <div>
                <h4>Giải đấu</h4>
                <p>{selectedTournament?.name}</p>
              </div>
              <div>
                <h4>Cuộc đua</h4>
                <p>
                  {races.find((r) => (r.id ?? r.Id) === selectedRaceId)?.name ??
                    "—"}
                </p>
              </div>
              <div>
                <h4>Mô tả</h4>
                <p>{selectedTournament?.description}</p>
              </div>
            </div>
            <div className="modal-actions">
              <button
                className="ghost-button"
                onClick={() => setShowConfirm(false)}
              >
                Hủy
              </button>
              <button
                className="primary-button"
                onClick={handleSubmitRegistration}
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang gửi..." : "Xác nhận gửi"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

export default OwnerTournamentRegisterPage;
