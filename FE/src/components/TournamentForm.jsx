import { useState } from "react";
import { createTournament } from "../services/adminApi";
import { request } from "../services/apiClient";
import { Input, Textarea, Button } from "./ui/Primitives";
import { vnInputToApiUtc, vnNowInput  } from "../utils/vnDateTime";

function TournamentForm({ onClose, onSuccess }) {
  const [submitting, setSubmitting] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    name: "",
    description: "",
    venue: "",
    category: "",
    startDate: "",
    endDate: "",
    prizePool: 0,
    imageUrl: "",
    registrationDeadline: "",
    minParticipants: 3,
    maxParticipants: 10,
    maxRounds: 1,
  });

  const updateForm = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleUpload = async (file) => {
    if (!file) return;
    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const res = await request("/api/auth/upload-document", { method: "POST", body: formData });
      const d = res?.data ?? res;
      updateForm("imageUrl", d?.url ?? "");
    } catch (e) {
      setError("Tải ảnh thất bại: " + (e.message ?? ""));
    }
    setUploading(false);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setError("");

    try {
      const payload = {
        name: form.name,
        description: form.description,
        venue: form.venue,
        category: form.category,
        startDate: vnInputToApiUtc(form.startDate),
        endDate: vnInputToApiUtc(form.endDate),
        prizePool: Number(form.prizePool),
        imageUrl: form.imageUrl || null,
        registrationDeadline: form.registrationDeadline ? vnInputToApiUtc(form.registrationDeadline) : null,
        minParticipants: Number(form.minParticipants),
        maxParticipants: Number(form.maxParticipants),
        maxRounds: Number(form.maxRounds),
      };

      await createTournament(payload);
      onSuccess();
    } catch (err) {
      setError(err.message || "Lỗi tạo giải đấu");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="admin-modal-backdrop" onClick={onClose}>
      <div className="admin-modal-panel" style={{maxWidth:600}} onClick={(e) => e.stopPropagation()}>
        <h2 style={{margin:"0 0 24px",fontSize:24}}>Tạo giải đấu</h2>
        {error && <div style={{padding:12,borderRadius:8,background:"rgba(201,105,90,0.16)",border:"1px solid rgba(201,105,90,0.35)",color:"var(--hr-danger)",fontSize:14,marginBottom:16}}>{error}</div>}
        <form onSubmit={handleSubmit}>
          <Input label="Tên giải đấu" value={form.name} onChange={(e) => updateForm("name", e.target.value)} placeholder="Giải vô địch quốc gia 2026" required />
          <Textarea label="Mô tả" value={form.description} onChange={(e) => updateForm("description", e.target.value)} placeholder="Mô tả ngắn về giải đấu..." rows={3} />
          <Input label="Địa điểm" value={form.venue} onChange={(e) => updateForm("venue", e.target.value)} placeholder="Hà Nội" />
          <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:16}}>
            <Input label="Thời gian bắt đầu *" type="datetime-local" min={vnNowInput()} value={form.startDate} onChange={(e) => updateForm("startDate", e.target.value)} required style={{colorScheme:"dark"}} />
            <Input label="Thời gian kết thúc *" type="datetime-local" min={vnNowInput()} value={form.endDate} onChange={(e) => updateForm("endDate", e.target.value)} required style={{colorScheme:"dark"}} />
          </div>
          <p style={{margin:"-12px 0 16px",fontSize:12,color:"var(--hr-muted)"}}>Giải đấu có thể bắt đầu và kết thúc trong cùng một ngày, miễn thời gian kết thúc sau thời gian bắt đầu.</p>
          <Input label="Hạn đăng ký ngựa *" type="datetime-local" min={vnNowInput()} value={form.registrationDeadline} onChange={(e) => updateForm("registrationDeadline", e.target.value)} required style={{colorScheme:"dark"}} hint="Thời điểm cuối cùng Chủ ngựa được gửi đăng ký tham gia giải." />
          <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:16}}>
            <Input label="Số người tham gia tối thiểu" type="number" value={form.minParticipants} onChange={(e) => updateForm("minParticipants", e.target.value)} min="3" required />
            <Input label="Số người tham gia tối đa" type="number" value={form.maxParticipants} onChange={(e) => updateForm("maxParticipants", e.target.value)} min="1" required />
          </div>
          {/* V0.1: MaxRounds must be explicit at create time — Round management identifies the
              Final Round as RoundNumber === Tournament.MaxRounds, so an omitted/silently-default
              value here (previously always 1) mislabels Round 1 as Final for any multi-round plan. */}
          <Input label="Số vòng đấu *" type="number" value={form.maxRounds} onChange={(e) => updateForm("maxRounds", e.target.value)} min="1" step="1" required hint="Vòng đấu cuối cùng (số thứ tự bằng Số vòng đấu) sẽ được coi là Vòng chung kết." />
          <Input label="Tổng tiền thưởng (VND)" type="number" value={form.prizePool} onChange={(e) => updateForm("prizePool", e.target.value)} placeholder="100000000" min="0" />
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"var(--hr-text)"}}>Ảnh đại diện</label>
            <input type="file" accept="image/*" onChange={(e) => handleUpload(e.target.files?.[0])} style={{display:"block",marginTop:4,color:"var(--hr-text)"}} />
            {uploading && <span style={{color:"var(--hr-gold-soft)",fontSize:12}}>Đang tải ảnh...</span>}
            {form.imageUrl && <img src={form.imageUrl} alt="preview" style={{width:120,borderRadius:8,marginTop:8}} />}
          </div>
          <div style={{display:"flex",gap:12,justifyContent:"flex-end",marginTop:24}}>
            <Button variant="secondary" onClick={onClose} type="button">Hủy</Button>
            <Button type="submit" disabled={submitting || uploading}>{submitting ? "Đang tạo..." : "Tạo"}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default TournamentForm;
