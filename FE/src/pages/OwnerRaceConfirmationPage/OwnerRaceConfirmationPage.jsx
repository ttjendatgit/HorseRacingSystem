import { useCallback, useEffect, useMemo, useState } from "react";
import { finalConfirmJockey, getMyRaceEntries } from "../../services/ownerHorseApi";
import { getOwnerRaceStatusLabel, getOwnerRaceStatusTone } from "../../utils/raceStatusDisplay";
import { getRegistrationStatusLabel, getRegistrationStatusTone } from "../../utils/registrationStatusDisplay";
import { getJockeyNameDisplay, getJockeyConfirmedDisplay } from "../../utils/jockeyAssignmentDisplay";
import "../OwnerSharedLayout.css";
import "./OwnerRaceConfirmationPage.css";

const field = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];

const formatDate = (value) => {
  if (!value) return "Chưa xác định";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa xác định";
  return new Intl.DateTimeFormat("vi-VN", {
    hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric",
  }).format(date);
};

function InfoRow({ label, value, tone }) {
  return (
    <div className="orc-info-row">
      <span>{label}</span>
      {tone ? (
        <strong className={`orc-status-pill orc-status-pill--${tone}`}>{value || "--"}</strong>
      ) : (
        <strong>{value || "--"}</strong>
      )}
    </div>
  );
}

