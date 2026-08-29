import { useState, useEffect, useRef } from "react";
import { request } from "../services/apiClient";
import { getTournamentRounds, getTournamentRaces } from "../services/adminApi";
import { Input, Select } from "./ui/Primitives";
import { apiToVNInput, apiToVNDisplay, apiToVNDate, apiToUtcDate, vnInputToApiUtc } from "../utils/vnDateTime";

// Canonical Race create/edit UI (Phase5B FE consolidation) — the ONLY FE Race payload builder.
// Round assignment uses a real RoundId (no free-text/ad-hoc rounds). RegistrationDeadline belongs
// to the Tournament, not the Race — shown here read-only for context only.
function RaceForm({ tournamentId, tournamentName, tournamentStartDate, tournamentEndDate, tournamentRegistrationDeadline, raceData, onClose, onSuccess }) {
  const isEdit = !!raceData;
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const [error, setError] = useState("");
  const [tracks, setTracks] = useState([]);
  const [referees, setReferees] = useState([]);
  const [showTrackForm, setShowTrackForm] = useState(false);
  const [newTrack, setNewTrack] = useState({ name: "", length: "", capacity: "", description: "" });
  const [tournamentRounds, setTournamentRounds] = useState([]);
  const [roundsLoading, setRoundsLoading] = useState(false);
  const [roundsError, setRoundsError] = useState("");
  const [roundRaces, setRoundRaces] = useState([]);
  const [roundRacesLoading, setRoundRacesLoading] = useState(false);

  // Vietnam-timezone policy (Asia/Ho_Chi_Minh, UTC+7) — see FE/src/utils/vnDateTime.js. All
  // Tournament/Round/Race datetimes shown or entered here go through that single shared utility,
  // never new Date(...).toLocaleString()/toISOString() directly against browser-local time.
  const startLabel = apiToVNDate(tournamentStartDate);
  const endLabel = apiToVNDate(tournamentEndDate);
  const registrationDeadlineLabel = apiToVNDisplay(tournamentRegistrationDeadline);

  const [form, setForm] = useState({
    tournamentId: tournamentId || "",
    roundId: raceData?.roundId || raceData?.RoundId || "",
    trackId: raceData?.trackId || raceData?.TrackId || "",
    name: raceData?.name || raceData?.Name || "",
    distance: raceData?.distance || raceData?.Distance || 1200,
    // No silent default — MaxParticipants is capacity-sensitive (must not exceed Track.Capacity),
    // so the Admin must explicitly enter it rather than inherit a guessed value.
    maxParticipants: raceData?.maxParticipants ?? raceData?.MaxParticipants ?? "",
    scheduledAt: apiToVNInput(raceData?.scheduledAt || raceData?.ScheduledAt),
    scheduledEndAt: apiToVNInput(raceData?.scheduledEndAt || raceData?.ScheduledEndAt || raceData?.actualEndTime || raceData?.ActualEndTime),
    // Nullish coalescing (not ||) — QualificationSlots = 0 is a valid, meaningful value (Final Race)
    // and must never be treated as falsy/omitted.
    qualificationSlots: raceData?.qualificationSlots ?? raceData?.QualificationSlots ?? "",
  });

  const [selectedRefereeIds, setSelectedRefereeIds] = useState(raceData?._selectedRefereeIds || []);

  const currentRaceId = raceData?.id || raceData?.Id || "";
  const selectedTrack = tracks.find((t) => (t.id || t.Id) === form.trackId);
  const editRoundLabel = isEdit
    ? [raceData?.roundNumber ?? raceData?.RoundNumber, raceData?.roundName ?? raceData?.RoundName].filter(Boolean).join(" — ")
    : "";

  // Round context — needed in both create and edit mode. Race.RoundId is immutable after
  // creation, but the current Round's own schedule is still only available via the Tournament's
  // Round list (RaceDetailResponse doesn't carry it), so look it up there either way.
  const currentRoundId = form.roundId || raceData?.roundId || raceData?.RoundId || "";
  const selectedRound = tournamentRounds.find((r) => (r.id ?? r.Id) === currentRoundId);
  const roundStartRaw = selectedRound?.scheduledStartDate ?? selectedRound?.ScheduledStartDate;
  const roundEndRaw = selectedRound?.scheduledEndDate ?? selectedRound?.ScheduledEndDate;
  const roundNumber = selectedRound?.roundNumber ?? selectedRound?.RoundNumber;
  const roundAdvanceCountRaw = selectedRound?.advanceCount ?? selectedRound?.AdvanceCount;
  const roundAdvanceCount = roundAdvanceCountRaw === null || roundAdvanceCountRaw === undefined || roundAdvanceCountRaw === ""
    ? null
    : Number(roundAdvanceCountRaw);
  const siblingQualificationSlots = roundRaces.reduce((total, race) => {
    const id = race?.id ?? race?.Id;
    if (currentRaceId && String(id) === String(currentRaceId)) return total;
    const slots = race?.qualificationSlots ?? race?.QualificationSlots;
    const numeric = slots === null || slots === undefined ? 0 : Number(slots);
    return total + (Number.isFinite(numeric) ? numeric : 0);
  }, 0);
  const remainingRoundSlots = roundAdvanceCount === null
    ? null
    : Math.max(roundAdvanceCount - siblingQualificationSlots, 0);

  const loadTracks = async () => {
    try {
      const list = await request("/api/tracks");
      setTracks(Array.isArray(list) ? list : list?.data ?? []);
    } catch { /* empty */ }
  };

  const loadReferees = async () => {
    try {
      // /active already drops referees with a Confirmed assignment in any Ongoing tournament.
      const list = await request("/api/referees/active");
      const available = Array.isArray(list) ? list : list?.data ?? [];
      setReferees(available);
    } catch { /* empty */ }
  };

  const loadRounds = async () => {
    if (!tournamentId) { setTournamentRounds([]); return; }
    setRoundsLoading(true);
    setRoundsError("");
    try {
      const list = await getTournamentRounds(tournamentId);
      setTournamentRounds(Array.isArray(list) ? list : []);
    } catch (err) {
      setRoundsError(err.message || "Không thể tải danh sách vòng đấu.");
      setTournamentRounds([]);
    } finally {
      setRoundsLoading(false);
    }
  };

  useEffect(() => {
    loadTracks();
    loadReferees();
  }, []);

  useEffect(() => {
    loadRounds();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tournamentId]);

  useEffect(() => {
    if (!tournamentId || !currentRoundId) {
      setRoundRaces([]);
      return;
    }

    let cancelled = false;
    setRoundRacesLoading(true);
    getTournamentRaces(tournamentId)
      .then((list) => {
        if (cancelled) return;
        const races = Array.isArray(list) ? list : [];
        setRoundRaces(races.filter((race) => String(race.roundId ?? race.RoundId) === String(currentRoundId)));
      })
      .catch(() => {
        if (!cancelled) setRoundRaces([]);
      })
      .finally(() => {
        if (!cancelled) setRoundRacesLoading(false);
      });

    return () => { cancelled = true; };
  }, [tournamentId, currentRoundId]);

  const updateForm = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleCreateTrack = async () => {
    if (!newTrack.name.trim()) return;
    try {
      const res = await request("/api/tracks", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: newTrack.name.trim(),
          description: newTrack.description.trim() || null,
          length: newTrack.length === "" ? null : Number(newTrack.length),
          capacity: newTrack.capacity === "" ? null : Number(newTrack.capacity),
        }),
      });
      const created = res?.data ?? res;
      const newTrackId = created?.id ?? created?.Id;
      await loadTracks();
      // Auto-select the newly created Track for the Race being configured, preserve every other
      // unsaved field, and never submit/create the Race itself here.
      if (newTrackId) updateForm("trackId", newTrackId);
      setNewTrack({ name: "", length: "", capacity: "", description: "" });
      setShowTrackForm(false);
    } catch (err) {
      setError("Lỗi tạo đường đua: " + (err.message || ""));
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (submittingRef.current) return;
    if (!form.name.trim()) { setError("Vui lòng nhập tên cuộc đua."); return; }
    if (!isEdit && !form.roundId) { setError("Vui lòng chọn vòng đấu (Round) trước khi tạo cuộc đua."); return; }
    if (!form.scheduledAt) { setError("Vui lòng chọn thời gian bắt đầu."); return; }
    if (form.maxParticipants === "" || Number(form.maxParticipants) <= 0) {
      setError("Vui lòng nhập Số ngựa tối đa lớn hơn 0.");
      return;
    }
    if (form.qualificationSlots === "") {
      setError("Vui lòng nhập Số suất đi tiếp từ cuộc đua.");
      return;
    }

    const qualificationSlots = Number(form.qualificationSlots);
    const maxParticipants = Number(form.maxParticipants);
    if (!Number.isInteger(qualificationSlots) || qualificationSlots < 0) {
      setError("Số suất đi tiếp từ cuộc đua không được âm.");
      return;
    }
    if (roundAdvanceCount === 0 && qualificationSlots !== 0) {
      setError("Vòng chung kết yêu cầu 0 suất đi tiếp.");
      return;
    }
    if (roundAdvanceCount !== null && roundAdvanceCount > 0) {
      if (qualificationSlots >= maxParticipants) {
        setError("Số suất đi tiếp từ cuộc đua phải nhỏ hơn Số ngựa tối đa.");
        return;
      }
      if (qualificationSlots > remainingRoundSlots) {
        setError(`Số suất đi tiếp vượt quá số suất còn lại của vòng (${remainingRoundSlots}).`);
        return;
      }
    }

    // Phase5B fix3: friendly client-side containment check before hitting the API — the backend
    // stays authoritative (same rules re-checked there), this just avoids a round-trip for the
    // obvious case of a Race scheduled outside its Round's window. Never auto-corrects the value.
    const raceStart = apiToUtcDate(vnInputToApiUtc(form.scheduledAt));
    const raceEnd = form.scheduledEndAt ? apiToUtcDate(vnInputToApiUtc(form.scheduledEndAt)) : null;
    if (roundStartRaw && raceStart && raceStart < apiToUtcDate(roundStartRaw)) {
      setError("Thời gian bắt đầu Cuộc đua không được trước thời gian bắt đầu Vòng đấu.");
      return;
    }
    if (roundEndRaw && raceEnd && raceEnd > apiToUtcDate(roundEndRaw)) {
      setError("Thời gian kết thúc Cuộc đua không được sau thời gian kết thúc Vòng đấu.");
      return;
    }
    if (raceEnd && raceStart >= raceEnd) {
      setError("Thời gian bắt đầu phải trước thời gian kết thúc.");
      return;
    }

    submittingRef.current = true;
    setSubmitting(true);
    setError("");

    try {
      const racePayload = {
        tournamentId: form.tournamentId,
        roundId: form.roundId || null,
        trackId: form.trackId || null,
        name: form.name,
        distance: Number(form.distance),
        maxParticipants: maxParticipants,
        scheduledAt: vnInputToApiUtc(form.scheduledAt),
        scheduledEndAt: form.scheduledEndAt ? vnInputToApiUtc(form.scheduledEndAt) : null,
        qualificationSlots: qualificationSlots,
      };

      const raceId = raceData?.id || raceData?.Id;
      if (isEdit && raceId) {
        await request(`/api/races/management/${raceId}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(racePayload) });
        const initRefIds = raceData?._selectedRefereeIds || [];
        const refToRemove = initRefIds.filter(id => !selectedRefereeIds.includes(id));
        for (const refId of refToRemove) {
          const assignments = await request(`/api/referees/race/${raceId}/assignments`);
          const list = Array.isArray(assignments?.data ?? assignments) ? (assignments?.data ?? assignments) : [];
          const assignment = list.find(a => (a.refereeId || a.RefereeId) === refId);
          if (assignment) { await request(`/api/referees/assignments/${assignment.id || assignment.Id}/respond`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ response: "Reject" }) }).catch(() => {}); }
        }
        const refToAdd = selectedRefereeIds.filter(id => !initRefIds.includes(id));
        for (const refId of refToAdd) { await request(`/api/races/${raceId}/referees`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refereeId: refId }) }).catch(() => {}); }
      } else {
        const raceRes = await request("/api/races/management", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(racePayload) });
        const newRaceId = raceRes?.data?.id ?? raceRes?.id;
        if (!newRaceId) throw new Error("Không lấy được ID cuộc đua");
        if (selectedRefereeIds.length > 0) { await Promise.all(selectedRefereeIds.map((refId) => request(`/api/races/${newRaceId}/referees`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refereeId: refId }) }))); }
      }
      onSuccess();
    } catch (err) {
      setError(err.message || "Lỗi tạo cuộc đua");
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  };

  return (
    <div className="admin-modal-backdrop" onClick={onClose}>
      <div className="admin-modal-panel" onClick={(e) => e.stopPropagation()}>
        <h2 style={{margin:"0 0 24px",fontSize:24}}>{isEdit ? "Sửa cuộc đua" : "Tạo cuộc đua mới"}</h2>

        {/* Tournament context — read-only. RegistrationDeadline belongs to the Tournament, not
            the Race; it is displayed here for context only and can never be edited from this form. */}
        {(tournamentName || isEdit) && (
          <div style={{padding:16,borderRadius:12,background:"rgba(184,134,59,0.08)",marginBottom:24,border:"1px solid var(--hr-border)"}}>
            <div style={{display:"flex",justifyContent:"space-between",alignItems:"flex-start",flexWrap:"wrap",gap:8}}>
              <div><span style={{fontSize:12,color:"var(--hr-muted)",display:"block"}}>Giải đấu</span><strong style={{fontSize:18,color:"var(--hr-paper)"}}>{tournamentName || "Đang tải..."}</strong></div>
              <div style={{textAlign:"right"}}><span style={{fontSize:12,color:"var(--hr-muted)",display:"block"}}>Thời gian giải</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{startLabel} → {endLabel}</strong></div>
            </div>
            <div style={{marginTop:10,paddingTop:10,borderTop:"1px solid var(--hr-border-soft)"}}>
              <span style={{fontSize:12,color:"var(--hr-muted)",display:"block"}}>Hạn đăng ký ngựa</span>
              <strong style={{fontSize:14,color:"var(--hr-paper)"}}>{registrationDeadlineLabel || "Chưa thiết lập"}</strong>
              <p style={{margin:"4px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Đây là hạn đăng ký của toàn giải đấu. Muốn thay đổi, hãy chỉnh sửa Giải đấu.</p>
            </div>
          </div>
        )}

        {error && <div style={{padding:12,borderRadius:8,background:"rgba(201,105,90,0.16)",border:"1px solid rgba(201,105,90,0.35)",color:"var(--hr-danger)",fontSize:14,marginBottom:16}}>{error}</div>}

        <form onSubmit={handleSubmit}>
          {/* Round — real RoundId only, filtered by the current Tournament. Race.RoundId can never
              change after creation (locked Phase5 rule), so this selector is create-only; edit mode
              shows the assigned Round read-only instead. */}
          {!isEdit ? (
            <div style={{marginBottom:16}}>
              <Select
                label="Vòng đấu *"
                value={form.roundId}
                onChange={(e) => updateForm("roundId", e.target.value)}
                disabled={roundsLoading || !!roundsError || tournamentRounds.length === 0}
                required
                options={
                  roundsLoading
                    ? [{ value: "", label: "Đang tải danh sách vòng đấu..." }]
                    : tournamentRounds.length === 0
                    ? [{ value: "", label: "-- Chưa có vòng đấu --" }]
                    : [
                        { value: "", label: "-- Chọn vòng đấu --" },
                        ...tournamentRounds.map((r) => ({
                          value: r.id ?? r.Id,
                          label: `Vòng ${r.roundNumber ?? r.RoundNumber} — ${r.name ?? r.Name}`,
                        })),
                      ]
                }
                error={roundsError || undefined}
                hint={
                  !roundsError && !roundsLoading && tournamentRounds.length === 0
                    ? "Giải đấu này chưa có vòng đấu nào. Vui lòng tạo vòng đấu (Round) trước khi tạo cuộc đua."
                    : !roundsError && !roundsLoading
                    ? "Chọn Vòng đấu đã được tạo trong Giải đấu này."
                    : undefined
                }
              />
              {roundsError && (
                <button type="button" onClick={loadRounds} style={{background:"none",border:"none",color:"var(--hr-gold-soft)",cursor:"pointer",fontSize:12,fontWeight:600,padding:0,marginTop:-8}}>
                  Thử lại
                </button>
              )}
            </div>
          ) : (
            <div style={{marginBottom:16}}>
              <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"var(--hr-text)"}}>Vòng đấu</label>
              <p style={{margin:0,fontSize:14,color:"var(--hr-paper)"}}>{editRoundLabel || "—"}</p>
              <p style={{margin:"4px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Vòng đấu của một Cuộc đua không thể thay đổi sau khi tạo.</p>
            </div>
          )}

          {/* Round schedule context — read-only, so the Admin knows the exact window a Race must
              fall inside before entering times below. */}
          {selectedRound && (
            <div style={{padding:"10px 14px",borderRadius:10,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)",marginBottom:16,fontSize:13}}>
              <div><span style={{color:"var(--hr-muted)"}}>Vòng đấu: </span><strong style={{color:"var(--hr-paper)"}}>Vòng {roundNumber} — {(selectedRound.name ?? selectedRound.Name)}</strong></div>
              <div><span style={{color:"var(--hr-muted)"}}>Thời gian vòng: </span><strong style={{color:"var(--hr-paper)"}}>{apiToVNDisplay(roundStartRaw)} → {apiToVNDisplay(roundEndRaw)}</strong></div>
              {roundAdvanceCount !== null && <div><span style={{color:"var(--hr-muted)"}}>Suất đi tiếp: </span><strong style={{color:"var(--hr-paper)"}}>Vòng {roundNumber} cần tổng cộng {roundAdvanceCount} suất đi tiếp.</strong></div>}
              {remainingRoundSlots !== null && <p style={{margin:"4px 0 0",fontSize:12,color:"var(--hr-muted)"}}>{roundRacesLoading ? "Đang tính số suất còn lại..." : `Vòng còn ${remainingRoundSlots} suất đi tiếp chưa phân bổ.`}</p>}
              <p style={{margin:"4px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Cuộc đua phải nằm hoàn toàn trong thời gian của Vòng đấu.</p>
            </div>
          )}

          {/* Track — one source of truth for TrackId: select an existing Track, or create one and
              it is auto-selected here (never submits the Race itself). */}
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"var(--hr-text)"}}>Đường đua *</label>
            <div style={{display:"flex",gap:8}}>
              <select className="hr-field" value={form.trackId} onChange={(e) => updateForm("trackId", e.target.value)} style={{flex:1,padding:10,borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)",color:"var(--hr-text)",fontSize:14}}>
                <option value="">-- Chọn đường đua --</option>
                {tracks.map((t) => (<option key={t.id || t.Id} value={t.id || t.Id}>{t.name || t.Name}</option>))}
              </select>
              <button type="button" onClick={() => setShowTrackForm(!showTrackForm)} style={{padding:"10px 16px",borderRadius:8,border:"1px solid var(--hr-border)",background:"transparent",color:"var(--hr-gold-soft)",cursor:"pointer",whiteSpace:"nowrap",fontSize:13,fontWeight:600}}>+ Tạo đường đua mới</button>
            </div>

            {selectedTrack && (
              <div style={{marginTop:8,display:"grid",gridTemplateColumns:"repeat(3, 1fr)",gap:8,padding:"8px 12px",borderRadius:8,background:"var(--hr-surface-2)",border:"1px solid var(--hr-border-soft)"}}>
                <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Tên đường đua</span><strong style={{fontSize:13,color:"var(--hr-paper)"}}>{selectedTrack.name || selectedTrack.Name}</strong></div>
                <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Chiều dài một vòng</span><strong style={{fontSize:13,color:"var(--hr-paper)"}}>{(selectedTrack.length ?? selectedTrack.Length) ? `${selectedTrack.length ?? selectedTrack.Length}m` : "Chưa có"}</strong></div>
                <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Sức chứa</span><strong style={{fontSize:13,color:"var(--hr-paper)"}}>{(selectedTrack.capacity ?? selectedTrack.Capacity) ?? "Chưa thiết lập"}</strong></div>
              </div>
            )}

            {showTrackForm && (
              <div style={{marginTop:8,padding:12,borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)"}}>
                <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:8,marginBottom:8}}>
                  <input className="hr-field" value={newTrack.name} onChange={(e) => setNewTrack({...newTrack, name: e.target.value})} placeholder="Tên đường đua *" style={{padding:"8px 12px",borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface)",color:"var(--hr-text)",fontSize:13}} autoFocus />
                  <input className="hr-field" type="number" min="1" value={newTrack.capacity} onChange={(e) => setNewTrack({...newTrack, capacity: e.target.value})} placeholder="Sức chứa tối đa" style={{padding:"8px 12px",borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface)",color:"var(--hr-text)",fontSize:13}} />
                  <input className="hr-field" type="number" min="1" value={newTrack.length} onChange={(e) => setNewTrack({...newTrack, length: e.target.value})} placeholder="Chiều dài một vòng (m)" style={{padding:"8px 12px",borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface)",color:"var(--hr-text)",fontSize:13}} />
                  <input className="hr-field" value={newTrack.description} onChange={(e) => setNewTrack({...newTrack, description: e.target.value})} placeholder="Mô tả (tuỳ chọn)" style={{padding:"8px 12px",borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface)",color:"var(--hr-text)",fontSize:13}} />
                </div>
                <p style={{margin:"0 0 8px",fontSize:11,color:"var(--hr-muted)"}}>"Chiều dài một vòng" là thông số vật lý cố định của đường đua — khác với "Khoảng cách cuộc đua" bên dưới (quãng đường thi đấu của riêng Cuộc đua này, có thể dài hơn một vòng nếu đua nhiều vòng).</p>
                <div style={{display:"flex",gap:8}}>
                  <button type="button" onClick={handleCreateTrack} style={{padding:"8px 16px",borderRadius:8,border:"none",background:"var(--hr-crimson)",color:"var(--hr-paper)",cursor:"pointer",fontSize:13,fontWeight:600}}>Tạo đường đua</button>
                  <button type="button" onClick={() => setShowTrackForm(false)} style={{padding:"8px 12px",borderRadius:8,border:"1px solid var(--hr-border-soft)",background:"transparent",color:"var(--hr-text)",cursor:"pointer",fontSize:13}}>Hủy</button>
                </div>
              </div>
            )}
          </div>

          <Input label="Tên cuộc đua *" value={form.name} onChange={(e) => updateForm("name", e.target.value)} placeholder="Ví dụ: Vòng loại 1, Bán kết A, Chung kết." hint="Ví dụ: Vòng loại 1, Bán kết A, Chung kết." required />

          <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:16,marginBottom:16}}>
            <Input label="Khoảng cách cuộc đua (m) *" type="number" value={form.distance} onChange={(e) => updateForm("distance", e.target.value)} min="100" step="100" hint="Quãng đường thi đấu của cuộc đua, không phải chiều dài vật lý của đường đua." />
            <Input label="Số ngựa tối đa *" type="number" value={form.maxParticipants} onChange={(e) => updateForm("maxParticipants", e.target.value)} min="1" placeholder="Nhập số ngựa" hint="Sức chứa tối đa của cuộc đua; số ngựa thực tế có thể ít hơn. Không được vượt quá sức chứa của đường đua." />
          </div>

          <Input label="Số suất đi tiếp từ cuộc đua *" type="number" value={form.qualificationSlots} onChange={(e) => updateForm("qualificationSlots", e.target.value)} min="0" hint={roundAdvanceCount === 0 ? "Vòng chung kết yêu cầu 0 suất đi tiếp." : remainingRoundSlots !== null ? `Tối đa ${remainingRoundSlots} suất cho cuộc đua này trước khi vượt AdvanceCount của vòng.` : "Nhập số suất đi tiếp từ cuộc đua này."} />

          <Input label="Thời gian bắt đầu *" type="datetime-local" value={form.scheduledAt} onChange={(e) => updateForm("scheduledAt", e.target.value)} required style={{colorScheme:"dark"}} hint="Phải nằm trong thời gian của Vòng đấu (giờ Việt Nam)."
            min={roundStartRaw ? apiToVNInput(roundStartRaw) : undefined} max={roundEndRaw ? apiToVNInput(roundEndRaw) : undefined} />
          <Input label="Thời gian kết thúc dự kiến *" type="datetime-local" value={form.scheduledEndAt} onChange={(e) => updateForm("scheduledEndAt", e.target.value)} style={{colorScheme:"dark"}} hint="Phải sau thời gian bắt đầu và nằm trong thời gian của Vòng đấu (giờ Việt Nam)."
            min={roundStartRaw ? apiToVNInput(roundStartRaw) : undefined} max={roundEndRaw ? apiToVNInput(roundEndRaw) : undefined} />

          {/* Referees */}
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"var(--hr-text)"}}>Trọng tài ({selectedRefereeIds.length})</label>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fill, minmax(180px, 1fr))",gap:8,maxHeight:180,overflowY:"auto",padding:8,border:"1px solid var(--hr-border-soft)",borderRadius:8}}>
              {referees.length === 0 ? (
                <p style={{margin:0,fontSize:12,color:"var(--hr-muted)"}}>Không có trọng tài khả dụng.</p>
              ) : referees.map((r) => {
                const id = r.id || r.Id;
                const checked = selectedRefereeIds.includes(id);
                return (
                  <label key={id} style={{display:"flex",alignItems:"center",gap:6,padding:"8px 10px",background:checked?"rgba(184,134,59,0.14)":"transparent",border:`1px solid ${checked?"var(--hr-focus)":"var(--hr-border-soft)"}`,borderRadius:6,cursor:"pointer",fontSize:13,color:"var(--hr-text)"}}>
                    <input type="checkbox" checked={checked} onChange={(e) => { e.target.checked ? setSelectedRefereeIds([...selectedRefereeIds, id]) : setSelectedRefereeIds(selectedRefereeIds.filter((i) => i !== id)); }} />
                    <span>{r.userFullName || r.UserFullName || r.fullName || r.FullName}</span>
                  </label>
                );
              })}
            </div>
            <p style={{margin:"4px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Chọn trọng tài phụ trách cuộc đua. Trọng tài đã xác nhận phân công trong giải đang diễn ra sẽ không hiển thị.</p>
          </div>

          <div style={{display:"flex",gap:12,justifyContent:"flex-end",marginTop:24,paddingTop:16,borderTop:"1px solid var(--hr-border-soft)"}}>
            <button type="button" onClick={onClose} style={{padding:"10px 20px",borderRadius:8,border:"1px solid var(--hr-border)",background:"transparent",cursor:"pointer",fontSize:14,color:"var(--hr-text)",fontWeight:600}}>Hủy</button>
            <button type="submit" disabled={submitting || (!isEdit && (roundsLoading || tournamentRounds.length === 0 || !form.roundId))} style={{padding:"10px 24px",borderRadius:8,border:"none",background:"var(--hr-crimson)",color:"var(--hr-paper)",cursor:(submitting || (!isEdit && (roundsLoading || tournamentRounds.length === 0 || !form.roundId)))?"not-allowed":"pointer",opacity:(submitting || (!isEdit && (roundsLoading || tournamentRounds.length === 0 || !form.roundId)))?0.6:1,fontSize:14,fontWeight:600}}>{submitting ? "Đang xử lý..." : isEdit ? "Lưu" : "Tạo"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default RaceForm;
