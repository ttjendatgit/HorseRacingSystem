import { useState, useEffect } from "react";
import { request } from "../../../services/apiClient";
import RaceForm from "../../../components/RaceForm";

const raceStatusBg = (s) => ({scheduled:"rgba(37,99,235,0.1)",registrationopen:"rgba(16,185,129,0.1)",registrationclosed:"rgba(100,116,139,0.1)",inprogress:"rgba(245,158,11,0.1)",finished:"rgba(16,185,129,0.1)",cancelled:"rgba(239,68,68,0.1)",awaitingresult:"rgba(139,92,246,0.1)",resultpendingapproval:"rgba(245,158,11,0.1)",resultapproved:"rgba(16,185,129,0.1)"})[s]||"rgba(100,116,139,0.1)";
const raceStatusColor = (s) => ({scheduled:"#60a5fa",registrationopen:"var(--hr-success)",registrationclosed:"var(--hr-muted)",inprogress:"#f59e0b",finished:"#10b981",cancelled:"#ef4444",awaitingresult:"#c4b5fd",resultpendingapproval:"#f59e0b",resultapproved:"var(--hr-success)"})[s]||"var(--hr-muted)";
const raceStatusLabel = (s) => ({scheduled:"Sắp diễn ra",registrationopen:"Mở đăng ký",registrationclosed:"Đã đóng đăng ký",inprogress:"Đang đua",finished:"Đã kết thúc",cancelled:"Đã hủy",awaitingresult:"Chờ kết quả",resultpendingapproval:"Chờ duyệt",resultapproved:"Đã duyệt KQ"})[s]||s||"Không xác định";
const fmtDate2 = (v) => v?new Date(v).toLocaleDateString("vi-VN",{day:"2-digit",month:"2-digit",year:"numeric"}):"";

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