export default function OwnerRaceConfirmationPage() {
  const [entries, setEntries] = useState([]);
  const [selected, setSelected] = useState(null);
  const [detailMode, setDetailMode] = useState("race");
  const [loading, setLoading] = useState(true);
  const [finalConfirmingId, setFinalConfirmingId] = useState(null);
  const [selectedInvitationByEntry, setSelectedInvitationByEntry] = useState({});
  const [message, setMessage] = useState("");
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [showTimeline, setShowTimeline] = useState(false);
  const [now] = useState(() => Date.now());

  const normalize = (entry) => ({
    entryId: field(entry, "entryId", "EntryId") ?? entry.id,
    raceId: field(entry, "raceId", "RaceId"),
    raceName: field(entry, "raceName", "RaceName") ?? "Cuộc đua",
    tournamentName: field(entry, "tournamentName", "TournamentName") ?? "",
    horseId: field(entry, "horseId", "HorseId"),
    horseName: field(entry, "horseName", "HorseName") ?? "Ngựa",
    jockeyId: field(entry, "jockeyId", "JockeyId") ?? null,
    jockeyName: field(entry, "jockeyName", "JockeyName") ?? "",
    scheduledAt: field(entry, "scheduledAt", "ScheduledAt"),
    location: field(entry, "location", "Location") ?? "Chưa xác định",
    distance: field(entry, "distance", "Distance"),
    maxParticipants: field(entry, "maxParticipants", "MaxParticipants"),
    raceStatus: field(entry, "raceStatus", "RaceStatus") ?? "",
    status: field(entry, "status", "Status") ?? "Pending",
    jockeyConfirmed: Boolean(field(entry, "jockeyConfirmed", "JockeyConfirmed")),
    gateNumber: field(entry, "gateNumber", "GateNumber"),
    // J3: candidates for Owner Final Confirm — Accepted invitations for this exact Horse+Race.
    acceptedInvitations: (field(entry, "acceptedInvitations", "AcceptedInvitations") ?? []).map((inv) => ({
      invitationId: field(inv, "invitationId", "InvitationId"),
      jockeyId: field(inv, "jockeyId", "JockeyId"),
      jockeyName: field(inv, "jockeyName", "JockeyName") ?? "",
    })),
  });

  const load = useCallback(async (preserveId) => {
    try {
      const data = await getMyRaceEntries();
      const list = (Array.isArray(data) ? data : []).map(normalize);
      setEntries(list);
      setSelected(list.find((item) => item.entryId === preserveId) ?? list[0] ?? null);
    } catch (error) {
      setMessage(`Không thể tải lịch đua: ${error.message}`);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleFinalConfirm = async (entry, invitationId) => {
    const invitation = entry.acceptedInvitations.find((inv) => inv.invitationId === invitationId);
    if (!invitation) return;
    const jockeyLabel = invitation.jockeyName || "kỵ sĩ này";
    if (!window.confirm(`Xác nhận chọn ${jockeyLabel} làm kỵ sĩ chính thức cho ${entry.horseName} trong ${entry.raceName}?`)) return;
    try {
      setFinalConfirmingId(entry.entryId);
      setMessage("");
      await finalConfirmJockey(entry.horseId, entry.raceId, invitationId);
      setMessage("Đã chọn kỵ sĩ chính thức.");
      await load(entry.entryId);
    } catch (error) {
      setMessage(`Không thể chọn kỵ sĩ chính thức: ${error.message}`);
    } finally {
      setFinalConfirmingId(null);
    }
  };

  const sorted = useMemo(() => [...entries].sort((a, b) => new Date(a.scheduledAt || 0) - new Date(b.scheduledAt || 0)), [entries]);
  const filtered = useMemo(() => sorted.filter((item) => {
    const matchesSearch = `${item.raceName} ${item.horseName} ${item.tournamentName}`.toLowerCase().includes(search.trim().toLowerCase());
    const time = item.scheduledAt ? new Date(item.scheduledAt).getTime() : 0;
    const matchesFilter = filter === "all" || (filter === "upcoming" ? time >= now : time < now);
    return matchesSearch && matchesFilter;
  }), [sorted, search, filter, now]);
  const upcomingCount = sorted.filter((item) => item.scheduledAt && new Date(item.scheduledAt).getTime() >= now).length;
  const todayCount = sorted.filter((item) => item.scheduledAt && new Date(item.scheduledAt).toDateString() === new Date(now).toDateString()).length;
  const groups = useMemo(() => filtered.reduce((result, item) => {
    const key = item.scheduledAt ? new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "short" }).format(new Date(item.scheduledAt)) : "Chưa xác định";
    (result[key] ||= []).push(item);
    return result;
  }, {}), [filtered]);

  return <div className="owner-page orc-page">
    <section className="page-header">
      <h1>Lịch đua</h1>
      <p>Theo dõi các cuộc đua của ngựa bạn và chọn kỵ sĩ chính thức.</p>
    </section>

    <div className="orc-summary">
      <div className="orc-stat"><span>Cuộc đua</span><strong>{sorted.length}</strong></div>
      <div className="orc-stat"><span>Sắp diễn ra</span><strong>{upcomingCount}</strong></div>
      <div className="orc-stat"><span>Hôm nay</span><strong>{todayCount}</strong></div>
    </div>

    {message && <p className={message.startsWith("Đã") ? "form-success" : "form-error"}>{message}</p>}

    <div className="orc-toolbar">
      <div className="orc-search">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="11" cy="11" r="8" /><path d="m21 21-4.35-4.35" /></svg>
        <input
          aria-label="Tìm cuộc đua, giải đấu, ngựa"
          placeholder="Tìm cuộc đua, giải đấu, ngựa..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>
      <div className="orc-filter-group">
        {[["all", "Tất cả"], ["upcoming", "Sắp tới"], ["completed", "Đã qua"]].map(([value, label]) => (
          <button key={value} className={`orc-filter-btn ${filter === value ? "orc-filter-btn--active" : ""}`} onClick={() => setFilter(value)}>{label}</button>
        ))}
      </div>
      <button className={`orc-timeline-toggle ${showTimeline ? "orc-timeline-toggle--active" : ""}`} onClick={() => setShowTimeline((value) => !value)}>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" /></svg>
        Timeline
      </button>
    </div>

    {loading ? (
      <div className="orc-loading"><div className="orc-skeleton" /><div className="orc-skeleton" /><div className="orc-skeleton" /></div>
    ) : filtered.length === 0 ? (
      <div className="orc-empty"><h3>Không có cuộc đua</h3><p>Chưa có cuộc đua phù hợp.</p></div>
    ) : <>
      <div className="orc-layout">
        <div className="orc-list">
          {filtered.map((item) => (
            <div
              key={item.entryId}
              className={`orc-race-card ${selected?.entryId === item.entryId ? "orc-race-card--active" : ""}`}
              onClick={() => { setSelected(item); setDetailMode("race"); }}
            >
              <div className="orc-race-card__top">
                <div className="orc-race-card__icon">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" /></svg>
                </div>
                <div className="orc-race-card__title">
                  <h3>{item.raceName}</h3>
                  <p>{item.horseName}{item.tournamentName ? ` · ${item.tournamentName}` : ""}</p>
                </div>
                <span className={`orc-badge ${item.jockeyId ? "orc-badge--pass" : "orc-badge--caution"}`}>
                  {item.jockeyId ? "Jockey chính thức" : "Chưa chọn Jockey chính thức"}
                </span>
              </div>
              <div className="orc-race-card__meta">
                {/* RaceStatus is authoritative for lifecycle — never a separate time-only badge
                    that could disagree with the detail panel's "Trạng thái cuộc đua". */}
                <span className={`orc-tag orc-tag--${getOwnerRaceStatusTone(item.raceStatus)}`}>
                  {getOwnerRaceStatusLabel(item.raceStatus)}
                </span>
                <span>{formatDate(item.scheduledAt)}</span>
                <span>{item.location}</span>
                {item.distance ? <span>{item.distance}m</span> : null}
              </div>
            </div>
          ))}
        </div>

        <div className="orc-detail">
          {selected ? <>
            <div className="orc-detail-tabs">
              <button className={`orc-detail-tab ${detailMode === "race" ? "orc-detail-tab--active" : ""}`} onClick={() => setDetailMode("race")}>Cuộc đua</button>
              <button className={`orc-detail-tab ${detailMode === "horse" ? "orc-detail-tab--active" : ""}`} onClick={() => setDetailMode("horse")}>Ngựa của tôi</button>
            </div>

            {detailMode === "race" ? <>
              <div className="orc-detail-heading">
                <h2>{selected.raceName}</h2>
                <p>{selected.tournamentName}</p>
              </div>

              <div className="orc-info-group">
                <h4>Thời gian &amp; địa điểm</h4>
                <div className="orc-info-rows">
                  <InfoRow label="Thời gian" value={formatDate(selected.scheduledAt)} />
                  <InfoRow label="Đường đua" value={selected.location} />
                  <InfoRow label="Cự ly" value={selected.distance ? `${selected.distance}m` : "--"} />
                  <InfoRow label="Số người tối đa" value={selected.maxParticipants} />
                </div>
              </div>

              <div className="orc-info-group">
                <h4>Trạng thái</h4>
                <div className="orc-info-rows">
                  <InfoRow
                    label="Trạng thái cuộc đua"
                    value={getOwnerRaceStatusLabel(selected.raceStatus)}
                    tone={getOwnerRaceStatusTone(selected.raceStatus)}
                  />
                </div>
              </div>
            </> : <>
              <div className="orc-detail-heading">
                <h2>{selected.horseName}</h2>
                <p>Thông tin tham gia cuộc đua</p>
              </div>

              <div className="orc-info-group">
                <h4>Jockey chính thức</h4>
                <div className="orc-info-rows">
                  <InfoRow label="Jockey" value={getJockeyNameDisplay(selected)} />
                  <InfoRow label="Kỵ sĩ xác nhận" value={getJockeyConfirmedDisplay(selected)} />
                </div>
              </div>

              <div className="orc-info-group">
                <h4>Trạng thái tham gia</h4>
                <div className="orc-info-rows">
                  <InfoRow label="Cổng xuất phát" value={selected.gateNumber ?? "Chưa xếp"} />
                  <InfoRow
                    label="Trạng thái tham gia"
                    value={getRegistrationStatusLabel(selected.status)}
                    tone={getRegistrationStatusTone(selected.status)}
                  />
                </div>
              </div>

              {selected.jockeyId == null && (
                <div className="orc-jockey-picker">
                  <h4>Chọn kỵ sĩ chính thức</h4>
                  {selected.acceptedInvitations.length === 0 ? (
                    <p className="orc-jockey-picker__empty">Chưa có kỵ sĩ nào chấp nhận lời mời.</p>
                  ) : (
                    <div className="orc-jockey-picker__controls">
                      <select
                        className="orc-jockey-select"
                        value={selectedInvitationByEntry[selected.entryId] ?? ""}
                        onChange={(e) => setSelectedInvitationByEntry((prev) => ({ ...prev, [selected.entryId]: e.target.value }))}
                      >
                        <option value="">-- Chọn kỵ sĩ --</option>
                        {selected.acceptedInvitations.map((inv) => (
                          <option key={inv.invitationId} value={inv.invitationId}>Đã chấp nhận lời mời · {inv.jockeyName || "Kỵ sĩ"}</option>
                        ))}
                      </select>
                      <button
                        className="primary-button"
                        disabled={!selectedInvitationByEntry[selected.entryId] || finalConfirmingId === selected.entryId}
                        onClick={() => handleFinalConfirm(selected, selectedInvitationByEntry[selected.entryId])}
                      >
                        {finalConfirmingId === selected.entryId ? "Đang xác nhận..." : "Chọn kỵ sĩ chính thức"}
                      </button>
                    </div>
                  )}
                </div>
              )}
            </>}
          </> : <p className="orc-detail-empty">Chọn một cuộc đua để xem chi tiết.</p>}
        </div>
      </div>

      {showTimeline && (
        <div className="orc-timeline">
          <h3>Lịch đua</h3>
          <div className="orc-tl-list">
            {Object.entries(groups).map(([date, items]) => (
              <div className="orc-tl-day" key={date}>
                <div className="orc-tl-line"><span className="orc-tl-dot" /><div className="orc-tl-bar" /></div>
                <div className="orc-tl-content">
                  <span className="orc-tl-date">{date}</span>
                  {items.map((item) => (
                    <div className="orc-tl-race" key={item.entryId} onClick={() => { setSelected(item); setDetailMode("race"); }}>
                      <div><strong>{item.raceName}</strong><span>{item.horseName} · {formatDate(item.scheduledAt)}</span></div>
                      <span className={`orc-badge ${item.jockeyId ? "orc-badge--pass" : "orc-badge--caution"}`}>{item.jockeyId ? "Jockey chính thức" : "Chưa chọn"}</span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </>}
  </div>;
}
