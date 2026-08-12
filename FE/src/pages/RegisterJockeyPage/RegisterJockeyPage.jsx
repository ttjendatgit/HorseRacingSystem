import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { register, uploadDocument } from "../../services/authApi";
import {
  normalizeApiRole,
  ROLE_ID_BY_VALUE,
  unwrapResponseData,
} from "../../services/authRoleUtils";
import "./RegisterJockeyPage.css";

function RegisterJockeyPage() {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [address, setAddress] = useState("");
  const [height, setHeight] = useState("");
  const [weight, setWeight] = useState("");
  const [idCardNumber, setIdCardNumber] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [licenseNumber, setLicenseNumber] = useState("");
  const [licenseFile, setLicenseFile] = useState("");
  const [agreed, setAgreed] = useState(false);

  const [uploadingLicense, setUploadingLicense] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [successMessage, setSuccessMessage] = useState("");
  const navigate = useNavigate();

  const handleFileUpload = async (file, setUrl, setUploading) => {
    if (!file) return;
    setUploading(true);
    setErrorMessage("");
    try {
      const response = await uploadDocument(file);
      const payload = unwrapResponseData(response);
      setUrl(payload?.url ?? "");
    } catch (error) {
      setErrorMessage(error.message || "Tải lên thất bại.");
    } finally {
      setUploading(false);
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setErrorMessage("");
    setSuccessMessage("");

    if (password !== confirmPassword) {
      setErrorMessage("Mật khẩu không khớp.");
      return;
    }

    if (!agreed) {
      setErrorMessage("Bạn phải xác nhận thông tin chính xác.");
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await register({
        email,
        password,
        role: ROLE_ID_BY_VALUE.jockey,
        fullName: fullName.trim() || null,
        phone: phone.trim() || null,
        address: address.trim() || null,
        dateOfBirth: dateOfBirth || null,
        height: height ? parseFloat(height) : null,
        weight: weight ? parseFloat(weight) : null,
        idCardNumber: idCardNumber.trim() || null,
        licenseNumber: licenseNumber.trim() || null,
        licenseFile: licenseFile || null,
      });
      const payload = unwrapResponseData(response);
      const apiRole = normalizeApiRole(payload?.role ?? payload?.Role);

      localStorage.setItem("authToken", payload?.token ?? "");
      localStorage.setItem("refreshToken", payload?.refreshToken ?? "");
      localStorage.setItem(
        "authUser",
        JSON.stringify({
          userId: payload?.userId,
          email: payload?.email,
          fullName: fullName.trim() || payload?.email,
          role: apiRole || "jockey",
        })
      );
      setSuccessMessage("Tài khoản Kỵ sĩ đã được tạo thành công.");
      navigate("/jockey/invitations");
    } catch (error) {
      setErrorMessage(
        error.message || "Đăng ký thất bại. Vui lòng thử lại."
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="auth-page register-role-page">
      <div className="register-layout">
        <div className="single-card single-card--wide">
          <form className="auth-form" onSubmit={handleSubmit}>
          {/* ── Thông tin cá nhân ── */}
          <fieldset className="form-fieldset">
            <legend className="form-legend">Thông tin cá nhân</legend>

            <div className="form-row">
              <div className="form-group">
                <label htmlFor="fullname" className="label-required">Họ và tên</label>
                <input id="fullname" type="text" name="name" autoComplete="name" placeholder="Nhập họ và tên của bạn" className="form-input" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
              </div>
              <div className="form-group">
                <label htmlFor="email" className="label-required">Email</label>
                <input id="email" type="email" name="email" autoComplete="email" placeholder="Nhập địa chỉ email" className="form-input" value={email} onChange={(e) => setEmail(e.target.value)} required />
              </div>
            </div>

            <div className="form-row">
              <div className="form-group">
                <label htmlFor="phone" className="label-required">Số điện thoại</label>
                <input id="phone" type="tel" name="phone" autoComplete="tel" placeholder="Nhập số điện thoại" className="form-input" value={phone} onChange={(e) => setPhone(e.target.value)} required />
              </div>
              <div className="form-group">
                <label htmlFor="dob" className="label-required">Ngày sinh</label>
                <input id="dob" type="date" name="dob" className="form-input" value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} required />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="address" className="label-required">Địa chỉ</label>
              <input id="address" type="text" name="address" autoComplete="street-address" placeholder="Nhập địa chỉ liên hệ" className="form-input" value={address} onChange={(e) => setAddress(e.target.value)} required />
            </div>

            <div className="form-row form-row--three">
              <div className="form-group">
                <label htmlFor="height" className="label-required">Chiều cao (cm)</label>
                <input id="height" type="number" step="0.1" min="100" max="250" placeholder="vd: 165.5" className="form-input" value={height} onChange={(e) => setHeight(e.target.value)} required />
              </div>
              <div className="form-group">
                <label htmlFor="weight" className="label-required">Cân nặng (kg)</label>
                <input id="weight" type="number" step="0.1" min="30" max="200" placeholder="vd: 55.0" className="form-input" value={weight} onChange={(e) => setWeight(e.target.value)} required />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="idCard" className="label-required">Số CCCD / CMND</label>
              <input id="idCard" type="text" name="idCard" placeholder="Nhập số CCCD/CMND của bạn" className="form-input" value={idCardNumber} onChange={(e) => setIdCardNumber(e.target.value)} required />
            </div>
          </fieldset>

          {/* ── Mật khẩu ── */}
          <fieldset className="form-fieldset">
            <legend className="form-legend">Bảo mật</legend>

            <div className="form-row">
              <div className="form-group">
                <label htmlFor="password" className="label-required">Mật khẩu</label>
                <input id="password" type="password" name="password" autoComplete="new-password" placeholder="Nhập mật khẩu" className="form-input" value={password} onChange={(e) => setPassword(e.target.value)} required />
              </div>
              <div className="form-group">
                <label htmlFor="confirm-password" className="label-required">Xác nhận mật khẩu</label>
                <input id="confirm-password" type="password" name="confirmPassword" autoComplete="new-password" placeholder="Xác nhận lại mật khẩu" className="form-input" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} required />
              </div>
            </div>
          </fieldset>

          {/* ── Thông tin giấy phép ── */}
          <fieldset className="form-fieldset">
            <legend className="form-legend">Thông tin giấy phép</legend>

            <div className="form-group">
              <label htmlFor="license" className="label-required">Số giấy phép thi đấu (License Number)</label>
              <input id="license" type="text" name="license" placeholder="Nhập số giấy phép thi đấu" className="form-input" value={licenseNumber} onChange={(e) => setLicenseNumber(e.target.value)} required />
            </div>

            <div className="form-group">
              <label className="label-required">Tải lên giấy phép thi đấu (PDF/JPG/PNG)</label>
              <div className="file-upload">
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,.pdf"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) handleFileUpload(file, setLicenseFile, setUploadingLicense);
                  }}
                  className="form-input form-input--file"
                />
                {uploadingLicense && <span className="file-upload__status">Đang tải lên...</span>}
                {licenseFile && !uploadingLicense && (
                  <span className="file-upload__status file-upload__status--success">Đã tải lên</span>
                )}
              </div>
            </div>
          </fieldset>

          {/* ── Xác nhận ── */}
          <div className="confirm-group">
            <div className="terms-agreement">
              <label>
                <input type="checkbox" required />
                <span>
                  Tôi đồng ý với{" "}
                  <a href="#" className="link-accent">Điều khoản dịch vụ</a>{" "}
                  và{" "}
                  <a href="#" className="link-accent">Chính sách bảo mật</a>{" "}
                  của RaceMaster.
                </span>
              </label>
            </div>

            <div className="commitment">
              <label>
                <input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} required />
                <span>Tôi cam kết các thông tin cung cấp là chính xác.</span>
              </label>
            </div>
          </div>

          {errorMessage && <p className="form-error">{errorMessage}</p>}
          {successMessage && <p className="form-success">{successMessage}</p>}

          <button type="submit" className="primary-button btn-block" disabled={isSubmitting}>
            {isSubmitting ? "Đang tạo..." : "Đăng ký Kỵ Sĩ"}
          </button>

          <p className="form-footer">
            Đã có tài khoản?{" "}
            <Link to="/login" className="link-accent">Đăng nhập</Link>
          </p>
        </form>
      </div>
      </div>

      <div className="login-footer">
        <div className="login-footer__inner">
          <div className="login-footer__links">
            <a href="#" aria-label="Facebook"><svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg></a>
            <a href="#" aria-label="Instagram"><svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.265.069 1.645.069 4.849 0 3.205-.012 3.584-.069 4.849-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zM12 0C8.741 0 8.333.014 7.053.072 2.695.272.273 2.69.073 7.052.014 8.333 0 8.741 0 12c0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98C8.333 23.986 8.741 24 12 24c3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98C15.668.014 15.259 0 12 0zm0 5.838a6.162 6.162 0 100 12.324 6.162 6.162 0 000-12.324zM12 16a4 4 0 110-8 4 4 0 010 8zm6.406-11.845a1.44 1.44 0 100 2.881 1.44 1.44 0 000-2.881z"/></svg></a>
            <a href="#" aria-label="YouTube"><svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M23.498 6.186a3.016 3.016 0 00-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 00.502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 002.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 002.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/></svg></a>
            <a href="#" aria-label="Discord"><svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189z"/></svg></a>
          </div>
          <div className="login-footer__copy">&copy; 2026 RaceMaster. All Rights Reserved.</div>
          <div className="login-footer__badge">SINCE 2026 &middot; GLOBAL RACING PLATFORM</div>
        </div>
      </div>
    </div>
  );
}

export default RegisterJockeyPage;
