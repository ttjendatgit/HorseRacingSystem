import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { deleteHorse, getMyHorses, inviteJockeyToHorse, removeJockeyFromHorse } from "../../services/ownerHorseApi";
import { getAvailableJockeys } from "../../services/jockeyApi";
import { resolveApiUrl } from "../../services/apiClient";
import { isJockeyRole } from "../../services/authRoleUtils";
import { isInvitationOfficial } from "../../utils/jockeyAssignmentDisplay";
import "./OwnerHorseListPage.css";

const approvalStatusMap = { 1: "Chờ duyệt", 2: "Đã duyệt", 3: "Từ chối" };
const statusClass = { 1: "pending", 2: "approved", 3: "rejected" };

// J2 lifecycle guard follow-up: backend is authoritative (HorseService.InviteJockeyAsync);
// this only filters the Owner's race picker for UX so it doesn't offer a Race/Tournament the
// backend would reject anyway. Never derive this from ScheduledAt — only actual Status counts.
const INVITABLE_TOURNAMENT_STATUSES = new Set([1, 2]); // Published, Ongoing
const INVITABLE_RACE_STATUSES = new Set([1, 7, 8]); // Scheduled, RegistrationOpen, RegistrationClosed

function isRaceInvitable(race) {
  const tournament = race?.tournament ?? race?.Tournament;
  const tournamentStatus = Number(tournament?.status ?? tournament?.Status);
  const raceStatus = Number(race?.status ?? race?.Status);
  return INVITABLE_TOURNAMENT_STATUSES.has(tournamentStatus) && INVITABLE_RACE_STATUSES.has(raceStatus);
}

