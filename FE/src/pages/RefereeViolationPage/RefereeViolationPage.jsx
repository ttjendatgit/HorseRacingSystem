import { useState, useEffect, useMemo } from "react";
import {
  getRaceEntries,
  getRaceViolations,
  recordViolation,
} from "../../services/refereeApi";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import "./RefereeViolationPage.css";

const VIOLATION_TYPES = [
  { value: 1, label: "Hành vi nguy hiểm", key: "dangerousbehavior" },
  { value: 2, label: "Xuất phát sai", key: "falsestart" },
  { value: 3, label: "Can thiệp", key: "interference" },
  { value: 4, label: "Phúc lợi động vật", key: "animalwelfare" },
  { value: 5, label: "Vi phạm thiết bị", key: "equipmentviolation" },
  { value: 6, label: "Khác", key: "other" },
];

const TYPE_LABELS = {
  1: "Hành vi nguy hiểm",
  2: "Xuất phát sai",
  3: "Can thiệp",
  4: "Phúc lợi động vật",
  5: "Vi phạm thiết bị",
  6: "Khác",
};

const STATUS_MAP = {
  Pending: "Chờ xử lý",
  Resolved: "Đã xử lý",
};

function getBadgeKey(typeValue) {
  const found = VIOLATION_TYPES.find((t) => t.value === typeValue);
  return found ? found.key : "other";
}

/* ── SVG: Referee flag + whistle (empty state) ── */
function FlagWhistleSVG() {
  return (
    <svg className="rv-empty-svg" viewBox="0 0 240 150" fill="none" xmlns="http://www.w3.org/2000/svg">
      {/* Flag pole */}
      <line x1="80" y1="28" x2="80" y2="120" stroke="currentColor" strokeWidth="2" opacity="0.2" strokeLinecap="round" />
      {/* Flag */}
      <path d="M82 32 L160 48 L82 64 Z" fill="var(--rh-gold)" fillOpacity="0.25" stroke="var(--rh-gold)" strokeWidth="0.8" strokeOpacity="0.3" strokeLinejoin="round" />
      {/* Flag wave lines */}
      <path d="M155 50 C165 52 170 48 175 50" stroke="var(--rh-gold)" strokeWidth="1" opacity="0.2" fill="none" strokeLinecap="round" />
      {/* Whistle */}
      <g opacity="0.15">
        <rect x="170" y="82" width="28" height="10" rx="4" stroke="currentColor" strokeWidth="1.2" fill="none" />
        <circle cx="198" cy="87" r="6" stroke="currentColor" strokeWidth="1.2" fill="none" />
        <line x1="170" y1="87" x2="160" y2="85" stroke="currentColor" strokeWidth="1" />
      </g>
      <text x="120" y="140" textAnchor="middle" fill="var(--rh-muted)" fontSize="12" fontFamily="sans-serif" opacity="0.7">
        Chọn cuộc đua để ghi nhận vi phạm
      </text>
    </svg>
  );
}

/* ── SVG: Empty history ── */
function EmptyViolationSVG() {
  return (
    <svg className="rv-empty-list-svg" viewBox="0 0 120 80" fill="none" xmlns="http://www.w3.org/2000/svg">
      {/* Shield with check */}
      <path d="M60 10 L90 22 L90 44 C90 60 60 74 60 74 C60 74 30 60 30 44 L30 22 Z" stroke="currentColor" strokeWidth="1" opacity="0.2" fill="currentColor" fillOpacity="0.03" strokeLinejoin="round" />
      {/* Checkmark inside */}
      <path d="M50 42 L56 50 L70 34" stroke="var(--rh-green)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" opacity="0.6" />
      <text x="60" y="90" textAnchor="middle" fill="var(--rh-muted)" fontSize="9" fontFamily="sans-serif" opacity="0.5">Chưa có vi phạm</text>
    </svg>
  );
}

