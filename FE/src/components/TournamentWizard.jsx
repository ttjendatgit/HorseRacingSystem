import { useState, useEffect } from "react";
import { request } from "../services/apiClient";
import { createTournament } from "../services/adminApi";

const DRAFT_KEY = "tournament_form_draft";

function TournamentWizard({ onClose, onSuccess }) {
  const [step, setStep] = useState(1);
  const [uploading, setUploading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  function getDefaultForm() {
    const now = new Date();
    const startDate = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
    const endDate = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000);
    const deadline = new Date(now.getTime() + 5 * 24 * 60 * 60 * 1000);

    return {
      name: "",
      description: "",
      venue: "",
      category: "",
      imageUrl: "",
      startDate: startDate.toISOString().slice(0, 16),
      endDate: endDate.toISOString().slice(0, 16),
      registrationDeadline: deadline.toISOString().slice(0, 16),
      prizePool: 0,
      minParticipants: 3,
      maxParticipants: 10,
    };
  }

  const [form, setForm] = useState(() => {
    const draft = localStorage.getItem(DRAFT_KEY);
    if (draft) {
      try {
        return JSON.parse(draft);
      } catch {
        return getDefaultForm();
      }
    }
    return getDefaultForm();
  });

  // Auto-save draft every 5 seconds
  useEffect(() => {
    const timer = setInterval(() => {
      localStorage.setItem(DRAFT_KEY, JSON.stringify(form));
    }, 5000);
    return () => clearInterval(timer);
  }, [form]);

  const updateForm = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleUpload = async (file) => {
    if (!file) return;
    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const res = await request("/api/auth/upload-document", {
        method: "POST",
        body: formData,
      });
      const d = res?.data ?? res;
      updateForm("imageUrl", d?.url ?? "");
    } catch (e) {
      setError("Tải ảnh thất bại: " + (e.message ?? ""));
    }
    setUploading(false);
  };

  const validateStep1 = () => {
    if (!form.name || form.name.length < 3) {
      setError("Tên giải đấu phải có ít nhất 3 ký tự");
      return false;
    }
    if (form.name.length > 200) {
      setError("Tên giải đấu không được vượt quá 200 ký tự");
      return false;
    }
    if (form.description && form.description.length > 1000) {
      setError("Mô tả không được vượt quá 1000 ký tự");
      return false;
    }
    setError("");
    return true;
  };

  const validateStep2 = () => {
    if (!form.startDate || !form.endDate) {
      setError("Vui lòng chọn ngày bắt đầu và kết thúc");
      return false;
    }
    if (new Date(form.startDate) >= new Date(form.endDate)) {
      setError("Ngày kết thúc phải sau ngày bắt đầu");
      return false;
    }
    if (form.registrationDeadline && new Date(form.registrationDeadline) >= new Date(form.startDate)) {
      setError("Hạn đăng ký phải trước ngày bắt đầu");
      return false;
    }
    if (form.prizePool < 0) {
      setError("Tổng giải thưởng không được âm");
      return false;
    }
    if (!form.minParticipants || Number(form.minParticipants) < 3) {
      setError("Số người tham gia tối thiểu phải từ 3 trở lên");
      return false;
    }
    if (!form.maxParticipants || Number(form.maxParticipants) <= 0) {
      setError("Số người tham gia tối đa phải lớn hơn 0");
      return false;
    }
    if (Number(form.minParticipants) > Number(form.maxParticipants)) {
      setError("Số người tham gia tối thiểu không được lớn hơn số người tham gia tối đa");
      return false;
    }
    setError("");
    return true;
  };

  const nextStep = () => {
    if (step === 1 && !validateStep1()) return;
    if (step === 2 && !validateStep2()) return;
    setStep(step + 1);
  };

  const prevStep = () => {
    setError("");
    setStep(step - 1);
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    setError("");
    try {
      const payload = {
        name: form.name,
        description: form.description,
        venue: form.venue,
        category: form.category,
        imageUrl: form.imageUrl,
        startDate: new Date(form.startDate).toISOString(),
        endDate: new Date(form.endDate).toISOString(),
        registrationDeadline: form.registrationDeadline
          ? new Date(form.registrationDeadline).toISOString()
          : null,
        prizePool: form.prizePool,
        minParticipants: Number(form.minParticipants),
        maxParticipants: Number(form.maxParticipants),
      };
      await createTournament(payload);
      localStorage.removeItem(DRAFT_KEY);
      onSuccess();
    } catch (e) {
      setError(e.message ?? "Lỗi tạo giải đấu");
    }
    setSubmitting(false);
  };

  const daysDiff = () => {
    const start = new Date(form.startDate);
    const end = new Date(form.endDate);
    return Math.ceil((end - start) / (1000 * 60 * 60 * 24));
  };

  const daysUntilDeadline = () => {
    if (!form.registrationDeadline) return null;
    const now = new Date();
    const deadline = new Date(form.registrationDeadline);
    return Math.ceil((deadline - now) / (1000 * 60 * 60 * 24));
  };

  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)",
      display: "flex", alignItems: "center", justifyContent: "center",
      zIndex: 1000, padding: 20
    }}>
      <div style={{
        background: "#fff", borderRadius: 16, maxWidth: 700, width: "100%",
        maxHeight: "90vh", overflow: "auto", padding: 32
      }}>
        {/* Header */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
          <h2 style={{ margin: 0, fontSize: 24, color: "#172033" }}>Tạo Giải đấu mới</h2>
          <button onClick={onClose} style={{
            background: "none", border: "none", fontSize: 24, cursor: "pointer", color: "#657086"
          }}>×</button>
        </div>

        {/* Stepper */}
        <div style={{ display: "flex", gap: 8, marginBottom: 32 }}>
          {[1, 2, 3].map((s) => (
            <div key={s} style={{ flex: 1 }}>
              <div style={{
                height: 4, borderRadius: 2,
                background: step >= s ? "#8f6420" : "#e2e8f0",
                marginBottom: 8
              }} />
              <div style={{
                fontSize: 12, fontWeight: step === s ? 700 : 500,
                color: step === s ? "#8f6420" : "#657086"
              }}>
                {s === 1 ? "Thông tin" : s === 2 ? "Lịch trình" : "Xác nhận"}
              </div>
            </div>
          ))}
        </div>

        {/* Error */}
        {error && (
          <div style={{
            padding: 12, borderRadius: 8, background: "rgba(239,68,68,0.1)",
            color: "#ef4444", fontSize: 14, marginBottom: 16
          }}>
            {error}
          </div>
        )}

        {/* Step 1: Thông tin cơ bản */}
        {step === 1 && (
          <div>
            <h3 style={{ fontSize: 18, marginBottom: 16, color: "#172033" }}>Thông tin cơ bản</h3>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                Tên giải đấu *
              </label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => updateForm("name", e.target.value)}
                placeholder="VD: Grand Prix Hà Nội 2026"
                style={{
                  width: "100%", padding: 12, borderRadius: 8,
                  border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                }}
              />
              <p style={{ fontSize: 11, color: "#657086", marginTop: 4 }}>
                3-200 ký tự. Sẽ hiển thị trên trang công khai.
              </p>
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                Mô tả
              </label>
              <textarea
                value={form.description}
                onChange={(e) => updateForm("description", e.target.value)}
                placeholder="Mô tả về giải đấu..."
                rows={4}
                style={{
                  width: "100%", padding: 12, borderRadius: 8,
                  border: "1px solid rgba(143,100,32,0.2)", fontSize: 14,
                  resize: "vertical"
                }}
              />
              <p style={{ fontSize: 11, color: "#657086", marginTop: 4 }}>
                Tối đa 1000 ký tự.
              </p>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 16 }}>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                  Địa điểm
                </label>
                <input
                  type="text"
                  value={form.venue}
                  onChange={(e) => updateForm("venue", e.target.value)}
                  placeholder="VD: Hà Nội"
                  style={{
                    width: "100%", padding: 12, borderRadius: 8,
                    border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                  }}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                  Hạng giải
                </label>
                <select
                  value={form.category}
                  onChange={(e) => updateForm("category", e.target.value)}
                  style={{
                    width: "100%", padding: 12, borderRadius: 8,
                    border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                  }}
                >
                  <option value="">Chọn hạng giải</option>
                  <option value="Grade 1">Grade 1</option>
                  <option value="Grade 2">Grade 2</option>
                  <option value="Grade 3">Grade 3</option>
                  <option value="Listed">Listed</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                Ảnh đại diện
              </label>
              <input
                type="file"
                accept="image/*"
                onChange={(e) => handleUpload(e.target.files?.[0])}
                style={{ display: "block", marginTop: 4 }}
              />
              {uploading && <span style={{ color: "#8f6420", fontSize: 12 }}>Đang tải ảnh...</span>}
              {form.imageUrl && (
                <img
                  src={form.imageUrl}
                  alt="preview"
                  style={{ width: 120, borderRadius: 8, marginTop: 8 }}
                />
              )}
            </div>
          </div>
        )}

        {/* Step 2: Lịch trình */}
        {step === 2 && (
          <div>
            <h3 style={{ fontSize: 18, marginBottom: 16, color: "#172033" }}>Lịch trình</h3>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 16 }}>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                  Ngày bắt đầu *
                </label>
                <input
                  type="datetime-local"
                  value={form.startDate}
                  onChange={(e) => updateForm("startDate", e.target.value)}
                  style={{
                    width: "100%", padding: 12, borderRadius: 8,
                    border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                  }}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                  Ngày kết thúc *
                </label>
                <input
                  type="datetime-local"
                  value={form.endDate}
                  onChange={(e) => updateForm("endDate", e.target.value)}
                  style={{
                    width: "100%", padding: 12, borderRadius: 8,
                    border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                  }}
                />
              </div>
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                Hạn đăng ký
              </label>
              <input
                type="datetime-local"
                value={form.registrationDeadline}
                onChange={(e) => updateForm("registrationDeadline", e.target.value)}
                style={{
                  width: "100%", padding: 12, borderRadius: 8,
                  border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                }}
              />
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                Tổng giải thưởng (VNĐ)
              </label>
              <input
                type="number"
                value={form.prizePool}
                onChange={(e) => updateForm("prizePool", Number(e.target.value))}
                placeholder="500000000"
                style={{
                  width: "100%", padding: 12, borderRadius: 8,
                  border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                }}
              />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 16 }}>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                  Số người tham gia tối thiểu *
                </label>
                <input
                  type="number"
                  min={3}
                  value={form.minParticipants}
                  onChange={(e) => updateForm("minParticipants", e.target.value)}
                  style={{
                    width: "100%", padding: 12, borderRadius: 8,
                    border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                  }}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#34415b" }}>
                  Số người tham gia tối đa *
                </label>
                <input
                  type="number"
                  min={1}
                  value={form.maxParticipants}
                  onChange={(e) => updateForm("maxParticipants", e.target.value)}
                  style={{
                    width: "100%", padding: 12, borderRadius: 8,
                    border: "1px solid rgba(143,100,32,0.2)", fontSize: 14
                  }}
                />
              </div>
            </div>
          </div>
        )}

        {/* Step 3: Xác nhận */}
        {step === 3 && (
          <div>
            <h3 style={{ fontSize: 18, marginBottom: 16, color: "#172033" }}>Xác nhận thông tin</h3>

            <div style={{
              padding: 20, borderRadius: 12,
              border: "1px solid rgba(143,100,32,0.2)",
              background: "rgba(255,250,240,0.5)",
              marginBottom: 16
            }}>
              <div style={{ display: "flex", gap: 16, alignItems: "flex-start" }}>
                {form.imageUrl && (
                  <img
                    src={form.imageUrl}
                    alt="preview"
                    style={{ width: 100, height: 100, borderRadius: 8, objectFit: "cover" }}
                  />
                )}
                <div style={{ flex: 1 }}>
                  <span style={{
                    display: "inline-block", padding: "2px 10px", borderRadius: 999,
                    fontSize: 11, fontWeight: 700, background: "rgba(100,116,139,0.1)",
                    color: "#64748b", marginBottom: 8
                  }}>
                    Bản nháp
                  </span>
                  <h4 style={{ margin: "0 0 8px", fontSize: 18, color: "#172033" }}>
                    {form.name}
                  </h4>
                  <p style={{ margin: "0 0 8px", fontSize: 13, color: "#657086" }}>
                    {form.venue && `📍 ${form.venue} · `}
                    📅 {new Date(form.startDate).toLocaleDateString("vi-VN")} → {new Date(form.endDate).toLocaleDateString("vi-VN")}
                  </p>
                  <p style={{ margin: 0, fontSize: 13, color: "#657086" }}>
                    0 cuộc đua · 0 đăng ký · {form.prizePool.toLocaleString("vi-VN")} VNĐ
                  </p>
                </div>
              </div>
            </div>

            <div style={{ padding: 16, background: "rgba(143,100,32,0.05)", borderRadius: 8 }}>
              <h4 style={{ margin: "0 0 12px", fontSize: 14, color: "#172033" }}>Tóm tắt:</h4>
              <ul style={{ margin: 0, paddingLeft: 20, fontSize: 13, color: "#34415b" }}>
                <li>{daysDiff()} ngày diễn ra</li>
                {daysUntilDeadline() !== null && (
                  <li>Hạn đăng ký còn {daysUntilDeadline()} ngày</li>
                )}
                <li>Tổng giải thưởng: {form.prizePool.toLocaleString("vi-VN")} VNĐ</li>
              </ul>
            </div>
          </div>
        )}

        {/* Footer */}
        <div style={{
          display: "flex", justifyContent: "space-between", marginTop: 32,
          paddingTop: 16, borderTop: "1px solid rgba(143,100,32,0.1)"
        }}>
          <button
            onClick={step === 1 ? onClose : prevStep}
            disabled={submitting}
            style={{
              padding: "10px 20px", borderRadius: 8,
              border: "1px solid rgba(143,100,32,0.2)",
              background: "transparent", cursor: "pointer",
              fontSize: 14, color: "#34415b"
            }}
          >
            {step === 1 ? "Hủy" : "← Quay lại"}
          </button>

          {step < 3 ? (
            <button
              onClick={nextStep}
              style={{
                padding: "10px 20px", borderRadius: 8,
                border: "none", background: "#8f6420", color: "#fff",
                cursor: "pointer", fontSize: 14, fontWeight: 600
              }}
            >
              Tiếp theo →
            </button>
          ) : (
            <button
              onClick={handleSubmit}
              disabled={submitting}
              style={{
                padding: "10px 24px", borderRadius: 8,
                border: "none", background: submitting ? "#ccc" : "#8f6420",
                color: "#fff", cursor: submitting ? "not-allowed" : "pointer",
                fontSize: 14, fontWeight: 600
              }}
            >
              {submitting ? "Đang tạo..." : "🎉 Tạo giải"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

export default TournamentWizard;