function OwnerHorseListPage() {
  // Task B Final Correction §5: Owner-only actions (Create/Edit/Delete) hidden for Jockey — UX
  // only, backend [Authorize(Roles="HorseOwner,Admin")] on those endpoints is authoritative.
  const canManageHorses = !isJockeyRole();
  const [horses, setHorses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("Tất cả");

  const [assignHorse, setAssignHorse] = useState(null);
  const [jockeys, setJockeys] = useState([]);
  const [selectedTournament, setSelectedTournament] = useState("");
  const [selectedRace, setSelectedRace] = useState("");
  const [selectedJockey, setSelectedJockey] = useState("");
  const [invitationMessage, setInvitationMessage] = useState("");
  const [jockeyError, setJockeyError] = useState("");
  const [jockeyLoading, setJockeyLoading] = useState(false);

  const [cancelHorse, setCancelHorse] = useState(null);
  const [selectedCancelTournament, setSelectedCancelTournament] = useState("");
  const [selectedCancelInvitationId, setSelectedCancelInvitationId] = useState("");
  const [cancelReason, setCancelReason] = useState("");
  const [cancelError, setCancelError] = useState("");

  const loadHorses = async () => {
    setLoading(true);
    try {
      const data = await getMyHorses();
      setHorses(Array.isArray(data) ? data : []);
    } catch (e) { setError(e.message || "Không thể tải danh sách ngựa."); }
    finally { setLoading(false); }
  };

  const openCancel = (horse) => {
    setCancelHorse(horse);
    setSelectedCancelTournament("");
    setSelectedCancelInvitationId("");
    setCancelReason("");
    setCancelError("");
  };

  const submitCancel = async () => {
    if (!selectedCancelInvitationId) { setCancelError("Vui lòng chọn kỵ sĩ cần hủy."); return; }
    if (!cancelReason.trim()) { setCancelError("Vui lòng nhập lý do hủy kỵ sĩ."); return; }
    // J2 follow-up: cancel is keyed by the exact invitation, not just the race, since a race
    // may now have more than one Pending/Accepted invitation from different jockeys.
    const invitations = cancelHorse.jockeyInvitations ?? cancelHorse.JockeyInvitations ?? [];
    const invitation = invitations.find(inv => (inv.id ?? inv.Id) === selectedCancelInvitationId);
    if (!invitation) { setCancelError("Không tìm thấy lời mời cần hủy."); return; }
    const raceId = invitation.raceId ?? invitation.RaceId;
    setLoading(true);
    try {
      await removeJockeyFromHorse(cancelHorse.id ?? cancelHorse.Id, raceId, selectedCancelInvitationId, cancelReason.trim());
      setCancelHorse(null);
      await loadHorses();
    } catch (e) { setCancelError(e.message || "Không thể thực hiện."); }
    finally { setLoading(false); }
  };

  useEffect(() => { loadHorses(); }, []);

  const filtered = useMemo(() =>
    horses.filter(h => {
      const name = h.name ?? h.Name ?? "";
      const s = approvalStatusMap[h.approvalStatus ?? h.ApprovalStatus ?? 0] ?? "Tất cả";
      return name.toLowerCase().includes(query.toLowerCase()) && (statusFilter === "Tất cả" || s === statusFilter);
    }), [query, statusFilter, horses]);

  const stats = useMemo(() => ({
    total: horses.length,
    approved: horses.filter(h => (h.approvalStatus ?? h.ApprovalStatus) === 2).length,
    pending: horses.filter(h => (h.approvalStatus ?? h.ApprovalStatus) === 1).length,
    rejected: horses.filter(h => (h.approvalStatus ?? h.ApprovalStatus) === 3).length,
    winRate: (() => {
      const totalR = horses.reduce((s, h) => s + Number(h.totalRaces ?? h.TotalRaces ?? 0), 0);
      const totalW = horses.reduce((s, h) => s + Number(h.totalWins ?? h.TotalWins ?? 0), 0);
      return totalR > 0 ? Math.round((totalW / totalR) * 100) : 0;
    })(),
  }), [horses]);

  const openAssign = async (horse) => {
    setAssignHorse(horse);
    setSelectedTournament("");
    setSelectedRace("");
    setSelectedJockey("");
    setInvitationMessage("");
    setJockeyError("");
    setJockeyLoading(true);
    try {
      const data = await getAvailableJockeys();
      let currentUserId = "";
      try {
        const authUser = JSON.parse(localStorage.getItem("authUser") || "{}");
        currentUserId = String(authUser.userId ?? authUser.UserId ?? "").toLowerCase();
      } catch {
        // The backend still enforces this rule if local auth data is unavailable.
      }
      const horseId = horse.id ?? horse.Id;

      setJockeys(
        Array.isArray(data)
          ? data.filter(j => {
              const approvalStatus = String(j.approvalStatus ?? j.ApprovalStatus ?? "").toLowerCase();
              const approvalStatusName = String(j.approvalStatusName ?? j.ApprovalStatusName ?? "").toLowerCase();
              if (approvalStatus !== "2" && approvalStatusName !== "approved") return false;
              const jUserId = String(j.userId ?? j.UserId ?? "").toLowerCase();
              // Exclude self; a jockey may already have other invitations elsewhere — that's allowed in J2
              if (jUserId === currentUserId) return false;
              return true;
            })
          : [],
      );
    } catch (e) {
      setJockeys([]);
      setJockeyError(e.message || "Không thể tải danh sách kỵ sĩ.");
    } finally {
      setJockeyLoading(false);
    }
  };

  const submitAssign = async () => {
    if (!selectedRace) { setJockeyError("Vui lòng chọn giải đua."); return; }
    if (!selectedJockey) { setJockeyError("Vui lòng chọn kỵ sĩ."); return; }
    try {
      await inviteJockeyToHorse(assignHorse.id ?? assignHorse.Id, {
        jockeyId: selectedJockey,
        raceId: selectedRace,
        message: invitationMessage.trim() || null,
      });
      setAssignHorse(null);
      await loadHorses();
    } catch (e) { setJockeyError(e.message || "Lỗi."); }
  };

  const handleDelete = async (horse) => {
    // Task C1 §1: a Horse with participation history is archived (not destroyed) by this same
    // endpoint — the confirm/result copy must not promise permanent deletion either way.
    if (!window.confirm(`Xóa ${horse.name ?? horse.Name}? Nếu ngựa đã có lịch sử tham gia, hệ thống sẽ lưu trữ thay vì xóa vĩnh viễn.`)) return;
    try {
      const message = await deleteHorse(horse.id ?? horse.Id);
      if (typeof message === "string" && message) alert(message);
      loadHorses();
    }
    catch (e) { alert(e.message); }
  };

  return (
    <div className="oh-page">
      {/* Header */}
      <div className="oh-top">
        <div>
          <h1>Ngựa của tôi</h1>
          <p className="oh-sub">{stats.total} con ngựa · {stats.approved} đã duyệt</p>
        </div>
        {canManageHorses && (
          <Link to="/owner/horses/new" className="oh-btn oh-btn--primary">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 5v14M5 12h14"/></svg>
            Thêm ngựa
          </Link>
        )}
      </div>

      {/* Stats */}
      <div className="oh-stats">
        <div className="oh-stat"><span>Tổng số</span><strong>{stats.total}</strong></div>
        <div className="oh-stat"><span>Đã duyệt</span><strong>{stats.approved}</strong><small>{stats.pending} chờ</small></div>
        <div className="oh-stat"><span>Tỉ lệ thắng</span><strong>{stats.winRate}%</strong></div>
        <div className="oh-stat"><span>Cần sửa</span><strong>{stats.rejected}</strong><small>{stats.pending} chờ duyệt</small></div>
      </div>

      {/* Filters */}
      <div className="oh-filters">
        <div className="oh-search">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
          <input placeholder="Tìm theo tên ngựa" value={query} onChange={e => setQuery(e.target.value)} />
        </div>
        <div className="oh-chip-group">
          {["Tất cả", "Đã duyệt", "Chờ duyệt", "Từ chối"].map(s => (
            <button key={s} className={`oh-chip ${statusFilter === s ? "oh-chip--active" : ""}`} onClick={() => setStatusFilter(s)}>{s}</button>
          ))}
        </div>
      </div>

      {/* Horse Grid */}
      {loading ? <p className="oh-muted">Đang tải...</p> : error ? <p className="oh-error">{error}</p> : filtered.length === 0 ? (
        <div className="oh-empty"><p>Không tìm thấy ngựa.</p></div>
      ) : (
        <div className="oh-grid">
          {filtered.map(h => {
            const id = h.id ?? h.Id;
            const name = h.name ?? h.Name ?? "Chưa có tên";
            const breed = h.breed ?? h.Breed ?? "";
            const age = h.age ?? h.Age ?? h.dateOfBirth ?? h.DateOfBirth ?? "";
            const gender = h.gender ?? h.Gender ?? "";
            const totalRaces = h.totalRaces ?? h.TotalRaces ?? 0;
            const totalWins = h.totalWins ?? h.TotalWins ?? 0;
            // eslint-disable-next-line no-unused-vars
            const winRate = totalRaces > 0 ? Math.round((totalWins / totalRaces) * 100) : 0;
            const approvalStatus = h.approvalStatus ?? h.ApprovalStatus ?? 0;
            const statusLabel = approvalStatusMap[approvalStatus] ?? "Chưa xác định";
            const approvalNote = h.approvalNote ?? h.ApprovalNote ?? "";
            const isRejected = approvalStatus === 3;
            // Task C1 §1: IsArchived is a distinct axis from ApprovalStatus (admin profile
            // review) — never conflate the two badges.
            const isArchived = h.isArchived ?? h.IsArchived ?? false;
            const horseWinRate = totalRaces > 0 ? Math.round((totalWins / totalRaces) * 100) : 0;
            const speed = Math.min(95, 45 + totalWins * 8);
            const stamina = Math.min(95, 35 + (totalRaces - totalWins) * 4);
            const imageUrl = resolveApiUrl(h.imageUrl ?? h.ImageUrl ?? "");
            const invitations = h.jockeyInvitations ?? h.JockeyInvitations ?? [];
            const raceEntriesForHorse = h.raceEntries ?? h.RaceEntries ?? [];
            // J3.1: once an invitation is the official pairing (RaceEntry.JockeyId match), it is
            // no longer offered here — Remove is only for a still-non-official Pending/Accepted
            // candidate. Official-ness is judged by JockeyId match, never by Status alone.
            const hasCancellableJockey = invitations.some(inv => {
              const status = String(inv.status ?? inv.Status).toLowerCase();
              const isPendingOrAccepted = (inv.status ?? inv.Status) === 1 || (inv.status ?? inv.Status) === 2 ||
                status === "pending" || status === "accepted";
              return isPendingOrAccepted && !isInvitationOfficial(inv, raceEntriesForHorse);
            });

            return (
              <div key={id} className="oh-card">
                <div className="oh-card-img" style={{ backgroundImage: imageUrl ? `url(${imageUrl})` : undefined, backgroundSize: 'cover', backgroundPosition: 'center' }}>
                  <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
                    <div className={"oh-card-status" + (statusClass[approvalStatus] ? " oh-card-status--" + statusClass[approvalStatus] : "")}>{statusLabel}</div>
                    {isArchived && (
                      <div className="oh-card-status oh-card-status--archived">Đã lưu trữ</div>
                    )}
                  </div>
                </div>
                <div className="oh-card-body">
                  <div className="oh-card-header">
                    <h3>{name}</h3>
                    <div className="oh-ring">
                      <svg width="44" height="44" viewBox="0 0 44 44">
                        <circle cx="22" cy="22" r="18" fill="none" stroke="rgba(0,0,0,0.06)" strokeWidth="3" />
                        <circle cx="22" cy="22" r="18" fill="none" stroke="#f2d28b" strokeWidth="3" strokeDasharray={`${horseWinRate * 1.13} 113`} strokeLinecap="round" transform="rotate(-90 22 22)" />
                      </svg>
                      <span className="oh-ring-label">{horseWinRate}%</span>
                    </div>
                  </div>
                  <p className="oh-breed">{breed}{breed && gender ? " · " : ""}{gender}</p>
                  <div className="oh-meta">
                    <span>{totalRaces} đua</span>
                    <span className="oh-dot" />
                    <span>{totalWins} thắng</span>
                    <span className="oh-dot" />
                    <span>{age}{typeof age === "number" ? " tuổi" : ""}</span>
                  </div>
                  {isRejected ? (
                    <p className="oh-approval-note">
                      <strong>Lý do từ chối</strong>
                      <span>{approvalNote || "Admin đã từ chối hồ sơ. Vui lòng cập nhật và gửi duyệt lại."}</span>
                    </p>
                  ) : null}
                  <div className="oh-bars">
                    <div className="oh-bar-row"><span className="oh-bar-l">Tốc độ</span><div className="oh-bar"><div className="oh-bar-fill oh-bar-gold" style={{width:speed+"%"}} /></div><span className="oh-bar-r">{speed}%</span></div>
                    <div className="oh-bar-row"><span className="oh-bar-l">Sức bền</span><div className="oh-bar"><div className="oh-bar-fill" style={{width:stamina+"%"}} /></div><span className="oh-bar-r">{stamina}%</span></div>
                  </div>
                  <div className="oh-actions">
                    <Link to={`/owner/horses/${id}`} className="oh-btn oh-btn--sm oh-btn--primary">Chi tiết</Link>
                    {hasCancellableJockey && (
                      <button className="oh-btn-icon" style={{color: "#ef4444"}} title="Hủy kỵ sĩ" onClick={() => openCancel(h)}>
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="8.5" cy="7" r="4"/><line x1="23" y1="11" x2="17" y2="11"/></svg>
                      </button>
                    )}
                    {/* New JockeyInvitations are backend-rejected for an archived Horse (HorseService.InviteJockeyAsync). */}
                    {!isArchived && (
                      <button className="oh-btn-icon" onClick={() => openAssign(h)} title="Mời kỵ sĩ">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="8.5" cy="7" r="4"/><line x1="20" y1="8" x2="20" y2="14"/><line x1="23" y1="11" x2="17" y2="11"/></svg>
                      </button>
                    )}
                    {/* Task C1 correction §2: once archived there is nothing further to delete —
                        hiding the button avoids a no-op "Delete" affordance on a terminal state. */}
                    {canManageHorses && !isArchived && (
                      <button className="oh-btn-icon" onClick={() => handleDelete(h)} title="Xóa">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/></svg>
                      </button>
                    )}
                    {/* Editing an archived Horse is backend-rejected (HorseService.UpdateHorseAsync) — hidden here to match. */}
                    {canManageHorses && !isArchived && isRejected ? (
                      <Link to={`/owner/horses/${id}/edit`} className="oh-btn oh-btn--sm oh-btn--resubmit">
                        Gửi duyệt lại
                      </Link>
                    ) : canManageHorses && !isArchived ? (
                      <Link to={`/owner/horses/${id}/edit`} className="oh-btn-icon" title="Chỉnh sửa">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                      </Link>
                    ) : null}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Assign Modal */}
      {assignHorse && (
        <div className="oh-modal" onClick={() => setAssignHorse(null)}>
          <div className="oh-modal-card" onClick={e => e.stopPropagation()}>
            <h3>Mời kỵ sĩ</h3>
            <p className="oh-muted" style={{textAlign:"left",padding:0,margin:"0 0 12px"}}>Chọn giải đấu và cuộc đua cho {assignHorse.name ?? assignHorse.Name}. Bạn có thể mời nhiều kỵ sĩ cho cùng một cuộc đua.</p>
            <select value={selectedTournament} onChange={e => { setSelectedTournament(e.target.value); setSelectedRace(""); }} className="oh-select" style={{marginBottom:"12px"}}>
              <option value="">-- Chọn giải đấu --</option>
              {(() => {
                const tourns = [];
                (assignHorse.raceEntries ?? assignHorse.RaceEntries ?? []).forEach(entry => {
                  const race = entry.race ?? entry.Race;
                  if (!race) return;
                  // J2: a race may already have other jockeys Pending/Accepted — the owner can
                  // still invite additional eligible jockeys for the same Horse+Race.
                  // Lifecycle guard follow-up: hide races the backend would reject anyway.
                  if (!isRaceInvitable(race)) return;
                  const t = race.tournament ?? race.Tournament;
                  if (t && !tourns.find(x => x.id === (t.id ?? t.Id))) {
                    tourns.push({ id: t.id ?? t.Id, name: t.name ?? t.Name });
                  }
                });
                return tourns.map(t => <option key={t.id} value={t.id}>{t.name}</option>);
              })()}
            </select>
            <select value={selectedRace} onChange={e => setSelectedRace(e.target.value)} className="oh-select" disabled={!selectedTournament} style={{marginBottom:"12px"}}>
              <option value="">-- Chọn cuộc đua --</option>
              {selectedTournament && (assignHorse.raceEntries ?? assignHorse.RaceEntries ?? [])
                .map(entry => entry.race ?? entry.Race)
                .filter(race => race && (race.tournament?.id ?? race.tournament?.Id ?? race.Tournament?.Id) === selectedTournament)
                .filter(isRaceInvitable)
                .map(race => (
                  <option key={race.id ?? race.Id} value={race.id ?? race.Id}>{race.name ?? race.Name}</option>
                ))}
            </select>
            <select value={selectedJockey} onChange={e => setSelectedJockey(e.target.value)} className="oh-select" disabled={jockeyLoading || jockeys.length === 0}>
              <option value="">{jockeyLoading ? "Đang tải danh sách kỵ sĩ..." : "-- Chọn kỵ sĩ --"}</option>
              {jockeys.map(j => <option key={j.id ?? j.Id} value={j.id ?? j.Id}>{j.fullName ?? j.FullName ?? j.email ?? j.Email}</option>)}
            </select>
            <textarea
              className="oh-textarea"
              value={invitationMessage}
              onChange={e => setInvitationMessage(e.target.value)}
              maxLength={500}
              rows={3}
              placeholder="Lời nhắn đến kỵ sĩ (không bắt buộc)"
            />
            {!jockeyLoading && jockeys.length === 0 && !jockeyError && (
              <p className="oh-error" style={{margin:"8px 0 0"}}>Chưa có kỵ sĩ khả dụng.</p>
            )}
            {jockeyError && <p className="oh-error" style={{margin:"8px 0 0"}}>{jockeyError}</p>}
            <div className="oh-modal-actions">
              <button className="oh-btn" onClick={() => setAssignHorse(null)}>Huỷ</button>
              <button className="oh-btn oh-btn--primary" onClick={submitAssign}>Xác nhận</button>
            </div>
          </div>
        </div>
      )}

      {/* Cancel Modal */}
      {cancelHorse && (
        <div className="oh-modal" onClick={() => setCancelHorse(null)}>
          <div className="oh-modal-card" onClick={e => e.stopPropagation()}>
            <h3>Hủy kỵ sĩ</h3>
            <p className="oh-muted" style={{textAlign:"left",padding:0,margin:"0 0 12px"}}>Chọn giải đấu và cuộc đua để hủy kỵ sĩ của {cancelHorse.name ?? cancelHorse.Name}</p>
            <select value={selectedCancelTournament} onChange={e => { setSelectedCancelTournament(e.target.value); setSelectedCancelInvitationId(""); }} className="oh-select" style={{marginBottom:"12px"}}>
              <option value="">-- Chọn giải đấu --</option>
              {(() => {
                const tourns = [];
                (cancelHorse.jockeyInvitations ?? cancelHorse.JockeyInvitations ?? [])
                  .filter(inv => ((inv.status ?? inv.Status) === 2 || (inv.status ?? inv.Status) === 1 || String(inv.status ?? inv.Status).toLowerCase() === "accepted" || String(inv.status ?? inv.Status).toLowerCase() === "pending")
                    && !isInvitationOfficial(inv, cancelHorse.raceEntries ?? cancelHorse.RaceEntries ?? []))
                  .forEach(inv => {
                    const raceId = inv.raceId ?? inv.RaceId;
                    const entry = (cancelHorse.raceEntries ?? cancelHorse.RaceEntries ?? []).find(e => (e.raceId ?? e.RaceId) === raceId);
                    const t = entry?.race?.tournament ?? entry?.Race?.Tournament;
                    if (t && !tourns.find(x => x.id === (t.id ?? t.Id))) {
                      tourns.push({ id: t.id ?? t.Id, name: t.name ?? t.Name });
                    }
                  });
                return tourns.map(t => <option key={t.id} value={t.id}>{t.name}</option>);
              })()}
            </select>
            {/* J2 follow-up: keyed by the specific invitation (not race) so a race with multiple
                Pending/Accepted jockeys cancels exactly the one the owner selected. */}
            <select value={selectedCancelInvitationId} onChange={e => setSelectedCancelInvitationId(e.target.value)} className="oh-select" disabled={!selectedCancelTournament} style={{marginBottom:"12px"}}>
              <option value="">-- Chọn kỵ sĩ cần hủy --</option>
              {selectedCancelTournament && (cancelHorse.jockeyInvitations ?? cancelHorse.JockeyInvitations ?? [])
                .filter(inv => ((inv.status ?? inv.Status) === 2 || (inv.status ?? inv.Status) === 1 || String(inv.status ?? inv.Status).toLowerCase() === "accepted" || String(inv.status ?? inv.Status).toLowerCase() === "pending")
                  && !isInvitationOfficial(inv, cancelHorse.raceEntries ?? cancelHorse.RaceEntries ?? []))
                .map(inv => {
                  const invitationId = inv.id ?? inv.Id;
                  const raceId = inv.raceId ?? inv.RaceId;
                  const entry = (cancelHorse.raceEntries ?? cancelHorse.RaceEntries ?? []).find(e => (e.raceId ?? e.RaceId) === raceId);
                  if (!entry) return null;
                  const tId = entry.race?.tournament?.id ?? entry.Race?.Tournament?.Id;
                  if (tId !== selectedCancelTournament) return null;
                  const raceName = entry.race?.name ?? entry.Race?.Name ?? "Giải đua";
                  const jockeyName = inv.jockey?.user?.fullName ?? inv.Jockey?.User?.FullName ?? "kỵ sĩ";
                  return <option key={invitationId} value={invitationId}>{raceName} (Kỵ sĩ: {jockeyName})</option>;
              })}
            </select>
            <textarea
              className="oh-textarea"
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              maxLength={500}
              rows={3}
              placeholder="Nhập lý do hủy kỵ sĩ *"
            />
            {cancelError && <p className="oh-error" style={{margin:"8px 0 0"}}>{cancelError}</p>}
            <div className="oh-modal-actions">
              <button className="oh-btn" onClick={() => setCancelHorse(null)}>Huỷ</button>
              <button className="oh-btn oh-btn--primary" style={{backgroundColor:"#ef4444",borderColor:"#ef4444"}} onClick={submitCancel}>Hủy kỵ sĩ</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default OwnerHorseListPage;