/* ── SVG: Prompt when form is hidden ── */
function ViolationPromptSVG() {
  return (
    <svg viewBox="0 0 200 100" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ width: 160, height: 80, color: "var(--rh-muted)" }}>
      {/* Flag icon */}
      <line x1="40" y1="20" x2="40" y2="80" stroke="currentColor" strokeWidth="1" opacity="0.2" strokeLinecap="round" />
      <path d="M42 24 L100 34 L42 44 Z" fill="var(--rh-gold)" fillOpacity="0.2" stroke="var(--rh-gold)" strokeWidth="0.6" strokeOpacity="0.25" strokeLinejoin="round" />
      {/* Plus icon */}
      <circle cx="130" cy="50" r="14" stroke="var(--rh-gold)" strokeWidth="1.5" opacity="0.4" fill="none" />
      <line x1="124" y1="50" x2="136" y2="50" stroke="var(--rh-gold)" strokeWidth="1.5" opacity="0.4" strokeLinecap="round" />
      <line x1="130" y1="44" x2="130" y2="56" stroke="var(--rh-gold)" strokeWidth="1.5" opacity="0.4" strokeLinecap="round" />
      <text x="145" y="54" fill="currentColor" fontSize="10" fontFamily="sans-serif" opacity="0.5">Nhấn + để ghi nhận</text>
    </svg>
  );
}

