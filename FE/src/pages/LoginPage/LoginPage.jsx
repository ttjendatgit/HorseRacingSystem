import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { login, forgotPassword, resetPassword } from "../../services/authApi";
import {
  normalizeApiRole,
  unwrapResponseData,
} from "../../services/authRoleUtils";
import "./LoginPage.css";

function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [showForgot, setShowForgot] = useState(false);
  const [forgotEmail, setForgotEmail] = useState("");
  const [forgotSent, setForgotSent] = useState(false);
  const [resetToken, setResetToken] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isResetting, setIsResetting] = useState(false);
  const [resetSuccess, setResetSuccess] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (event) => {
    event.preventDefault();
    setErrorMessage("");

    if (!email.trim()) {
      setErrorMessage("Vui lòng nhập email.");
      return;
    }
    if (!password.trim()) {
      setErrorMessage("Vui lòng nhập mật khẩu.");
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await login({ email, password });
      const payload = unwrapResponseData(response);
      const apiRole = normalizeApiRole(payload?.role ?? payload?.Role);

      if (!apiRole) {
        const rawRole = JSON.stringify(payload?.role ?? payload?.Role);
        throw new Error(`Vai trò không hợp lệ từ máy chủ: ${rawRole}`);
      }

      const normalizedRole = apiRole;

      localStorage.setItem("authToken", payload?.token ?? "");
      localStorage.setItem("refreshToken", payload?.refreshToken ?? "");
      localStorage.setItem(
        "authUser",
        JSON.stringify({
          userId: payload?.userId,
          email: payload?.email,
          fullName: payload?.fullName ?? payload?.FullName,
          role: normalizedRole,
        }),
      );

      const ROLE_ROUTES = {
        spectator: "/spectator",
        jockey: "/jockey",
        horse_owner: "/owner",
        referee: "/referee",
        admin: "/admin",
      };

      navigate(ROLE_ROUTES[normalizedRole] ?? "/");
    } catch (error) {
      if (error.status === 401) {
        setErrorMessage("Email hoặc mật khẩu không đúng. Vui lòng thử lại.");
      } else if (error.status === 403) {
        setErrorMessage("Tài khoản của bạn đã bị vô hiệu hoá.");
      } else {
        setErrorMessage(error.message || "Đăng nhập thất bại. Vui lòng thử lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleForgotPassword = async (e) => {
    e.preventDefault();
    setErrorMessage("");
    setForgotSent(false);
    setResetSuccess(false);

    if (!forgotEmail.trim()) {
      setErrorMessage("Vui lòng nhập email.");
      return;
    }

    try {
      await forgotPassword(forgotEmail.trim());
      setForgotSent(true);
      setResetToken("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err) {
      if (err?.status === 429) {
        setErrorMessage("Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 1 phút.");
      } else if (err?.status === 0) {
        setErrorMessage("Không thể kết nối tới máy chủ. Vui lòng kiểm tra mạng hoặc thử lại sau.");
      } else {
        setErrorMessage(err?.message || "Có lỗi xảy ra.");
      }
    }
  };

  const handleResetPassword = async (e) => {
    e.preventDefault();
    setErrorMessage("");

    if (!forgotEmail.trim()) {
      setErrorMessage("Vui lòng nhập email.");
      return;
    }
    if (!resetToken.trim()) {
      setErrorMessage("Vui lòng nhập mã đặt lại mật khẩu.");
      return;
    }
    if (!newPassword.trim()) {
      setErrorMessage("Vui lòng nhập mật khẩu mới.");
      return;
    }
    if (newPassword.length < 8) {
      setErrorMessage("Mật khẩu mới phải có ít nhất 8 ký tự.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setErrorMessage("Xác nhận mật khẩu không khớp.");
      return;
    }

    setIsResetting(true);

    try {
      await resetPassword({
        email: forgotEmail.trim(),
        token: resetToken.trim(),
        newPassword,
        confirmPassword,
      });
      setResetSuccess(true);
      setResetToken("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err) {
      if (err?.status === 0) {
        setErrorMessage("Không thể kết nối tới máy chủ. Vui lòng kiểm tra mạng hoặc thử lại sau.");
      } else {
        setErrorMessage(err?.message || "Đặt lại mật khẩu thất bại. Vui lòng thử lại.");
      }
    } finally {
      setIsResetting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-left">
        <div className="login-left__inner">
          <div className="single-card">
            {!showForgot ? (
              <form className="auth-form" onSubmit={handleSubmit}>
                <h2>Chào mừng trở lại</h2>
                <p className="auth-form__subtitle">
                  Đăng nhập vào tài khoản RaceMaster để tiếp tục
                </p>

                <div className="form-group">
                  <label htmlFor="email" className="label-required">Email</label>
                  <input
                    id="email"
                    type="email"
                    placeholder="Địa chỉ Email"
                    autoComplete="email"
                    className="form-input"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="password" className="label-required">Mật khẩu</label>
                  <input
                    id="password"
                    type="password"
                    placeholder="Mật khẩu"
                    autoComplete="current-password"
                    className="form-input"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                  />
                  <div style={{ textAlign: "right", marginTop: 2 }}>
                    <button type="button" onClick={() => setShowForgot(true)} className="link-accent" style={{ background: "none", border: "none", cursor: "pointer", fontSize: 13, padding: 0 }}>
                      Quên mật khẩu?
                    </button>
                  </div>
                </div>

                {errorMessage && <p className="form-error">{errorMessage}</p>}

                <button type="submit" className="primary-button btn-block" disabled={isSubmitting}>
                  {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
                </button>

                <p className="form-footer">
                  Chưa có tài khoản?{" "}
                  <Link to="/register" className="link-accent">Đăng ký ngay</Link>
                </p>
              </form>
            ) : (
              <form className="auth-form" onSubmit={forgotSent ? handleResetPassword : handleForgotPassword}>
                <h2>Quên mật khẩu</h2>
                <p className="auth-form__subtitle">
                  {forgotSent
                    ? "Mã đặt lại mật khẩu đã được gửi đến email của bạn. Vui lòng nhập mã và thiết lập mật khẩu mới."
                    : "Nhập email của bạn, chúng tôi sẽ gửi mã đặt lại mật khẩu."}
                </p>

                <div className="form-group">
                  <label htmlFor="forgot-email" className="label-required">Email</label>
                  <input
                    id="forgot-email"
                    type="email"
                    placeholder="Địa chỉ Email"
                    className="form-input"
                    value={forgotEmail}
                    onChange={(e) => setForgotEmail(e.target.value)}
                    required
                  />
                </div>

                {forgotSent && (
                  <>
                    <div className="form-group">
                      <label htmlFor="reset-token" className="label-required">Mã đặt lại mật khẩu</label>
                      <input
                        id="reset-token"
                        type="text"
                        placeholder="Nhập mã từ email"
                        className="form-input"
                        value={resetToken}
                        onChange={(e) => setResetToken(e.target.value)}
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="new-password" className="label-required">Mật khẩu mới</label>
                      <input
                        id="new-password"
                        type="password"
                        placeholder="Nhập mật khẩu mới"
                        className="form-input"
                        value={newPassword}
                        onChange={(e) => setNewPassword(e.target.value)}
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="confirm-password" className="label-required">Xác nhận mật khẩu mới</label>
                      <input
                        id="confirm-password"
                        type="password"
                        placeholder="Nhập lại mật khẩu mới"
                        className="form-input"
                        value={confirmPassword}
                        onChange={(e) => setConfirmPassword(e.target.value)}
                        required
                      />
                    </div>
                  </>
                )}

                {forgotSent && (
                  <p style={{ padding: "10px 14px", borderRadius: 10, fontSize: 13, background: "rgba(120,216,154,0.12)", border: "1px solid rgba(120,216,154,0.3)", color: "#166534" }}>
                    Hướng dẫn đặt lại mật khẩu đã được gửi đến email của bạn. Nếu không thấy trong hộp thư đến, hãy kiểm tra thư mục Spam/Junk.
                  </p>
                )}

                {resetSuccess && (
                  <p style={{ padding: "10px 14px", borderRadius: 10, fontSize: 13, background: "rgba(120,216,154,0.12)", border: "1px solid rgba(120,216,154,0.3)", color: "#166534" }}>
                    Mật khẩu đã được cập nhật thành công. Bạn có thể đăng nhập lại ngay bây giờ.
                  </p>
                )}

                {errorMessage && <p className="form-error">{errorMessage}</p>}

                <button type="submit" className="primary-button btn-block" disabled={isResetting}>
                  {isResetting ? "Đang xử lý..." : forgotSent ? "Đặt lại mật khẩu" : "Gửi yêu cầu"}
                </button>

                {forgotSent && !resetSuccess && (
                  <button
                    type="button"
                    onClick={(e) => {
                      setResetToken("");
                      setNewPassword("");
                      setConfirmPassword("");
                      handleForgotPassword(e);
                    }}
                    className="link-accent"
                    style={{ background: "none", border: "none", cursor: "pointer", fontSize: 14, textAlign: "center", padding: 0, marginTop: 8 }}
                  >
                    Gửi lại mã cho {forgotEmail}
                  </button>
                )}

                <button type="button" onClick={() => { setShowForgot(false); setForgotSent(false); setResetToken(""); setNewPassword(""); setConfirmPassword(""); setErrorMessage(""); setResetSuccess(false); }} className="link-accent" style={{ background: "none", border: "none", cursor: "pointer", fontSize: 14, textAlign: "center", padding: 0, marginTop: 8 }}>
                  Quay lại đăng nhập
                </button>
              </form>
            )}
          </div>
        </div>
      </div>
      <div className="login-right">
        <div className="login-right__image" />
        <div className="login-right__overlay">
          <div className="login-right__content">
            <div className="login-right__sparkle">
              <svg width="40" height="40" viewBox="0 0 40 40" fill="none">
                <path d="M20 0l2.5 7.5L30 10l-7.5 2.5L20 20l-2.5-7.5L10 10l7.5-2.5L20 0z" fill="white" opacity="0.6"/>
                <path d="M8 24l1.5 4.5L14 30l-4.5 1.5L8 36l-1.5-4.5L2 30l4.5-1.5L8 24z" fill="white" opacity="0.4"/>
              </svg>
            </div>
            <p className="login-right__text">
              <strong>Experience<br />the thrill<br />of every race.</strong>
              Follow tournaments, track live results, explore horse rankings, and make your predictions — all in one modern platform.
            </p>
            <div className="login-right__tagline">
              <span>Since 2026</span>
              <span className="login-right__dot">&#9679;</span>
              <span>Trusted Horse Racing Platform</span>
            </div>
          </div>
        </div>
        <div className="login-right__beam" />
        <div className="login-right__sweep" />
      </div>
      <div className="login-footer">
        <div className="login-footer__inner">
          <div className="login-footer__links">
            <a href="#" aria-label="Facebook">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>
            </a>
            <a href="#" aria-label="Instagram">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.265.069 1.645.069 4.849 0 3.205-.012 3.584-.069 4.849-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zM12 0C8.741 0 8.333.014 7.053.072 2.695.272.273 2.69.073 7.052.014 8.333 0 8.741 0 12c0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98C8.333 23.986 8.741 24 12 24c3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98C15.668.014 15.259 0 12 0zm0 5.838a6.162 6.162 0 100 12.324 6.162 6.162 0 000-12.324zM12 16a4 4 0 110-8 4 4 0 010 8zm6.406-11.845a1.44 1.44 0 100 2.881 1.44 1.44 0 000-2.881z"/></svg>
            </a>
            <a href="#" aria-label="YouTube">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M23.498 6.186a3.016 3.016 0 00-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 00.502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 002.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 002.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/></svg>
            </a>
            <a href="#" aria-label="Discord">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="#657086"><path d="M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189z"/></svg>
            </a>
          </div>
          <div className="login-footer__copy">
            &copy; 2026 RaceMaster. All Rights Reserved.
          </div>
          <div className="login-footer__badge">SINCE 2026 &middot; GLOBAL RACING PLATFORM</div>
        </div>
      </div>
    </div>
  );
}

export default LoginPage;
