import { Fragment, useEffect, useState, useCallback } from "react";
import { updateProfile, changePassword, getProfile, uploadDocument } from "../../services/authApi";
import {
  getContracts, signContractJockey,
  getMyRaceComplaints, createRaceComplaint, uploadRaceComplaintEvidence, withdrawRaceComplaint, getEligibleRaceComplaintRaces,
} from "../../services/managementApi";
import { getMyJockeyProfile, updateJockeyProfile } from "../../services/jockeyApi";
import { getJockeyApprovalDisplay } from "../../utils/jockeyApproval";
import { getJockeyDisplayStats } from "../../utils/jockeyStats";
import { validateJockeyRegistration } from "../../utils/jockeyRegistrationValidation";
import {
  EVIDENCE_ACCEPT_ATTR,
  RACE_COMPLAINT_TYPE_OPTIONS,
  canFilerWithdraw,
  getRaceComplaintStatusDetails,
  getRaceComplaintTypeLabel,
  mapEligibleRacesToOptions,
  validateEvidenceFile,
} from "../../utils/raceComplaintDisplay";
import ComplaintEvidenceGallery from "../../components/ComplaintEvidenceGallery";
import ComplaintEvidenceUploader from "../../components/ComplaintEvidenceUploader";
import { ProfileLayout, Field, Detail, msgBox, grid2, btnPrimary, btnSecondary, fieldStyle, fieldLabel, inputBase } from "../ProfileCommon";
import "../ProfilePages.css";

