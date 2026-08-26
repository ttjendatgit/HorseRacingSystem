import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  getMyHorses,
  getMyTournamentRegistrations,
  registerHorseForTournament,
  withdrawTournamentRegistration,
} from "../../services/ownerHorseApi";
import { getOwnerTournaments } from "../../services/ownerApi";
import { apiToVNDisplay } from "../../utils/vnDateTime";
import { getTournamentRegistrationState, getCapacityFullMessage } from "../../utils/tournamentRegistration";
import { getRegistrationStatusLabel } from "../../utils/registrationStatusDisplay";
import {
  buildTournamentRequirementItems,
  canSubmitTournamentRegistration,
  getHorseTournamentSelectionState,
  ownerHasActiveTournamentRegistration,
} from "../../utils/ownerDemoDisplay";
import "../OwnerSharedLayout.css";
import "./OwnerTournamentRegisterPage.css";

const mapTournament = (tournament) => {
  const registrationState = getTournamentRegistrationState(tournament);
  return {
    id: tournament?.id ?? tournament?.Id,
    name: tournament?.name ?? tournament?.Name ?? "Giải đấu",
    description: tournament?.description ?? tournament?.Description ?? "Không có mô tả.",
    registrationDeadline: tournament?.registrationDeadline ?? tournament?.RegistrationDeadline,
    raceCount: tournament?.raceCount ?? tournament?.RaceCount ?? 0,
    registerable: registrationState.canRegister,
    registrationLabel: registrationState.label,
    registrationKey: registrationState.key,
    capacityMessage: getCapacityFullMessage(tournament),
  };
};

const mapRegistration = (r) => {
  const status = r.status ?? r.Status ?? "Pending";
  return {
    id: r.id ?? r.Id ?? Date.now(),
    horseId: r.horseId ?? r.HorseId,
    tournamentId: r.tournamentId ?? r.TournamentId,
    horse: r.horseName ?? r.HorseName ?? "Không rõ",
    tournament: r.tournamentName ?? r.TournamentName ?? "Giải đấu",
    tournamentStatus: r.tournamentStatus ?? r.TournamentStatus ?? "",
    statusRaw: status,
    status: getRegistrationStatusLabel(status),
    submitted: (r.createdAt ?? r.CreatedAt ?? "").toString().slice(0, 10),
    note: r.note ?? r.Note ?? "",
  };
};

