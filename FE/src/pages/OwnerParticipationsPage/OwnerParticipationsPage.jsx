import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getMyTournamentRegistrations, getMyRaceEntries } from "../../services/ownerHorseApi";
import {
  createRaceComplaint,
  getMyRaceComplaints,
  getPrizesByTournament,
  uploadRaceComplaintEvidence,
  withdrawRaceComplaint,
} from "../../services/managementApi";
import { getRaceEntries, getRaceResult, getTournament } from "../../services/spectatorApi";
import { apiToVNDisplay } from "../../utils/vnDateTime";
import { getRegistrationStatusLabel } from "../../utils/registrationStatusDisplay";
import { getOwnerRaceStatusLabel } from "../../utils/raceStatusDisplay";
import { getJockeyNameDisplay } from "../../utils/jockeyAssignmentDisplay";
import {
  EVIDENCE_ACCEPT_ATTR,
  RACE_COMPLAINT_TYPE_OPTIONS,
  canFilerMutateEvidence,
  canFilerWithdraw,
  getRaceComplaintStatusDetails,
  getRaceComplaintTypeLabel,
  groupComplaintEvidenceByUploader,
  hasFinalComplaintRuling,
  validateEvidenceFile,
} from "../../utils/raceComplaintDisplay";
import {
  OWNER_FINAL_RANKING_SUBTITLE,
  canShowOwnerComplaintCta,
  formatAffectsResult,
  getFinalRaceRanking,
  getOwnerResultStatusDetails,
  normalizeResultStatus,
} from "../../utils/ownerDemoDisplay";
import { buildRankingDisplayList } from "../../utils/raceResultDisplay";
import { RaceButton, RaceModalShell, RaceSelect } from "../../components/ui/RaceUi";
import RaceRankingPanel from "../../components/RaceRankingPanel";
import ComplaintEvidenceGallery from "../../components/ComplaintEvidenceGallery";
import ComplaintEvidenceUploader from "../../components/ComplaintEvidenceUploader";
import PrizeBreakdown from "../../components/PrizeBreakdown";
import "../../components/PrizeBreakdown.css";
import {
  getTournamentLifecycleLabel,
  normalizeTournamentStatus,
} from "../../utils/tournamentRegistration";
import "../OwnerSharedLayout.css";
import "./OwnerParticipationsPage.css";

const REGISTRATION_STATUS_NUM = { 1: "pending", 2: "approved", 3: "rejected", 4: "withdrawn" };
const RACE_STATUS_NUM = { 1: "scheduled", 2: "inprogress", 3: "finished", 4: "cancelled", 7: "registrationopen", 8: "registrationclosed" };

const REGISTRATION_TONE = { pending: "warning", approved: "success", rejected: "danger", withdrawn: "neutral" };
const TOURNAMENT_TONE = { draft: "neutral", published: "gold", ongoing: "live", finished: "success", cancelled: "danger" };
const RACE_TONE = { scheduled: "neutral", registrationopen: "gold", registrationclosed: "neutral", inprogress: "live", finished: "success", cancelled: "danger" };
const COMPLAINT_TONE = { pending: "warning", active: "gold", approved: "success", rejected: "danger", inactive: "neutral" };
const STATUS_PRIORITY = { inprogress: 0, registrationopen: 1, registrationclosed: 1, scheduled: 1, finished: 2, cancelled: 3 };

const field = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];

const normalizeKey = (value, numericMap = {}) => {
  if (value === null || value === undefined || value === "") return "";
  if (typeof value === "number") return numericMap[value] ?? String(value);
  const text = String(value).trim();
  if (/^\d+$/.test(text)) return numericMap[Number(text)] ?? text;
  return text.replace(/[\s_-]+/g, "").toLowerCase();
};

const getTournamentKey = (status) => normalizeTournamentStatus({ status }).toLowerCase();

