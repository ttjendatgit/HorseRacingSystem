import { useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  formatJockeyDate,
  getJockeyInvitations,
  normalizeInvitationStatus,
  respondJockeyInvitation,
  withdrawJockeyInvitation,
} from "../../services/jockeyApi";
import "../SpectatorSharedLayout.css";
import "./JockeyInvitationPage.css";

function DetailRow({ label, value }) {
  return (
    <div className="jockey-detail-row">
      <span>{label}</span>
      <strong>{value || "Chưa xác định"}</strong>
    </div>
  );
}

function JockeyInvitationDetailPage() {
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const [invitation, setInvitation] = useState(location.state?.invitation ?? null);
  const [loading, setLoading] = useState(!location.state?.invitation);
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState("");
  const [withdrawReason, setWithdrawReason] = useState("");

  useEffect(() => {
    if (invitation) return;
    let cancelled = false;

    const loadInvitation = async () => {
      try {
        setLoading(true);
        const data = await getJockeyInvitations();
        if (!cancelled) {
          setInvitation(data.find((item) => String(item.id) === String(id)) ?? null);
        }
      } catch (error) {
        if (!cancelled) {
          setMessage(error.message || "Không thể tải chi tiết lời mời.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadInvitation();
    return () => {
      cancelled = true;
    };
  }, [id, invitation]);

  const horseWinRate = useMemo(() => {
    const totalRaces = Number(invitation?.horseTotalRaces || 0);
    const totalWins = Number(invitation?.horseTotalWins || 0);
    return totalRaces > 0 ? `${Math.round((totalWins / totalRaces) * 100)}%` : "0%";
  }, [invitation]);

  const statusLabel = useMemo(() => {
    const status = normalizeInvitationStatus(invitation?.status);
    if (status === "Pending") return "Chờ phản hồi";
    if (status === "Accepted") return "Đã chấp nhận";
    if (status === "Declined") return "Đã từ chối";
    if (status === "Withdrawn") return "Đã xin rút";
    return status || "Chưa xác định";
  }, [invitation?.status]);

  const handleResponse = async (accept) => {
    setSubmitting(true);
    try {
      await respondJockeyInvitation(id, accept);
      navigate("/jockey/invitations", {
        state: {
          message: accept ? "Đã chấp nhận lời mời." : "Đã từ chối lời mời.",
          focusTab: accept ? "accepted" : "declined",
        },
      });
    } catch (error) {
      setMessage(error.message || "Không thể xử lý lời mời.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleWithdraw = async () => {
    if (withdrawReason.trim().length < 3) {
      setMessage("Lý do xin rút phải có ít nhất 3 ký tự.");
      return;
    }
    setSubmitting(true);
    try {
      await withdrawJockeyInvitation(id, withdrawReason.trim());
      setInvitation((current) => ({ ...current, status: "Withdrawn", responseNote: withdrawReason.trim() }));
      setMessage("Đã xin rút khỏi cuộc đua và thông báo cho chủ ngựa.");
    } catch (error) {
      setMessage(error.message || "Không thể xin rút khỏi cuộc đua.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="spectator-page jockey-invitation-detail">
      <div className="spectator-layout">
        <aside className="spectator-sidebar">
          <div className="spectator-sidebar__header">
            <p className="pill">Chi Tiết Lời Mời</p>
            <h3>Xem lại cuộc đua</h3>
            <p className="muted">Xác nhận cuộc đua và ngựa trước khi phản hồi.</p>
          </div>
          <div className="spectator-sidebar__card">
            <p className="muted">Trạng thái lời mời</p>
            <h4>{loading ? "Đang tải..." : statusLabel}</h4>
            <span>{formatJockeyDate(invitation?.createdAt, "Ngày tạo chưa xác định")}</span>
          </div>
          <Link className="jockey-back-link" to="/jockey/invitations">
            Quay lại lời mời
          </Link>
        </aside>

        <div className="spectator-content">
          <section className="jockey-page-header">
            <div>
              <span className="pill">Chi Tiết Lời Mời</span>
              <h1>{invitation?.raceName ?? "Lời mời đua"}</h1>
              <p>
                Xem thông tin cuộc đua và ngựa, sau đó chấp nhận hoặc từ chối
                lời mời này.
              </p>
              {message ? <p className="jockey-message">{message}</p> : null}
              {invitation?.message ? (
                <p className="jockey-message"><strong>Lời nhắn từ chủ ngựa:</strong> {invitation.message}</p>
              ) : null}
              {invitation?.responseNote && ["Declined", "Withdrawn"].includes(normalizeInvitationStatus(invitation.status)) ? (
                <p className="jockey-message"><strong>Lý do:</strong> {invitation.responseNote}</p>
              ) : null}
            </div>
          </section>

          {loading ? (
            <div className="jockey-loading-panel">
              <div className="skeleton-line wide" />
              <div className="skeleton-line" />
            </div>
          ) : !invitation ? (
            <div className="jockey-empty-state">
              <h3>Không tìm thấy lời mời</h3>
              <p className="muted">Lời mời có thể đã được chấp nhận hoặc từ chối.</p>
            </div>
          ) : (
            <>
              <section className="jockey-detail-grid">
                <article className="jockey-detail-panel">
                  <div className="section-heading">
                    <h2>Thông Tin Cuộc Đua</h2>
                    <p>Chi tiết cuộc đua trong lời mời này.</p>
                  </div>
                  <DetailRow label="Cuộc đua" value={invitation.raceName} />
                  <DetailRow label="Giải đấu" value={invitation.tournamentName} />
                  <DetailRow
                    label="Thời gian dự kiến"
                    value={formatJockeyDate(invitation.scheduledAt)}
                  />
                  <DetailRow label="Đường đua" value={invitation.location} />
                  <DetailRow
                    label="Cự ly"
                    value={invitation.distance ? `${invitation.distance}m` : ""}
                  />
                  <DetailRow
                    label="Số người tham gia tối đa"
                    value={invitation.maxParticipants}
                  />
                </article>

                <article className="jockey-detail-panel">
                  <div className="section-heading">
                    <h2>Thông Tin Ngựa</h2>
                    <p>Hồ sơ ngựa được phân công cho lời mời này.</p>
                  </div>
                  <DetailRow label="Ngựa" value={invitation.horseName} />
                  <DetailRow label="Chủ ngựa" value={invitation.ownerName} />
                  <DetailRow label="Giống" value={invitation.horseBreed} />
                  <DetailRow
                    label="Tuổi"
                    value={invitation.horseAge ? `${invitation.horseAge} tuổi` : ""}
                  />
                  <DetailRow
                    label="Cân nặng"
                    value={invitation.horseWeight ? `${invitation.horseWeight} kg` : ""}
                  />
                  <DetailRow label="Màu sắc" value={invitation.horseColor} />
                  <DetailRow label="Tỷ lệ thắng" value={horseWinRate} />
                </article>
              </section>

              <section className="jockey-response-panel">
                <div>
                  <h2>Phản hồi lời mời</h2>
                  <p className="muted">
                    Chấp nhận nghĩa là bạn đồng ý lời mời và sẵn sàng tham gia cuộc đua này.
                    Chủ ngựa sẽ chọn kỵ sĩ chính thức sau.
                  </p>
                </div>
                {normalizeInvitationStatus(invitation.status) === "Pending" ? (
                  <div className="jockey-response-panel__actions">
                    <button
                      type="button"
                      className="jockey-response-button jockey-response-button--decline"
                      disabled={submitting}
                      onClick={() => handleResponse(false)}
                    >
                      Từ chối lời mời
                    </button>
                    <button
                      type="button"
                      className="jockey-response-button jockey-response-button--accept"
                      disabled={submitting}
                      onClick={() => handleResponse(true)}
                    >
                      {submitting ? "Đang xử lý..." : "Chấp nhận lời mời"}
                    </button>
                  </div>
                ) : normalizeInvitationStatus(invitation.status) === "Accepted" ? (
                  <div className="jockey-withdraw-form">
                    <textarea
                      value={withdrawReason}
                      onChange={(event) => setWithdrawReason(event.target.value)}
                      maxLength={500}
                      placeholder="Nhập lý do xin rút khỏi cuộc đua..."
                      disabled={submitting}
                    />
                    <button
                      type="button"
                      className="jockey-response-button jockey-response-button--decline"
                      disabled={submitting || withdrawReason.trim().length < 3}
                      onClick={handleWithdraw}
                    >
                      {submitting ? "Đang xử lý..." : "Xác nhận xin rút"}
                    </button>
                  </div>
                ) : (
                  <span className="jockey-response-completed">
                    Lời mời này {statusLabel.toLowerCase()}.
                  </span>
                )}
              </section>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default JockeyInvitationDetailPage;
