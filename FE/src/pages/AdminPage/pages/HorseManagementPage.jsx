import { useState, useEffect, useMemo, useCallback } from "react";
import { request } from "../../../services/apiClient";
import { updateOwnerHorseStatus } from "../../../services/adminApi";

// Task C1 UI correction: ApprovalStatus (admin profile review) and IsArchived (participation-
// history-preserving soft delete) are distinct axes — never conflate their labels/badges.
const approvalStatusMap = { 1: "Chờ duyệt", 2: "Đã duyệt", 3: "Từ chối" };
const approvalStatusTone = { 1: "#b8860b", 2: "#0f7a5a", 3: "#b91c1c" };
const filterTabs = [
  { key: "all", label: "Tất cả" },
  { key: "1", label: "Chờ duyệt" },
  { key: "2", label: "Đã duyệt" },
  { key: "3", label: "Từ chối" },
];

export default function HorseManagementPage() {
  const [horses, setHorses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [actingId, setActingId] = useState("");
  // Defaults to the Pending queue — this screen's whole purpose is to make Pending Horses (the
  // Admin's actual work) impossible to miss, per Task C1 UI correction.
  const [filter, setFilter] = useState("1");

  const loadHorses = useCallback(() => {
    setLoading(true);
    return Promise.all([
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

  useEffect(() => { loadHorses(); }, [loadHorses]);

  const getJockeyName = (h) => {
    const invs = h.jockeyInvitations || h.JockeyInvitations || [];
    const entries = h.raceEntries || h.RaceEntries || [];
    const active = invs.find(i => (i.status || i.Status) === 2 || (i.status || i.Status) === "Accepted");
    if (active) return active.jockey?.user?.fullName || active.Jockey?.User?.FullName;
    const lastEntry = [...entries].reverse().find(e => e.jockey || e.Jockey);
    return lastEntry?.jockey?.user?.fullName || lastEntry?.Jockey?.User?.FullName || "Chưa có";
  };

  // Counts derived directly from the loaded Horse list — never fabricated.
  const counts = useMemo(() => {
    const result = { all: horses.length, 1: 0, 2: 0, 3: 0 };
    horses.forEach(h => {
      const status = h.approvalStatus ?? h.ApprovalStatus;
      if (result[status] !== undefined) result[status] += 1;
    });
    return result;
  }, [horses]);

  const filtered = useMemo(() => {
    if (filter === "all") return horses;
    const target = Number(filter);
    return horses.filter(h => (h.approvalStatus ?? h.ApprovalStatus) === target);
  }, [horses, filter]);

  const handleApprove = async (h) => {
    const id = h.id || h.Id;
    const ownerUserId = h.owner?.userId ?? h.Owner?.UserId;
    if (!ownerUserId) { setMessage("Không tìm thấy chủ sở hữu của ngựa này."); return; }
    setActingId(id);
    setMessage("");
    try {
      await updateOwnerHorseStatus(ownerUserId, id, { status: "Approved" });
      setMessage(`${h.name || h.Name} đã được phê duyệt.`);
      await loadHorses();
    } catch (err) {
      setMessage(err.message || "Không thể phê duyệt ngựa.");
    } finally {
      setActingId("");
    }
  };

  const [rejectingHorse, setRejectingHorse] = useState(null);
  const [rejectNote, setRejectNote] = useState("");

  const handleReject = (h) => {
    setRejectingHorse(h);
    setRejectNote("");
  };

  const confirmRejectHorse = async () => {
    if (!rejectingHorse) return;
    const h = rejectingHorse;
    const id = h.id || h.Id;
    const ownerUserId = h.owner?.userId ?? h.Owner?.UserId;
    if (!ownerUserId) { setMessage("Không tìm thấy chủ sở hữu của ngựa này."); return; }
    const note = rejectNote.trim() || null;
    setActingId(id);
    setMessage("");
    try {
      await updateOwnerHorseStatus(ownerUserId, id, { status: "Rejected", note });
      setMessage(`${h.name || h.Name} đã bị từ chối.`);
      setRejectingHorse(null);
      await loadHorses();
    } catch (err) {
      setMessage(err.message || "Không thể từ chối ngựa.");
    } finally {
      setActingId("");
    }
  };

  if (loading && horses.length === 0) return <div style={{padding:40,textAlign:"center",color:"var(--hr-muted)"}}>Đang tải...</div>;

  return (
    <div style={{maxWidth:1200,margin:"0 auto",padding:"24px 32px"}}>
      <h1 style={{margin:"0 0 8px",fontSize:28,color:"var(--hr-paper)"}}>Quản lý ngựa</h1>
      <p style={{margin:"0 0 20px",color:"var(--hr-muted)",fontSize:14}}>Duyệt hồ sơ ngựa mới và theo dõi trạng thái lưu trữ.</p>

      {message && (
        <p style={{margin:"0 0 16px",padding:"10px 14px",borderRadius:8,background:"rgba(215,170,77,0.12)",color:"var(--hr-paper)",fontSize:13}}>{message}</p>
      )}

      <div style={{display:"flex",gap:8,flexWrap:"wrap",marginBottom:20}}>
        {filterTabs.map(tab => {
          const count = tab.key === "all" ? counts.all : counts[tab.key];
          const active = filter === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => setFilter(tab.key)}
              style={{
                padding:"8px 16px",borderRadius:999,border:active ? "1.5px solid var(--hr-gold, #d7aa4d)" : "1px solid var(--hr-border)",
                background:active ? "rgba(215,170,77,0.16)" : "transparent",
                color:active ? "var(--hr-paper)" : "var(--hr-muted)",
                fontSize:13,fontWeight:active ? 700 : 500,cursor:"pointer",
              }}
            >
              {tab.label} <span style={{opacity:0.75}}>({count})</span>
            </button>
          );
        })}
      </div>

      {filtered.length === 0 ? (
        <p style={{color:"var(--hr-muted)"}}>Không có ngựa nào ở trạng thái này.</p>
      ) : (
        <div style={{overflowX:"auto"}}>
        <table style={{width:"100%",borderCollapse:"collapse",fontSize:14}}>
          <thead><tr style={{borderBottom:"2px solid var(--hr-border)"}}>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Ngựa</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Giống</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Chủ sở hữu</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Email</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Kỵ sĩ</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Đang đua</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Duyệt hồ sơ</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Lưu trữ</th>
            <th style={{padding:"12px 16px",textAlign:"left",color:"var(--hr-muted)",fontSize:12,textTransform:"uppercase",letterSpacing:1}}>Hành động</th>
          </tr></thead>
          <tbody>
            {filtered.map(h => {
              const id = h.id || h.Id;
              const approvalStatus = h.approvalStatus ?? h.ApprovalStatus;
              const isArchived = h.isArchived ?? h.IsArchived ?? false;
              const isPending = approvalStatus === 1;
              const note = h.approvalNote ?? h.ApprovalNote;
              return (
                <tr key={id} style={{borderBottom:"1px solid var(--hr-border-soft)"}}>
                  <td style={{padding:"12px 16px",fontWeight:600,color:"var(--hr-paper)"}}>{h.name || h.Name}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)"}}>{h.breed || h.Breed || "-"}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)"}}>{h.owner?.user?.fullName || h.Owner?.User?.FullName || "-"}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-muted)"}}>{h.owner?.user?.email || h.Owner?.User?.Email || "-"}</td>
                  <td style={{padding:"12px 16px",color:"var(--hr-text)"}}>🏇 {getJockeyName(h)}</td>
                  <td style={{padding:"12px 16px"}}>
                    {h.isBusy ? (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(185,138,69,0.16)",color:"var(--hr-warning)"}}>Đang đua</span>
                    ) : (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(112,139,104,0.16)",color:"var(--hr-success)"}}>Sẵn sàng</span>
                    )}
                  </td>
                  <td style={{padding:"12px 16px"}}>
                    <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(148,163,184,0.16)",color:approvalStatusTone[approvalStatus] || "var(--hr-muted)"}}>
                      {approvalStatusMap[approvalStatus] || "Không xác định"}
                    </span>
                    {note && <div style={{marginTop:4,fontSize:11,color:"var(--hr-muted)"}}>{note}</div>}
                  </td>
                  <td style={{padding:"12px 16px"}}>
                    {isArchived ? (
                      <span style={{padding:"2px 10px",borderRadius:999,fontSize:11,fontWeight:700,background:"rgba(71,85,105,0.16)",color:"#475569"}}>Đã lưu trữ</span>
                    ) : (
                      <span style={{color:"var(--hr-muted)"}}>-</span>
                    )}
                  </td>
                  <td style={{padding:"12px 16px"}}>
                    {/* Task C1 UI correction §6: an archived Horse never exposes Approve/Reject, regardless of ApprovalStatus. */}
                    {isPending && !isArchived ? (
                      <div style={{display:"flex",gap:8}}>
                        <button
                          onClick={() => handleApprove(h)}
                          disabled={actingId === id}
                          style={{padding:"6px 12px",borderRadius:8,border:"none",background:"var(--hr-success, #708b68)",color:"#fff",fontSize:12,fontWeight:700,cursor:"pointer",opacity:actingId === id ? 0.6 : 1}}
                        >
                          Phê duyệt
                        </button>
                        <button
                          onClick={() => handleReject(h)}
                          disabled={actingId === id}
                          style={{padding:"6px 12px",borderRadius:8,border:"none",background:"#b91c1c",color:"#fff",fontSize:12,fontWeight:700,cursor:"pointer",opacity:actingId === id ? 0.6 : 1}}
                        >
                          Từ chối
                        </button>
                      </div>
                    ) : (
                      <span style={{color:"var(--hr-muted)"}}>-</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        </div>
      )}

      {rejectingHorse && (
        <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.65)", zIndex: 9999, display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
          <div style={{ width: "100%", maxWidth: 460, background: "var(--hr-surface, #1e293b)", border: "1px solid var(--hr-border, #334155)", borderRadius: 12, padding: 20, boxShadow: "0 20px 25px -5px rgba(0,0,0,0.5)" }}>
            <h3 style={{ margin: "0 0 12px", color: "var(--hr-paper, #f8fafc)", fontSize: 16 }}>❌ Từ Chối Phê Duyệt Ngựa Đua</h3>
            <p style={{ margin: "0 0 12px", fontSize: 13, color: "var(--hr-muted, #94a3b8)" }}>
              Nhập lý do từ chối chiến mã <strong>{rejectingHorse.name || rejectingHorse.Name}</strong> (không bắt buộc):
            </p>
            <textarea
              style={{ width: "100%", height: 90, padding: 10, borderRadius: 8, border: "1px solid var(--hr-border, #475569)", background: "var(--hr-bg-deep, #0f172a)", color: "#f8fafc", fontSize: 13, resize: "none" }}
              placeholder="Nhập ghi chú từ chối tại đây..."
              value={rejectNote}
              onChange={(e) => setRejectNote(e.target.value)}
            />
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button
                type="button"
                className="ghost-button"
                style={{ padding: "6px 14px", fontSize: 12 }}
                onClick={() => setRejectingHorse(null)}
              >
                Hủy
              </button>
              <button
                type="button"
                style={{ padding: "6px 14px", fontSize: 12, borderRadius: 6, border: "none", background: "#ef4444", color: "#fff", fontWeight: 700, cursor: "pointer" }}
                onClick={confirmRejectHorse}
                disabled={actingId !== ""}
              >
                {actingId ? "..." : "Xác nhận từ chối"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
