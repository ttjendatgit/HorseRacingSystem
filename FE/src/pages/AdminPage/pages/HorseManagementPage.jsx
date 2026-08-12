import { useState, useEffect } from "react";
import { request } from "../../../services/apiClient";

export default function HorseManagementPage() {
  const [horses, setHorses] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      request("/api/horses/all").catch(() => ({ data: [] })),
      request("/api/races/management/busy-horses").catch(() => ({ data: [] })),
    ])
      .then(([hData, busyData]) => {
        const list = Array.isArray(hData?.data ?? hData) ? (hData?.data ?? hData) : [];
        const busyIds = Array.isArray(busyData?.data ?? busyData) ? (busyData?.data ?? busyData) : [];
        setHorses(list.map(h => ({
          ...h,
          isBusy: busyIds.includes(h.id || h.Id),
        })));
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const getJockeyName = (h) => {
    const invs = h.jockeyInvitations || h.JockeyInvitations || [];
    const entries = h.raceEntries || h.RaceEntries || [];
    const active = invs.find(i => (i.status || i.Status) === 2 || (i.status || i.Status) === "Accepted");
    if (active) return active.jockey?.user?.fullName || active.Jockey?.User?.FullName;
    const lastEntry = [...entries].reverse().find(e => e.jockey || e.Jockey);
    return lastEntry?.jockey?.user?.fullName || lastEntry?.Jockey?.User?.FullName || "Chưa có";
  };

  if (loading) return <div style={{padding:40,textAlign:"center",color:"var(--hr-muted)"}}>Đang tải...</div>;

  return (
    <div style={{maxWidth:1000,margin:"0 auto",padding:"24px 32px"}}>
      <h1 style={{margin:"0 0 24px",fontSize:28,color:"var(--hr-paper)"}}>Quản lý ngựa</h1>
      {horses.length === 0 ? (
        <p style={{color:"var(--hr-muted)"}}>Chưa có ngựa nào.</p>
      ) : (
        <table style={{width:"100%",borderCollapse:"collapse",fontSize:14}}>
          <thead><tr style={{borderBottom:"2px solid var(--hr-border)"}}>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Ngựa</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Giống</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Kỵ sĩ</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Chủ sở hữu</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Trạng thái</th>
          </tr></thead>
          <tbody>
            {horses.map(h => {
              const id = h.id || h.Id;
              return (
                <tr key={id} style={{borderBottom:"1px solid var(--hr-border-soft)"}}>
                  <td style={{padding:"12px 16px",fontWeight:600,color:"var(--hr-paper)"}}>{h.name || h.Name}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)"}}>{h.breed || h.Breed || "-"}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-text)"}}>🏇 {getJockeyName(h)}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)"}}>{h.owner?.user?.fullName || h.Owner?.User?.FullName || "-"}</td>
                  <td style={{padding:"12px 16px"}}>
                    {h.isBusy ? (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(185,138,69,0.16)",color:"var(--hr-warning)"}}>Đang đua</span>
                    ) : (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(112,139,104,0.16)",color:"var(--hr-success)"}}>Sẵn sàng</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}