export default function TournamentDetail({ t, onBack, setMessage, getTournamentRaces }) {
  const tId = t.id??t.Id;
  const [races, setRaces] = useState([]);
  const [showRaceForm, setShowRaceForm] = useState(false);
  const [editRaceData, setEditRaceData] = useState(null);
  const [expandedRaceId, setExpandedRaceId] = useState(null);
  const [raceDetailData, setRaceDetailData] = useState({});

  const viewTournament = async () => {
    try{const d=await getTournamentRaces(tId);setRaces(Array.isArray(d)?d:[]);}catch{setRaces([]);}
  };

  useEffect(() => { viewTournament(); }, [tId]);

  return (
    <><div style={{marginBottom:12}}><button className="ghost-button" onClick={onBack} style={{fontSize:13}}>← Quay lại</button></div>
    <div style={{padding:"20px 24px",borderRadius:16,border:"1px solid var(--hr-border)",background:"var(--hr-surface)",marginBottom:20}}>
      <div style={{display:"flex",justifyContent:"space-between",alignItems:"flex-start",flexWrap:"wrap",gap:12}}>
        <div><h2 style={{margin:"0 0 4px",fontSize:24,color:"var(--hr-paper)",fontFamily:"var(--font-display)"}}>{t.name??t.Name}</h2>
          <p style={{margin:0,fontSize:13,color:"var(--hr-muted)"}}>{(t.venue??t.Venue)?"📍 "+(t.venue??t.Venue)+" · ":""}📅 {fmtDate2(t.startDate??t.StartDate)} → {fmtDate2(t.endDate??t.EndDate)}</p></div>
        <div style={{display:"flex",gap:8}}>
          <button className="primary-button" style={{padding:"6px 14px",fontSize:13}} onClick={()=>setShowRaceForm(true)}>+ Tạo cuộc đua</button>
        </div>
      </div>
    </div>
    {showRaceForm && !editRaceData && <RaceForm tournamentId={tId} tournamentName={t.name??t.Name} tournamentStartDate={t.startDate??t.StartDate} tournamentEndDate={t.endDate??t.EndDate}
      onClose={()=>setShowRaceForm(false)} onSuccess={()=>{setShowRaceForm(false);setMessage("Cuộc đua đã tạo!");viewTournament();}}/>}
    {editRaceData && <RaceForm tournamentId={tId} tournamentName={t.name??t.Name} tournamentStartDate={t.startDate??t.StartDate} tournamentEndDate={t.endDate??t.EndDate}
      raceData={editRaceData} onClose={()=>setEditRaceData(null)} onSuccess={()=>{setEditRaceData(null);setMessage("Đã cập nhật!");viewTournament();}}/>}
    <h3 style={{margin:"0 0 12px",fontSize:18,color:"var(--hr-paper)"}}>Cuộc đua ({races.length})</h3>
    {races.length===0 ? <p style={{color:"var(--hr-muted)",fontSize:14}}>Chưa có cuộc đua nào.</p>
    : <div style={{display:"grid",gap:10}}>{races.map((race)=>{
      const id=race.id??race.Id;const st=(race.status??race.Status??"").toString().toLowerCase();
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
            <p style={{margin:"2px 0 0",fontSize:12,color:"var(--hr-muted)"}}>{fmtDate2(race.scheduledAt??race.ScheduledAt)} · {(race.distance??race.Distance??0)}m</p></div>
          <div style={{display:"flex",gap:6,alignItems:"center",flexWrap:"wrap"}}>
            {st==="scheduled"&&<button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:"rgba(112,139,104,0.16)",color:"var(--hr-success)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();try{await request(`/api/races/management/${id}/open-registration`,{method:"POST"});setMessage("Đã mở đăng ký!");viewTournament()}catch(err){setMessage(err.message)}}}>Mở đăng ký</button>}
            {st==="registrationopen"&&<button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid var(--hr-border)",background:"var(--hr-surface-2)",color:"var(--hr-text)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();try{await request(`/api/races/management/${id}/close-registration`,{method:"POST"});setMessage("Đã đóng đăng ký!");viewTournament()}catch(err){setMessage(err.message)}}}>Đóng đăng ký</button>}
            {st==="registrationclosed"&&<button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid transparent",background:"var(--hr-gold)",color:"var(--hr-bg-deep)",cursor:det.refAssignments?.some(r=>(r.status||r.Status)==="Confirmed")?"pointer":"not-allowed",fontWeight:700,opacity:det.refAssignments?.some(r=>(r.status||r.Status)==="Confirmed")?1:0.5}} disabled={!det.refAssignments?.some(r=>(r.status||r.Status)==="Confirmed")} onClick={async(e)=>{e.stopPropagation();try{await request(`/api/races/management/${id}/start`,{method:"POST"});setMessage("Đã bắt đầu!");viewTournament()}catch(err){setMessage(err.message)}}}>Bắt đầu</button>}
            {st==="inprogress"&&<span style={{fontSize:11,color:"var(--hr-warning)"}}>Đang đua - chờ trọng tài nộp kết quả</span>}
            {st==="awaitingresult"&&<span style={{fontSize:11,color:"var(--hr-warning)"}}>Chờ trọng tài nộp lại kết quả</span>}
            {st==="resultpendingapproval"&&<><button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:"rgba(112,139,104,0.16)",color:"var(--hr-success)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();if(!window.confirm("Duyệt kết quả này? Sau khi duyệt bạn có thể kết thúc cuộc đua."))return;try{await request(`/api/admin/races/${id}/approve-result`,{method:"POST"});setMessage("Đã duyệt kết quả!");viewTournament()}catch(err){setMessage(err.message)}}}>Duyệt KQ</button><button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(201,105,90,.4)",background:"rgba(201,105,90,0.16)",color:"var(--hr-danger)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();const reason=window.prompt("Lý do từ chối:");if(!reason)return;try{await request(`/api/admin/races/${id}/reject-result`,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({reason})});setMessage("Đã từ chối kết quả.");viewTournament()}catch(err){setMessage(err.message)}}}>Từ chối</button></>}
            {st==="resultapproved"&&<button style={{padding:"6px 14px",fontSize:12,borderRadius:6,border:"1px solid rgba(112,139,104,.4)",background:"rgba(112,139,104,0.16)",color:"var(--hr-success)",cursor:"pointer",fontWeight:600}} onClick={async(e)=>{e.stopPropagation();if(!window.confirm("Kết thúc cuộc đua? Dự đoán sẽ được thanh toán theo tỉ lệ cược."))return;try{await request(`/api/races/management/${id}/end`,{method:"POST"});setMessage("Đã kết thúc và thanh toán dự đoán!");viewTournament()}catch(err){setMessage(err.message)}}}>Kết thúc</button>}
            <button className="ghost-button" style={{padding:"4px 10px",fontSize:11}} onClick={async(e)=>{e.stopPropagation();try{const[d,entries,refs]=await Promise.all([request(`/api/races/${id}`),request(`/api/referees/race/${id}/entries`),request(`/api/referees/race/${id}/assignments`)]);const r=d?.data??d;r._selectedHorseIds=(Array.isArray(entries?.data??entries)?(entries?.data??entries):[]).map(e=>e.horseId||e.HorseId);r._selectedRefereeIds=(Array.isArray(refs?.data??refs)?(refs?.data??refs):[]).map(r=>r.refereeId||r.RefereeId);setEditRaceData(r);}catch(err){setMessage(err.message)}}}>Sửa</button>
            <span style={{fontSize:11,color:"var(--hr-muted)"}}>{exp?"▲":"▼"}</span>
          </div>
        </div>
        {exp&&<div style={{padding:"0 16px 16px",borderTop:"1px solid var(--hr-border-soft)"}}>
          <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(120px,1fr))",gap:10,padding:"12px 0"}}>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Khoảng cách</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{(race.distance??race.Distance??0)}m</strong></div>
            <div><span style={{fontSize:10,color:"var(--hr-muted)",display:"block"}}>Ngựa</span><strong style={{fontSize:14,color:"var(--hr-paper)"}}>{det.entries?.length||(race.entriesCount??race.EntriesCount??0)}/{race.maxParticipants??race.MaxParticipants??12}</strong></div>
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
          {(race.roundNames||race.RoundNames)&&<div style={{marginTop:8}}><h4 style={{fontSize:12,margin:"0 0 4px",color:"var(--hr-paper)"}}>Vòng đấu</h4><div style={{display:"flex",gap:4,flexWrap:"wrap"}}>{(race.roundNames||race.RoundNames||"").split(",").map((n,i)=><span key={i} style={{padding:"2px 8px",borderRadius:6,background:"rgba(184,134,59,0.12)",fontSize:11,color:"var(--hr-gold-soft)"}}>{n.trim()}</span>)}</div></div>}
          {det.refAssignments?.length>0&&<div style={{marginTop:6}}><h4 style={{fontSize:12,margin:"0 0 4px",color:"var(--hr-paper)"}}>Trọng tài</h4>{det.refAssignments.map((r,i)=>{const ss=(r.status||r.Status||"").toLowerCase();return(<div key={i} style={{display:"flex",alignItems:"center",gap:4,marginBottom:2,fontSize:11}}><span style={{width:6,height:6,borderRadius:"50%",background:ss==="confirmed"?"#10b981":"#f59e0b"}}/><span style={{color:"var(--hr-text)"}}>{r.refereeName||r.RefereeName}</span><span style={{color:"var(--hr-muted)"}}>{ss==="confirmed"?"✅":"⏳"}</span></div>)})}</div>}
          {(st==="resultpendingapproval"||st==="resultapproved")&&det.result&&(()=>{const wid=det.result.winningHorseId||det.result.WinningHorseId;const we=det.entries.find(e=>(e.horseId||e.HorseId)===wid);return(<div style={{marginTop:8,padding:"8px 12px",borderRadius:8,background:"rgba(112,139,104,0.1)",border:"1px solid rgba(112,139,104,0.25)",fontSize:12}}><strong style={{color:"var(--hr-success)"}}>Kết quả trọng tài nộp:</strong> <strong style={{color:"var(--hr-paper)"}}>{we?.horseName||we?.HorseName||"Chưa xác định"}</strong>{we?.jockeyName||we?.JockeyName?<span style={{color:"var(--hr-muted)"}}> — {we?.jockeyName||we?.JockeyName}</span>:null}{det.result.notes||det.result.Notes?<div style={{color:"var(--hr-muted)",marginTop:2}}>{det.result.notes||det.result.Notes}</div>:null}</div>)})()}
          {det.report&&<div style={{marginTop:8,padding:"8px 12px",borderRadius:8,background:"rgba(139,92,246,0.1)",border:"1px solid rgba(139,92,246,0.25)",fontSize:12}}><strong style={{color:"#c4b5fd"}}>📋 Báo cáo trọng tài</strong><p style={{margin:"4px 0 0",color:"var(--hr-text)"}}>{det.report.details||det.report.Details||"—"}</p>{(det.report.incidents||det.report.Incidents)&&<p style={{margin:"4px 0 0",color:"var(--hr-muted)"}}>Sự cố: {det.report.incidents||det.report.Incidents}</p>}<span style={{display:"block",marginTop:4,fontSize:10,color:"var(--hr-muted)"}}>{det.report.refereeName||det.report.RefereeName||"Trọng tài"}{det.report.completedAt||det.report.CompletedAt?` · ${new Date(det.report.completedAt||det.report.CompletedAt).toLocaleString("vi-VN")}`:""}</span></div>}
        </div>}
      </div>;
    })}</div>}
    </>
  );
}
