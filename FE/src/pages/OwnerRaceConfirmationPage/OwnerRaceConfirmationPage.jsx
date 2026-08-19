import { useCallback, useEffect, useMemo, useState } from "react";
import { confirmRaceEntry, getMyRaceEntries } from "../../services/ownerHorseApi";
import { getOwnerRaceStatusLabel } from "../../utils/raceStatusDisplay";
import "../JockeySchedulePage/JockeySchedulePage.css";

const field = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];

const formatDate = (value) => {
  if (!value) return "Chưa xác định";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa xác định";
  return new Intl.DateTimeFormat("vi-VN", {
    hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric",
  }).format(date);
};

function DetailBlock({ label, value }) {
  return <div className="js-block"><div><span className="js-block-label">{label}</span><strong className="js-block-value">{value || "--"}</strong></div></div>;
}

export default function OwnerRaceConfirmationPage() {
  const [entries, setEntries] = useState([]);
  const [selected, setSelected] = useState(null);
  const [detailMode, setDetailMode] = useState("race");
  const [loading, setLoading] = useState(true);
  const [confirmingId, setConfirmingId] = useState(null);
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
    jockeyName: field(entry, "jockeyName", "JockeyName") ?? "Chưa có kỵ sĩ",
    scheduledAt: field(entry, "scheduledAt", "ScheduledAt"),
    location: field(entry, "location", "Location") ?? "Chưa xác định",
    distance: field(entry, "distance", "Distance"),
    maxParticipants: field(entry, "maxParticipants", "MaxParticipants"),
    raceStatus: field(entry, "raceStatus", "RaceStatus") ?? "",
    status: field(entry, "status", "Status") ?? "Pending",
    ownerConfirmed: Boolean(field(entry, "ownerConfirmed", "OwnerConfirmed")),
    jockeyConfirmed: Boolean(field(entry, "jockeyConfirmed", "JockeyConfirmed")),
    gateNumber: field(entry, "gateNumber", "GateNumber"),
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

  const handleConfirm = async (entry) => {
    try {
      setConfirmingId(entry.entryId);
      setMessage("");
      await confirmRaceEntry(entry.raceId, entry.entryId);
      setMessage("Đã xác nhận tham gia cuộc đua.");
      await load(entry.entryId);
    } catch (error) {
      setMessage(`Không thể xác nhận: ${error.message}`);
    } finally {
      setConfirmingId(null);
    }
  };

  const sorted = useMemo(() => [...entries].sort((a, b) => new Date(a.scheduledAt || 0) - new Date(b.scheduledAt || 0)), [entries]);
  const filtered = useMemo(() => sorted.filter((item) => {
    const matchesSearch = `${item.raceName} ${item.horseName} ${item.tournamentName}`.toLowerCase().includes(search.trim().toLowerCase());
    const time = item.scheduledAt ? new Date(item.scheduledAt).getTime() : 0;
    const matchesFilter = filter === "all" || (filter === "upcoming" ? time >= now : time < now);
    return matchesSearch && matchesFilter;
  }), [sorted, search, filter, now]);
  const pending = sorted.filter((item) => !item.ownerConfirmed);
  const upcomingCount = sorted.filter((item) => item.scheduledAt && new Date(item.scheduledAt).getTime() >= now).length;
  const todayCount = sorted.filter((item) => item.scheduledAt && new Date(item.scheduledAt).toDateString() === new Date(now).toDateString()).length;
  const groups = useMemo(() => filtered.reduce((result, item) => {
    const key = item.scheduledAt ? new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "short" }).format(new Date(item.scheduledAt)) : "Chưa xác định";
    (result[key] ||= []).push(item);
    return result;
  }, {}), [filtered]);

  return <div className="js-page">
    <div className="js-header">
      <h1>Lịch đua</h1>
      <p className="js-sub">Theo dõi và xác nhận các cuộc đua của ngựa bạn.</p>
      <div className="js-summary">
        <span className="js-sum-chip"><strong>{sorted.length}</strong> Cuộc đua</span>
        <span className="js-sum-chip"><strong>{upcomingCount}</strong> Sắp diễn ra</span>
        <span className="js-sum-chip"><strong>{todayCount}</strong> Hôm nay</span>
      </div>
    </div>

    {message && <div className={message.startsWith("Đã") ? "form-success" : "form-error"}>{message}</div>}

    {pending.length > 0 && <div className="js-pending">
      <div className="js-pending__header"><h3>Chờ bạn xác nhận tham gia ({pending.length})</h3><span>Cuộc đua chỉ bắt đầu khi chủ ngựa xác nhận</span></div>
      <div className="js-pending__list">{pending.map((item) => <div key={item.entryId} className="js-pending__item">
        <div><strong>{item.raceName}</strong><span>{item.horseName} · {item.tournamentName} · {formatDate(item.scheduledAt)}</span></div>
        <button className="js-btn js-btn--primary" disabled={confirmingId === item.entryId} onClick={() => handleConfirm(item)}>{confirmingId === item.entryId ? "Đang xác nhận..." : "Xác nhận"}</button>
      </div>)}</div>
    </div>}

    <div className="js-toolbar">
      <div className="js-search"><span>⌕</span><input placeholder="Tìm cuộc đua, giải đấu, ngựa..." value={search} onChange={(e) => setSearch(e.target.value)} /></div>
      <div className="js-filter-group">{[["all","Tất cả"],["upcoming","Sắp tới"],["completed","Đã qua"]].map(([value,label]) => <button key={value} className={`js-filter-btn ${filter === value ? "js-filter-btn--active" : ""}`} onClick={() => setFilter(value)}>{label}</button>)}</div>
      <button className={`js-pill ${showTimeline ? "js-pill--active" : ""}`} onClick={() => setShowTimeline((value) => !value)}>◷ Timeline</button>
    </div>

    {loading ? <div className="js-loading"><div className="js-skeleton"/><div className="js-skeleton"/></div> : filtered.length === 0 ? <div className="js-empty"><h3>Không có cuộc đua</h3><p>Chưa có cuộc đua phù hợp.</p></div> : <>
      <div className="js-layout">
        <div className="js-list">{filtered.map((item) => <div key={item.entryId} className={`js-card ${selected?.entryId === item.entryId ? "js-card--active" : ""}`} onClick={() => { setSelected(item); setDetailMode("race"); }}>
          <div className="js-card__top"><div className="js-card__icon">◷</div><div><h3>{item.raceName}</h3><p className="js-card__sub">{item.horseName}{item.tournamentName ? ` · ${item.tournamentName}` : ""}</p></div><span className={`js-badge ${item.ownerConfirmed ? "js-badge--ok" : "js-badge--warn"}`}>{item.ownerConfirmed ? "Đã xác nhận" : "Chờ xác nhận"}</span></div>
          <div className="js-card__meta"><span>{formatDate(item.scheduledAt)}</span><span>{item.location}</span>{item.distance && <span>{item.distance}m</span>}</div>
        </div>)}</div>
        <div className="js-detail">{selected ? <><div className="js-detail-tabs"><button className={`js-dt ${detailMode === "race" ? "js-dt--active" : ""}`} onClick={() => setDetailMode("race")}>Cuộc đua</button><button className={`js-dt ${detailMode === "horse" ? "js-dt--active" : ""}`} onClick={() => setDetailMode("horse")}>Ngựa của tôi</button></div>
          {detailMode === "race" ? <><h2 className="js-detail-title">{selected.raceName}</h2><p className="js-detail-sub">{selected.tournamentName}</p><div className="js-blocks"><DetailBlock label="Thời gian" value={formatDate(selected.scheduledAt)} /><DetailBlock label="Đường đua" value={selected.location} /><DetailBlock label="Cự ly" value={selected.distance ? `${selected.distance}m` : "--"} /><DetailBlock label="Số người tối đa" value={selected.maxParticipants} /><DetailBlock label="Trạng thái" value={getOwnerRaceStatusLabel(selected.raceStatus)} /></div></> : <><h2 className="js-detail-title">{selected.horseName}</h2><p className="js-detail-sub">Thông tin tham gia cuộc đua</p><div className="js-blocks"><DetailBlock label="Kỵ sĩ" value={selected.jockeyName} /><DetailBlock label="Kỵ sĩ xác nhận" value={selected.jockeyConfirmed ? "Đã xác nhận" : "Chờ xác nhận"} /><DetailBlock label="Cổng xuất phát" value={selected.gateNumber ?? "Chưa xếp"} /><DetailBlock label="Trạng thái đăng ký" value={selected.status} /></div></>}
          <div className="js-detail-actions">{!selected.ownerConfirmed && <button className="js-btn js-btn--primary" disabled={confirmingId === selected.entryId} onClick={() => handleConfirm(selected)}>{confirmingId === selected.entryId ? "Đang xác nhận..." : "Xác nhận tham gia cuộc đua"}</button>}</div></> : <p className="js-detail-empty">Chọn một cuộc đua để xem chi tiết.</p>}</div>
      </div>
      {showTimeline && <div className="js-timeline"><h3>Lịch đua</h3><div className="js-tl-list">{Object.entries(groups).map(([date, items]) => <div className="js-tl-day" key={date}><div className="js-tl-line"><span className="js-tl-dot"/><div className="js-tl-bar"/></div><div className="js-tl-content"><span className="js-tl-date">{date}</span>{items.map((item) => <div className="js-tl-race" key={item.entryId} onClick={() => { setSelected(item); setDetailMode("race"); }}><div><strong>{item.raceName}</strong><span>{item.horseName} · {formatDate(item.scheduledAt)}</span></div><span className={`js-badge ${item.ownerConfirmed ? "js-badge--ok" : "js-badge--warn"}`}>{item.ownerConfirmed ? "Đã xác nhận" : "Chờ"}</span></div>)}</div></div>)}</div></div>}
    </>}
  </div>;
}
