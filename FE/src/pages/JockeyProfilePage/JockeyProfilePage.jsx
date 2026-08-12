import { useEffect, useState, useCallback } from "react";
import { updateProfile, changePassword, getProfile } from "../../services/authApi";
import { getContracts, signContractJockey, getProtests, createProtest } from "../../services/managementApi";
import { ProfileLayout, Field, Detail, msgBox, grid2, btnPrimary, btnSecondary } from "../ProfileCommon";
import "../ProfilePages.css";

const JOCKEY_TABS = [
  { key: "info", label: "Thông tin cá nhân" },
  { key: "password", label: "Mật khẩu & bảo mật" },
  { key: "contracts", label: "Hợp đồng" },
  { key: "protests", label: "Khiếu nại" },
];

const statusColor = (s) => {
  const str = String(s || "").toLowerCase();
  if (str.includes("active")) return { bg: "rgba(16,185,129,0.1)", color: "#0f7a5a" };
  if (str.includes("draft") || str.includes("pending")) return { bg: "rgba(245,158,11,0.1)", color: "#b8860b" };
  if (str.includes("expired") || str.includes("terminated") || str.includes("rejected")) return { bg: "rgba(239,68,68,0.1)", color: "#b91c1c" };
  if (str.includes("approved") || str.includes("upheld")) return { bg: "rgba(16,185,129,0.1)", color: "#0f7a5a" };
  return { bg: "rgba(100,116,139,0.1)", color: "#64748b" };
};