const bucketOf = (registration) => {
  const tournamentStatus = normalizeTournamentStatus({ status: registration.tournamentStatus });
  if (tournamentStatus === "Ongoing") return "ongoing";
  if (tournamentStatus === "Finished" || tournamentStatus === "Cancelled") return "finished";
  return "upcoming";
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
  if (entry.roundName && entry.roundNumber != null) return `Vòng ${entry.roundNumber} · ${entry.roundName}`;
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
    const statusDelta = (STATUS_PRIORITY[a.raceStatusKey] ?? 4) - (STATUS_PRIORITY[b.raceStatusKey] ?? 4);
    if (statusDelta !== 0) return statusDelta;
    const resultDelta = (a.finishPosition == null ? 1 : 0) - (b.finishPosition == null ? 1 : 0);
    if (resultDelta !== 0) return resultDelta;
    return getEntrySortValue(a) - getEntrySortValue(b);
  });

const normalizeRaceResult = (result) => {
  if (!result) return null;
  return {
    raceId: field(result, "raceId", "RaceId"),
    resultStatus: field(result, "resultStatus", "ResultStatus"),
    rankings: field(result, "rankings", "Rankings") ?? [],
    notes: field(result, "notes", "Notes") ?? "",
    rejectedReason: field(result, "rejectedReason", "RejectedReason") ?? "",
  };
};

const normalizeComplaint = (complaint) => ({
  id: field(complaint, "id", "Id"),
  raceId: field(complaint, "raceId", "RaceId"),
  raceName: field(complaint, "raceName", "RaceName") ?? "Cuộc đua",
  tournamentName: field(complaint, "tournamentName", "TournamentName") ?? "",
  type: field(complaint, "type", "Type"),
  reason: field(complaint, "reason", "Reason") ?? "",
  evidenceDescription: field(complaint, "evidenceDescription", "EvidenceDescription") ?? "",
  status: field(complaint, "status", "Status") ?? "",
  refereeResponse: field(complaint, "refereeResponse", "RefereeResponse") ?? "",
  ruling: field(complaint, "ruling", "Ruling") ?? "",
  affectsResult: field(complaint, "affectsResult", "AffectsResult"),
  resolvedAt: field(complaint, "resolvedAt", "ResolvedAt"),
  createdAt: field(complaint, "createdAt", "CreatedAt"),
  evidence: field(complaint, "evidence", "Evidence") ?? [],
});

function StatusBadge({ label, tone = "neutral" }) {
  return <span className={`op-status op-status--${tone}`}>{label}</span>;
}