const getRegistrationTone = (status) => {
  const normalized = String(status ?? "").trim().toLowerCase();
  if (normalized === "approved") return "approved";
  if (normalized === "rejected") return "rejected";
  if (normalized === "withdrawn") return "withdrawn";
  return "pending";
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
  const [withdrawingId, setWithdrawingId] = useState("");
  const [withdrawMsg, setWithdrawMsg] = useState("");

  const refreshRegistrations = async () => {
    try {
      const data = await getMyTournamentRegistrations();
      const list = Array.isArray(data) ? data : [];
      setRegistrations(list.map(mapRegistration));
    } catch {
      // keep existing list on refresh failure
    }
  };

  const handleWithdraw = async (registration) => {
    setWithdrawingId(registration.id);
    setWithdrawMsg("");
    try {
      await withdrawTournamentRegistration(registration.id);
      await refreshRegistrations();
      setWithdrawMsg("Đã rút đăng ký thành công.");
    } catch (err) {
      setWithdrawMsg("Lỗi: " + (err?.message || "Không thể rút đăng ký."));
    } finally {
      setWithdrawingId("");
    }
  };

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
        const visibleHorses = (Array.isArray(data) ? data : []).filter(
          (horse) => !(horse.isArchived ?? horse.IsArchived),
        );

        if (isMounted) {
          setHorses(visibleHorses);
        }
      } catch (err) {
        if (isMounted) {
          setHorses([]);
          setSelectedHorseId("");
          setHorseError(err?.message || "Không thể tải danh sách ngựa.");
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
          setRegistrations(list.map(mapRegistration));
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

  useEffect(() => {
    if (isHorseLoading || horses.length === 0) return;
    const currentHorse = horses.find((horse) => horse.id === selectedHorseId);
    const currentState = getHorseTournamentSelectionState(currentHorse, registrations, selectedTournamentId);
    if (currentHorse && currentState.selectable) return;

    const firstSelectable = horses.find(
      (horse) => getHorseTournamentSelectionState(horse, registrations, selectedTournamentId).selectable,
    );
    setSelectedHorseId(firstSelectable?.id ?? horses[0]?.id ?? "");
  }, [horses, isHorseLoading, registrations, selectedHorseId, selectedTournamentId]);

  // Resolved only after the Tournament list has actually loaded — before that, "not found" would
  // be a false negative, not a real "closed" state.
  const tournamentUnavailable =
    !isTournamentLoading && cameFromTournamentCard && !selectedTournament?.registerable;

  const unavailableTournamentMessage = selectedTournament
    ? selectedTournament.registrationKey === "unpublished"
      ? "Giải đấu chưa được công bố."
      // Capacity being full gets its own X/Y message — never phrased as "Đã đóng đăng ký",
      // since the registration deadline itself may still be open.
      : selectedTournament.registrationKey === "full"
        ? selectedTournament.capacityMessage
        : `Giải đấu ${selectedTournament.registrationLabel.toLowerCase()}.`
    : cameFromTournamentCard
      ? "Giải đấu chưa được công bố."
      : "Cần chọn giải đấu đang mở đăng ký.";

  const hasExistingRegistration = useMemo(() => {
    if (!selectedHorseId || !selectedTournamentId) return false;
    return registrations.some(
      (registration) =>
        String(registration.horseId) === String(selectedHorseId) &&
        String(registration.tournamentId) === String(selectedTournamentId) &&
        (registration.statusRaw === "Pending" || registration.statusRaw === "Approved"),
    );
  }, [registrations, selectedHorseId, selectedTournamentId]);

  const hasOwnerActiveRegistration = useMemo(
    () => ownerHasActiveTournamentRegistration(registrations, selectedTournamentId),
    [registrations, selectedTournamentId],
  );

  const selectedHorseState = useMemo(
    () => getHorseTournamentSelectionState(selectedHorse, registrations, selectedTournamentId),
    [registrations, selectedHorse, selectedTournamentId],
  );

  const selectedHorseTone = selectedHorseState.selectable
    ? "success"
    : selectedHorseState.label === "Chờ duyệt"
      ? "pending"
      : "warning";

  const eligibilityChecks = buildTournamentRequirementItems({
    tournament: selectedTournament,
    selectedHorseState,
    hasHorse: Boolean(selectedHorse),
  });

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
          tournamentStatus: "Published",
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

  // Not currently registerable-available at all (direct nav, no Tournament card clicked) — the
  // select is already empty/disabled in this case; the CTA must say so too instead of the generic
  // "Đăng ký", which reads as actionable even while the button itself stays disabled. Neutral
  // wording only — this is an unavailable state, not an error.
  const noRegisterableTournament =
    !isTournamentLoading && !cameFromTournamentCard && tournaments.filter((t) => t.registerable).length === 0;

  const canSubmit = canSubmitTournamentRegistration({
    selectedHorse,
    selectedHorseState,
    selectedTournament,
    isSubmitting,
    hasExistingRegistration,
    hasOwnerActiveRegistration,
  });

  return (
    <div className="owner-page owner-tournament-register">
      <div>
        <div className="owner-content">
          <section className="page-header">
            <h1>Đăng ký ngựa vào giải đấu</h1>
            <p>Chọn ngựa và giải đấu, kiểm tra điều kiện đăng ký, và theo dõi trạng thái từng yêu cầu đã gửi.</p>
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
                      Đang tải danh sách ngựa...
                    </option>
                  ) : horses.length === 0 ? (
                    <option value="">Không có ngựa khả dụng</option>
                  ) : null}
                  {horses.map((horse) => {
                    const state = getHorseTournamentSelectionState(horse, registrations, selectedTournamentId);
                    return (
                      <option key={horse.id} value={horse.id} disabled={!state.selectable}>
                        {horse.name} · {state.label}
                        {state.reason ? ` · ${state.reason}` : ""}
                      </option>
                    );
                  })}
                </select>
                {horseError ? <p className="form-error">{horseError}</p> : null}
                {selectedHorse ? (
                  <div className={`horse-registration-state horse-registration-state--${selectedHorseTone}`}>
                    <span>{selectedHorseState.label}</span>
                    <strong>{selectedHorse.name}</strong>
                    <p>
                      {selectedHorseState.reason ||
                        "Ngựa đã được duyệt và có thể gửi đăng ký cho giải đấu đã chọn."}
                    </p>
                  </div>
                ) : null}
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
                          Hạn đăng ký: {apiToVNDisplay(selectedTournament.registrationDeadline) || "Chưa thiết lập"} · {selectedTournament.registrationLabel}
                        </p>
                      </div>
                    </div>
                  ) : (
                    <p className="form-error">Giải đấu chưa được công bố.</p>
                  )}
                  {tournamentUnavailable ? (
                    <p className="form-error" style={{ whiteSpace: "pre-line" }}>{unavailableTournamentMessage}</p>
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
                    {selectedHorse?.name ?? "Không có ngựa đủ điều kiện"}
                  </strong>
                  <p>
                    {selectedHorse
                      ? `${selectedHorseState.label} · ${selectedHorseState.reason || `${selectedHorse.totalWins ?? 0} trận thắng / ${selectedHorse.totalRaces ?? 0} cuộc đua`}`
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
                  aria-disabled={!canSubmit}
                >
                    {isSubmitting
                    ? "Đang gửi..."
                    : tournamentUnavailable
                      ? unavailableTournamentMessage
                      : noRegisterableTournament
                        ? "Không có giải đấu để đăng ký"
                        : hasExistingRegistration || hasOwnerActiveRegistration
                          ? "Đã đăng ký"
                          : "Đăng ký"}
                </button>
                {hasExistingRegistration ? (
                  <p className="form-error">
                    Ngựa này đã được đăng ký cho giải đấu đã chọn.
                  </p>
                ) : hasOwnerActiveRegistration ? (
                  <p className="form-error">
                    Bạn đã có một ngựa đang đăng ký hoặc đã được duyệt cho giải này.
                  </p>
                ) : null}
              </div>
            </form>

            <div className="eligibility-card">
              <div className="section-heading">
                <h2>Kiểm tra điều kiện đăng ký</h2>
                <p>
                  Một số dòng là yêu cầu bắt buộc, một số phản ánh tình trạng hiện tại của giải
                  đấu, và dòng cuối cùng còn được hệ thống xác thực lại khi bạn gửi đăng ký.
                </p>
              </div>
              <div className="eligibility-list">
                {eligibilityChecks.map((check) => (
                  <div
                    key={check.label}
                    className={`eligibility-item eligibility-item--${check.tone}`}
                  >
                    <span>{check.label}</span>
                    <strong>{check.detail}</strong>
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
              <p>Lịch sử các yêu cầu đăng ký giải đấu đã gửi từ chuồng ngựa của bạn.</p>
            </div>
            {withdrawMsg ? (
              <p className={withdrawMsg.startsWith("Lỗi") ? "form-error" : "form-success"}>
                {withdrawMsg}
              </p>
            ) : null}
            <div className="registration-table">
              {registrations.length === 0 ? (
                <p className="muted">Chưa có đăng ký nào.</p>
              ) : (
                registrations.map((registration) => {
                  // Task C1 §2: Withdraw is only offered while the backend would actually allow
                  // it — Pending or Approved, and the Tournament is still Published. The
                  // Approved+RaceEntry-exists case can't be known client-side, so that rejection
                  // surfaces from the backend's response message after the attempt.
                  const canWithdraw =
                    (registration.statusRaw === "Pending" || registration.statusRaw === "Approved") &&
                    registration.tournamentStatus === "Published";
                  const tone = getRegistrationTone(registration.statusRaw);
                  return (
                    <article key={registration.id} className={`registration-row registration-row--${tone}`}>
                      <div className="registration-row__main">
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
                          <strong className={`registration-status-pill registration-status-pill--${tone}`}>
                            {registration.status}
                          </strong>
                        </div>
                        {canWithdraw ? (
                          <div>
                            <button
                              type="button"
                              className="ghost-button"
                              disabled={withdrawingId === registration.id}
                              onClick={() => handleWithdraw(registration)}
                            >
                              {withdrawingId === registration.id ? "Đang rút..." : "Rút đăng ký"}
                            </button>
                          </div>
                        ) : null}
                      </div>
                      {tone === "rejected" && registration.note ? (
                        <div className="registration-row__reason">
                          <span>Lý do từ chối</span>
                          <p>{registration.note}</p>
                        </div>
                      ) : null}
                    </article>
                  );
                })
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
