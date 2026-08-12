import { useEffect, useState, useCallback } from "react";
import { updateProfile, changePassword, getProfile } from "../../services/authApi";
import { ProfileLayout, Field, Detail, msgBox, grid2, btnPrimary, btnSecondary } from "../ProfileCommon";
import "../ProfilePages.css";

const REF_TABS = [
  { key: "info", label: "Thông tin cá nhân" },
  { key: "password", label: "Mật khẩu & bảo mật" },
];

export default function RefereeProfilePage() {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("info");
  const [msg, setMsg] = useState(null);
  const [editMode, setEditMode] = useState(false);
  const [info, setInfo] = useState({ fullName: "", phoneNumber: "" });
  const [pw, setPw] = useState({ currentPassword: "", newPassword: "", confirmNewPassword: "" });

  const showMsg = useCallback((type, text) => { setMsg({ type, text }); setTimeout(() => setMsg(null), 4000); }, []);

  useEffect(() => {
    getProfile()
      .then((d) => { const p = d?.data ?? d; setProfile(p); setInfo({ fullName: p.fullName ?? p.FullName ?? "", phoneNumber: p.phoneNumber ?? p.PhoneNumber ?? "" }); })
      .catch(() => { /* empty */ })
      .finally(() => setLoading(false));
  }, []);

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

  if (loading) return <div className="spectator-page"><p>Đang tải...</p></div>;
  if (!profile) return <div className="spectator-page"><p>Không tìm thấy hồ sơ.</p></div>;

  return (
    <ProfileLayout profile={profile} roleLabel="Trọng tài" tabs={REF_TABS} activeTab={activeTab} setActiveTab={setActiveTab}>
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
              <Detail label="Giấy phép" value={profile.licenseNumber ?? profile.LicenseNumber ?? "-"} />
              <Detail label="Chuyên môn" value={profile.specialization ?? profile.Specialization ?? "-"} />
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
    </ProfileLayout>
  );
}
