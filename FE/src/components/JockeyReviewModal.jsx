import { useEffect, useState } from "react";
import { getJockeyAdminDetail, approveJockey, rejectJockey, setUserActive } from "../services/adminApi";
import { getJockeyApprovalDisplay } from "../utils/jockeyApproval";
import { isRejectReasonValid, formatJockeyReviewValue, getLicenseDocumentType } from "../utils/jockeyAdminReview";
import { apiToVNDate } from "../utils/vnDateTime";
import "./JockeyReviewModal.css";

// J-ADMIN-REVIEW UX polish: "Admin Jockey Verification Center" — a wide two-column review modal
// (personal info left, license document + approval state right) with an in-context document
// preview (image thumbnail / embedded PDF, both expandable to a lightbox) so Admin never has to
// leave the page just to inspect a document, and a sticky footer that anchors the review verdict
// as the conclusion of the review. Reuses the shared admin-modal-backdrop/-panel shell and J-UX's
// getJockeyApprovalDisplay for status copy — only this component's own layout is new.
//
// Footer action set is state-dependent and deliberately keeps two separate concerns apart:
//   Pending  -> [Từ chối] [Phê duyệt hồ sơ]      (ApprovalStatus: Pending -> Rejected/Approved)
//   Rejected -> [Đóng] [Phê duyệt lại]            (ApprovalStatus: Rejected -> Approved)
//   Approved + active   -> [Đóng] [Khóa Kỵ sĩ]    (User.IsActive: true -> false; ApprovalStatus untouched)
//   Approved + inactive -> [Đóng] [Mở khóa Kỵ sĩ] (User.IsActive: false -> true; ApprovalStatus untouched)
// Lock/unlock reuse the existing Admin User activate/deactivate endpoints (setUserActive) — never
// RejectJockeyAsync — because locking is an operational-access decision, not a re-review of the
// Jockey's profile verification.
function JockeyReviewModal({ jockeyId, onClose, onChanged }) {
  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [showRejectForm, setShowRejectForm] = useState(false);
  const [reason, setReason] = useState("");
  const [lightboxOpen, setLightboxOpen] = useState(false);

  const load = () => {
    setLoading(true);
    getJockeyAdminDetail(jockeyId)
      .then((d) => { setDetail(d); setError(""); })
      .catch((e) => setError(e.message || "Không thể tải hồ sơ kỵ sĩ."))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [jockeyId]);

  // Layered Escape: lightbox first, then the reject-reason form, then the modal itself — never
  // skips a level, so a hasty Escape can't silently discard an in-progress reject reason.
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key !== "Escape") return;
      if (lightboxOpen) { setLightboxOpen(false); return; }
      if (showRejectForm) { setShowRejectForm(false); return; }
      onClose?.();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [lightboxOpen, showRejectForm, onClose]);

  const approval = detail ? getJockeyApprovalDisplay(detail) : null;
  const docType = detail ? getLicenseDocumentType(detail.licenseFile) : "none";

  // MICRO-FIX: ApprovalStatus (profile verification) and User.IsActive (account/operational
  // access) are separate concerns. An Approved Jockey can still be locked out operationally —
  // that is never expressed as ApprovalStatus.Rejected, only as User.IsActive = false via the
  // existing Admin activate/deactivate User endpoints (reused as-is, no backend change).
  const isLocked = approval?.isApproved && detail?.isActive === false;
  const badgeTone = isLocked ? "pending" : approval?.tone;
  const badgeLabel = isLocked ? "Đã được phê duyệt · Đang bị khóa" : approval?.label;

  const handleApprove = async () => {
    const confirmText = approval?.isRejected
      ? "Xác nhận phê duyệt lại hồ sơ kỵ sĩ này?"
      : "Xác nhận phê duyệt hồ sơ kỵ sĩ này?";
    if (!window.confirm(confirmText)) return;
    setBusy(true);
    setError("");
    try {
      await approveJockey(detail.id);
      onChanged?.();
      load();
    } catch (e) {
      setError(e.message || "Phê duyệt thất bại.");
    } finally {
      setBusy(false);
    }
  };

  const handleReject = async () => {
    if (!isRejectReasonValid(reason)) return;
    setBusy(true);
    setError("");
    try {
      await rejectJockey(detail.id, reason.trim());
      setShowRejectForm(false);
      setReason("");
      onChanged?.();
      load();
    } catch (e) {
      setError(e.message || "Từ chối thất bại.");
    } finally {
      setBusy(false);
    }
  };

  const handleLock = async () => {
    if (!window.confirm("Xác nhận khóa kỵ sĩ này? Kỵ sĩ sẽ tạm thời không thể tham gia các hoạt động thi đấu.")) return;
    setBusy(true);
    setError("");
    try {
      await setUserActive(detail.userId, false);
      onChanged?.();
      load();
    } catch (e) {
      setError(e.message || "Khóa kỵ sĩ thất bại.");
    } finally {
      setBusy(false);
    }
  };

  const handleUnlock = async () => {
    setBusy(true);
    setError("");
    try {
      await setUserActive(detail.userId, true);
      onChanged?.();
      load();
    } catch (e) {
      setError(e.message || "Mở khóa kỵ sĩ thất bại.");
    } finally {
      setBusy(false);
    }
  };

  const docAltText = `Tài liệu giấy phép thi đấu của ${detail?.fullName || "kỵ sĩ"}`;

  return (
    <div className="admin-modal-backdrop" onClick={onClose}>
      <div
        className="admin-modal-panel jrm-panel"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="jrm-heading-name"
      >
        <div className="jrm-header">
          <div style={{ minWidth: 0 }}>
            <p className="jrm-header__eyebrow">Hồ sơ kỵ sĩ</p>
            <h2 id="jrm-heading-name" className="jrm-header__name">{detail?.fullName || (loading ? "Đang tải..." : "Kỵ sĩ chưa đặt tên")}</h2>
            {detail?.email && <span className="jrm-header__email">{detail.email}</span>}
            {detail?.createdAt && (
              <span className="jrm-header__email">Đăng ký: {apiToVNDate(detail.createdAt)}</span>
            )}
          </div>
          <div className="jrm-header__right">
            {approval && <span className={`status status--${badgeTone}`}>{badgeLabel}</span>}
            <button type="button" className="jrm-close" onClick={onClose} aria-label="Đóng hồ sơ kỵ sĩ">
              ✕
            </button>
          </div>
        </div>

        {loading && <p style={{ padding: "22px 28px", margin: 0, color: "var(--hr-muted)" }}>Đang tải hồ sơ...</p>}
        {error && <div className="jrm-inline-error" role="alert">{error}</div>}

        {!loading && detail && (
          <>
            <div className="jrm-body">
              <div className="jrm-col">
                <section>
                  <h3 className="jrm-section-title">Thông tin cá nhân</h3>
                  <div className="jrm-field-grid">
                    <div className="jrm-field">
                      <span className="jrm-field__label">Họ tên</span>
                      <strong className="jrm-field__value">{formatJockeyReviewValue(detail.fullName)}</strong>
                    </div>
                    <div className="jrm-field">
                      <span className="jrm-field__label">Điện thoại</span>
                      <strong className="jrm-field__value">{formatJockeyReviewValue(detail.phone)}</strong>
                    </div>
                    <div className="jrm-field">
                      <span className="jrm-field__label">Ngày sinh</span>
                      <strong className="jrm-field__value">{detail.dateOfBirth ? apiToVNDate(detail.dateOfBirth) : "—"}</strong>
                    </div>
                    <div className="jrm-field">
                      <span className="jrm-field__label">CCCD / CMND</span>
                      <strong className="jrm-field__value">{formatJockeyReviewValue(detail.idCardNumber)}</strong>
                    </div>
                    <div className="jrm-field jrm-field--full">
                      <span className="jrm-field__label">Email</span>
                      <strong className="jrm-field__value">{formatJockeyReviewValue(detail.email)}</strong>
                    </div>
                    <div className="jrm-field jrm-field--full">
                      <span className="jrm-field__label">Địa chỉ</span>
                      <strong className="jrm-field__value">{formatJockeyReviewValue(detail.address)}</strong>
                    </div>
                  </div>
                </section>

                <section>
                  <h3 className="jrm-section-title">Thể chất</h3>
                  <div className="jrm-field-grid">
                    <div className="jrm-field">
                      <span className="jrm-field__label">Chiều cao</span>
                      <strong className="jrm-field__value">{detail.height != null ? `${detail.height} cm` : "—"}</strong>
                    </div>
                    <div className="jrm-field">
                      <span className="jrm-field__label">Cân nặng</span>
                      <strong className="jrm-field__value">{detail.weight != null ? `${detail.weight} kg` : "—"}</strong>
                    </div>
                  </div>
                </section>
              </div>

              <div className="jrm-col">
                <section className="jrm-license-card">
                  <h3 className="jrm-section-title" style={{ margin: 0 }}>Giấy phép thi đấu</h3>
                  <div className="jrm-field">
                    <span className="jrm-field__label">Số giấy phép</span>
                    <strong className="jrm-field__value">{formatJockeyReviewValue(detail.licenseNumber)}</strong>
                  </div>

                  {docType === "image" && (
                    <button
                      type="button"
                      className="jrm-doc-preview jrm-doc-preview--clickable"
                      onClick={() => setLightboxOpen(true)}
                      aria-label="Phóng to tài liệu giấy phép"
                    >
                      <img src={detail.licenseFile} alt={docAltText} />
                      <span className="jrm-doc-preview__hint">Phóng to ⤢</span>
                    </button>
                  )}
                  {docType === "pdf" && (
                    <button
                      type="button"
                      className="jrm-doc-preview jrm-doc-preview--clickable"
                      onClick={() => setLightboxOpen(true)}
                      aria-label="Xem tài liệu PDF giấy phép ở chế độ phóng to"
                    >
                      <iframe src={detail.licenseFile} title={docAltText} />
                      <span className="jrm-doc-preview__hint">Xem tài liệu ⤢</span>
                    </button>
                  )}
                  {docType === "unknown" && (
                    <div className="jrm-doc-empty">Không thể xem trước định dạng tệp này — vui lòng mở bản gốc.</div>
                  )}
                  {docType === "none" && (
                    <div className="jrm-doc-empty">Chưa có tài liệu giấy phép</div>
                  )}

                  {detail.licenseFile && (
                    <a className="jrm-doc-link" href={detail.licenseFile} target="_blank" rel="noopener noreferrer">
                      Mở bản gốc ↗
                    </a>
                  )}
                </section>

                {approval?.isRejected && approval.note && (
                  <div className="jrm-reject-note">
                    <span className="jrm-reject-note__label">Lý do từ chối</span>
                    <p className="jrm-reject-note__text">{approval.note}</p>
                  </div>
                )}
              </div>
            </div>

            <div className={`jrm-footer${showRejectForm ? " jrm-footer--reject" : ""}`}>
              {showRejectForm ? (
                <div className="form-group">
                  <label htmlFor="jrm-reason">Lý do từ chối *</label>
                  <textarea
                    id="jrm-reason"
                    className="form-textarea"
                    rows={3}
                    placeholder="Nhập lý do từ chối hồ sơ kỵ sĩ này..."
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    autoFocus
                  />
                  <div className="jrm-footer__actions">
                    <button type="button" onClick={() => { setShowRejectForm(false); setReason(""); }} disabled={busy}>
                      Quay lại
                    </button>
                    <button
                      type="button"
                      className="admin-danger"
                      onClick={handleReject}
                      disabled={busy || !isRejectReasonValid(reason)}
                    >
                      {busy ? "Đang xử lý..." : "Xác nhận từ chối"}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="jrm-footer__actions">
                  {approval?.isPending && (
                    <>
                      <button type="button" className="admin-danger" onClick={() => setShowRejectForm(true)} disabled={busy}>
                        Từ chối
                      </button>
                      <button type="button" className="primary-button" onClick={handleApprove} disabled={busy}>
                        {busy ? "Đang xử lý..." : "Phê duyệt hồ sơ"}
                      </button>
                    </>
                  )}
                  {approval?.isRejected && (
                    <>
                      <button type="button" onClick={onClose} disabled={busy}>Đóng</button>
                      <button type="button" className="primary-button" onClick={handleApprove} disabled={busy}>
                        {busy ? "Đang xử lý..." : "Phê duyệt lại"}
                      </button>
                    </>
                  )}
                  {isLocked ? (
                    <>
                      <button type="button" onClick={onClose} disabled={busy}>Đóng</button>
                      <button type="button" className="primary-button" onClick={handleUnlock} disabled={busy}>
                        {busy ? "Đang xử lý..." : "Mở khóa Kỵ sĩ"}
                      </button>
                    </>
                  ) : approval?.isApproved && (
                    <>
                      <button type="button" onClick={onClose} disabled={busy}>Đóng</button>
                      <button type="button" className="admin-danger" onClick={handleLock} disabled={busy}>
                        {busy ? "Đang xử lý..." : "Khóa Kỵ sĩ"}
                      </button>
                    </>
                  )}
                </div>
              )}
            </div>
          </>
        )}

        {lightboxOpen && detail?.licenseFile && (
          <div
            className="jrm-lightbox"
            onClick={() => setLightboxOpen(false)}
            role="dialog"
            aria-modal="true"
            aria-label="Xem tài liệu giấy phép phóng to"
          >
            <div className="jrm-lightbox__content" onClick={(e) => e.stopPropagation()}>
              <button type="button" className="jrm-lightbox__close" onClick={() => setLightboxOpen(false)} aria-label="Đóng bản xem phóng to">
                ✕
              </button>
              {docType === "image" ? (
                <img src={detail.licenseFile} alt={`${docAltText} (phóng to)`} />
              ) : (
                <iframe src={detail.licenseFile} title={`${docAltText} (bản đầy đủ)`} />
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default JockeyReviewModal;
