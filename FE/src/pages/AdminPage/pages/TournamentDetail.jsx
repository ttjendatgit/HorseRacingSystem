import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "../../../services/apiClient";
import RaceForm from "../../../components/RaceForm";
import {
  getTournamentRegistrationSummary,
  getPendingTournamentRegistrations,
  approveTournamentRegistration,
  rejectTournamentRegistration,
} from "../../../services/adminApi";
import { apiToVNDate, apiToVNDisplay } from "../../../utils/vnDateTime";
import { getTournamentRegistrationState, isFinalRound } from "../../../utils/tournamentRegistration";

// Race progress (RaceStatus) — event lifecycle only. RegistrationOpen/
// RegistrationClosed are transitional compatibility values and display as neutral pre-race state.
const raceStatusBg = (s) => ({scheduled:"rgba(37,99,235,0.1)",registrationopen:"rgba(100,116,139,0.1)",registrationclosed:"rgba(100,116,139,0.1)",inprogress:"rgba(245,158,11,0.1)",finished:"rgba(16,185,129,0.1)",cancelled:"rgba(239,68,68,0.1)"})[s]||"rgba(100,116,139,0.1)";
const raceStatusColor = (s) => ({scheduled:"#60a5fa",registrationopen:"var(--hr-muted)",registrationclosed:"var(--hr-muted)",inprogress:"#f59e0b",finished:"#10b981",cancelled:"#ef4444"})[s]||"var(--hr-muted)";
const raceStatusLabel = (s) => ({scheduled:"Sắp diễn ra",registrationopen:"Chuẩn bị",registrationclosed:"Chuẩn bị",inprogress:"Đang đua",finished:"Đã kết thúc",cancelled:"Đã hủy"})[s]||s||"Không xác định";

// Result status (RaceResultStatus) — separate concern from race progress.
// Only ever "provisional" or "official"; absent entirely until a referee submits.
const resultStatusBg = (s) => ({provisional:"rgba(245,158,11,0.14)",official:"rgba(16,185,129,0.14)"})[s]||"transparent";
const resultStatusColor = (s) => ({provisional:"var(--hr-warning)",official:"var(--hr-success)"})[s]||"var(--hr-muted)";
const resultStatusLabel = (s) => ({provisional:"Tạm thời (chờ duyệt)",official:"Chính thức"})[s]||"";

// Vietnam-timezone policy (Asia/Ho_Chi_Minh, UTC+7) — see FE/src/utils/vnDateTime.js.
const fmtDate2 = (v) => apiToVNDate(v);
const fmtDateTime2 = (v) => apiToVNDisplay(v);

function OddsEditor({ raceId, horseId, odds, setMessage }) {
  const [val, setVal] = useState(String(odds));
  const [saving, setSaving] = useState(false);
  const save = async () => {
    const num = Number(val);
    if (!num || num <= 0) { setMessage("Tỷ lệ cược phải lớn hơn 0."); return; }
    setSaving(true);
    try {
      await request(`/api/races/management/${raceId}/entries/${horseId}/odds`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ odds: num }),
      });
      setMessage("Đã cập nhật tỷ lệ cược!");
    } catch (err) { setMessage(err.message); }
    setSaving(false);
  };
  return (
    <span style={{display:"flex",alignItems:"center",gap:4}}>
      <input type="number" step="0.01" min="1" value={val} onChange={(e)=>setVal(e.target.value)}
        className="hr-field"
        style={{width:60,padding:"3px 6px",borderRadius:6,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)",color:"var(--hr-text)",fontSize:12}} />
      <button onClick={save} disabled={saving} style={{padding:"3px 8px",borderRadius:6,border:"none",background:"var(--hr-crimson)",color:"var(--hr-paper)",cursor:saving?"not-allowed":"pointer",opacity:saving?0.6:1,fontSize:11}}>{saving?"...":"Lưu"}</button>
    </span>
  );
}