export default function JockeyProfilePage() {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("info");
  const [msg, setMsg] = useState(null);
  const [editMode, setEditMode] = useState(false);
  const [info, setInfo] = useState({ fullName: "", phoneNumber: "" });
  const [pw, setPw] = useState({ currentPassword: "", newPassword: "", confirmNewPassword: "" });
  const [contracts, setContracts] = useState([]);
  const [contractsLoading, setContractsLoading] = useState(false);
  const [protests, setProtests] = useState([]);
  const [protestsLoading, setProtestsLoading] = useState(false);
  const [showProtestForm, setShowProtestForm] = useState(false);
  const [protestForm, setProtestForm] = useState({ raceId: "", reason: "", evidence: "" });

  const showMsg = useCallback((type, text) => { setMsg({ type, text }); setTimeout(() => setMsg(null), 4000); }, []);

  useEffect(() => {
    getProfile()
      .then((d) => { const p = d?.data ?? d; setProfile(p); setInfo({ fullName: p.fullName ?? p.FullName ?? "", phoneNumber: p.phoneNumber ?? p.PhoneNumber ?? "" }); })
      .catch(() => { /* empty */ })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (activeTab !== "contracts") return;
    setContractsLoading(true);
    getContracts().then((d) => setContracts(Array.isArray(d) ? d : d?.data ?? [])).catch(() => { /* empty */ }).finally(() => setContractsLoading(false));
  }, [activeTab]);

  useEffect(() => {
    if (activeTab !== "protests") return;
    setProtestsLoading(true);
    getProtests().then((d) => setProtests(Array.isArray(d) ? d : d?.data ?? [])).catch(() => { /* empty */ }).finally(() => setProtestsLoading(false));
  }, [activeTab]);

  const saveInfo = async () => {
    try {
      const res = await updateProfile({ fullName: info.fullName, phoneNumber: info.phoneNumber });
      const d = res?.data ?? res;
      setProfile((prev) => ({ ...prev, ...d }));
      setEditMode(false);
      try { const stored = JSON.parse(localStorage.getItem("authUser") || "{}"); stored.fullName = info.fullName; localStorage.setItem("authUser", JSON.stringify(stored)); } catch { /* ok */ }
      showMsg("success", "Cập nhật hồ sơ thành công!");
    } catch (e) { showMsg("error", e?.message ?? "Cập nhật thất bại."); }
  };

  const savePassword = async () => {
    if (pw.newPassword !== pw.confirmNewPassword) { showMsg("error", "Mật khẩu mới không khớp."); return; }
    if (pw.newPassword.length < 8) { showMsg("error", "Mật khẩu phải có ít nhất 8 ký tự."); return; }
    try {
      await changePassword({ currentPassword: pw.currentPassword, newPassword: pw.newPassword, confirmNewPassword: pw.confirmNewPassword });
      setPw({ currentPassword: "", newPassword: "", confirmNewPassword: "" });
      showMsg("success", "Đổi mật khẩu thành công!");
    } catch (e) { showMsg("error", e?.message ?? "Đổi mật khẩu thất bại."); }
  };

  const signContract = async (id) => {
    try {
      await signContractJockey(id);
      showMsg("success", "Đã ký hợp đồng!");
      getContracts().then((d) => setContracts(Array.isArray(d) ? d : d?.data ?? []));
    } catch (e) { showMsg("error", e?.message ?? "Lỗi."); }
  };

  if (loading) return <div className="spectator-page"><p>Đang tải...</p></div>;
  if (!profile) return <div className="spectator-page"><p>Không tìm thấy hồ sơ.</p></div>;

  return (
    <ProfileLayout profile={profile} roleLabel="Kỵ sĩ" tabs={JOCKEY_TABS} activeTab={activeTab} setActiveTab={setActiveTab}>
      {msg && <div style={msgBox(msg.type)}>{msg.text}</div>}

      {activeTab === "info" && (
        <section className="sp-section">
          <div className="sp-section-header">
            <h2>Thông tin cá nhân</h2>
            {!editMode ? (
              <button style={btnSecondary} onClick={() => setEditMode(true)}>Chỉnh sửa</button>
            ) : (
              <div style={{ display: "flex", gap: 10 }}>
                <button style={btnSecondary} onClick={() => { setEditMode(false); setInfo({ fullName: profile.fullName ?? profile.FullName ?? "", phoneNumber: profile.phoneNumber ?? profile.PhoneNumber ?? "" }); }}>Huỷ</button>
                <button style={btnPrimary} onClick={saveInfo}>Lưu</button>
              </div>
            )}
          </div>
          <div className="sp-card">
            <Field label="Họ và tên" value={info.fullName} onChange={(e) => setInfo((p) => ({ ...p, fullName: e.target.value }))} readOnly={!editMode} placeholder="Nhập họ tên" />
            <Field label="Email" value={profile.email ?? profile.Email ?? ""} readOnly placeholder="Email" />
            <Field label="Số điện thoại" value={info.phoneNumber} onChange={(e) => setInfo((p) => ({ ...p, phoneNumber: e.target.value }))} readOnly={!editMode} placeholder="Nhập số điện thoại" />
          </div>
          {!editMode && (
            <div style={grid2}>
              <Detail label="Hạng" value={`#${profile.rank ?? profile.Rank ?? "-"}`} />
              <Detail label="Tỉ lệ thắng" value={`${profile.winRate ?? profile.WinRate ?? 0}%`} />
              <Detail label="Giấy phép" value={profile.licenseNumber ?? profile.LicenseNumber ?? "-"} />
              <Detail label="Quốc tịch" value={profile.nationality ?? profile.Nationality ?? "-"} />
              <Detail label="Ngày tham gia" value={profile.createdAt ? new Date(profile.createdAt).toLocaleDateString() : "-"} />
            </div>
          )}
        </section>
      )}

      {activeTab === "password" && (
        <section className="sp-section">
          <div className="sp-section-header"><h2>Mật khẩu & bảo mật</h2></div>
          <div className="sp-card">
            <Field label="Mật khẩu hiện tại" type="password" value={pw.currentPassword} onChange={(e) => setPw((p) => ({ ...p, currentPassword: e.target.value }))} placeholder="Nhập mật khẩu hiện tại" />
            <Field label="Mật khẩu mới" type="password" value={pw.newPassword} onChange={(e) => setPw((p) => ({ ...p, newPassword: e.target.value }))} placeholder="Nhập mật khẩu mới (ít nhất 8 ký tự)" />
            <Field label="Xác nhận mật khẩu mới" type="password" value={pw.confirmNewPassword} onChange={(e) => setPw((p) => ({ ...p, confirmNewPassword: e.target.value }))} placeholder="Nhập lại mật khẩu mới" />
            <div style={{ marginTop: 8 }}><button style={btnPrimary} onClick={savePassword}>Đổi mật khẩu</button></div>
          </div>
        </section>
      )}

      {activeTab === "contracts" && (
        <section className="sp-section">
          <div className="sp-section-header"><h2>Hợp đồng</h2></div>
          <div className="sp-card" style={{ overflowX: "auto" }}>
            {contractsLoading ? <p>Đang tải...</p> : contracts.length === 0 ? (
              <p className="muted" style={{ textAlign: "center", padding: "24px 0" }}>Chưa có hợp đồng nào.</p>
            ) : (
              <table className="sp-history-table">
                <thead><tr><th>Ngày</th><th>Chủ ngựa</th><th>Ngựa</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
                <tbody>
                  {contracts.map((c) => {
                    const id = c.id ?? c.Id;
                    const s = c.status ?? c.Status ?? "";
                    const colors = statusColor(s);
                    return (
                      <tr key={id}>
                        <td>{c.createdAt ?? c.CreatedAt ? new Date(c.createdAt ?? c.CreatedAt).toLocaleDateString() : "-"}</td>
                        <td>{c.ownerName ?? c.OwnerName ?? "-"}</td>
                        <td>{c.horseName ?? c.HorseName ?? "-"}</td>
                        <td><span style={{ display: "inline-block", padding: "2px 10px", borderRadius: 20, fontSize: 12, fontWeight: 600, ...colors }}>{s}</span></td>
                        <td>
                          {(String(s).toLowerCase().includes("draft") || String(s).toLowerCase().includes("pending")) && (
                            <button style={btnPrimary} onClick={() => signContract(id)}>Ký hợp đồng</button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </section>
      )}

      {activeTab === "protests" && (
        <section className="sp-section">
          <div className="sp-section-header">
            <h2>Khiếu nại</h2>
            <button style={btnSecondary} onClick={() => setShowProtestForm(!showProtestForm)}>{showProtestForm ? "Huỷ" : "Tạo khiếu nại"}</button>
          </div>
          {showProtestForm && (
            <div className="sp-card" style={{ marginBottom: 16 }}>
              <Field label="Race ID" value={protestForm.raceId} onChange={(e) => setProtestForm((p) => ({ ...p, raceId: e.target.value }))} placeholder="Nhập ID cuộc đua" />
              <Field label="Lý do" value={protestForm.reason} onChange={(e) => setProtestForm((p) => ({ ...p, reason: e.target.value }))} placeholder="Mô tả lý do khiếu nại" />
              <Field label="Bằng chứng" value={protestForm.evidence} onChange={(e) => setProtestForm((p) => ({ ...p, evidence: e.target.value }))} placeholder="Link / mô tả bằng chứng (không bắt buộc)" />
              <div style={{ marginTop: 8 }}>
                <button style={btnPrimary} onClick={async () => {
                  if (!protestForm.raceId || !protestForm.reason) { showMsg("error", "Vui lòng nhập Race ID và lý do."); return; }
                  try {
                    await createProtest({ raceId: protestForm.raceId, againstEntryId: null, reason: protestForm.reason, evidence: protestForm.evidence || null });
                    setShowProtestForm(false);
                    setProtestForm({ raceId: "", reason: "", evidence: "" });
                    showMsg("success", "Đã gửi khiếu nại!");
                    getProtests().then((d) => setProtests(Array.isArray(d) ? d : d?.data ?? []));
                  } catch (e) { showMsg("error", e?.message ?? "Gửi khiếu nại thất bại."); }
                }}>Gửi khiếu nại</button>
              </div>
            </div>
          )}
          <div className="sp-card" style={{ overflowX: "auto" }}>
            {protestsLoading ? <p>Đang tải...</p> : protests.length === 0 ? (
              <p className="muted" style={{ textAlign: "center", padding: "24px 0" }}>Chưa có khiếu nại nào.</p>
            ) : (
              <table className="sp-history-table">
                <thead><tr><th>Ngày</th><th>Lý do</th><th>Trạng thái</th></tr></thead>
                <tbody>
                  {protests.map((p) => {
                    const id = p.id ?? p.Id;
                    const s = p.status ?? p.Status ?? "";
                    const colors = statusColor(s);
                    return (
                      <tr key={id}>
                        <td>{p.createdAt ?? p.CreatedAt ? new Date(p.createdAt ?? p.CreatedAt).toLocaleDateString() : "-"}</td>
                        <td>{p.reason ?? p.Reason ?? "-"}</td>
                        <td><span style={{ display: "inline-block", padding: "2px 10px", borderRadius: 20, fontSize: 12, fontWeight: 600, ...colors }}>{s}</span></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </section>
      )}
    </ProfileLayout>
  );
}
