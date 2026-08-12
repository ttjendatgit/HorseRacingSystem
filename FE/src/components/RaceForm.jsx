import { useState, useEffect, useRef } from "react";
import { request } from "../services/apiClient";
import { Input } from "./ui/Primitives";

function RaceForm({ tournamentId, tournamentName, tournamentStartDate, tournamentEndDate, raceData, onClose, onSuccess }) {
  const isEdit = !!raceData;
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const [error, setError] = useState("");
  const [tracks, setTracks] = useState([]);
  const [referees, setReferees] = useState([]);
  const [showTrackForm, setShowTrackForm] = useState(false);
  const [newTrackName, setNewTrackName] = useState("");

  const fmtDate = (v) => v ? new Date(v).toISOString().slice(0, 10) : "";
  const startLabel = fmtDate(tournamentStartDate);
  const endLabel = fmtDate(tournamentEndDate);
  const toLocal = (v) => { if (!v) return ""; const d = new Date(v); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };

  const [form, setForm] = useState({
    tournamentId: tournamentId || "",
    trackId: raceData?.trackId || "",
    name: raceData?.name || raceData?.Name || "",
    distance: raceData?.distance || raceData?.Distance || 1200,
    maxParticipants: raceData?.maxParticipants || raceData?.MaxParticipants || 8,
    scheduledAt: toLocal(raceData?.scheduledAt || raceData?.ScheduledAt),
    scheduledEndAt: toLocal(raceData?.scheduledEndAt || raceData?.ScheduledEndAt || raceData?.actualEndTime || raceData?.ActualEndTime),
  });

  const [rounds, setRounds] = useState(() => {
    if (isEdit && raceData?.roundNames) {
      return raceData.roundNames.split(",").map(name => ({ name: name.trim(), scheduledAt: "" }));
    }
    return [{ name: "Vòng 1", scheduledAt: "" }];
  });

  const [selectedRefereeIds, setSelectedRefereeIds] = useState(raceData?._selectedRefereeIds || []);

  const loadTracks = async () => {
    try {
      const list = await request("/api/tracks");
      setTracks(Array.isArray(list) ? list : list?.data ?? []);
    } catch { /* empty */ }
  };

  const loadReferees = async () => {
    try {
      const list = await request("/api/referees");
      setReferees(Array.isArray(list) ? list : list?.data ?? []);
    } catch { /* empty */ }
  };

  useEffect(() => {
    loadTracks();
    loadReferees();
  }, []);

  const updateForm = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const updateRound = (idx, field, value) => {
    setRounds((prev) => prev.map((r, i) => i === idx ? { ...r, [field]: value } : r));
  };

  const addRound = () => {
    const nextNum = rounds.length + 1;
    setRounds((prev) => [...prev, { name: `Vòng ${nextNum}`, scheduledAt: "" }]);
  };

  const removeRound = (idx) => {
    setRounds((prev) => prev.filter((_, i) => i !== idx));
  };

  const handleCreateTrack = async () => {
    if (!newTrackName.trim()) return;
    try {
      await request("/api/tracks", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: newTrackName.trim() }),
      });
      setNewTrackName("");
      setShowTrackForm(false);
      loadTracks();
    } catch (err) {
      setError("Lỗi tạo đường đua: " + (err.message || ""));
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (submittingRef.current) return;
    if (!form.name.trim()) { setError("Vui lòng nhập tên cuộc đua."); return; }
    if (!form.scheduledAt) { setError("Vui lòng chọn thời gian bắt đầu."); return; }
    submittingRef.current = true;
    setSubmitting(true);
    setError("");

    try {
      const racePayload = {
        tournamentId: form.tournamentId,
        trackId: form.trackId || null,
        name: form.name,
        distance: Number(form.distance),
        maxParticipants: Number(form.maxParticipants),
        scheduledAt: new Date(form.scheduledAt).toISOString(),
        scheduledEndAt: form.scheduledEndAt ? new Date(form.scheduledEndAt).toISOString() : null,
        roundNames: rounds.filter(r => r.name).map(r => r.name).join(","),
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
    <div style={{position:"fixed",inset:0,background:"rgba(0,0,0,0.5)",display:"flex",alignItems:"center",justifyContent:"center",zIndex:1000,padding:20}}>
      <div style={{background:"#fff",borderRadius:16,maxWidth:700,width:"100%",maxHeight:"90vh",overflow:"auto",padding:32}}>
        <h2 style={{margin:"0 0 24px",fontSize:24,color:"#172033"}}>{isEdit ? "Sửa cuộc đua" : "Tạo cuộc đua mới"}</h2>

        {/* Tournament Info */}
        {(tournamentName || isEdit) && (
          <div style={{padding:16,borderRadius:12,background:"rgba(143,100,32,0.06)",marginBottom:24,border:"1px solid rgba(143,100,32,0.15)"}}>
            <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",flexWrap:"wrap",gap:8}}>
              <div><span style={{fontSize:12,color:"#657086",display:"block"}}>Giải đấu</span><strong style={{fontSize:18,color:"#172033"}}>{tournamentName || "Đang tải..."}</strong></div>
              <div style={{textAlign:"right"}}><span style={{fontSize:12,color:"#657086",display:"block"}}>Thời gian giải</span><strong style={{fontSize:14,color:"#172033"}}>{startLabel} → {endLabel}</strong></div>
            </div>
          </div>
        )}

        {error && <div style={{padding:12,borderRadius:8,background:"rgba(239,68,68,0.1)",color:"#ef4444",fontSize:14,marginBottom:16}}>{error}</div>}

        <form onSubmit={handleSubmit}>
          {/* Track */}
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"#34415b"}}>Đường đua</label>
            <div style={{display:"flex",gap:8}}>
              <select value={form.trackId} onChange={(e) => updateForm("trackId", e.target.value)} style={{flex:1,padding:10,borderRadius:8,border:"1px solid rgba(143,100,32,0.2)",fontSize:14}}>
                <option value="">-- Chọn đường đua --</option>
                {tracks.map((t) => (<option key={t.id || t.Id} value={t.id || t.Id}>{t.name || t.Name}</option>))}
              </select>
              <button type="button" onClick={() => setShowTrackForm(!showTrackForm)} style={{padding:"10px 16px",borderRadius:8,border:"1px solid #e6a54a",background:"transparent",color:"#e6a54a",cursor:"pointer",whiteSpace:"nowrap",fontSize:13,fontWeight:600}}>+ Tạo đường đua</button>
            </div>
            {showTrackForm && (
              <div style={{marginTop:8,display:"flex",gap:8}}>
                <input value={newTrackName} onChange={(e) => setNewTrackName(e.target.value)} placeholder="Tên đường đua mới" style={{flex:1,padding:"8px 12px",borderRadius:8,border:"1px solid rgba(143,100,32,0.2)",fontSize:13}} autoFocus />
                <button type="button" onClick={handleCreateTrack} style={{padding:"8px 16px",borderRadius:8,border:"none",background:"#e6a54a",color:"#fff",cursor:"pointer",fontSize:13}}>Tạo</button>
                <button type="button" onClick={() => setShowTrackForm(false)} style={{padding:"8px 12px",borderRadius:8,border:"none",background:"transparent",color:"#657086",cursor:"pointer",fontSize:13}}>Hủy</button>
              </div>
            )}
          </div>

          <Input label="Tên cuộc đua" value={form.name} onChange={(e) => updateForm("name", e.target.value)} placeholder="Ví dụ: Chung kết 1200m" required />

          {/* Rounds inline */}
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"#34415b"}}>Vòng đua ({rounds.length})</label>
            {rounds.map((round, idx) => (
              <div key={idx} style={{display:"grid",gridTemplateColumns:"1fr 2fr auto",gap:8,marginBottom:8}}>
                <input value={round.name} onChange={(e) => updateRound(idx, "name", e.target.value)} placeholder="VD: Vòng loại, Bán kết..." style={{padding:"10px 12px",borderRadius:8,border:"1px solid rgba(143,100,32,0.2)",fontSize:13}} />
                <input type="datetime-local" value={round.scheduledAt} onChange={(e) => updateRound(idx, "scheduledAt", e.target.value)} style={{padding:"10px 12px",borderRadius:8,border:"1px solid rgba(143,100,32,0.2)",fontSize:13}} />
                {rounds.length > 1 && <button type="button" onClick={() => removeRound(idx)} style={{padding:"10px 16px",borderRadius:8,border:"1px solid #ef4444",background:"transparent",color:"#ef4444",cursor:"pointer",fontSize:16}}>X</button>}
              </div>
            ))}
            <button type="button" onClick={addRound} style={{background:"none",border:"none",color:"#8f6420",cursor:"pointer",fontSize:13,fontWeight:600,padding:0}}>+ Thêm vòng</button>
          </div>

          <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:16,marginBottom:16}}>
            <Input label="Khoảng cách (m)" type="number" value={form.distance} onChange={(e) => updateForm("distance", e.target.value)} min="100" step="100" />
            <Input label="Số ngựa tối đa" type="number" value={form.maxParticipants} onChange={(e) => updateForm("maxParticipants", e.target.value)} min="2" max="20" />
          </div>

          <Input label="Thời gian bắt đầu" type="datetime-local" value={form.scheduledAt} onChange={(e) => updateForm("scheduledAt", e.target.value)} required />
          <Input label="Thời gian kết thúc (dự kiến)" type="datetime-local" value={form.scheduledEndAt} onChange={(e) => updateForm("scheduledEndAt", e.target.value)} />

          {/* Referees */}
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"#34415b"}}>Chọn trọng tài ({selectedRefereeIds.length})</label>
            <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fill, minmax(180px, 1fr))",gap:8,maxHeight:180,overflowY:"auto",padding:8,border:"1px solid rgba(143,100,32,0.1)",borderRadius:8}}>
              {referees.map((r) => {
                const id = r.id || r.Id;
                const checked = selectedRefereeIds.includes(id);
                return (
                  <label key={id} style={{display:"flex",alignItems:"center",gap:6,padding:"8px 10px",background:checked?"#eef2ff":"transparent",border:`2px solid ${checked?"#6366f1":"#e5e7eb"}`,borderRadius:6,cursor:"pointer",fontSize:13}}>
                    <input type="checkbox" checked={checked} onChange={(e) => { e.target.checked ? setSelectedRefereeIds([...selectedRefereeIds, id]) : setSelectedRefereeIds(selectedRefereeIds.filter((i) => i !== id)); }} />
                    <span>{r.userFullName || r.UserFullName || r.fullName || r.FullName}</span>
                  </label>
                );
              })}
            </div>
          </div>

          <div style={{display:"flex",gap:12,justifyContent:"flex-end",marginTop:24,paddingTop:16,borderTop:"1px solid rgba(143,100,32,0.1)"}}>
            <button type="button" onClick={onClose} style={{padding:"10px 20px",borderRadius:8,border:"1px solid rgba(143,100,32,0.2)",background:"transparent",cursor:"pointer",fontSize:14,color:"#34415b"}}>Hủy</button>
            <button type="submit" disabled={submitting} style={{padding:"10px 24px",borderRadius:8,border:"none",background:submitting?"#ccc":"#8f6420",color:"#fff",cursor:submitting?"not-allowed":"pointer",fontSize:14,fontWeight:600}}>{submitting ? "Đang xử lý..." : isEdit ? "Lưu" : "Tạo"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default RaceForm;