export default function TournamentDetail({ t, onBack, setMessage, getTournamentRaces, getTournamentRounds }) {
  const tId = t.id??t.Id;
  const status = t.statusName??t.StatusName;
  const isDraft = status === "Draft";
  const navigate = useNavigate();
  const [races, setRaces] = useState([]);
  const [rounds, setRounds] = useState([]);
  const [showRaceForm, setShowRaceForm] = useState(false);
  const [editRaceData, setEditRaceData] = useState(null);
  const [expandedRaceId, setExpandedRaceId] = useState(null);
  const [raceDetailData, setRaceDetailData] = useState({});
  const [regSummary, setRegSummary] = useState({ approvedCount: 0, pendingCount: 0 });
  const [pendingRegs, setPendingRegs] = useState([]);

  const viewTournament = async () => {
    try{const d=await getTournamentRaces(tId);setRaces(Array.isArray(d)?d:[]);}catch{setRaces([]);}
    // Vòng đấu summary — read-only context, reuses the existing rounds endpoint (already used
    // by ScheduleManagement) rather than duplicating any round-management UI here.
    try{const rd=await getTournamentRounds(tId);setRounds(Array.isArray(rd)?rd:[]);}catch{setRounds([]);}
  };

  // Task B Correction 2 §3: minimum Admin Tournament-registration review — reuses the existing
  // Tournament registration APIs (summary + all-pending, filtered to this Tournament), never the
  // RaceEntry approval endpoints.
  const refreshRegistrations = async () => {
    try {
      const [summary, allPending] = await Promise.all([
        getTournamentRegistrationSummary(tId),
        getPendingTournamentRegistrations(),
      ]);
      setRegSummary(summary || { approvedCount: 0, pendingCount: 0 });
      const list = Array.isArray(allPending) ? allPending : [];
      setPendingRegs(list.filter((r) => String(r.tournamentId ?? r.TournamentId) === String(tId)));
    } catch { /* non-critical */ }
  };

  useEffect(() => { viewTournament(); }, [tId]);
  useEffect(() => { refreshRegistrations(); }, [tId]);

  const handleApproveRegistration = async (id) => {
    try {
      await approveTournamentRegistration(id);
      setMessage("Đã duyệt đăng ký ngựa vào giải.");
      refreshRegistrations();
    } catch (err) { setMessage(err.message); }
  };

  const handleRejectRegistration = async (id) => {
    const reason = window.prompt("Lý do từ chối (không bắt buộc):");
    if (reason === null) return;
    try {
      await rejectTournamentRegistration(id, reason);
      setMessage("Đã từ chối đăng ký.");
      refreshRegistrations();
    } catch (err) { setMessage(err.message); }
  };

  const regDeadlineRaw = t.registrationDeadline ?? t.RegistrationDeadline;
  const registrationState = getTournamentRegistrationState(t).label;
  const maxParticipants = t.maxParticipants ?? t.MaxParticipants ?? "?";
  // Task capacity gate §5: Admin must see capacity-full plainly, without needing to click
  // Approve first to discover it via the backend's rejection.
  const isCapacityFull = typeof maxParticipants === "number" && maxParticipants > 0 && regSummary.approvedCount >= maxParticipants;

  return (
    <><div style={{marginBottom:12}}><button className="ghost-button" onClick={onBack} style={{fontSize:13}}>← Quay lại</button></div>
    <div style={{padding:"20px 24px",borderRadius:16,border:"1px solid var(--hr-border)",background:"var(--hr-surface)",marginBottom:20}}>
      <div style={{display:"flex",justifyContent:"space-between",alignItems:"flex-start",flexWrap:"wrap",gap:12}}>
        <div><h2 style={{margin:"0 0 4px",fontSize:24,color:"var(--hr-paper)",fontFamily:"var(--font-display)"}}>{t.name??t.Name}</h2>
          <p style={{margin:0,fontSize:13,color:"var(--hr-muted)"}}>{(t.venue??t.Venue)?"📍 "+(t.venue??t.Venue)+" · ":""}📅 {fmtDateTime2(t.startDate??t.StartDate)} → {fmtDateTime2(t.endDate??t.EndDate)}</p>
          <p style={{margin:"2px 0 0",fontSize:13,color:"var(--hr-muted)"}}>⏳ Hạn đăng ký: {fmtDateTime2(regDeadlineRaw) || "Chưa thiết lập"} · {registrationState}</p></div>
        <div style={{display:"flex",gap:8}}>
          {isDraft && <button className="primary-button" style={{padding:"6px 14px",fontSize:13}} onClick={()=>navigate(`/admin/rounds?tournamentId=${tId}`)}>+ Tạo vòng đấu</button>}
          <button className="primary-button" style={{padding:"6px 14px",fontSize:13}} onClick={()=>setShowRaceForm(true)}>+ Tạo cuộc đua</button>
        </div>
      </div>
    </div>
    <div style={{padding:"20px 24px",borderRadius:16,border:"1px solid var(--hr-border)",background:"var(--hr-surface)",marginBottom:20}}>
      <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:12,flexWrap:"wrap",gap:8}}>
        <h3 style={{margin:0,fontSize:16,color:"var(--hr-paper)"}}>ĐĂNG KÝ NGỰA</h3>
        <div style={{fontSize:13,color:"var(--hr-muted)"}}>
          Đã duyệt: <strong style={{color:isCapacityFull?"var(--hr-warning)":"var(--hr-paper)"}}>{regSummary.approvedCount}/{maxParticipants}</strong>
          {" "}· Chờ duyệt: <strong style={{color:"var(--hr-gold-soft)"}}>{regSummary.pendingCount}</strong>
          {isCapacityFull && (
            <span style={{marginLeft:8,padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(185,138,69,0.16)",color:"var(--hr-warning)"}}>
              Đã đủ số lượng tham gia
            </span>
          )}
        </div>
      </div>
      {pendingRegs.length === 0 ? (
        <p style={{color:"var(--hr-muted)",fontSize:13,margin:0}}>Không có đăng ký nào đang chờ duyệt.</p>
      ) : (
        <div style={{display:"grid",gap:8}}>
          {pendingRegs.map((r) => {
            const id = r.id ?? r.Id;
            return (
              <div key={id} style={{display:"flex",justifyContent:"space-between",alignItems:"center",flexWrap:"wrap",gap:10,padding:"10px 14px",borderRadius:10,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)"}}>
                <div>
                  <strong style={{fontSize:14,color:"var(--hr-paper)"}}>{r.horseName ?? r.HorseName}</strong>
                  <p style={{margin:"2px 0 0",fontSize:12,color:"var(--hr-muted)"}}>Chủ ngựa: {r.ownerName ?? r.OwnerName ?? "Chưa xác định"} · {r.tournamentName ?? r.TournamentName} · Gửi lúc {fmtDateTime2(r.createdAt ?? r.CreatedAt)}</p>
                </div>
                <div style={{display:"flex",gap:8,alignItems:"center"}}>
                  <span style={{fontSize:11,padding:"2px 8px",borderRadius:999,background:"rgba(185,138,69,0.14)",color:"var(--hr-warning)",fontWeight:700}}>Chờ duyệt</span>
                  <button style={{padding:"6px 12px",fontSize:12,borderRadius:6,border:"1px solid rgba(201,105,90,.4)",background:"rgba(201,105,90,0.16)",color:"var(--hr-danger)",cursor:"pointer",fontWeight:600}} onClick={()=>handleRejectRegistration(id)}>Từ chối</button>
                  <button
                    style={{padding:"6px 12px",fontSize:12,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:isCapacityFull?"rgba(148,163,184,0.12)":"rgba(112,139,104,0.16)",color:isCapacityFull?"var(--hr-muted)":"var(--hr-success)",cursor:isCapacityFull?"not-allowed":"pointer",fontWeight:600,opacity:isCapacityFull?0.6:1}}
                    disabled={isCapacityFull}
                    title={isCapacityFull ? "Giải đấu đã đủ số lượng ngựa tham gia" : undefined}
                    onClick={()=>handleApproveRegistration(id)}
                  >
                    Phê duyệt
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
    <div style={{padding:"20px 24px",borderRadius:16,border:"1px solid var(--hr-border)",background:"var(--hr-surface)",marginBottom:20}}>
      <h3 style={{margin:"0 0 12px",fontSize:16,color:"var(--hr-paper)"}}>VÒNG ĐẤU ({rounds.length})</h3>
      {rounds.length === 0 ? (
        <p style={{color:"var(--hr-muted)",fontSize:13,margin:0}}>Chưa có vòng đấu nào.</p>
      ) : (
        <div style={{display:"grid",gap:8}}>
          {rounds.map((r) => {
            const rid = r.id ?? r.Id;
            const roundNumber = r.roundNumber ?? r.RoundNumber;
            const roundName = r.name ?? r.Name;
            const advanceCount = r.advanceCount ?? r.AdvanceCount;
            const raceCount = r.raceCount ?? r.RaceCount ?? 0;
            const scheduledStart = r.scheduledStartDate ?? r.ScheduledStartDate;
            // V0/V0.1: Final is defined by RoundNumber === Tournament.MaxRounds, not AdvanceCount
            // alone — Draft data may temporarily hold AdvanceCount=0 on a non-final round.
            const isFinal = isFinalRound(r, t);
            return (
              <div key={rid} style={{display:"flex",justifyContent:"space-between",alignItems:"center",flexWrap:"wrap",gap:10,padding:"10px 14px",borderRadius:10,border:"1px solid var(--hr-border-soft)",background:"var(--hr-surface-2)"}}>
                <div>
                  <strong style={{fontSize:14,color:"var(--hr-paper)"}}>Vòng {roundNumber}{roundName ? ` — ${roundName}` : ""}</strong>
                  {isFinal && <span style={{marginLeft:8,padding:"1px 8px",borderRadius:999,fontSize:10,fontWeight:700,background:"rgba(184,134,59,0.16)",color:"var(--hr-gold-soft)"}}>Vòng chung kết</span>}
                  <p style={{margin:"2px 0 0",fontSize:12,color:"var(--hr-muted)"}}>
                    {scheduledStart ? fmtDateTime2(scheduledStart) : "Chưa xếp lịch"} · {raceCount} cuộc đua
                    {advanceCount != null ? ` · Số ngựa vào vòng sau: ${advanceCount}` : ""}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
    {showRaceForm && !editRaceData && <RaceForm tournamentId={tId} tournamentName={t.name??t.Name} tournamentStartDate={t.startDate??t.StartDate} tournamentEndDate={t.endDate??t.EndDate} tournamentRegistrationDeadline={t.registrationDeadline??t.RegistrationDeadline}
      onClose={()=>setShowRaceForm(false)} onSuccess={()=>{setShowRaceForm(false);setMessage("Cuộc đua đã tạo!");viewTournament();}}/>}
    {editRaceData && <RaceForm tournamentId={tId} tournamentName={t.name??t.Name} tournamentStartDate={t.startDate??t.StartDate} tournamentEndDate={t.endDate??t.EndDate} tournamentRegistrationDeadline={t.registrationDeadline??t.RegistrationDeadline}
      raceData={editRaceData} onClose={()=>setEditRaceData(null)} onSuccess={()=>{setEditRaceData(null);setMessage("Đã cập nhật!");viewTournament();}}/>}
    <h3 style={{margin:"0 0 12px",fontSize:18,color:"var(--hr-paper)"}}>Cuộc đua ({races.length})</h3>
    {races.length===0 ? <p style={{color:"var(--hr-muted)",fontSize:14}}>Chưa có cuộc đua nào.</p>
    : <div style={{display:"grid",gap:10}}>{races.map((race)=>{
      const id=race.id??race.Id;const st=(race.status??race.Status??"").toString().toLowerCase();
      const rst=(race.resultStatus??race.ResultStatus??"").toString().toLowerCase();
      const rejectedReason=race.rejectedReason??race.RejectedReason;
      const exp=expandedRaceId===id;const det=raceDetailData[id]||{};
      const toggleExpand=async()=>{
        if(exp){setExpandedRaceId(null);return;}setExpandedRaceId(id);
        if(!raceDetailData[id]){try{
          const[data,entries,refs,report,result]=await Promise.all([
            request(`/api/races/management/${id}`),
            request(`/api/referees/race/${id}/entries`),
            request(`/api/referees/race/${id}/assignments`),
            request(`/api/referees/race/${id}/report`).catch(()=>null),
            request(`/api/races/${id}/result`).catch(()=>null),
          ]);
          setRaceDetailData(prev=>({...prev,[id]:{
            detail:data?.data??data,
            entries:Array.isArray(entries?.data??entries)?(entries?.data??entries):[],
            refAssignments:Array.isArray(refs?.data??refs)?(refs?.data??refs):[],
            report:report?.data??report,
            result:result?.data??result,
          }}));
        }catch{ /* ignore */ }}
      };
      return <div key={id} style={{borderRadius:12,border:"1px solid var(--hr-border)",background:"var(--hr-surface)",overflow:"hidden"}}>
        <div role="button" tabIndex={0} onClick={toggleExpand} onKeyDown={(e)=>{if(e.key==="Enter"||e.key===" "){e.preventDefault();toggleExpand();}}} style={{padding:"12px 16px",display:"flex",justifyContent:"space-between",alignItems:"center",cursor:"pointer",flexWrap:"wrap",gap:8}}>
          <div><strong style={{fontSize:15,color:"var(--hr-paper)"}}>{race.name??race.Name}</strong>
            <span style={{marginLeft:6,padding:"1px 8px",borderRadius:999,fontSize:10,fontWeight:700,background:raceStatusBg(st),color:raceStatusColor(st)}}>{raceStatusLabel(st)}</span>
            {rst&&<span style={{marginLeft:4,padding:"1px 8px",borderRadius:999,fontSize:10,fontWeight:700,background:resultStatusBg(rst),color:resultStatusColor(rst)}}>{resultStatusLabel(rst)}</span>}
            {(race.roundNumber??race.RoundNumber)!=null&&<p style={{margin:"3px 0 0"}}><span style={{padding:"1px 8px",borderRadius:999,fontSize:10,fontWeight:700,background:"rgba(148,163,184,0.14)",color:"var(--hr-text)"}}>Vòng {race.roundNumber??race.RoundNumber}{(race.roundName??race.RoundName)?` · ${race.roundName??race.RoundName}`:""}</span></p>}
            <p style={{margin:"2px 0 0",fontSize:12,color:"var(--hr-muted)"}}>{fmtDate2(race.scheduledAt??race.ScheduledAt)} · {(race.distance??race.Distance??0)}m</p></div>
          <div style={{display:"flex",gap:6,alignItems:"center",flexWrap:"wrap"}}>
            {["scheduled","registrationopen","registrationclosed"].includes(st)&&<button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid transparent",background:"var(--hr-gold)",color:"var(--hr-bg-deep)",cursor:det.refAssignments?.some(r=>(r.status||r.Status)==="Confirmed")?"pointer":"not-allowed",fontWeight:700,opacity:det.refAssignments?.some(r=>(r.status||r.Status)==="Confirmed")?1:0.5}} disabled={!det.refAssignments?.some(r=>(r.status||r.Status)==="Confirmed")} onClick={async(e)=>{e.stopPropagation();try{await request(`/api/races/management/${id}/start`,{method:"POST"});setMessage("Đã bắt đầu!");viewTournament()}catch(err){setMessage(err.message)}}}>Bắt đầu</button>}
            {st==="inprogress"&&<button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:"rgba(112,139,104,0.16)",color:"var(--hr-success)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();if(!window.confirm("Kết thúc cuộc đua? Thao tác này đánh dấu cuộc đua đã diễn ra xong — kết quả sẽ được trọng tài nộp riêng sau đó."))return;try{await request(`/api/races/management/${id}/end`,{method:"POST"});setMessage("Đã kết thúc cuộc đua. Chờ trọng tài nộp kết quả.");viewTournament()}catch(err){setMessage(err.message)}}}>Kết thúc cuộc đua</button>}
            {st==="finished"&&!rst&&<span style={{fontSize:11,color:"var(--hr-muted)"}}>Đã kết thúc — chờ trọng tài nộp kết quả</span>}
            {st==="finished"&&rst==="provisional"&&<><button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:"rgba(112,139,104,0.16)",color:"var(--hr-success)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();if(!window.confirm("Duyệt kết quả này thành chính thức (Official)?"))return;try{await request(`/api/admin/races/${id}/approve-result`,{method:"POST"});setMessage("Đã duyệt kết quả chính thức!");viewTournament()}catch(err){setMessage(err.message)}}}>Duyệt KQ</button><button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(201,105,90,.4)",background:"rgba(201,105,90,0.16)",color:"var(--hr-danger)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();const reason=window.prompt("Lý do từ chối:");if(!reason)return;try{await request(`/api/admin/races/${id}/reject-result`,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({reason})});setMessage("Đã từ chối kết quả tạm thời. Trọng tài cần nộp lại.");viewTournament()}catch(err){setMessage(err.message)}}}>Từ chối</button></>}
            {st==="finished"&&rst==="official"&&<span style={{fontSize:11,color:"var(--hr-success)",fontWeight:600}}>✓ Kết quả chính thức — dự đoán đã thanh toán</span>}
            {rejectedReason&&rst==="provisional"&&<span style={{fontSize:10,color:"var(--hr-danger)"}}>Đã bị từ chối trước đó: {rejectedReason}</span>}
            <button className="ghost-button" style={{padding:"4px 10px",fontSize:11}} onClick={async(e)=>{e.stopPropagation();try{const[d,entries,refs]=await Promise.all([request(`/api/races/${id}`),request(`/api/referees/race/${id}/entries`),request(`/api/referees/race/${id}/assignments`)]);const r=d?.data??d;r._selectedHorseIds=(Array.isArray(entries?.data??entries)?(entries?.data??entries):[]).map(e=>e.horseId||e.HorseId);r._selectedRefereeIds=(Array.isArray(refs?.data??refs)?(refs?.data??refs):[]).map(r=>r.refereeId||r.RefereeId);setEditRaceData(r);}catch(err){setMessage(err.message)}}}>Sửa</button>
            <span style={{fontSize:11,color:"var(--hr-muted)"}}>{exp?"▲":"▼"}</span>
          </div>
        </div>
        {exp&&<div style={{padding:"0 16px 16px",borderTop:"1px solid var(--hr-border-soft)"}}>
          <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(120px,1fr))",gap:10,padding:"12px 0"}}>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Vòng đấu</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{(race.roundNumber??race.RoundNumber)?`Vòng ${race.roundNumber??race.RoundNumber}${(race.roundName??race.RoundName)?" — "+(race.roundName??race.RoundName):""}`:"—"}</strong></div>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Đường đua</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{race.trackName??race.TrackName??"Chưa chọn"}</strong></div>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Khoảng cách</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{(race.distance??race.Distance??0)}m</strong></div>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Ngựa</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{det.entries?.length||(race.entriesCount??race.EntriesCount??0)}/{race.maxParticipants??race.MaxParticipants}</strong></div>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Bắt đầu</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{fmtDate2(race.scheduledAt??race.ScheduledAt)}</strong></div>
          </div>
          {det.entries?.length>0 && <div style={{marginTop:8}}>
            <h4 style={{fontSize:12,margin:"0 0 6px",color:"var(--hr-paper)"}}>Tỷ lệ cược (admin có thể chỉnh)</h4>
            <table style={{width:"100%",borderCollapse:"collapse",fontSize:12}}>
              <thead><tr>
                <th style={{textAlign:"left",padding:"4px 8px",borderBottom:"1px solid var(--hr-border-soft)",color:"var(--hr-muted)"}}>Ngựa</th>
                <th style={{textAlign:"left",padding:"4px 8px",borderBottom:"1px solid var(--hr-border-soft)",color:"var(--hr-muted)"}}>Chủ ngựa</th>
                <th style={{textAlign:"left",padding:"4px 8px",borderBottom:"1px solid var(--hr-border-soft)",color:"var(--hr-muted)"}}>Kỵ sĩ</th>
                <th style={{textAlign:"left",padding:"4px 8px",borderBottom:"1px solid var(--hr-border-soft)",color:"var(--hr-muted)"}}>Tỷ lệ</th>
              </tr></thead>
              <tbody>{det.entries.map((e,i)=>(
                <tr key={i}><td style={{padding:"4px 8px",fontWeight:600,color:"var(--hr-paper)"}}>{e.horseName||e.HorseName}</td>
                <td style={{padding:"4px 8px",color:"var(--hr-muted)"}}>{e.ownerName||e.OwnerName||"Chưa xác định"}</td>
                <td style={{padding:"4px 8px",color:"var(--hr-muted)"}}>{e.jockeyName||e.JockeyName||"Chưa có"}</td>
                <td style={{padding:"4px 8px"}}><OddsEditor raceId={id} horseId={e.horseId||e.HorseId} odds={e.odds||e.Odds||1} setMessage={setMessage} /></td></tr>
              ))}</tbody>
            </table>
          </div>}
          {det.refAssignments?.length>0&&<div style={{marginTop:6}}><h4 style={{fontSize:12,margin:"0 0 4px",color:"var(--hr-paper)"}}>Trọng tài</h4>{det.refAssignments.map((r,i)=>{const ss=(r.status||r.Status||"").toLowerCase();return(<div key={i} style={{display:"flex",alignItems:"center",gap:4,marginBottom:2,fontSize:11}}><span style={{width:6,height:6,borderRadius:"50%",background:ss==="confirmed"?"#10b981":"#f59e0b"}}/><span style={{color:"var(--hr-text)"}}>{r.refereeName||r.RefereeName}</span><span style={{color:"var(--hr-muted)"}}>{ss==="confirmed"?"✅":"⏳"}</span></div>)})}</div>}
          {st==="finished"&&det.result&&(()=>{
            const dRst=(det.result.resultStatus||det.result.ResultStatus||"").toLowerCase();
            const isOfficial=dRst==="official";
            const wid=det.result.winningHorseId||det.result.WinningHorseId;
            const we=det.entries.find(e=>(e.horseId||e.HorseId)===wid);
            return(<div style={{marginTop:8,padding:"8px 12px",borderRadius:8,background:isOfficial?"rgba(112,139,104,0.1)":"rgba(185,138,69,0.1)",border:`1px solid ${isOfficial?"rgba(112,139,104,0.25)":"rgba(185,138,69,0.3)"}`,fontSize:12}}>
              <strong style={{color:isOfficial?"var(--hr-success)":"var(--hr-warning)"}}>{isOfficial?"Kết quả chính thức:":"Kết quả tạm thời (chưa duyệt):"}</strong>{" "}
              <strong style={{color:"var(--hr-paper)"}}>{we?.horseName||we?.HorseName||"Chưa xác định"}</strong>
              {we?.jockeyName||we?.JockeyName?<span style={{color:"var(--hr-muted)"}}> — {we?.jockeyName||we?.JockeyName}</span>:null}
              {(det.result.notes||det.result.Notes)?<div style={{color:"var(--hr-muted)",marginTop:2}}>{det.result.notes||det.result.Notes}</div>:null}
            </div>)
          })()}
          {det.report&&<div style={{marginTop:8,padding:"8px 12px",borderRadius:8,background:"rgba(139,92,246,0.1)",border:"1px solid rgba(139,92,246,0.25)",fontSize:12}}><strong style={{color:"#c4b5fd"}}>📋 Báo cáo trọng tài</strong><p style={{margin:"4px 0 0",color:"var(--hr-text)"}}>{det.report.details||det.report.Details||"—"}</p>{(det.report.incidents||det.report.Incidents)&&<p style={{margin:"4px 0 0",color:"var(--hr-muted)"}}>Sự cố: {det.report.incidents||det.report.Incidents}</p>}<span style={{display:"block",marginTop:4,fontSize:10,color:"var(--hr-muted)"}}>{det.report.refereeName||det.report.RefereeName||"Trọng tài"}{det.report.completedAt||det.report.CompletedAt?` · ${new Date(det.report.completedAt||det.report.CompletedAt).toLocaleString("vi-VN")}`:""}</span></div>}
        </div>}
      </div>;
    })}</div>}
    </>
  );
}
