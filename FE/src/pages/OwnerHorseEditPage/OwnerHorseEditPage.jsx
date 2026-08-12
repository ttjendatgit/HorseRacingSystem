import { useState, useEffect, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getHorse, updateHorse } from "../../services/ownerHorseApi";
import { request, resolveApiUrl } from "../../services/apiClient";
import { validateHorseStats } from "../../utils/horseValidation";
const getFullUrl = (url) => resolveApiUrl(url);

function OwnerHorseEditPage() {
  const navigate = useNavigate();
  const { id } = useParams();
  const fileInputRef = useRef(null);
  const [formValues, setFormValues] = useState({
    name: "", breed: "", gender: "", dateOfBirth: "", age: "",
    weight: "", height: "", color: "", totalRaces: "", totalWins: "",
    imageUrl: "",
  });
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");

  const parseNumber = (value) => {
    if (!value) return undefined;
    const n = Number(value.toString().replace(/[^0-9.]/g, ""));
    return Number.isNaN(n) ? undefined : n;
  };

  useEffect(() => {
    (async () => {
      try {
        const horse = await getHorse(id);
        const img = horse.imageUrl ?? horse.ImageUrl ?? "";
        setFormValues({
          name: horse.name ?? "",
          breed: horse.breed ?? "",
          gender: horse.gender ?? "",
          dateOfBirth: horse.dateOfBirth ? horse.dateOfBirth.slice(0, 10) : "",
          age: horse.age != null ? String(horse.age) : "",
          weight: horse.weight != null ? String(horse.weight) : "",
          height: horse.height != null ? String(horse.height) : "",
          color: horse.color ?? "",
          totalRaces: horse.totalRaces != null ? String(horse.totalRaces) : "",
          totalWins: horse.totalWins != null ? String(horse.totalWins) : "",
          imageUrl: img,
        });
        if (img) setImagePreview(getFullUrl(img));
      } catch (e) { setError(e?.message || "Không thể tải ngựa."); }
      finally { setIsLoading(false); }
    })();
  }, [id]);

  const updateField = (field) => (event) => {
    const value = event.target.value;
    setFormValues((prev) => {
      const next = { ...prev, [field]: value };
      if (field === "dateOfBirth" && value) {
        const birth = new Date(value);
        const today = new Date();
        let age = today.getFullYear() - birth.getFullYear();
        const m = today.getMonth() - birth.getMonth();
        if (m < 0 || (m === 0 && today.getDate() < birth.getDate())) age--;
        next.age = age > 0 ? String(age) : "";
      }
      return next;
    });
  };

  const handleFileChange = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) { setError("Tối đa 5MB."); return; }
    setImageFile(file);
    setImagePreview(URL.createObjectURL(file));
    setError("");
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");
    const name = formValues.name.trim();
    if (!name) { setError("Tên ngựa là bắt buộc."); return; }

    const age = parseNumber(formValues.age) ?? 0;
    const v = validateHorseStats({ dateOfBirth: formValues.dateOfBirth, age, totalRaces: parseNumber(formValues.totalRaces) ?? 0, totalWins: parseNumber(formValues.totalWins) ?? 0 });
    if (v) { setError(v); return; }

    setIsSubmitting(true);
    try {
      let imageUrl = formValues.imageUrl || null;
      if (imageFile) {
        setUploading(true);
        try {
          const fd = new FormData();
          fd.append("file", imageFile);
          const data = await request("/api/horses/upload-image", { method: "POST", body: fd });
          imageUrl = data?.url || imageUrl;
          if (!imageUrl) { setError("Tải lên thất bại."); setIsSubmitting(false); return; }
        } catch (e) {
          setError("Tải lên thất bại: " + e.message);
          setIsSubmitting(false);
          return;
        } finally { setUploading(false); }
      }
      await updateHorse(id, {
        name,
        breed: formValues.breed.trim() || undefined,
        gender: formValues.gender.trim() || undefined,
        dateOfBirth: formValues.dateOfBirth || undefined,
        age,
        weight: parseNumber(formValues.weight),
        height: parseNumber(formValues.height),
        color: formValues.color.trim() || undefined,
        totalRaces: parseNumber(formValues.totalRaces) ?? 0,
        totalWins: parseNumber(formValues.totalWins) ?? 0,
        imageUrl: imageUrl || undefined,
      });
      navigate("/owner/horses");
    } catch (e) { setError(e?.message || "Không thể lưu."); }
    finally { setIsSubmitting(false); }
  };

  if (isLoading) return <div className="owner-page"><p className="muted">Đang tải...</p></div>;

  return (
    <div className="owner-page owner-horse-form-page">
      <div>
        <div className="owner-content">
          <section className="page-header"><h1>Chỉnh sửa ngựa</h1></section>
          <form className="horse-form-card" onSubmit={handleSubmit}>
            <div className="form-section">
              <h3>Ảnh ngựa</h3>
              <div style={{ display: "flex", gap: 20, alignItems: "start" }}>
                <div onClick={() => fileInputRef.current?.click()} style={{ width: 200, height: 160, border: "2px dashed rgba(231,198,120,.2)", borderRadius: 14, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", background: imagePreview ? `url(${imagePreview}) center/cover no-repeat` : "rgba(231,198,120,.04)", overflow: "hidden", flexShrink: 0 }}>
                  {!imagePreview && <span style={{ color: "#657086", fontSize: 13 }}>Nhấn để tải lên</span>}
                </div>
                <div>
                  <input ref={fileInputRef} type="file" accept="image/*" onChange={handleFileChange} style={{ display: "none" }} />
                  <p className="muted" style={{ fontSize: 13 }}>JPG, PNG, GIF, WEBP (tối đa 5MB)</p>
                  {imageFile && <p style={{ color: "#6ee7b7", fontSize: 13 }}>{imageFile.name}</p>}
                  <button type="button" className="ghost-button" onClick={() => { setImageFile(null); setImagePreview(formValues.imageUrl ? getFullUrl(formValues.imageUrl) : ""); if (fileInputRef.current) fileInputRef.current.value = ""; }} style={{ marginTop: 8 }}>Đặt lại</button>
                </div>
              </div>
            </div>

            <div className="form-section"><h3>Thông tin ngựa</h3>
              <div className="form-field">
                <label className="label-required">Tên ngựa</label>
                <input className="form-input" value={formValues.name} onChange={updateField("name")} required />
              </div>
              <div className="form-grid-two">
                <div className="form-field"><label>Giống</label><input className="form-input" placeholder="Thuần chủng" value={formValues.breed} onChange={updateField("breed")} /></div>
                <div className="form-field"><label>Giới tính</label>
  <select className="form-input" value={formValues.gender} onChange={updateField("gender")} style={{ appearance: "auto" }}>
    <option value="">-- Chọn --</option>
    <option value="Đực">Ngựa đực (Stallion)</option>
    <option value="Cái">Ngựa cái (Mare)</option>
    <option value="Gelding">Gelding (Ngựa thiến)</option>
  </select>
</div>
              </div>
              <div className="form-grid-two">
                <div className="form-field"><label>Màu sắc</label><input className="form-input" placeholder="Nâu" value={formValues.color} onChange={updateField("color")} /></div>
                <div className="form-field"><label>Ngày sinh</label><input className="form-input" type="date" value={formValues.dateOfBirth} onChange={updateField("dateOfBirth")} /></div>
              </div>
              <div className="form-grid-three">
                <div className="form-field"><label>Tuổi </label><input className="form-input" type="number" value={formValues.age} readOnly style={{ background: "rgba(231,198,120,.04)", cursor: "not-allowed" }} /></div>
                <div className="form-field"><label>Cân nặng (kg)</label><input className="form-input" placeholder="480" value={formValues.weight} onChange={updateField("weight")} /></div>
                <div className="form-field"><label>Chiều cao (cm)</label><input className="form-input" placeholder="165" value={formValues.height} onChange={updateField("height")} /></div>
              </div>
              <div className="form-section"><h3>Tổng sự nghiệp</h3>
                <div className="form-grid-two">
                  <div className="form-field"><label>Tổng số cuộc đua</label><input className="form-input" type="number" value={formValues.totalRaces} onChange={updateField("totalRaces")} min={0} /></div>
                  <div className="form-field"><label>Tổng số trận thắng</label><input className="form-input" type="number" value={formValues.totalWins} onChange={updateField("totalWins")} min={0} /></div>
                </div>
              </div>
            </div>

            {error && <p className="form-error">{error}</p>}
            <div className="form-actions">
              <button className="ghost-button" type="button" onClick={() => navigate("/owner/horses")}>Hủy</button>
              <button className="primary-button" type="submit" disabled={isSubmitting || uploading}>
                {uploading ? "Đang tải lên..." : isSubmitting ? "Đang lưu..." : "Lưu ngựa"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

export default OwnerHorseEditPage;