const JOCKEY_TABS = [
  { key: "info", label: "Thông tin cá nhân" },
  { key: "password", label: "Mật khẩu & bảo mật" },
  { key: "contracts", label: "Hợp đồng" },
  { key: "complaints", label: "Khiếu nại" },
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
  const [jockeyProfile, setJockeyProfile] = useState(null);
  const [approval, setApproval] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("info");
  const [msg, setMsg] = useState(null);
  const [editMode, setEditMode] = useState(false);
  const [info, setInfo] = useState({
    fullName: "",
    phoneNumber: "",
    address: "",
    dateOfBirth: "",
    height: "",
    weight: "",
    idCardNumber: "",
    licenseNumber: "",
    licenseFile: "",
  });
  const [uploadingLicense, setUploadingLicense] = useState(false);
  const [pw, setPw] = useState({ currentPassword: "", newPassword: "", confirmNewPassword: "" });
  const [contracts, setContracts] = useState([]);
  const [contractsLoading, setContractsLoading] = useState(false);
  const [complaints, setComplaints] = useState([]);
  const [complaintsLoading, setComplaintsLoading] = useState(false);
  const [showComplaintForm, setShowComplaintForm] = useState(false);
  const [eligibleRaceOptions, setEligibleRaceOptions] = useState([]);
  const [complaintForm, setComplaintForm] = useState({ raceId: "", type: "ResultJudging", reason: "", evidenceDescription: "" });
  const [complaintFiles, setComplaintFiles] = useState([]);
  const [expandedComplaintId, setExpandedComplaintId] = useState(null);

  const showMsg = useCallback((type, text) => { setMsg({ type, text }); setTimeout(() => setMsg(null), 4000); }, []);

  const handleFileUpload = async (file) => {
    if (!file) return;
    setUploadingLicense(true);
    try {
      const response = await uploadDocument(file);
      const payload = response?.data ?? response?.Data ?? response;
      setInfo((p) => ({ ...p, licenseFile: payload?.url ?? "" }));
      showMsg("success", "Đã tải lên tài liệu thành công.");
    } catch (error) {
      showMsg("error", error.message || "Tải lên tài liệu thất bại.");
    } finally {
      setUploadingLicense(false);
    }
  };

  const loadProfiles = useCallback(() => {
    setLoading(true);
    getProfile()
      .then((d) => {
        const p = d?.data ?? d;
        setProfile(p);
        setInfo((prev) => ({
          ...prev,
          fullName: p.fullName ?? p.FullName ?? "",
          phoneNumber: p.phoneNumber ?? p.PhoneNumber ?? "",
        }));
      })
      .catch(() => { /* empty */ })
      .finally(() => setLoading(false));

    getMyJockeyProfile()
      .then((jockeyProfileData) => {
        setJockeyProfile(jockeyProfileData);
        setApproval(getJockeyApprovalDisplay(jockeyProfileData));
        setInfo((prev) => ({
          ...prev,
          address: jockeyProfileData.address ?? jockeyProfileData.Address ?? "",
          dateOfBirth: jockeyProfileData.dateOfBirth ? jockeyProfileData.dateOfBirth.split("T")[0] : "",
          height: jockeyProfileData.height ?? jockeyProfileData.Height ?? "",
          weight: jockeyProfileData.weight ?? jockeyProfileData.Weight ?? "",
          idCardNumber: jockeyProfileData.idCardNumber ?? jockeyProfileData.IdCardNumber ?? "",
          licenseNumber: jockeyProfileData.licenseNumber ?? jockeyProfileData.LicenseNumber ?? "",
          licenseFile: jockeyProfileData.licenseFile ?? jockeyProfileData.LicenseFile ?? "",
        }));
      })
      .catch(() => setApproval(null));
  }, [showMsg]);

  useEffect(() => {
    loadProfiles();
  }, [loadProfiles]);

  useEffect(() => {
    if (activeTab !== "contracts") return;
    setContractsLoading(true);
    getContracts().then((d) => setContracts(Array.isArray(d) ? d : d?.data ?? [])).catch(() => { /* empty */ }).finally(() => setContractsLoading(false));
  }, [activeTab]);

  useEffect(() => {
    if (activeTab !== "complaints") return;
    setComplaintsLoading(true);
    Promise.all([
      getMyRaceComplaints(),
      getEligibleRaceComplaintRaces(),
    ])
      .then(([complaintData, raceData]) => {
        setComplaints(Array.isArray(complaintData) ? complaintData : complaintData?.data ?? []);
        setEligibleRaceOptions(mapEligibleRacesToOptions(Array.isArray(raceData) ? raceData : raceData?.data ?? []));
      })
      .catch(() => { /* empty */ })
      .finally(() => setComplaintsLoading(false));
  }, [activeTab]);

  const saveInfo = async () => {
    try {
      if (approval?.isRejected || approval?.isPending) {
        // Validation similar to registration
        const identityErrors = validateJockeyRegistration(
          { phone: info.phoneNumber.trim(), idCardNumber: info.idCardNumber.trim(), dateOfBirth: info.dateOfBirth },
          new Date()
        );
        const identityErrorMessages = Object.values(identityErrors);
        if (identityErrorMessages.length > 0) {
          showMsg("error", identityErrorMessages.join(" "));
          return;
        }

        await updateJockeyProfile({
          fullName: info.fullName.trim(),
          phone: info.phoneNumber.trim(),
          address: info.address.trim(),
          dateOfBirth: info.dateOfBirth || null,
          height: info.height ? parseFloat(info.height) : null,
          weight: info.weight ? parseFloat(info.weight) : null,
          idCardNumber: info.idCardNumber.trim(),
          licenseNumber: info.licenseNumber.trim(),
          licenseFile: info.licenseFile,
        });

        try {
          const stored = JSON.parse(localStorage.getItem("authUser") || "{}");
          stored.fullName = info.fullName.trim();
          localStorage.setItem("authUser", JSON.stringify(stored));
        } catch { /* ok */ }

        showMsg("success", "Cập nhật hồ sơ và gửi lại yêu cầu duyệt thành công!");
        setEditMode(false);
        loadProfiles();
      } else {
        const res = await updateProfile({ fullName: info.fullName, phoneNumber: info.phoneNumber });
        const d = res?.data ?? res;
        setProfile((prev) => ({ ...prev, ...d }));
        setEditMode(false);
        try { const stored = JSON.parse(localStorage.getItem("authUser") || "{}"); stored.fullName = info.fullName; localStorage.setItem("authUser", JSON.stringify(stored)); } catch { /* ok */ }
        showMsg("success", "Cập nhật hồ sơ thành công!");
      }
    } catch (e) {
      showMsg("error", e?.message ?? "Cập nhật thất bại.");
    }
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

  const withdraw = async (id) => {
    try {
      await withdrawRaceComplaint(id);
      showMsg("success", "Đã rút khiếu nại.");
      getMyRaceComplaints().then((d) => setComplaints(Array.isArray(d) ? d : d?.data ?? []));
    } catch (e) {
      showMsg("error", e?.message ?? "Rút khiếu nại thất bại.");
    }
  };

  if (loading) return <div className="spectator-page"><p>Đang tải...</p></div>;
  if (!profile) return <div className="spectator-page"><p>Không tìm thấy hồ sơ.</p></div>;
  const jockeyStats = getJockeyDisplayStats(jockeyProfile);

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
                <button style={btnSecondary} onClick={() => { setEditMode(false); loadProfiles(); }}>Huỷ</button>
                <button style={btnPrimary} onClick={saveInfo}>Lưu</button>
              </div>
            )}
          </div>
          <div className="sp-card">
            <Field label="Họ và tên" value={info.fullName} onChange={(e) => setInfo((p) => ({ ...p, fullName: e.target.value }))} readOnly={!editMode} placeholder="Nhập họ tên" />
            <Field label="Email" value={profile.email ?? profile.Email ?? ""} readOnly placeholder="Email" />
            <Field label="Số điện thoại" value={info.phoneNumber} onChange={(e) => setInfo((p) => ({ ...p, phoneNumber: e.target.value }))} readOnly={!editMode} placeholder="Nhập số điện thoại" />
            {editMode && (approval?.isRejected || approval?.isPending) && (
              <>
                <Field label="Địa chỉ" value={info.address} onChange={(e) => setInfo((p) => ({ ...p, address: e.target.value }))} placeholder="Nhập địa chỉ" />
                <div style={grid2}>
                  <Field label="Ngày sinh" type="date" value={info.dateOfBirth} onChange={(e) => setInfo((p) => ({ ...p, dateOfBirth: e.target.value }))} />
                  <Field label="Số CCCD/CMND" value={info.idCardNumber} onChange={(e) => setInfo((p) => ({ ...p, idCardNumber: e.target.value }))} placeholder="Nhập số CCCD/CMND" />
                </div>
                <div style={grid2}>
                  <Field label="Chiều cao (cm)" type="number" step="0.1" value={info.height} onChange={(e) => setInfo((p) => ({ ...p, height: e.target.value }))} placeholder="vd: 165.5" />
                  <Field label="Cân nặng (kg)" type="number" step="0.1" value={info.weight} onChange={(e) => setInfo((p) => ({ ...p, weight: e.target.value }))} placeholder="vd: 55.0" />
                </div>
                <Field label="Số giấy phép thi đấu" value={info.licenseNumber} onChange={(e) => setInfo((p) => ({ ...p, licenseNumber: e.target.value }))} placeholder="Nhập số giấy phép" />
                <div style={fieldStyle}>
                  <label style={fieldLabel}>Tải lên giấy phép thi đấu (PDF/JPG/PNG)</label>
                  <div className="file-upload" style={{ display: 'flex', alignItems: 'center', gap: '10px', marginTop: '6px' }}>
                    <input
                      type="file"
                      accept=".jpg,.jpeg,.png,.pdf"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (file) handleFileUpload(file);
                      }}
                      style={{ ...inputBase, width: 'auto' }}
                    />
                    {uploadingLicense && <span className="file-upload__status">Đang tải lên...</span>}
                    {info.licenseFile && !uploadingLicense && (
                      <span className="file-upload__status file-upload__status--success" style={{ color: 'var(--hr-success)' }}>Đã tải lên</span>
                    )}
                  </div>
                </div>
              </>
            )}
          </div>
          {!editMode && (
            <div style={grid2}>
              <Detail label="Trạng thái hồ sơ" value={approval?.label ?? "Đang tải..."} />
              <Detail label="Hạng" value={`#${jockeyStats.rank ?? "-"}`} />
              <Detail label="Tỉ lệ thắng" value={`${jockeyStats.winRate}%`} />
              <Detail label="Giấy phép" value={jockeyProfile?.licenseNumber ?? "-"} />
              <Detail label="Địa chỉ" value={jockeyProfile?.address ?? "-"} />
              <Detail label="Ngày sinh" value={jockeyProfile?.dateOfBirth ? new Date(jockeyProfile.dateOfBirth).toLocaleDateString("vi-VN") : "-"} />
              <Detail label="Chiều cao" value={jockeyProfile?.height ? `${jockeyProfile.height} cm` : "-"} />
              <Detail label="Cân nặng" value={jockeyProfile?.weight ? `${jockeyProfile.weight} kg` : "-"} />
              <Detail label="Số CCCD / CMND" value={jockeyProfile?.idCardNumber ?? "-"} />
              <Detail label="Ngày tham gia" value={profile.createdAt ? new Date(profile.createdAt).toLocaleDateString() : "-"} />
              {approval?.isRejected && approval.note && (
                <Detail label="Lý do từ chối" value={approval.note} />
              )}
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

      {activeTab === "complaints" && (
        <section className="sp-section">
          <div className="sp-section-header">
            <h2>Khiếu nại cuộc đua</h2>
            <button style={btnSecondary} onClick={() => setShowComplaintForm(!showComplaintForm)}>{showComplaintForm ? "Huỷ" : "Khiếu nại cuộc đua"}</button>
          </div>
          {showComplaintForm && (
            <div className="sp-card" style={{ marginBottom: 16 }}>
              <div style={fieldStyle}>
                <label style={fieldLabel}>Cuộc đua</label>
                <select
                  style={{ ...inputBase, appearance: "auto" }}
                  value={complaintForm.raceId}
                  onChange={(e) => setComplaintForm((p) => ({ ...p, raceId: e.target.value }))}
                >
                  <option value="">Chọn cuộc đua</option>
                  {eligibleRaceOptions.map((race) => (
                    <option key={race.value} value={race.value}>{race.label}</option>
                  ))}
                </select>
              </div>
              <div style={fieldStyle}>
                <label style={fieldLabel}>Loại khiếu nại</label>
                <select
                  style={{ ...inputBase, appearance: "auto" }}
                  value={complaintForm.type}
                  onChange={(e) => setComplaintForm((p) => ({ ...p, type: e.target.value }))}
                >
                  {RACE_COMPLAINT_TYPE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                  ))}
                </select>
              </div>
              <Field label="Nội dung" value={complaintForm.reason} onChange={(e) => setComplaintForm((p) => ({ ...p, reason: e.target.value }))} placeholder="Mô tả nội dung khiếu nại" />
              <Field label="Bằng chứng (tùy chọn)" value={complaintForm.evidenceDescription} onChange={(e) => setComplaintForm((p) => ({ ...p, evidenceDescription: e.target.value }))} placeholder="Mô tả bằng chứng liên quan (không bắt buộc)" />
              <div style={fieldStyle}>
                <label style={fieldLabel}>Ảnh / video bằng chứng (tùy chọn, có thể chọn nhiều)</label>
                <input
                  type="file"
                  multiple
                  accept={EVIDENCE_ACCEPT_ATTR}
                  onChange={(e) => {
                    const picked = Array.from(e.target.files || []);
                    const accepted = [];
                    for (const file of picked) {
                      const check = validateEvidenceFile(file);
                      if (check.valid) accepted.push(file);
                      else showMsg("error", check.error);
                    }
                    if (accepted.length > 0) setComplaintFiles((prev) => [...prev, ...accepted]);
                    e.target.value = "";
                  }}
                />
                {complaintFiles.length > 0 && (
                  <ul style={{ margin: "6px 0 0", padding: 0, listStyle: "none", display: "grid", gap: 2 }}>
                    {complaintFiles.map((file, index) => (
                      <li key={`${file.name}-${index}`} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: 12 }}>
                        <span>{file.name}</span>
                        <button type="button" style={btnSecondary} onClick={() => setComplaintFiles((prev) => prev.filter((_, i) => i !== index))}>Xóa</button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              <div style={{ marginTop: 8 }}>
                <button style={btnPrimary} onClick={async () => {
                  if (!complaintForm.raceId || !complaintForm.reason) { showMsg("error", "Vui lòng chọn cuộc đua và nhập nội dung."); return; }
                  try {
                    const res = await createRaceComplaint({
                      raceId: complaintForm.raceId,
                      type: complaintForm.type,
                      reason: complaintForm.reason,
                      evidenceDescription: complaintForm.evidenceDescription || null,
                    });
                    const created = res?.data ?? res;
                    for (const file of complaintFiles) {
                      try { await uploadRaceComplaintEvidence(created.id ?? created.Id, file); } catch { /* complaint already recorded */ }
                    }
                    setShowComplaintForm(false);
                    setComplaintForm({ raceId: "", type: "ResultJudging", reason: "", evidenceDescription: "" });
                    setComplaintFiles([]);
                    showMsg("success", "Đã gửi khiếu nại!");
                    getMyRaceComplaints().then((d) => setComplaints(Array.isArray(d) ? d : d?.data ?? []));
                  } catch (e) { showMsg("error", e?.message ?? "Gửi khiếu nại thất bại."); }
                }}>Gửi khiếu nại</button>
              </div>
            </div>
          )}
          <div className="sp-card" style={{ overflowX: "auto" }}>
            {complaintsLoading ? <p>Đang tải...</p> : complaints.length === 0 ? (
              <p className="muted" style={{ textAlign: "center", padding: "24px 0" }}>Chưa có khiếu nại nào.</p>
            ) : (
              <table className="sp-history-table">
                <thead><tr><th>Ngày</th><th>Loại</th><th>Nội dung</th><th>Trạng thái</th><th>Bằng chứng</th><th>Thao tác</th></tr></thead>
                <tbody>
                  {complaints.map((c) => {
                    const id = c.id ?? c.Id;
                    const s = c.status ?? c.Status ?? "";
                    const status = getRaceComplaintStatusDetails(s);
                    const colors = statusColor(status.status);
                    const withdrawable = canFilerWithdraw(status.status);
                    const evidenceList = c.evidence ?? c.Evidence ?? [];
                    const evidenceCount = evidenceList.length;
                    const filerEvidenceCount = evidenceList.filter((e) => (e.evidenceSource ?? e.EvidenceSource) === "Filer").length;
                    const expanded = expandedComplaintId === id;
                    return (
                      <Fragment key={id}>
                        <tr>
                          <td>{c.createdAt ?? c.CreatedAt ? new Date(c.createdAt ?? c.CreatedAt).toLocaleDateString() : "-"}</td>
                          <td>{getRaceComplaintTypeLabel(c.type ?? c.Type)}</td>
                          <td>{c.reason ?? c.Reason ?? "-"}</td>
                          <td><span style={{ display: "inline-block", padding: "2px 10px", borderRadius: 20, fontSize: 12, fontWeight: 600, ...colors }}>{status.label}</span></td>
                          <td>
                            <button style={btnSecondary} onClick={() => setExpandedComplaintId(expanded ? null : id)}>
                              {evidenceCount > 0 ? `Xem (${evidenceCount})` : "Thêm"}
                            </button>
                          </td>
                          <td>{withdrawable ? <button style={btnSecondary} onClick={() => withdraw(id)}>Rút</button> : "-"}</td>
                        </tr>
                        {expanded && (
                          <tr>
                            <td colSpan={6} style={{ background: "var(--hr-surface-2, rgba(0,0,0,0.02))", padding: "10px 14px" }}>
                              <ComplaintEvidenceGallery
                                evidence={evidenceList}
                                complaintId={id}
                                complaintStatus={s}
                                viewerRole="filer"
                                onDeleted={() => getMyRaceComplaints().then((d) => setComplaints(Array.isArray(d) ? d : d?.data ?? []))}
                              />
                              {withdrawable && (
                                <div style={{ marginTop: 8 }}>
                                  <ComplaintEvidenceUploader
                                    complaintId={id}
                                    currentCount={filerEvidenceCount}
                                    onUploaded={() => getMyRaceComplaints().then((d) => setComplaints(Array.isArray(d) ? d : d?.data ?? []))}
                                  />
                                </div>
                              )}
                            </td>
                          </tr>
                        )}
                      </Fragment>
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