function RefereeViolationPage() {
  const [assignments, setAssignments] = useState([]);
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [violations, setViolations] = useState([]);
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form, setForm] = useState({
    horseId: "",
    violationType: 3,
    description: "",
  });

  /* ---------- load assignments on mount ---------- */
  useEffect(() => {
    let ignore = false;
    const fn = async () => {
      try {
        const data = await getMyAssignments();
        if (!ignore)
          setAssignments(
            Array.isArray(data?.data)
              ? data.data
              : Array.isArray(data)
                ? data
                : []
          );
      } catch (e) {
        if (!ignore)
          setError(
            "Không thể tải danh sách cuộc đua: " + (e.message || "")
          );
      }
    };
    fn();
    return () => { ignore = true; };
  }, []);

  /* ---------- load entries when race changes ---------- */
  useEffect(() => {
    if (!selectedRaceId) return;
    let ignore = false;
    const fn = async () => {
      try {
        const data = await getRaceEntries(selectedRaceId);
        if (!ignore)
          setEntries(
            Array.isArray(data?.data)
              ? data.data
              : Array.isArray(data)
                ? data
                : []
          );
      } catch {
        if (!ignore) setEntries([]);
      }
    };
    fn();
    return () => { ignore = true; };
  }, [selectedRaceId]);

  /* ---------- load violations ---------- */
  const loadViolations = async (raceId) => {
    setLoading(true);
    setError("");
    try {
      const data = await getRaceViolations(raceId);
      setViolations(
        Array.isArray(data?.data)
          ? data.data
          : Array.isArray(data)
            ? data
            : []
      );
    } catch (e) {
      setError("Không thể tải vi phạm: " + (e.message || ""));
      setViolations([]);
    } finally {
      setLoading(false);
    }
  };

  /* ---------- select a race ---------- */
  const handleRaceSelect = (raceId) => {
    setSelectedRaceId(raceId);
    setError("");
    setSuccess("");
    setViolations([]);
    setEntries([]);
    setShowForm(false);
    if (raceId) {
      loadViolations(raceId);
      setForm({ horseId: "", violationType: 3, description: "" });
    }
  };

  /* ---------- submit violation ---------- */
  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");

    if (!form.horseId || !selectedRaceId) {
      setError("Vui lòng chọn ngựa.");
      return;
    }
    if (!form.description.trim()) {
      setError("Vui lòng nhập mô tả vi phạm.");
      return;
    }

    setSubmitting(true);
    try {
      await recordViolation({
        horseId: form.horseId,
        raceId: selectedRaceId,
        violationType: form.violationType,
        description: form.description,
      });
      setSuccess("Đã ghi nhận vi phạm thành công.");
      setShowForm(false);
      setForm({ horseId: "", violationType: 3, description: "" });
      loadViolations(selectedRaceId);
    } catch (e) {
      setError(e?.message || "Không thể ghi nhận vi phạm.");
    } finally {
      setSubmitting(false);
    }
  };

  /* ---------- derived data ---------- */
  const assignedRaces = useMemo(
    () => [...new Map(assignments.map((a) => [a.raceId, a])).values()],
    [assignments]
  );

  const totalViolations = violations.length;
  const resolvedCount = violations.filter(
    (v) => v.status === "Resolved"
  ).length;
  const pendingCount = violations.filter(
    (v) => v.status === "Pending" || !v.status
  ).length;

  /* ---------- render ---------- */
  return (
    <div className="rv-wrap">
      <div className="rv-container">
        {/* ════════ Top Bar ════════ */}
        <header className="rv-topbar">
          <div>
            <h1>Vi phạm</h1>
            <p className="rv-topbar-sub">
              Ghi nhận vi phạm quy tắc trong cuộc đua để duy trì cạnh tranh
              công bằng.
            </p>
          </div>
          {selectedRaceId && (
            <button
              className="rv-btn--gold"
              onClick={() => setShowForm((p) => !p)}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              {showForm ? "Thu gọn" : "+ Ghi nhận vi phạm"}
            </button>
          )}
        </header>

        {/* ════════ KPI Cards ════════ */}
        <div className="rv-kpi-bar">
          <div className="rv-kpi-card">
            <span className="rv-kpi-label">Tổng số</span>
            <div className="rv-kpi-value">{totalViolations}</div>
            <span className="rv-kpi-sub">Vi phạm được ghi nhận</span>
          </div>
          <div className="rv-kpi-card">
            <span className="rv-kpi-label">Đã xử lý</span>
            <div className="rv-kpi-value rv-kpi-value--green">
              {resolvedCount}
            </div>
            <span className="rv-kpi-sub">
              {totalViolations > 0
                ? Math.round((resolvedCount / totalViolations) * 100)
                : 0}
              % tổng số
            </span>
          </div>
          <div className="rv-kpi-card">
            <span className="rv-kpi-label">Chờ xử lý</span>
            <div className="rv-kpi-value rv-kpi-value--amber">
              {pendingCount}
            </div>
            <span className="rv-kpi-sub">Đang chờ xem xét</span>
          </div>
        </div>

        {/* ════════ Alerts ════════ */}
        {error && <div className="rv-alert rv-alert--error">{error}</div>}
        {success && (
          <div className="rv-alert rv-alert--success">{success}</div>
        )}

        {/* ════════ Race Selector ════════ */}
        <div className="rv-card rv-race-selector">
          <div className="rv-card__header">
            <h3>Chọn cuộc đua</h3>
            {selectedRaceId && (
              <span className="rv-count">{entries.length} ngựa</span>
            )}
          </div>
          <select
            className="rv-select"
            value={selectedRaceId}
            onChange={(e) => handleRaceSelect(e.target.value)}
          >
            <option value="">-- Chọn một cuộc đua --</option>
            {assignedRaces.map((a) => (
              <option key={a.raceId} value={a.raceId}>
                {a.raceName || a.raceId}
              </option>
            ))}
          </select>
        </div>

        {/* ════════ No race selected → full-width empty state ════════ */}
        {!selectedRaceId && (
          <div className="rv-card">
            <div className="rv-empty-state">
              <FlagWhistleSVG />
              <p>Chọn một cuộc đua để xem và ghi nhận vi phạm.</p>
            </div>
          </div>
        )}

        {/* ════════ 2-Column Content ════════ */}
        {selectedRaceId && (
          <div className="rv-grid">
            {/* ── Left Column: Thêm mới ── */}
            <div className="rv-card">
              <div className="rv-card__header">
                <h3>Thêm mới</h3>
              </div>

              {showForm ? (
                <form className="rv-form" onSubmit={handleSubmit}>
                  <div className="rv-form-row">
                    <label className="rv-label rv-label--required">
                      Ngựa
                    </label>
                    <select
                      className="rv-form-input"
                      value={form.horseId}
                      onChange={(e) =>
                        setForm({ ...form, horseId: e.target.value })
                      }
                      required
                    >
                      <option value="">-- Chọn ngựa --</option>
                      {entries.map((entry) => (
                        <option key={entry.horseId} value={entry.horseId}>
                          {entry.horseName}
                          {entry.jockeyName
                            ? ` (Nài: ${entry.jockeyName})`
                            : ""}
                        </option>
                      ))}
                    </select>
                    {entries.length === 0 && (
                      <p className="rv-hint">
                        Không có ngựa nào trong cuộc đua này.
                      </p>
                    )}
                  </div>

                  <div className="rv-form-row">
                    <label className="rv-label rv-label--required">
                      Loại vi phạm
                    </label>
                    <select
                      className="rv-form-input"
                      value={form.violationType}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          violationType: Number(e.target.value),
                        })
                      }
                    >
                      {VIOLATION_TYPES.map((t) => (
                        <option key={t.value} value={t.value}>
                          {t.label}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="rv-form-row">
                    <label className="rv-label rv-label--required">
                      Mô tả
                    </label>
                    <textarea
                      className="rv-textarea"
                      rows={3}
                      placeholder="Mô tả chi tiết vi phạm..."
                      value={form.description}
                      onChange={(e) =>
                        setForm({ ...form, description: e.target.value })
                      }
                      required
                    />
                  </div>

                  <div className="rv-form-actions">
                    <button
                      type="submit"
                      className="rv-btn rv-btn--primary"
                      disabled={submitting}
                    >
                      {submitting ? "Đang gửi..." : "Ghi nhận vi phạm"}
                    </button>
                    <button
                      type="button"
                      className="rv-btn rv-btn--cancel"
                      onClick={() => setShowForm(false)}
                    >
                      Hủy
                    </button>
                  </div>
                </form>
              ) : (
                <div className="rv-empty-list">
                  <ViolationPromptSVG />
                  <p>
                    Nhấn "+ Ghi nhận vi phạm" để ghi lại vi phạm cho cuộc đua
                    này.
                  </p>
                </div>
              )}
            </div>

            {/* ── Right Column: Lịch sử gần đây ── */}
            <div className="rv-card">
              <div className="rv-card__header">
                <h3>Lịch sử gần đây</h3>
                <span className="rv-count">{totalViolations}</span>
              </div>

              {loading ? (
                <div className="rv-loading">
                  <div className="rv-skeleton" />
                  <div className="rv-skeleton" />
                  <div className="rv-skeleton" />
                </div>
              ) : violations.length === 0 ? (
                <div className="rv-empty-list">
                  <EmptyViolationSVG />
                  <p>
                    Chưa có vi phạm nào được ghi nhận cho cuộc đua này.
                  </p>
                </div>
              ) : (
                <div className="rv-list">
                  {violations.map((v) => (
                    <div key={v.id} className="rv-item">
                      <div className="rv-item__top">
                        <strong className="rv-item__name">
                          {v.horseName || "Ngựa #" + v.horseId}
                        </strong>
                        <span
                          className={`rv-badge rv-badge--${getBadgeKey(
                            v.violationType
                          )}`}
                        >
                          {TYPE_LABELS[v.violationType] ||
                            "Vi phạm #" + v.violationType}
                        </span>
                      </div>

                      <p className="rv-item__desc">{v.description}</p>

                      <div className="rv-item__bottom">
                        <span className="rv-item__time">
                          {v.createdAt
                            ? new Date(v.createdAt).toLocaleString("vi-VN")
                            : ""}
                        </span>
                        {v.status && (
                          <span
                            className={`rv-badge rv-badge--status-${v.status.toLowerCase()}`}
                          >
                            {STATUS_MAP[v.status] || v.status}
                          </span>
                        )}
                        {!v.status && (
                          <span className="rv-badge rv-badge--status-pending">
                            Chờ xử lý
                          </span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default RefereeViolationPage;
