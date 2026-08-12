import { useState } from "react";
import { createTournament } from "../services/adminApi";
import { request } from "../services/apiClient";
import { Input, Textarea, Button } from "./ui/Primitives";
import { colors } from "../styles/designTokens";

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
        startDate: new Date(form.startDate).toISOString(),
        endDate: new Date(form.endDate).toISOString(),
        prizePool: Number(form.prizePool),
        imageUrl: form.imageUrl || null,
        registrationDeadline: form.registrationDeadline ? new Date(form.registrationDeadline).toISOString() : null,
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
    <div style={{position:"fixed",inset:0,background:"rgba(0,0,0,0.5)",display:"flex",alignItems:"center",justifyContent:"center",zIndex:1000,padding:20}}>
      <div style={{background:"#fff",borderRadius:16,maxWidth:600,width:"100%",maxHeight:"90vh",overflow:"auto",padding:32}}>
        <h2 style={{margin:"0 0 24px",fontSize:24,color:colors.ink}}>Tạo giải đấu</h2>
        {error && <div style={{padding:12,borderRadius:8,background:"rgba(239,68,68,0.1)",color:"#ef4444",fontSize:14,marginBottom:16}}>{error}</div>}
        <form onSubmit={handleSubmit}>
          <Input label="Tên giải đấu" value={form.name} onChange={(e) => updateForm("name", e.target.value)} placeholder="Giải vô địch quốc gia 2026" required />
          <Textarea label="Mô tả" value={form.description} onChange={(e) => updateForm("description", e.target.value)} placeholder="Mô tả ngắn về giải đấu..." rows={3} />
          <Input label="Địa điểm" value={form.venue} onChange={(e) => updateForm("venue", e.target.value)} placeholder="Hà Nội" />
          <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:16}}>
            <Input label="Ngày bắt đầu" type="date" value={form.startDate} onChange={(e) => updateForm("startDate", e.target.value)} required />
            <Input label="Ngày kết thúc" type="date" value={form.endDate} onChange={(e) => updateForm("endDate", e.target.value)} required />
          </div>
          <Input label="Hạn đăng ký" type="date" value={form.registrationDeadline} onChange={(e) => updateForm("registrationDeadline", e.target.value)} />
          <Input label="Tổng tiền thưởng (VND)" type="number" value={form.prizePool} onChange={(e) => updateForm("prizePool", e.target.value)} placeholder="100000000" min="0" />
          <div style={{marginBottom:16}}>
            <label style={{display:"block",fontSize:13,fontWeight:600,marginBottom:6,color:"#34415b"}}>Ảnh đại diện</label>
            <input type="file" accept="image/*" onChange={(e) => handleUpload(e.target.files?.[0])} style={{display:"block",marginTop:4}} />
            {uploading && <span style={{color:"#8f6420",fontSize:12}}>Đang tải ảnh...</span>}
            {form.imageUrl && <img src={form.imageUrl} alt="preview" style={{width:120,borderRadius:8,marginTop:8}} />}
          </div>
          <div style={{display:"flex",gap:12,justifyContent:"flex-end",marginTop:24}}>
            <Button variant="ghost" onClick={onClose} type="button">Hủy</Button>
            <Button type="submit" disabled={submitting || uploading}>{submitting ? "Đang tạo..." : "Tạo"}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default TournamentForm;
