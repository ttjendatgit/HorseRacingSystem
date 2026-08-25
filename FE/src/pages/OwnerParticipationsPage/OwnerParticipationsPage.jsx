import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getMyTournamentRegistrations, getMyRaceEntries } from "../../services/ownerHorseApi";
import { createRaceComplaint } from "../../services/managementApi";
import { apiToVNDisplay } from "../../utils/vnDateTime";
import { getRegistrationStatusLabel } from "../../utils/registrationStatusDisplay";
import { getOwnerRaceStatusLabel } from "../../utils/raceStatusDisplay";
import { getJockeyNameDisplay } from "../../utils/jockeyAssignmentDisplay";
import { RACE_COMPLAINT_TYPE_OPTIONS } from "../../utils/raceComplaintDisplay";
import { RaceButton, RaceModalShell, RaceSelect } from "../../components/ui/RaceUi";
import {
  getTournamentLifecycleLabel,
  normalizeTournamentStatus,
} from "../../utils/tournamentRegistration";
import "../OwnerSharedLayout.css";
import "./OwnerParticipationsPage.css";

const REGISTRATION_STATUS_NUM = {
  1: "pending",
  2: "approved",
  3: "rejected",
  4: "withdrawn",
};

const RACE_STATUS_NUM = {
  1: "scheduled",
  2: "inprogress",
  3: "finished",
  4: "cancelled",
  7: "registrationopen",
  8: "registrationclosed",
};

const normalizeKey = (value, numericMap = {}) => {
  if (value === null || value === undefined || value === "") return "";
  if (typeof value === "number") return numericMap[value] ?? String(value);
  const text = String(value).trim();
  if (/^\d+$/.test(text)) return numericMap[Number(text)] ?? text;
  return text.replace(/[\s_-]+/g, "").toLowerCase();
};

const getTournamentKey = (status) =>
  normalizeTournamentStatus({ status }).toLowerCase();

const bucketOf = (registration) => {
  const tournamentStatus = normalizeTournamentStatus({ status: registration.tournamentStatus });
  if (tournamentStatus === "Ongoing") return "ongoing";
  if (tournamentStatus === "Finished" || tournamentStatus === "Cancelled") return "finished";
  return "upcoming";
};

const REGISTRATION_TONE = {
  pending: "warning",
  approved: "success",
  rejected: "danger",
  withdrawn: "neutral",
};

const TOURNAMENT_TONE = {
  draft: "neutral",
  published: "gold",
  ongoing: "live",
  finished: "success",
  cancelled: "danger",
};

const RACE_TONE = {
  scheduled: "neutral",
  registrationopen: "gold",
  registrationclosed: "neutral",
  inprogress: "live",
  finished: "success",
  cancelled: "danger",
};

const STATUS_PRIORITY = {
  inprogress: 0,
  registrationopen: 1,
  registrationclosed: 1,
  scheduled: 1,
  finished: 2,
  cancelled: 3,
};

const formatDateRange = (start, end) => {
  const startText = apiToVNDisplay(start);
  const endText = apiToVNDisplay(end);
  if (startText && endText) return `${startText} - ${endText}`;
  return startText || endText || "Chưa xác định";
};

const formatShortDate = (value) => {
  const text = apiToVNDisplay(value);
  return text ? text.slice(0, 5) : "chưa rõ";
};

const formatRoundLabel = (entry) => {
  if (entry.roundName && entry.roundNumber != null) {
    return `Vòng ${entry.roundNumber} · ${entry.roundName}`;
  }
  if (entry.roundName) return entry.roundName;
  if (entry.roundNumber != null) return `Vòng ${entry.roundNumber}`;
  return "Chưa gắn vòng";
};

const formatRank = (position) => {
  if (position === null || position === undefined || position === "") return "Chưa có";
  return `Hạng ${position}`;
};

const getEntrySortValue = (entry) => {
  const time = entry.scheduledAt ? new Date(entry.scheduledAt).getTime() : 0;
  return Number.isNaN(time) ? 0 : time;
};

const sortEntriesForDisplay = (items) =>
  [...items].sort((a, b) => {
    const statusDelta =
      (STATUS_PRIORITY[a.raceStatusKey] ?? 4) - (STATUS_PRIORITY[b.raceStatusKey] ?? 4);
    if (statusDelta !== 0) return statusDelta;

    const resultDelta =
      (a.finishPosition == null ? 1 : 0) - (b.finishPosition == null ? 1 : 0);
    if (resultDelta !== 0) return resultDelta;

    return getEntrySortValue(a) - getEntrySortValue(b);
  });

