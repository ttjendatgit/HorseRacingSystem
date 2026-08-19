import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  getMyHorses,
  getMyTournamentRegistrations,
  registerHorseForTournament,
} from "../../services/ownerHorseApi";
import { getOwnerTournaments } from "../../services/ownerApi";
import { apiToVNDisplay } from "../../utils/vnDateTime";
import { canRegisterTournament } from "../../utils/tournamentRegistration";
import "../OwnerSharedLayout.css";
import "./OwnerTournamentRegisterPage.css";

const mapTournament = (tournament) => ({
  id: tournament?.id ?? tournament?.Id,
  name: tournament?.name ?? tournament?.Name ?? "Giải đấu",
  description: tournament?.description ?? tournament?.Description ?? "Không có mô tả.",
  registrationDeadline: tournament?.registrationDeadline ?? tournament?.RegistrationDeadline,
  raceCount: tournament?.raceCount ?? tournament?.RaceCount ?? 0,
  registerable: canRegisterTournament(tournament),
});

const registrationStatusLabel = (status) => {
  if (status === "Approved") return "Đã duyệt";
  if (status === "Rejected") return "Từ chối";
  if (status === "Withdrawn") return "Đã rút";
  return "Chờ duyệt";
};

function OwnerTournamentRegisterPage() {
  const [searchParams] = useSearchParams();
  // The exact TournamentId the Owner clicked "Đăng ký" on, if they came from a Tournament card —
  // this must never be silently swapped for a different Tournament (Task B Final §4).
  const requestedTournamentId = searchParams.get("tournamentId") || "";
  const cameFromTournamentCard = !!requestedTournamentId;

  const [tournaments, setTournaments] = useState([]);
  const [horses, setHorses] = useState([]);
  const [selectedHorseId, setSelectedHorseId] = useState("");
  const [selectedTournamentId, setSelectedTournamentId] = useState(requestedTournamentId);
  const [showConfirm, setShowConfirm] = useState(false);
  const [registrations, setRegistrations] = useState([]);
  const [isHorseLoading, setIsHorseLoading] = useState(true);
  const [horseError, setHorseError] = useState("");
  const [isTournamentLoading, setIsTournamentLoading] = useState(true);
  const [tournamentError, setTournamentError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [msg, setMsg] = useState("");

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
        const mapped = (Array.isArray(data) ? data : []).map(mapTournament);

        if (isMounted) {
          setTournaments(mapped);
          // Only pick a default Tournament when the Owner arrived with none in mind (direct
          // nav, no card clicked). A clicked TournamentId is preserved exactly as-is below —
          // never overwritten here, even if it turns out not to be registerable.
          if (!cameFromTournamentCard) {
            const firstRegisterable = mapped.find((t) => t.registerable);
            setSelectedTournamentId(firstRegisterable?.id ?? "");
          }
        }
      } catch (err) {
        if (isMounted) {
          setTournaments([]);
          setTournamentError(err?.message || "Không thể tải giải đấu.");
        }
      } finally {
        if (isMounted) setIsTournamentLoading(false);
      }

      try {
        const data = await getMyTournamentRegistrations();
        if (isMounted) {
          const list = Array.isArray(data) ? data : [];
          setRegistrations(
            list.map((r) => {
              const status = r.status ?? r.Status ?? "Pending";
              return {
                id: r.id ?? r.Id ?? Date.now(),
                horseId: r.horseId ?? r.HorseId,
                tournamentId: r.tournamentId ?? r.TournamentId,
                horse: r.horseName ?? r.HorseName ?? "Không rõ",
                tournament: r.tournamentName ?? r.TournamentName ?? "Giải đấu",
                statusRaw: status,
                status: registrationStatusLabel(status),
                submitted: (r.createdAt ?? r.CreatedAt ?? "").toString().slice(0, 10),
              };
            }),
          );
        }
      } catch {
        if (isMounted) setRegistrations([]);
      }
    };

    fetchOwnerData();
    return () => {
      isMounted = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requestedTournamentId]);

  const selectedHorse = useMemo(
    () => horses.find((horse) => horse.id === selectedHorseId),
    [horses, selectedHorseId],
  );

  const selectedTournament = useMemo(
    () => tournaments.find((tournament) => tournament.id === selectedTournamentId),
    [selectedTournamentId, tournaments],
  );

  // Resolved only after the Tournament list has actually loaded — before that, "not found" would
  // be a false negative, not a real "closed" state.
  const tournamentUnavailable =
    !isTournamentLoading && cameFromTournamentCard && !selectedTournament?.registerable;

  const hasExistingRegistration = useMemo(() => {
    if (!selectedHorseId || !selectedTournamentId) return false;
    return registrations.some(
      (registration) =>
        String(registration.horseId) === String(selectedHorseId) &&
        String(registration.tournamentId) === String(selectedTournamentId) &&
        (registration.statusRaw === "Pending" || registration.statusRaw === "Approved"),
    );
  }, [registrations, selectedHorseId, selectedTournamentId]);

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
      label: "Trạng thái đăng ký giải",
      value: selectedTournament
        ? (selectedTournament.registerable ? "Đang mở đăng ký" : "Đã đóng đăng ký")
        : "Chưa chọn giải đấu",
      tone: selectedTournament?.registerable ? "success" : "warning",
    },
  ];

  const handleSubmitRegistration = async () => {
    if (!selectedHorse || !selectedTournament) return;
    setIsSubmitting(true);
    setMsg("");
    try {
      await registerHorseForTournament(selectedTournament.id, selectedHorse.id);
      setRegistrations((current) => [
        {
          id: Date.now(),
          horseId: selectedHorse.id,
          tournamentId: selectedTournament.id,
          horse: selectedHorse.name,
          tournament: selectedTournament.name,
          statusRaw: "Pending",
          status: "Chờ duyệt",
          submitted: new Date().toISOString().slice(0, 10),
        },
        ...current,
      ]);
      setMsg("Đăng ký đã được gửi thành công! Admin sẽ duyệt ngựa của bạn.");
    } catch (err) {
      setMsg("Lỗi: " + err.message);
    } finally {
      setIsSubmitting(false);
      setShowConfirm(false);
    }
  };

  const canSubmit =
    !!selectedHorse &&
    !!selectedTournament?.registerable &&
    !isSubmitting &&
    !hasExistingRegistration;

  return (
    <div className="owner-page owner-tournament-register">
      <div>
        <div className="owner-content">
          <section className="page-header">
            <h1>Đăng ký ngựa vào giải đấu</h1>
            <p>Chọn ngựa, kiểm tra điều kiện và theo dõi từng yêu cầu.</p>
          </section>

          <section className="register-grid">
            <form className="register-form">
              <div className="register-form__heading">
                <span className="pill">Đăng ký mới</span>
                <h2>Đăng ký giải đấu</h2>
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

              {/* Task B Final §4: entering from a Tournament card shows that exact Tournament
                  read-only/preselected — the Owner only chooses a Horse. Direct navigation (no
                  card clicked) falls back to a picker of currently-registerable Tournaments. */}
              {cameFromTournamentCard ? (
                <div className="form-field">
                  <span className="label-required">Giải đấu</span>
                  {isTournamentLoading ? (
                    <p className="muted">Đang tải thông tin giải đấu...</p>
                  ) : selectedTournament ? (
                    <div className="selection-summary" style={{ gridTemplateColumns: "1fr" }}>
                      <div>
                        <span>Giải đấu đã chọn</span>
                        <strong>{selectedTournament.name}</strong>
                        <p>
                          Hạn đăng ký: {apiToVNDisplay(selectedTournament.registrationDeadline) || "Chưa thiết lập"}
                        </p>
                      </div>
                    </div>
                  ) : (
                    <p className="form-error">Không tìm thấy giải đấu đã chọn.</p>
                  )}
                  {tournamentUnavailable ? (
                    <p className="form-error">Giải đấu hiện không mở đăng ký.</p>
                  ) : null}
                </div>
              ) : (
                <div className="form-field">
                  <label className="label-required" htmlFor="select-tournament">
                    Chọn giải đấu
                  </label>
                  <select
                    id="select-tournament"
                    className="form-select"
                    value={selectedTournamentId}
                    onChange={(event) => setSelectedTournamentId(event.target.value)}
                    disabled={isTournamentLoading || tournaments.filter((t) => t.registerable).length === 0}
                  >
                    {isTournamentLoading ? (
                      <option value="">Đang tải giải đấu đang mở...</option>
                    ) : tournaments.filter((t) => t.registerable).length === 0 ? (
                      <option value="">Không có giải đấu đang mở đăng ký</option>
                    ) : null}
                    {tournaments.filter((t) => t.registerable).map((tournament) => (
                      <option key={tournament.id} value={tournament.id}>
                        {tournament.name}
                      </option>
                    ))}
                  </select>
                  {tournamentError ? (
                    <p className="form-error">{tournamentError}</p>
                  ) : null}
                </div>
              )}

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
                    {selectedTournament?.name ?? "Không có giải đấu"}
                  </strong>
                  <p>{selectedTournament?.description ?? "Cần chọn giải đấu đang mở đăng ký."}</p>
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
                  disabled={!canSubmit}
                >
                  {isSubmitting
                    ? "Đang gửi..."
                    : hasExistingRegistration
                      ? "Đã đăng ký"
                      : tournamentUnavailable
                        ? "Giải đấu đã đóng"
                        : "Xem lại đăng ký"}
                </button>
                {hasExistingRegistration ? (
                  <p className="form-error">
                    Ngựa này đã được đăng ký cho giải đấu đã chọn.
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
                  Đăng ký thuộc về Giải đấu — không phải Cuộc đua riêng lẻ. Sau khi Admin duyệt,
                  ngựa sẽ đủ điều kiện được phân công vào các Cuộc đua trong giải.
                </p>
              </div>
            </div>
          </section>

          <section className="registration-status">
            <div className="section-heading">
              <h2>Trạng thái đăng ký</h2>
              <p>Theo dõi yêu cầu giải đấu đã gửi từ chuồng ngựa.</p>
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
                <h4>Hạn đăng ký</h4>
                <p>{apiToVNDisplay(selectedTournament?.registrationDeadline) || "—"}</p>
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
