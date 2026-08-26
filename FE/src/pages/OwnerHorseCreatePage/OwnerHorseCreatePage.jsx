import { useState, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { createHorse } from "../../services/ownerHorseApi";
import { request } from "../../services/apiClient";
import {
  hasHorseMeasurementErrors,
  preventInvalidIntegerKey,
  sanitizeDigitsOnly,
  validateHorseMeasurements,
  validateHorseStats,
} from "../../utils/horseValidation";

const digitFields = new Set(["weight", "height", "totalRaces", "totalWins"]);
const measurementFields = new Set(["weight", "height"]);

function OwnerHorseCreatePage() {
  const navigate = useNavigate();
  const fileInputRef = useRef(null);
  const [formValues, setFormValues] = useState({
    name: "",
    breed: "",
    gender: "",
    dateOfBirth: "",
    age: "",
    weight: "",
    height: "",
    color: "",
    totalRaces: "",
    totalWins: "",
  });
  const [fieldErrors, setFieldErrors] = useState({});
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState("");
  const [uploading, setUploading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState("");

  const parseNumber = (value) => {
    if (!value) return undefined;
    const n = Number(value);
    return Number.isNaN(n) ? undefined : n;
  };

  const updateField = (field) => (event) => {
    const rawValue = event.target.value;
    const value = digitFields.has(field) ? sanitizeDigitsOnly(rawValue) : rawValue;
    const nextValues = { ...formValues, [field]: value };

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

    if (measurementFields.has(field)) {
      setFieldErrors(validateHorseMeasurements(nextValues));
    }
  };

  const handleFileChange = (event) => {
    const file = event.target.files[0];
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      setError("Tối đa 5MB.");
      return;
    }
    setImageFile(file);
    setImagePreview(URL.createObjectURL(file));
    setError("");
  };

  const clearImage = () => {
    setImageFile(null);
    setImagePreview("");
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");

    const name = formValues.name.trim();
    if (!name) {
      setError("Tên ngựa là bắt buộc.");
      return;
    }

    const measurementErrors = validateHorseMeasurements(formValues);
    setFieldErrors(measurementErrors);
    if (hasHorseMeasurementErrors(measurementErrors)) {
      return;
    }

    const age = parseNumber(formValues.age) ?? 0;
    const validationError = validateHorseStats({
      dateOfBirth: formValues.dateOfBirth,
      age,
      totalRaces: parseNumber(formValues.totalRaces) ?? 0,
      totalWins: parseNumber(formValues.totalWins) ?? 0,
    });
    if (validationError) {
      setError(validationError);
      return;
    }

    setIsSubmitting(true);
    try {
      let imageUrl = null;
      if (imageFile) {
        setUploading(true);
        try {
          const formData = new FormData();
          formData.append("file", imageFile);
          const data = await request("/api/horses/upload-image", { method: "POST", body: formData });
          imageUrl = data?.url || null;
          if (!imageUrl) {
            setError("Tải lên thất bại: không có URL trả về.");
            setIsSubmitting(false);
            return;
          }
        } catch (uploadError) {
          setError("Tải lên thất bại: " + uploadError.message);
          setIsSubmitting(false);
          return;
        } finally {
          setUploading(false);
        }
      }

      await createHorse({
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
    } catch (submitError) {
      setError(submitError?.message || "Không thể lưu ngựa.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="owner-page owner-horse-form-page">
      <div>
        <div className="owner-content">
          <section className="page-header horse-form-header">
            <span className="pill">Hồ sơ ngựa</span>
            <h1>Tạo ngựa mới</h1>
            <p>Nhập thông tin cơ bản và chỉ số thể chất để gửi Admin duyệt.</p>
          </section>

          <form className="horse-form-card" onSubmit={handleSubmit} noValidate>
            <div className="horse-form-grid">
              <section className="form-section horse-form-panel">
                <div className="form-section__heading">
                  <span>01</span>
                  <h3>Ảnh ngựa</h3>
                </div>
                <button
                  type="button"
                  className={`image-upload-target${imagePreview ? " image-upload-target--has-image" : ""}`}
                  onClick={() => fileInputRef.current?.click()}
                  style={imagePreview ? { "--horse-image": `url(${imagePreview})` } : undefined}
                >
                  {!imagePreview ? (
                    <>
                      <strong>Tải ảnh lên</strong>
                      <small>JPG, PNG, GIF, WEBP · tối đa 5MB</small>
                    </>
                  ) : null}
                </button>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  onChange={handleFileChange}
                  style={{ display: "none" }}
                />
                <div className="image-upload-meta">
                  <p>{imageFile ? imageFile.name : "Ảnh rõ mặt bên giúp Admin kiểm tra nhanh hơn."}</p>
                  {imagePreview ? (
                    <button type="button" className="ghost-button" onClick={clearImage}>
                      Xóa ảnh
                    </button>
                  ) : null}
                </div>
              </section>

              <section className="form-section horse-form-panel">
                <div className="form-section__heading">
                  <span>02</span>
                  <h3>Thông tin ngựa</h3>
                </div>
                <div className="form-field">
                  <label className="label-required" htmlFor="horse-name">Tên ngựa</label>
                  <input
                    id="horse-name"
                    className="form-input"
                    type="text"
                    placeholder="Nhập tên ngựa"
                    value={formValues.name}
                    onChange={updateField("name")}
                    required
                  />
                </div>
                <div className="form-grid-two">
                  <div className="form-field">
                    <label htmlFor="horse-breed">Giống</label>
                    <input
                      id="horse-breed"
                      className="form-input"
                      type="text"
                      placeholder="Thuần chủng"
                      value={formValues.breed}
                      onChange={updateField("breed")}
                    />
                  </div>
                  <div className="form-field">
                    <label htmlFor="horse-gender">Giới tính</label>
                    <select
                      id="horse-gender"
                      className="form-input"
                      value={formValues.gender}
                      onChange={updateField("gender")}
                    >
                      <option value="">Chọn giới tính</option>
                      <option value="Đực">Ngựa đực (Stallion)</option>
                      <option value="Cái">Ngựa cái (Mare)</option>
                      <option value="Gelding">Gelding (Ngựa thiến)</option>
                    </select>
                  </div>
                </div>
                <div className="form-grid-two">
                  <div className="form-field">
                    <label htmlFor="horse-color">Màu sắc</label>
                    <input
                      id="horse-color"
                      className="form-input"
                      type="text"
                      placeholder="Nâu"
                      value={formValues.color}
                      onChange={updateField("color")}
                    />
                  </div>
                  <div className="form-field">
                    <label htmlFor="horse-dob">Ngày sinh</label>
                    <input
                      id="horse-dob"
                      className="form-input"
                      type="date"
                      value={formValues.dateOfBirth}
                      onChange={updateField("dateOfBirth")}
                    />
                  </div>
                </div>
                <div className="form-grid-three">
                  <div className="form-field">
                    <label htmlFor="horse-age">Tuổi</label>
                    <input
                      id="horse-age"
                      className="form-input form-input--readonly"
                      type="text"
                      value={formValues.age}
                      readOnly
                      placeholder="Tự tính"
                    />
                  </div>
                  <div className="form-field">
                    <label htmlFor="horse-weight">Cân nặng (kg)</label>
                    <input
                      id="horse-weight"
                      className={`form-input${fieldErrors.weight ? " form-input--invalid" : ""}`}
                      type="text"
                      inputMode="numeric"
                      pattern="[0-9]*"
                      placeholder="480"
                      value={formValues.weight}
                      onChange={updateField("weight")}
                      onKeyDown={preventInvalidIntegerKey}
                      aria-invalid={Boolean(fieldErrors.weight)}
                      aria-describedby={fieldErrors.weight ? "horse-weight-error" : undefined}
                    />
                    {fieldErrors.weight ? (
                      <p id="horse-weight-error" className="form-field-error">{fieldErrors.weight}</p>
                    ) : null}
                  </div>
                  <div className="form-field">
                    <label htmlFor="horse-height">Chiều cao (cm)</label>
                    <input
                      id="horse-height"
                      className={`form-input${fieldErrors.height ? " form-input--invalid" : ""}`}
                      type="text"
                      inputMode="numeric"
                      pattern="[0-9]*"
                      placeholder="165"
                      value={formValues.height}
                      onChange={updateField("height")}
                      onKeyDown={preventInvalidIntegerKey}
                      aria-invalid={Boolean(fieldErrors.height)}
                      aria-describedby={fieldErrors.height ? "horse-height-error" : undefined}
                    />
                    {fieldErrors.height ? (
                      <p id="horse-height-error" className="form-field-error">{fieldErrors.height}</p>
                    ) : null}
                  </div>
                </div>
              </section>
            </div>

            <section className="form-section horse-form-panel horse-form-panel--compact">
              <div className="form-section__heading">
                <span>03</span>
                <h3>Tổng sự nghiệp</h3>
              </div>
              <div className="form-grid-two">
                <div className="form-field">
                  <label htmlFor="horse-total-races">Tổng số cuộc đua</label>
                  <input
                    id="horse-total-races"
                    className="form-input"
                    type="text"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    placeholder="0"
                    value={formValues.totalRaces}
                    onChange={updateField("totalRaces")}
                    onKeyDown={preventInvalidIntegerKey}
                  />
                </div>
                <div className="form-field">
                  <label htmlFor="horse-total-wins">Tổng số trận thắng</label>
                  <input
                    id="horse-total-wins"
                    className="form-input"
                    type="text"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    placeholder="0"
                    value={formValues.totalWins}
                    onChange={updateField("totalWins")}
                    onKeyDown={preventInvalidIntegerKey}
                  />
                </div>
              </div>
            </section>

            {error ? <p className="form-error">{error}</p> : null}

            <div className="form-actions">
              <button className="ghost-button" type="button" onClick={() => navigate("/owner/horses")}>
                Hủy
              </button>
              <button className="primary-button" type="submit" disabled={isSubmitting || uploading}>
                {uploading ? "Đang tải lên..." : isSubmitting ? "Đang gửi..." : "Gửi hồ sơ duyệt"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

export default OwnerHorseCreatePage;