function StatusBadge({ label, tone = "neutral" }) {
  return <span className={`op-status op-status--${tone}`}>{label}</span>;
}

function OwnerParticipationsPage() {
  const [registrations, setRegistrations] = useState([]);
  const [entries, setEntries] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState(null);
  const [expandedRaces, setExpandedRaces] = useState({});
  const [complaintModal, setComplaintModal] = useState(null); // { entry }
  const [complaintForm, setComplaintForm] = useState({ type: "ResultJudging", reason: "", evidenceDescription: "" });
  const [complaintSubmitting, setComplaintSubmitting] = useState(false);
  const [complaintMsg, setComplaintMsg] = useState(null);

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
            const tournamentStatus = r.tournamentStatus ?? r.TournamentStatus ?? "";
            return {
              id: r.id ?? r.Id,
              horseId: r.horseId ?? r.HorseId,
              horseName: r.horseName ?? r.HorseName ?? "Không rõ",
              tournamentId: r.tournamentId ?? r.TournamentId,
              tournamentName: r.tournamentName ?? r.TournamentName ?? "Giải đấu",
              tournamentStatus,
              tournamentStatusKey: getTournamentKey(tournamentStatus),
              tournamentStatusLabel: getTournamentLifecycleLabel({ status: tournamentStatus }),
              tournamentStartDate: r.tournamentStartDate ?? r.TournamentStartDate,
              tournamentEndDate: r.tournamentEndDate ?? r.TournamentEndDate,
              statusKey: normalizeKey(status, REGISTRATION_STATUS_NUM),
              statusLabel: getRegistrationStatusLabel(status),
              createdAt: r.createdAt ?? r.CreatedAt,
              approvedAt: r.approvedAt ?? r.ApprovedAt,
              note: r.note ?? r.Note ?? "",
            };
          }),
        );
        setEntries(
          (Array.isArray(entryData) ? entryData : []).map((e) => {
            const raceStatus = e.raceStatus ?? e.RaceStatus ?? "";
            const entryStatus = e.status ?? e.Status ?? "";
            return {
              entryId: e.entryId ?? e.EntryId,
              horseId: e.horseId ?? e.HorseId,
              tournamentId: e.tournamentId ?? e.TournamentId,
              raceId: e.raceId ?? e.RaceId,
              raceName: e.raceName ?? e.RaceName ?? "Cuộc đua",
              roundNumber: e.roundNumber ?? e.RoundNumber,
              roundName: e.roundName ?? e.RoundName,
              scheduledAt: e.scheduledAt ?? e.ScheduledAt,
              scheduledEndAt: e.scheduledEndAt ?? e.ScheduledEndAt,
              location: e.location ?? e.Location ?? "",
              raceStatus,
              raceStatusKey: normalizeKey(raceStatus, RACE_STATUS_NUM),
              statusKey: normalizeKey(entryStatus, REGISTRATION_STATUS_NUM),
              statusLabel: getRegistrationStatusLabel(entryStatus),
              jockeyId: e.jockeyId ?? e.JockeyId,
              jockeyName: e.jockeyName ?? e.JockeyName ?? "",
              gateNumber: e.gateNumber ?? e.GateNumber,
              finishPosition: e.finishPosition ?? e.FinishPosition ?? null,
            };
          }),
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
    const result = { upcoming: [], ongoing: [], finished: [] };
    registrations.forEach((registration) => {
      result[bucketOf(registration)].push(registration);
    });
    return result;
  }, [registrations]);

  const sections = [
    {
      key: "upcoming",
      title: "Sắp diễn ra",
      hint: "Đăng ký đang chờ bước tiếp theo.",
      empty: "Chưa có lượt tham gia sắp diễn ra.",
      items: groups.upcoming,
    },
    {
      key: "ongoing",
      title: "Đang diễn ra",
      hint: "Theo dõi phân công và trạng thái cuộc đua.",
      empty: "Chưa có lượt tham gia đang diễn ra.",
      items: groups.ongoing,
    },
    {
      key: "finished",
      title: "Đã kết thúc",
      hint: "Lịch sử tham gia và kết quả đã ghi nhận.",
      empty: "Chưa có lượt tham gia đã kết thúc.",
      items: groups.finished,
    },
  ];

  const defaultTab = groups.upcoming.length
    ? "upcoming"
    : groups.ongoing.length
      ? "ongoing"
      : groups.finished.length
        ? "finished"
        : "upcoming";
  const selectedKey = activeTab ?? defaultTab;
  const selectedSection = sections.find((section) => section.key === selectedKey) ?? sections[0];
  const isSelectedEmpty = selectedSection.items.length === 0;

  const toggleRaceDisclosure = (registrationId) => {
    setExpandedRaces((current) => ({
      ...current,
      [registrationId]: !current[registrationId],
    }));
  };

  const openComplaintModal = (entry) => {
    setComplaintForm({ type: "ResultJudging", reason: "", evidenceDescription: "" });
    setComplaintMsg(null);
    setComplaintModal({ entry });
  };

  const submitComplaint = async () => {
    if (!complaintModal) return;
    if (!complaintForm.reason.trim()) {
      setComplaintMsg({ type: "error", text: "Vui lòng nhập nội dung khiếu nại." });
      return;
    }
    setComplaintSubmitting(true);
    try {
      await createRaceComplaint({
        raceId: complaintModal.entry.raceId,
        type: complaintForm.type,
        reason: complaintForm.reason.trim(),
        evidenceDescription: complaintForm.evidenceDescription?.trim() || null,
      });
      setComplaintModal(null);
      setError("");
    } catch (err) {
      setComplaintMsg({ type: "error", text: err?.message || "Gửi khiếu nại thất bại." });
    } finally {
      setComplaintSubmitting(false);
    }
  };

  const renderRaceRow = (entry) => {
    const jockeyDisplay = getJockeyNameDisplay({
      jockeyId: entry.jockeyId,
      jockeyName: entry.jockeyName,
    });

    return (
      <div key={entry.entryId ?? `${entry.raceId}-${entry.horseId}`} className="op-race-row">
        <div className="op-race-row__top">
          <strong>{entry.raceName}</strong>
          <StatusBadge
            label={getOwnerRaceStatusLabel(entry.raceStatus)}
            tone={RACE_TONE[entry.raceStatusKey] ?? "neutral"}
          />
        </div>
        <p>{formatRoundLabel(entry)} · {apiToVNDisplay(entry.scheduledAt) || "Chưa xếp lịch"}</p>
        <p>
          Kỵ sĩ: {jockeyDisplay} · Cổng {entry.gateNumber ?? "chưa xếp"} · Kết quả: {formatRank(entry.finishPosition)}
        </p>
        {entry.raceStatusKey === "finished" && (
          <div className="op-race-row__actions">
            <RaceButton size="compact" variant="ghost" onClick={() => openComplaintModal(entry)}>
              Khiếu nại cuộc đua
            </RaceButton>
          </div>
        )}
      </div>
    );
  };

  const renderRegistrationCard = (registration, sectionKey) => {
    const relatedEntries = sortEntriesForDisplay(
      entriesFor(registration.tournamentId, registration.horseId),
    );
    const expanded = !!expandedRaces[registration.id];
    const visibleEntries = expanded ? relatedEntries : relatedEntries.slice(0, 1);
    const hiddenCount = Math.max(relatedEntries.length - 1, 0);
    const registrationTone = REGISTRATION_TONE[registration.statusKey] ?? "neutral";
    const tournamentTone = TOURNAMENT_TONE[registration.tournamentStatusKey] ?? "neutral";

    return (
      <article key={registration.id} className="op-card">
        <div className="op-card__head">
          <div className="op-card__title">
            <h3>{registration.tournamentName}</h3>
            <div className="op-card__badges">
              <StatusBadge label={registration.tournamentStatusLabel} tone={tournamentTone} />
              <StatusBadge label={registration.statusLabel} tone={registrationTone} />
            </div>
          </div>
          <div className="op-card__horse">
            <strong>{registration.horseName}</strong>
            <span>{formatDateRange(registration.tournamentStartDate, registration.tournamentEndDate)}</span>
          </div>
        </div>

        <p className="op-meta-line">
          Đăng ký: {formatShortDate(registration.createdAt)} · Duyệt: {formatShortDate(registration.approvedAt)}
          {registration.note ? ` · Ghi chú: ${registration.note}` : ""}
        </p>

        {relatedEntries.length > 0 ? (
          <div className="op-race-list">
            {visibleEntries.map(renderRaceRow)}
            {hiddenCount > 0 ? (
              <button
                type="button"
                className="op-disclosure"
                onClick={() => toggleRaceDisclosure(registration.id)}
              >
                {expanded ? "Thu gọn" : `Xem ${hiddenCount + 1} cuộc đua`}
              </button>
            ) : null}
          </div>
        ) : sectionKey !== "upcoming" ? (
          <p className="op-inline-empty">Chưa có phân công cuộc đua cho lượt tham gia này.</p>
        ) : null}
      </article>
    );
  };

  return (
    <div className="owner-page owner-participations">
      <div className="owner-content op-shell">
        <section className="op-page-head">
          <div>
            <h1>Tham gia của tôi</h1>
            <p>Theo dõi giải đấu, ngựa đăng ký, phân công cuộc đua và kết quả.</p>
          </div>
          <div className="op-tabs" role="tablist" aria-label="Nhóm lượt tham gia">
            {sections.map((section) => (
              <button
                key={section.key}
                type="button"
                role="tab"
                aria-selected={selectedKey === section.key}
                className={`op-tab ${selectedKey === section.key ? "op-tab--active" : ""}`}
                onClick={() => setActiveTab(section.key)}
              >
                {section.title} <span>{section.items.length}</span>
              </button>
            ))}
          </div>
        </section>

        {error ? <p className="op-error">{error}</p> : null}

        {isLoading ? (
          <section className="op-state-card">
            <strong>Đang tải lượt tham gia</strong>
            <span>Đang lấy danh sách đăng ký và cuộc đua liên quan.</span>
          </section>
        ) : registrations.length === 0 ? (
          <section className="op-empty op-empty--global">
            <div>
              <strong>Bạn chưa đăng ký giải đấu nào</strong>
              <span>Khi đăng ký ngựa vào giải đấu, trạng thái tham gia sẽ xuất hiện tại đây.</span>
            </div>
            <Link className="op-primary-link" to="/owner/register-tournament">
              Đăng ký giải đấu
            </Link>
          </section>
        ) : (
          <section className={`op-panel ${isSelectedEmpty ? "op-panel--empty" : ""}`} role="tabpanel">
            <div className="op-panel__head">
              <div>
                <h2>{selectedSection.title}</h2>
                <p>{selectedSection.hint}</p>
              </div>
              <span>{selectedSection.items.length} lượt</span>
            </div>

            {isSelectedEmpty ? (
              <p className="op-section-empty">{selectedSection.empty}</p>
            ) : (
              <div className="op-list">
                {selectedSection.items.map((registration) =>
                  renderRegistrationCard(registration, selectedSection.key),
                )}
              </div>
            )}
          </section>
        )}
      </div>

      {complaintModal && (
        <RaceModalShell
          title="Khiếu nại cuộc đua"
          description={complaintModal.entry.raceName}
          onClose={() => setComplaintModal(null)}
          footer={(
            <>
              <RaceButton variant="ghost" onClick={() => setComplaintModal(null)}>Hủy</RaceButton>
              <RaceButton loading={complaintSubmitting} disabled={complaintSubmitting} onClick={submitComplaint}>
                Gửi khiếu nại
              </RaceButton>
            </>
          )}
        >
          {complaintMsg && (
            <p className="rm-field__message rm-field__message--error">{complaintMsg.text}</p>
          )}
          <RaceSelect
            label="Loại khiếu nại"
            value={complaintForm.type}
            onChange={(e) => setComplaintForm((prev) => ({ ...prev, type: e.target.value }))}
            options={RACE_COMPLAINT_TYPE_OPTIONS}
          />
          <div className="rm-field">
            <label className="rm-field__label" htmlFor="op-complaint-reason">Nội dung</label>
            <textarea
              id="op-complaint-reason"
              className="rm-control"
              rows={4}
              value={complaintForm.reason}
              onChange={(e) => setComplaintForm((prev) => ({ ...prev, reason: e.target.value }))}
              placeholder="Mô tả nội dung khiếu nại..."
            />
          </div>
          <div className="rm-field">
            <label className="rm-field__label" htmlFor="op-complaint-evidence">Bằng chứng (tùy chọn)</label>
            <textarea
              id="op-complaint-evidence"
              className="rm-control"
              rows={2}
              value={complaintForm.evidenceDescription}
              onChange={(e) => setComplaintForm((prev) => ({ ...prev, evidenceDescription: e.target.value }))}
              placeholder="Mô tả bằng chứng liên quan (không bắt buộc)..."
            />
          </div>
        </RaceModalShell>
      )}
    </div>
  );
}

export default OwnerParticipationsPage;
