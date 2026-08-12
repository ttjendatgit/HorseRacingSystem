import { useState, useEffect } from "react";
import { request } from "../../../services/apiClient";

export default function RefereeManagementPage() {
  const [referees, setReferees] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    request("/api/referees")
      .then(d => {
        const list = Array.isArray(d?.data ?? d) ? (d?.data ?? d) : [];
        // Load assignments for each referee
        return Promise.all(list.map(async (ref) => {
          try {
            const assignments = await request(`/api/referees/${ref.id || ref.Id}/assignments`);
            const list2 = Array.isArray(assignments?.data ?? assignments) ? (assignments?.data ?? assignments) : [];
            return { ...ref, assignments: list2, assignmentCount: list2.length };
          } catch { return { ...ref, assignments: [], assignmentCount: 0 }; }
        }));
      })
      .then(setReferees)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div style={{padding:40,textAlign:"center",color:"var(--hr-muted)"}}>Đang tải...</div>;

  return (
    <div style={{maxWidth:1000,margin:"0 auto",padding:"24px 32px"}}>
      <h1 style={{margin:"0 0 24px",fontSize:28,color:"var(--hr-paper)"}}>Quản lý trọng tài</h1>
      {referees.length === 0 ? (
        <p style={{color:"var(--hr-muted)"}}>Chưa có trọng tài nào.</p>
      ) : (
        <table style={{width:"100%",borderCollapse:"collapse",fontSize:14}}>
          <thead><tr style={{borderBottom:"2px solid var(--hr-border)"}}>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Trọng tài</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Chuyên môn</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Số lần phân công</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Trạng thái</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Cuộc đua hiện tại</th>
          </tr></thead>
          <tbody>
            {referees.map(r => {
              const id = r.id || r.Id;
              const activeAssignments = (r.assignments || []).filter(
                a => (a.status || a.Status || "").toLowerCase() === "confirmed"
              );
              return (
                <tr key={id} style={{borderBottom:"1px solid var(--hr-border-soft)"}}>
                  <td style={{padding:"12px 16px",fontWeight:600,color:"var(--hr-paper)"}}>{r.userFullName || r.UserFullName}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)"}}>{r.specialization || r.Specialization || "-"}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-text)"}}>{r.totalAssignments || r.TotalAssignments || 0}</td>
                  <td style={{padding:"12px 16px"}}>
                    {r.isActive || r.IsActive ? (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(112,139,104,0.16)",color:"var(--hr-success)"}}>Hoạt động</span>
                    ) : (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(201,105,90,0.16)",color:"var(--hr-danger)"}}>Không hoạt động</span>
                    )}
                  </td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)",fontSize:13}}>
                    {activeAssignments.length > 0
                      ? activeAssignments.map((a, i) => (
                          <span key={i}>{a.raceName || a.RaceName}{i < activeAssignments.length - 1 ? ", " : ""}</span>
                        ))
                      : "Không có"}
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