function OwnerParticipationsPage() {
  const [registrations, setRegistrations] = useState([]);
  const [entries, setEntries] = useState([]);
  const [raceResults, setRaceResults] = useState({});
  const [complaints, setComplaints] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isComplaintLoading, setIsComplaintLoading] = useState(false);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState(null);
  const [expandedRaces, setExpandedRaces] = useState({});
  const [expandedComplaints, setExpandedComplaints] = useState({});
  const [complaintModal, setComplaintModal] = useState(null);
  const [withdrawTarget, setWithdrawTarget] = useState(null);
  const [complaintForm, setComplaintForm] = useState({ type: "ResultJudging", reason: "", evidenceDescription: "" });
  const [complaintFiles, setComplaintFiles] = useState([]);
  const [complaintSubmitting, setComplaintSubmitting] = useState(false);
  const [complaintMsg, setComplaintMsg] = useState(null);
  const [withdrawingId, setWithdrawingId] = useState("");
  const [tournamentDetails, setTournamentDetails] = useState({});
  const [prizesByTournament, setPrizesByTournament] = useState({});
  const [raceEntriesByRaceId, setRaceEntriesByRaceId] = useState({});

  const refreshComplaints = useCallback(async () => {
    setIsComplaintLoading(true);
    try {
      const data = await getMyRaceComplaints();
      setComplaints((Array.isArray(data) ? data : []).map(normalizeComplaint));
    } catch (err) {
      setComplaintMsg({ type: "error", text: err?.message || "Không thể tải lịch sử khiếu nại." });
    } finally {
      setIsComplaintLoading(false);
    }
  }, []);

  useEffect(() => {
    let isMounted = true;
    const load = async () => {
      setIsLoading(true);
      setError("");
      try {
        const [regData, entryData, complaintData] = await Promise.all([
          getMyTournamentRegistrations(),
          getMyRaceEntries(),
          getMyRaceComplaints().catch(() => []),
        ]);
        if (!isMounted) return;

        const mappedRegistrations = (Array.isArray(regData) ? regData : []).map((r) => {
          const status = field(r, "status", "Status") ?? "Pending";
          const tournamentStatus = field(r, "tournamentStatus", "TournamentStatus") ?? "";
          return {
            id: field(r, "id", "Id"),
            horseId: field(r, "horseId", "HorseId"),
            horseName: field(r, "horseName", "HorseName") ?? "Không rõ",
            tournamentId: field(r, "tournamentId", "TournamentId"),
            tournamentName: field(r, "tournamentName", "TournamentName") ?? "Giải đấu",
            tournamentStatus,
            tournamentStatusKey: getTournamentKey(tournamentStatus),
            tournamentStatusLabel: getTournamentLifecycleLabel({ status: tournamentStatus }),
            tournamentStartDate: field(r, "tournamentStartDate", "TournamentStartDate"),
            tournamentEndDate: field(r, "tournamentEndDate", "TournamentEndDate"),
            statusKey: normalizeKey(status, REGISTRATION_STATUS_NUM),
            statusLabel: getRegistrationStatusLabel(status),
            createdAt: field(r, "createdAt", "CreatedAt"),
            approvedAt: field(r, "approvedAt", "ApprovedAt"),
            note: field(r, "note", "Note") ?? "",
          };
        });

        const mappedEntries = (Array.isArray(entryData) ? entryData : []).map((e) => {
          const raceStatus = field(e, "raceStatus", "RaceStatus") ?? "";
          const entryStatus = field(e, "status", "Status") ?? "";
          return {
            entryId: field(e, "entryId", "EntryId"),
            horseId: field(e, "horseId", "HorseId"),
            horseName: field(e, "horseName", "HorseName") ?? "",
            tournamentId: field(e, "tournamentId", "TournamentId"),
            tournamentName: field(e, "tournamentName", "TournamentName") ?? "",
            raceId: field(e, "raceId", "RaceId"),
            raceName: field(e, "raceName", "RaceName") ?? "Cuộc đua",
            roundNumber: field(e, "roundNumber", "RoundNumber"),
            roundName: field(e, "roundName", "RoundName"),
            scheduledAt: field(e, "scheduledAt", "ScheduledAt"),
            location: field(e, "location", "Location") ?? "",
            raceStatus,
            raceStatusKey: normalizeKey(raceStatus, RACE_STATUS_NUM),
            statusKey: normalizeKey(entryStatus, REGISTRATION_STATUS_NUM),
            statusLabel: getRegistrationStatusLabel(entryStatus),
            jockeyId: field(e, "jockeyId", "JockeyId"),
            jockeyName: field(e, "jockeyName", "JockeyName") ?? "",
            gateNumber: field(e, "gateNumber", "GateNumber"),
            finishPosition: field(e, "finishPosition", "FinishPosition") ?? null,
          };
        });

        setRegistrations(mappedRegistrations);
        setEntries(mappedEntries);
        setComplaints((Array.isArray(complaintData) ? complaintData : []).map(normalizeComplaint));

        const finishedRaceIds = [...new Set(mappedEntries.filter((entry) => entry.raceStatusKey === "finished").map((entry) => entry.raceId).filter(Boolean))];
        const resultPairs = await Promise.all(finishedRaceIds.map(async (raceId) => {
          try {
            return [raceId, normalizeRaceResult(await getRaceResult(raceId))];
          } catch {
            return [raceId, null];
          }
        }));
        if (isMounted) setRaceResults(Object.fromEntries(resultPairs.filter(([, result]) => result)));

        // Ranking rows cover every Horse in the Race, not just this Owner's own — `mappedEntries`
        // is scoped to this Owner's RaceEntries only, so it can't resolve the official Jockey for
        // any other Owner's Horse in the same ranking. GetRaceEntries (AllowAnonymous, public race
        // schedule data — same one Spectators use) returns every Approved entry for the Race,
        // Jockey included, so it's the only correct source for the ranking panel's Jockey lookup.
        const fullEntryPairs = await Promise.all(finishedRaceIds.map(async (raceId) => {
          try {
            return [raceId, await getRaceEntries(raceId)];
          } catch {
            return [raceId, []];
          }
        }));
        if (isMounted) setRaceEntriesByRaceId(Object.fromEntries(fullEntryPairs));

        const finishedTournamentIds = [...new Set(mappedRegistrations.filter((registration) => bucketOf(registration) === "finished").map((registration) => registration.tournamentId).filter(Boolean))];
        const artifactPairs = await Promise.all(finishedTournamentIds.map(async (tournamentId) => {
          const [tournament, prizes] = await Promise.all([
            getTournament(tournamentId).catch(() => null),
            getPrizesByTournament(tournamentId).catch(() => []),
          ]);
          return [tournamentId, { tournament, prizes: Array.isArray(prizes) ? prizes : [] }];
        }));
        if (isMounted) {
          setTournamentDetails(Object.fromEntries(artifactPairs.map(([id, value]) => [id, value.tournament]).filter(([, value]) => value)));
          setPrizesByTournament(Object.fromEntries(artifactPairs.map(([id, value]) => [id, value.prizes])));
        }
      } catch (err) {
        if (isMounted) setError(err?.message || "Không thể tải danh sách tham gia.");
      } finally {
        if (isMounted) setIsLoading(false);
      }
    };
    load();
    return () => { isMounted = false; };
  }, []);

  const entriesFor = (tournamentId, horseId) =>
    entries.filter((entry) => String(entry.tournamentId) === String(tournamentId) && String(entry.horseId) === String(horseId));

  // Full, race-wide RaceEntry list (all Horses/Jockeys, not just this Owner's) — the only correct
  // source for a ranking panel's Jockey lookup. See the raceEntriesByRaceId fetch above.
  const fullEntriesForRace = (raceId) => raceEntriesByRaceId[raceId] ?? [];

  const groups = useMemo(() => {
    const result = { upcoming: [], ongoing: [], finished: [] };
    registrations.forEach((registration) => { result[bucketOf(registration)].push(registration); });
    return result;
  }, [registrations]);

  const sections = [
    { key: "upcoming", title: "Sắp diễn ra", hint: "Đăng ký đang chờ bước tiếp theo.", empty: "Chưa có lượt tham gia sắp diễn ra.", items: groups.upcoming },
    { key: "ongoing", title: "Đang diễn ra", hint: "Theo dõi phân công và trạng thái cuộc đua.", empty: "Chưa có lượt tham gia đang diễn ra.", items: groups.ongoing },
    { key: "finished", title: "Đã kết thúc", hint: "Lịch sử tham gia, kết quả chính thức và giải thưởng.", empty: "Chưa có lượt tham gia đã kết thúc.", items: groups.finished },
  ];

  const defaultTab = groups.upcoming.length ? "upcoming" : groups.ongoing.length ? "ongoing" : groups.finished.length ? "finished" : "upcoming";
  const selectedKey = activeTab ?? defaultTab;
  const selectedSection = sections.find((section) => section.key === selectedKey) ?? sections[0];
  const isSelectedEmpty = selectedSection.items.length === 0;

  const toggleRaceDisclosure = (registrationId) => {
    setExpandedRaces((current) => ({ ...current, [registrationId]: !current[registrationId] }));
  };

  const toggleComplaintDisclosure = (complaintId) => {
    setExpandedComplaints((current) => ({ ...current, [complaintId]: !current[complaintId] }));
  };

  const openComplaintModal = (entry) => {
    setComplaintForm({ type: "ResultJudging", reason: "", evidenceDescription: "" });
    setComplaintFiles([]);
    setComplaintMsg(null);
    setComplaintModal({ entry });
  };

  const addComplaintFiles = (fileList) => {
    const picked = Array.from(fileList || []);
    const accepted = [];
    for (const file of picked) {
      const check = validateEvidenceFile(file);
      if (check.valid) accepted.push(file);
      else setComplaintMsg({ type: "error", text: check.error });
    }
    if (accepted.length > 0) setComplaintFiles((prev) => [...prev, ...accepted]);
  };

  const removeComplaintFile = (index) => {
    setComplaintFiles((prev) => prev.filter((_, i) => i !== index));
  };

  const submitComplaint = async () => {
    if (!complaintModal) return;
    if (!complaintForm.reason.trim()) {
      setComplaintMsg({ type: "error", text: "Vui lòng nhập nội dung khiếu nại." });
      return;
    }
    setComplaintSubmitting(true);
    try {
      const res = await createRaceComplaint({
        raceId: complaintModal.entry.raceId,
        type: complaintForm.type,
        reason: complaintForm.reason.trim(),
        evidenceDescription: complaintForm.evidenceDescription?.trim() || null,
      });
      const created = res?.data ?? res;
      let failedUploads = 0;
      for (const file of complaintFiles) {
        try {
          await uploadRaceComplaintEvidence(created.id ?? created.Id, file);
        } catch {
          failedUploads += 1;
        }
      }
      setComplaintModal(null);
      setComplaintFiles([]);
      await refreshComplaints();
      setComplaintMsg({
        type: failedUploads > 0 ? "error" : "success",
        text: failedUploads > 0
          ? "Khiếu nại đã được ghi nhận. Một số tệp chưa tải lên, bạn có thể thêm lại trong lịch sử khiếu nại."
          : "Khiếu nại đã được ghi nhận.",
      });
    } catch (err) {
      setComplaintMsg({ type: "error", text: err?.message || "Gửi khiếu nại thất bại." });
    } finally {
      setComplaintSubmitting(false);
    }
  };

  const confirmWithdrawComplaint = async () => {
    if (!withdrawTarget) return;
    const id = withdrawTarget.id;
    setWithdrawingId(id);
    try {
      await withdrawRaceComplaint(id);
      await refreshComplaints();
      setWithdrawTarget(null);
      setComplaintMsg({ type: "success", text: "Đã rút khiếu nại." });
    } catch (err) {
      setComplaintMsg({ type: "error", text: err?.message || "Không thể rút khiếu nại." });
    } finally {
      setWithdrawingId("");
    }
  };

  const renderRaceResult = (entry) => {
    const result = raceResults[entry.raceId];
    if (!result) return null;
    const details = getOwnerResultStatusDetails(result.resultStatus);
    return (
      <RaceRankingPanel
        title={details.label}
        rows={buildRankingDisplayList(result.rankings, fullEntriesForRace(entry.raceId))}
        isOfficial={details.isOfficial}
        rejectedReason={result.rejectedReason}
        notes={result.notes}
      />
    );
  };

  const renderRaceRow = (entry) => {
    const result = raceResults[entry.raceId];
    const resultDetails = getOwnerResultStatusDetails(result?.resultStatus);
    const jockeyDisplay = getJockeyNameDisplay({ jockeyId: entry.jockeyId, jockeyName: entry.jockeyName });
    const canComplain = canShowOwnerComplaintCta(entry, result);

    return (
      <div key={entry.entryId ?? `${entry.raceId}-${entry.horseId}`} className="op-race-row">
        <div className="op-race-row__top">
          <strong>{entry.raceName}</strong>
          <span className="op-race-row__badges">
            <StatusBadge label={getOwnerRaceStatusLabel(entry.raceStatus)} tone={RACE_TONE[entry.raceStatusKey] ?? "neutral"} />
            <StatusBadge label={resultDetails.label} tone={resultDetails.tone} />
          </span>
        </div>
        <p>{formatRoundLabel(entry)} · {apiToVNDisplay(entry.scheduledAt) || "Chưa xếp lịch"}</p>
        <p>{entry.jockeyId ? `Jockey chính thức: ${jockeyDisplay}` : "Chưa phân công Jockey"} · Cổng {entry.gateNumber ?? "chưa xếp"} · Kết quả của ngựa: {formatRank(entry.finishPosition)}</p>
        {renderRaceResult(entry)}
        {canComplain && (
          <div className="op-race-row__actions">
            <RaceButton size="compact" variant="ghost" onClick={() => openComplaintModal(entry)}>Khiếu nại cuộc đua</RaceButton>
          </div>
        )}
      </div>
    );
  };

  const renderFinishedHistory = (registration, relatedEntries) => {
    const tournament = tournamentDetails[registration.tournamentId];
    const prizes = prizesByTournament[registration.tournamentId] ?? [];
    const finalRanking = getFinalRaceRanking({ registration, entries: relatedEntries, raceResults, tournament });
    const prizePool = tournament?.prizePool ?? tournament?.PrizePool;
    if (!tournament && prizes.length === 0 && !finalRanking) return null;

    return (
      <section className="op-history-panel">
        <div className="op-history-panel__head">
          <div>
            <h4>Lịch sử giải đã kết thúc</h4>
            <p>Thông tin này chỉ là kết quả và cấu hình giải thưởng, không phải thanh toán ví.</p>
          </div>
          {prizePool != null ? <StatusBadge label={`Quỹ thưởng ${(Number(prizePool) || 0).toLocaleString("vi-VN")}đ`} tone="gold" /> : null}
        </div>
        {finalRanking ? (
          <div className="op-final-ranking">
            <span>{OWNER_FINAL_RANKING_SUBTITLE}</span>
            <RaceRankingPanel
              title={finalRanking.title}
              rows={buildRankingDisplayList(finalRanking.result?.rankings, fullEntriesForRace(finalRanking.entry.raceId))}
              isOfficial
            />
          </div>
        ) : (
          <p className="op-inline-empty">Chưa có xếp hạng chung cuộc từ kết quả chính thức của vòng Chung kết.</p>
        )}
        {prizes.length > 0 ? <PrizeBreakdown prizes={prizes} title="Cơ cấu giải thưởng" /> : null}
      </section>
    );
  };

  const renderRegistrationCard = (registration, sectionKey) => {
    const relatedEntries = sortEntriesForDisplay(entriesFor(registration.tournamentId, registration.horseId));
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
              <button type="button" className="op-disclosure" onClick={() => toggleRaceDisclosure(registration.id)}>
                {expanded ? "Thu gọn" : `Xem ${hiddenCount + 1} cuộc đua`}
              </button>
            ) : null}
          </div>
        ) : sectionKey !== "upcoming" ? (
          <p className="op-inline-empty">Chưa có phân công cuộc đua cho lượt tham gia này.</p>
        ) : null}
        {sectionKey === "finished" ? renderFinishedHistory(registration, relatedEntries) : null}
      </article>
    );
  };

  const renderComplaintCard = (complaint) => {
    const statusDetails = getRaceComplaintStatusDetails(complaint.status);
    const tone = COMPLAINT_TONE[statusDetails.variant] ?? "neutral";
    const expanded = !!expandedComplaints[complaint.id];
    const groupedEvidence = groupComplaintEvidenceByUploader(complaint.evidence);
    const filerCount = groupedEvidence.filerEvidence.length;
    const refereeCount = groupedEvidence.refereeEvidence.length;
    const canWithdraw = canFilerWithdraw(complaint.status);
    const canUploadMore = canFilerMutateEvidence(complaint.status);
    const isRuled = hasFinalComplaintRuling(complaint.status);

    return (
      <article key={complaint.id} className="op-complaint-card">
        <div className="op-complaint-card__head">
          <div>
            <h3>{complaint.raceName}</h3>
            <p>{complaint.tournamentName || "Không rõ giải đấu"} · {apiToVNDisplay(complaint.createdAt) || "Chưa rõ thời gian"}</p>
          </div>
          <StatusBadge label={statusDetails.label} tone={tone} />
        </div>
        <div className="op-complaint-summary">
          <span>{getRaceComplaintTypeLabel(complaint.type)}</span>
          <span>Bằng chứng của bạn: {filerCount} · Bằng chứng trọng tài: {refereeCount}</span>
        </div>
        <div className="op-complaint-actions">
          <RaceButton size="compact" variant="ghost" onClick={() => toggleComplaintDisclosure(complaint.id)}>
            {expanded ? "Thu gọn" : isRuled ? "Mở kết luận" : "Xem chi tiết"}
          </RaceButton>
          {canWithdraw ? (
            <RaceButton size="compact" variant="danger" disabled={withdrawingId === complaint.id} onClick={() => setWithdrawTarget(complaint)}>
              Rút khiếu nại
            </RaceButton>
          ) : null}
        </div>
        {expanded ? (
          <div className="op-complaint-detail">
            <section>
              <h4>Khiếu nại của bạn</h4>
              <dl>
                <div><dt>Loại</dt><dd>{getRaceComplaintTypeLabel(complaint.type)}</dd></div>
                <div><dt>Nội dung</dt><dd>{complaint.reason || "-"}</dd></div>
                <div><dt>Mô tả bằng chứng</dt><dd>{complaint.evidenceDescription || "Không có"}</dd></div>
              </dl>
              {filerCount > 0 ? (
                <ComplaintEvidenceGallery evidence={groupedEvidence.filerEvidence} complaintId={complaint.id} complaintStatus={complaint.status} viewerRole="filer" onDeleted={refreshComplaints} />
              ) : <p className="op-inline-empty">Chưa có bằng chứng từ người khiếu nại.</p>}
              {canUploadMore ? <ComplaintEvidenceUploader complaintId={complaint.id} currentCount={filerCount} onUploaded={refreshComplaints} /> : null}
            </section>
            <section>
              <h4>Giải trình trọng tài</h4>
              {complaint.refereeResponse ? <p>{complaint.refereeResponse}</p> : <p className="op-muted-copy">Chưa có giải trình trọng tài.</p>}
              {refereeCount > 0 ? <ComplaintEvidenceGallery evidence={groupedEvidence.refereeEvidence} /> : <p className="op-inline-empty">Chưa có bằng chứng từ trọng tài.</p>}
            </section>
            <section className="op-conclusion-section">
              <h4>Kết luận</h4>
              <dl className="op-conclusion-meta">
                <div className="op-conclusion-meta-item"><dt>Trạng thái</dt><dd><StatusBadge label={statusDetails.label} tone={tone} /></dd></div>
                <div className="op-conclusion-meta-item"><dt>Kết luận Admin</dt><dd>{complaint.ruling || "Chưa có kết luận"}</dd></div>
                {normalizeResultStatus(complaint.status) === "Upheld" || complaint.affectsResult != null ? (
                  <div className="op-conclusion-meta-item"><dt>Ảnh hưởng kết quả</dt><dd>{formatAffectsResult(complaint.affectsResult)}</dd></div>
                ) : null}
                <div className="op-conclusion-meta-item"><dt>Thời điểm xử lý</dt><dd>{apiToVNDisplay(complaint.resolvedAt) || "Chưa xử lý"}</dd></div>
              </dl>
            </section>
          </div>
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
            <p>Theo dõi giải đấu, ngựa đăng ký, phân công cuộc đua, kết quả và khiếu nại.</p>
          </div>
          <div className="op-tabs" role="tablist" aria-label="Nhóm lượt tham gia">
            {sections.map((section) => (
              <button key={section.key} type="button" role="tab" aria-selected={selectedKey === section.key} className={`op-tab ${selectedKey === section.key ? "op-tab--active" : ""}`} onClick={() => setActiveTab(section.key)}>
                {section.title} <span>{section.items.length}</span>
              </button>
            ))}
          </div>
        </section>

        {error ? <p className="op-error">{error}</p> : null}
        {complaintMsg ? <p className={complaintMsg.type === "error" ? "op-error" : "op-success"}>{complaintMsg.text}</p> : null}

        {isLoading ? (
          <section className="op-state-card"><strong>Đang tải lượt tham gia</strong><span>Đang lấy danh sách đăng ký, cuộc đua, kết quả và khiếu nại liên quan.</span></section>
        ) : registrations.length === 0 ? (
          <section className="op-empty op-empty--global">
            <div><strong>Bạn chưa đăng ký giải đấu nào</strong><span>Khi đăng ký ngựa vào giải đấu, trạng thái tham gia sẽ xuất hiện tại đây.</span></div>
            <Link className="op-primary-link" to="/owner/register-tournament">Đăng ký giải đấu</Link>
          </section>
        ) : (
          <section className={`op-panel ${isSelectedEmpty ? "op-panel--empty" : ""}`} role="tabpanel">
            <div className="op-panel__head">
              <div><h2>{selectedSection.title}</h2><p>{selectedSection.hint}</p></div>
              <span>{selectedSection.items.length} lượt</span>
            </div>
            {isSelectedEmpty ? <p className="op-section-empty">{selectedSection.empty}</p> : <div className="op-list">{selectedSection.items.map((registration) => renderRegistrationCard(registration, selectedSection.key))}</div>}
          </section>
        )}

        <section className="op-panel op-complaints-panel">
          <div className="op-panel__head">
            <div><h2>Lịch sử khiếu nại</h2><p>Theo dõi khiếu nại đã gửi, bằng chứng, giải trình trọng tài và kết luận Admin.</p></div>
            <span>{complaints.length} mục</span>
          </div>
          {isComplaintLoading ? <p className="op-section-empty">Đang tải khiếu nại...</p> : complaints.length === 0 ? <p className="op-section-empty">Chưa có khiếu nại nào.</p> : <div className="op-complaint-list">{complaints.map(renderComplaintCard)}</div>}
        </section>
      </div>

      {complaintModal && (
        <RaceModalShell
          title="Khiếu nại cuộc đua"
          description={complaintModal.entry.raceName}
          onClose={() => setComplaintModal(null)}
          footer={<><RaceButton variant="ghost" onClick={() => setComplaintModal(null)}>Hủy</RaceButton><RaceButton loading={complaintSubmitting} disabled={complaintSubmitting} onClick={submitComplaint}>Gửi khiếu nại</RaceButton></>}
        >
          {complaintMsg?.type === "error" && <p className="rm-field__message rm-field__message--error">{complaintMsg.text}</p>}
          <RaceSelect label="Loại khiếu nại" value={complaintForm.type} onChange={(e) => setComplaintForm((prev) => ({ ...prev, type: e.target.value }))} options={RACE_COMPLAINT_TYPE_OPTIONS} />
          <div className="rm-field">
            <label className="rm-field__label" htmlFor="op-complaint-reason">Nội dung</label>
            <textarea id="op-complaint-reason" className="rm-control" rows={4} value={complaintForm.reason} onChange={(e) => setComplaintForm((prev) => ({ ...prev, reason: e.target.value }))} placeholder="Mô tả nội dung khiếu nại..." />
          </div>
          <div className="rm-field">
            <label className="rm-field__label" htmlFor="op-complaint-evidence">Bằng chứng (tùy chọn)</label>
            <textarea id="op-complaint-evidence" className="rm-control" rows={2} value={complaintForm.evidenceDescription} onChange={(e) => setComplaintForm((prev) => ({ ...prev, evidenceDescription: e.target.value }))} placeholder="Mô tả bằng chứng liên quan..." />
          </div>
          <div className="rm-field">
            <label className="rm-field__label" htmlFor="op-complaint-files">Ảnh / video bằng chứng (tùy chọn)</label>
            <input id="op-complaint-files" type="file" multiple accept={EVIDENCE_ACCEPT_ATTR} onChange={(e) => { addComplaintFiles(e.target.files); e.target.value = ""; }} />
            {complaintFiles.length > 0 && (
              <ul className="op-file-list">
                {complaintFiles.map((file, index) => <li key={`${file.name}-${index}`}><span>{file.name}</span><button type="button" className="ghost-button" onClick={() => removeComplaintFile(index)}>Xóa</button></li>)}
              </ul>
            )}
          </div>
        </RaceModalShell>
      )}

      {withdrawTarget && (
        <RaceModalShell
          title="Rút khiếu nại"
          description={withdrawTarget.raceName}
          onClose={() => setWithdrawTarget(null)}
          footer={<><RaceButton variant="ghost" onClick={() => setWithdrawTarget(null)}>Hủy</RaceButton><RaceButton variant="danger" loading={withdrawingId === withdrawTarget.id} onClick={confirmWithdrawComplaint}>Rút khiếu nại</RaceButton></>}
        >
          <p className="op-muted-copy">Bạn có chắc muốn rút khiếu nại này? Trạng thái sẽ được cập nhật thành Đã rút khiếu nại.</p>
        </RaceModalShell>
      )}
    </div>
  );
}

export default OwnerParticipationsPage;
