import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getHorse } from "../../services/ownerHorseApi";
import { resolveApiUrl } from "../../services/apiClient";
import "../OwnerSharedLayout.css";
import "./OwnerHorseDetailPage.css";

const getHorseImageUrl = (imageUrl) => resolveApiUrl(imageUrl);

const invitationStatusLabels = {
  1: "Chờ duyệt",
  2: "Đã chấp nhận",
  3: "Từ chối",
};

const registrationStatusLabels = {
  1: "Chờ duyệt",
  2: "Đã duyệt",
  3: "Từ chối",
};

function OwnerHorseDetailPage() {
  const { id } = useParams();
  const [horse, setHorse] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  const approvalStatusMap = useMemo(
    () => ({
      1: "Chờ duyệt",
      2: "Đã duyệt",
      3: "Từ chối",
    }),
    [],
  );

  useEffect(() => {
    let isMounted = true;
    const fetchHorse = async () => {
      setIsLoading(true);
      setError("");
      try {
        const data = await getHorse(id);
        if (isMounted) {
          setHorse(data || null);
          if (!data) {
            setError("Không tìm thấy ngựa.");
          }
        }
      } catch (fetchError) {
        if (isMounted) {
          setError(fetchError?.message || "Không thể tải chi tiết ngựa.");
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    };

    fetchHorse();
    return () => {
      isMounted = false;
    };
  }, [id]);

  const statusLabel = horse
    ? (approvalStatusMap[horse.approvalStatus] ?? "Chờ duyệt")
    : "Chờ duyệt";

  const formattedDob = horse?.dateOfBirth
    ? new Date(horse.dateOfBirth).toLocaleDateString()
    : "-";
  const imageUrl = getHorseImageUrl(horse?.imageUrl);
  const winRate =
    horse?.totalRaces > 0
      ? Math.round(((horse.totalWins ?? 0) / horse.totalRaces) * 100)
      : 0;

  const primaryDetails = horse
    ? [
        { label: "Giống", value: horse.breed || "-" },
        { label: "Giới tính", value: horse.gender || "-" },
        { label: "Màu sắc", value: horse.color || "-" },
        { label: "Ngày sinh", value: formattedDob },
      ]
    : [];

  const metricDetails = horse
    ? [
        { label: "Tuổi", value: horse.age ?? "-" },
        { label: "Cân nặng (kg)", value: horse.weight ?? "-" },
        { label: "Chiều cao (cm)", value: horse.height ?? "-" },
      ]
    : [];

  const careerDetails = horse
    ? [
        { label: "Tổng số cuộc đua", value: horse.totalRaces ?? 0 },
        { label: "Tổng số trận thắng", value: horse.totalWins ?? 0 },
        { label: "Trạng thái duyệt", value: statusLabel },
      ]
    : [];

  // ---- Jockey Invitation helpers ----
  const getInvitationStatusLabel = (invitation) => {
    const status = invitation?.status ?? invitation?.Status;
    return typeof status === "number"
      ? invitationStatusLabels[status] ?? "Chờ duyệt"
      : status || "Chờ duyệt";
  };

  const getJockeyName = (invitation) =>
    invitation?.jockey?.user?.fullName ??
    invitation?.Jockey?.User?.FullName ??
    invitation?.jockey?.user?.FullName ??
    invitation?.Jockey?.user?.fullName ??
    "Kỵ sĩ đã chọn";

  const getInvitationDate = (invitation) => {
    const date = invitation?.createdAt ?? invitation?.CreatedAt;
    return date ? new Date(date).toLocaleDateString() : "-";
  };

  const jockeyInvitations = horse
    ? horse?.jockeyInvitations ?? horse?.JockeyInvitations ?? []
    : [];

  // ---- Race Entry helpers ----
  const getEntryStatusLabel = (entry) => {
    const status = entry?.status ?? entry?.Status;
    return typeof status === "number"
      ? registrationStatusLabels[status] ?? "Chờ duyệt"
      : status || "Chờ duyệt";
  };

  const getRaceName = (entry) =>
    entry?.race?.name ??
    entry?.Race?.Name ??
    "Cuộc đua không xác định";

  const getRaceDate = (entry) => {
    const date =
      entry?.race?.scheduledAt ??
      entry?.Race?.ScheduledAt;
    return date ? new Date(date).toLocaleDateString() : "-";
  };

  const getEntryGateNumber = (entry) =>
    entry?.gateNumber ?? entry?.GateNumber ?? "-";

  const raceEntries = horse
    ? horse?.raceEntries ?? horse?.RaceEntries ?? []
    : [];

  return (
    <div className="owner-page owner-horse-detail">
      <div><div className="owner-content">
          {error ? <p className="form-error">{error}</p> : null}

          {isLoading || !horse ? (
            <p className="muted">Đang tải chi tiết ngựa...</p>
          ) : (
            <section className="horse-banner" aria-label={`${horse.name} chi tiết`}>
              <div className="horse-banner__media">
                {imageUrl ? (
                  <img src={imageUrl} alt={horse.name} />
                ) : (
                  <div className="horse-banner__placeholder" aria-hidden="true">
                    {horse.name?.slice(0, 1) || "H"}
                  </div>
                )}
              </div>
              <div className="horse-banner__content">
                <div className="horse-banner__header">
                  <span className={`horse-detail-status horse-detail-status--${statusLabel.toLowerCase()}`}>
                    {statusLabel}
                  </span>
                  <Link className="secondary-button" to={`/owner/horses/${horse.id}/edit`}>
                    Chỉnh sửa hồ sơ
                  </Link>
                </div>
                <span className="pill">Chi tiết ngựa</span>
                <h1>{horse.name}</h1>
                <p>
                  {horse.breed || "Không rõ giống"}
                  {horse.color ? ` • ${horse.color}` : ""}
                  {horse.gender ? ` • ${horse.gender}` : ""}
                </p>
                <div className="horse-banner__meta">
                  <div>
                    <span>Tuổi</span>
                    <strong>{horse.age ?? "-"} tuổi</strong>
                  </div>
                  <div>
                    <span>Cân nặng</span>
                    <strong>{horse.weight ?? "-"} kg</strong>
                  </div>
                  <div>
                    <span>Chiều cao</span>
                    <strong>{horse.height ?? "-"} cm</strong>
                  </div>
                  <div>
                    <span>Ngày sinh</span>
                    <strong>{formattedDob}</strong>
                  </div>
                </div>
              </div>
            </section>
          )}

          <section className="stat-grid">
            <article className="stat-card">
              <span className="stat-card__label">01</span>
              <p className="muted">Tổng số trận thắng</p>
              <h3>{horse?.totalWins ?? 0}</h3>
              <span className="stat-trend">Tổng sự nghiệp</span>
            </article>
            <article className="stat-card">
              <span className="stat-card__label">02</span>
              <p className="muted">Tổng số cuộc đua</p>
              <h3>{horse?.totalRaces ?? 0}</h3>
              <span className="stat-trend">Tổng sự nghiệp</span>
            </article>
            <article className="stat-card">
              <span className="stat-card__label">03</span>
              <p className="muted">Tỷ lệ thắng</p>
              <h3>{winRate}%</h3>
              <span className="stat-trend">Hiệu suất đua</span>
            </article>
          </section>

          <section className="horse-detail-columns">
            <div className="horse-detail-stack">
              <div className="section-heading">
                <h2>Hồ sơ ngựa</h2>
                <p>Thông tin cốt lõi từ sổ đăng ký ngựa.</p>
              </div>
              <div className="horse-info-list">
                {primaryDetails.map((item) => (
                  <article key={item.label} className="horse-info-card">
                    <span>{item.label}</span>
                    <strong>{item.value}</strong>
                  </article>
                ))}
              </div>
            </div>

            <div className="horse-detail-stack">
              <div className="section-heading">
                <h2>Chỉ số thể chất</h2>
                <p>Số đo mới nhất được ghi nhận.</p>
              </div>
              <div className="horse-info-list">
                {metricDetails.map((item) => (
                  <article key={item.label} className="horse-info-card">
                    <span>{item.label}</span>
                    <strong>{item.value}</strong>
                  </article>
                ))}
              </div>

              <div className="section-heading">
                <h2>Tổng sự nghiệp</h2>
                <p>Số trận thắng và cuộc đua của ngựa này.</p>
              </div>
              <div className="horse-info-list">
                {careerDetails.map((item) => (
                  <article key={item.label} className="horse-info-card">
                    <span>{item.label}</span>
                    <strong>{item.value}</strong>
                  </article>
                ))}
              </div>
            </div>
          </section>

          {/* ---- Jockey Invitations ---- */}
          {horse ? (
            <section className="horse-detail-stack" style={{ marginTop: "24px" }}>
              <div className="section-heading">
                <h2>Lời mời kỵ sĩ</h2>
                <p>Các lời mời đã gửi đến kỵ sĩ cho ngựa này.</p>
              </div>
              {jockeyInvitations.length === 0 ? (
                <p className="muted">Chưa có lời mời kỵ sĩ nào.</p>
              ) : (
                <div className="horse-info-list">
                  {jockeyInvitations.map((invitation) => {
                    const invId = invitation?.id ?? invitation?.Id;
                    return (
                      <article key={invId} className="horse-invitation-card">
                        <div className="horse-invitation-card__row">
                          <span>Kỵ sĩ</span>
                          <strong>{getJockeyName(invitation)}</strong>
                        </div>
                        <div className="horse-invitation-card__row">
                          <span>Trạng thái</span>
                          <strong>{getInvitationStatusLabel(invitation)}</strong>
                        </div>
                        <div className="horse-invitation-card__row">
                          <span>Ngày gửi</span>
                          <strong>{getInvitationDate(invitation)}</strong>
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}
            </section>
          ) : null}

          {/* ---- Race Entries ---- */}
          {horse ? (
            <section className="horse-detail-stack" style={{ marginTop: "24px" }}>
              <div className="section-heading">
                <h2>Tham gia cuộc đua</h2>
                <p>Danh sách các cuộc đua ngựa này đã đăng ký.</p>
              </div>
              {raceEntries.length === 0 ? (
                <p className="muted">Chưa tham gia cuộc đua nào</p>
              ) : (
                <div className="horse-info-list">
                  {raceEntries.map((entry) => {
                    const entryId = entry?.id ?? entry?.Id;
                    return (
                      <article key={entryId} className="horse-entry-card">
                        <div className="horse-entry-card__row">
                          <span>Cuộc đua</span>
                          <strong>{getRaceName(entry)}</strong>
                        </div>
                        <div className="horse-entry-card__row">
                          <span>Ngày</span>
                          <strong>{getRaceDate(entry)}</strong>
                        </div>
                        <div className="horse-entry-card__row">
                          <span>Cổng</span>
                          <strong>{getEntryGateNumber(entry)}</strong>
                        </div>
                        <div className="horse-entry-card__row">
                          <span>Trạng thái</span>
                          <strong>{getEntryStatusLabel(entry)}</strong>
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}
            </section>
          ) : null}
        </div>
      </div>
    </div>
  );
}

export default OwnerHorseDetailPage;
