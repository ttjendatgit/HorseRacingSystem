import { useState, useEffect, useMemo } from "react";
import {
  getRaceEntries,
  getRaceHealthChecks,
  createHealthCheck,
  approveHorseForRace,
  rejectHorseForRace,
  getHorseHealthHistory,
} from "../../services/refereeApi";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import HorseBodyMap from "../../components/HorseBodyMap/HorseBodyMap3D";
import "./RefereeHealthCheckPage.css";

const STATUS_MAP = {
  Passed: "Đạt",
  Failed: "Không đạt",
  RequiresRecheck: "Cần kiểm tra lại",
};

const STATUS_OPTIONS = ["Passed", "Failed", "RequiresRecheck"];

/* ── SVG: Vet checking a horse (empty state) ── */
function VetHorseSVG() {
  return (
    <svg className="rh-empty-svg" viewBox="0 0 240 150" fill="none" xmlns="http://www.w3.org/2000/svg">
      <g opacity="0.25">
        <path d="M52 100 C48 94 46 86 50 80 C54 74 62 72 70 74 L78 72 C82 70 86 66 90 66 C94 66 96 70 94 76 L92 82 C98 80 104 82 108 88 L112 86 L114 90 L110 94 L104 96 C106 100 104 106 98 110 C92 114 84 116 76 118 C68 120 58 118 52 112 Z" fill="currentColor" />
        <rect x="58" y="116" width="5" height="24" rx="2" fill="currentColor" />
        <rect x="68" y="116" width="5" height="24" rx="2" fill="currentColor" />
        <rect x="86" y="116" width="5" height="24" rx="2" fill="currentColor" />
        <rect x="96" y="116" width="5" height="24" rx="2" fill="currentColor" />
      </g>
      <g opacity="0.2">
        <circle cx="162" cy="54" r="9" fill="currentColor" />
        <rect x="156" y="63" width="12" height="28" rx="4" fill="currentColor" />
        <rect x="150" y="88" width="8" height="22" rx="3" fill="currentColor" />
        <rect x="166" y="88" width="8" height="22" rx="3" fill="currentColor" />
      </g>
      <path d="M162 63 C162 56 168 50 174 48" stroke="currentColor" strokeWidth="1.5" opacity="0.4" />
      <circle cx="177" cy="46" r="5" stroke="currentColor" strokeWidth="1.2" opacity="0.4" fill="none" />
      <path d="M190 60 L194 56 L198 64 L202 50 L206 58 L210 56" stroke="var(--rh-gold-dim)" strokeWidth="1.5" opacity="0.7" fill="none" strokeLinecap="round" strokeLinejoin="round" />
      <text x="120" y="140" textAnchor="middle" fill="currentColor" fontSize="12" fontFamily="sans-serif" opacity="0.8">
        Chọn cuộc đua để bắt đầu kiểm tra
      </text>
    </svg>
  );
}

/* ── SVG: Empty history ── */
function EmptyHistorySVG() {
  return (
    <svg className="rh-empty-list-svg" viewBox="0 0 120 80" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="30" y="12" width="60" height="54" rx="6" stroke="currentColor" strokeWidth="1" opacity="0.3" fill="currentColor" fillOpacity="0.05" />
      <rect x="44" y="6" width="32" height="10" rx="3" stroke="currentColor" strokeWidth="1" opacity="0.25" fill="currentColor" fillOpacity="0.05" />
      <rect x="40" y="30" width="40" height="2" rx="1" fill="currentColor" opacity="0.2" />
      <rect x="40" y="38" width="32" height="2" rx="1" fill="currentColor" opacity="0.15" />
      <rect x="40" y="46" width="36" height="2" rx="1" fill="currentColor" opacity="0.15" />
      <path d="M40 54 L46 60 L56 48" stroke="var(--rh-green)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" opacity="0.8" />
    </svg>
  );
}

/* ── SVG: Body map prompt ── */
function BodyMapPromptSVG() {
  return (
    <svg viewBox="0 0 200 100" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ width: 160, height: 80, color: "var(--rh-muted)" }}>
      <path d="M30 55 C26 50 24 44 28 38 C32 32 40 30 48 32 L56 30 C60 28 64 24 68 24 C72 24 74 28 72 34 L70 40 C76 38 82 40 86 46 L90 44 L92 48 L88 52 L82 54 C84 58 82 64 76 68 C70 72 62 74 54 76 C46 78 36 76 30 70 Z" stroke="currentColor" strokeWidth="0.8" fill="none" opacity="0.35" />
      <line x1="36" y1="74" x2="36" y2="90" stroke="currentColor" strokeWidth="0.8" opacity="0.3" />
      <line x1="46" y1="74" x2="46" y2="90" stroke="currentColor" strokeWidth="0.8" opacity="0.3" />
      <line x1="64" y1="74" x2="64" y2="90" stroke="currentColor" strokeWidth="0.8" opacity="0.3" />
      <line x1="74" y1="74" x2="74" y2="90" stroke="currentColor" strokeWidth="0.8" opacity="0.3" />
      <circle cx="110" cy="50" r="14" stroke="var(--rh-gold)" strokeWidth="1.5" opacity="0.6" fill="none" />
      <line x1="104" y1="50" x2="116" y2="50" stroke="var(--rh-gold)" strokeWidth="1.5" opacity="0.6" strokeLinecap="round" />
      <line x1="110" y1="44" x2="110" y2="56" stroke="var(--rh-gold)" strokeWidth="1.5" opacity="0.6" strokeLinecap="round" />
      <text x="145" y="54" fill="currentColor" fontSize="10" fontFamily="sans-serif" opacity="0.7">Nhấn + để thêm</text>
    </svg>
  );
}

function RefereeHealthCheckPage() {
  const [assignments, setAssignments] = useState([]);
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [healthChecks, setHealthChecks] = useState([]);
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form, setForm] = useState({
    horseId: "",
    status: "Passed",
    notes: "",
    bodyPart: "",
  });
  const [bodyMapData] = useState({});
  const [bodyMapSelected, setBodyMapSelected] = useState(null);
  const [showHistoryModal, setShowHistoryModal] = useState(false);
  const [historyData, setHistoryData] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  //Fetch API get assigments
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
          setError("Không thể tải danh sách cuộc đua: " + (e.message || ""));
      }
    };
    fn();
    return () => { ignore = true; };
  }, []);

  //Fetch API get race entries when selected
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

  //Load health checks for selected race
  const loadHealthChecks = async (raceId) => {
    setLoading(true);
    setError("");
    try {
      const data = await getRaceHealthChecks(raceId);
      setHealthChecks(
        Array.isArray(data?.data)
          ? data.data
          : Array.isArray(data)
            ? data
            : []
      );
    } catch (e) {
      setError("Không thể tải kiểm tra sức khỏe: " + (e.message || ""));
      setHealthChecks([]);
    } finally {
      setLoading(false);
    }
  };

  //Handle race selection
  const handleRaceSelect = (raceId) => {
    setSelectedRaceId(raceId);
    setError("");
    setSuccess("");
    setHealthChecks([]);
    setEntries([]);
    setShowForm(false);
    setBodyMapSelected(null);
    if (raceId) {
      loadHealthChecks(raceId);
      setForm({ horseId: "", status: "Passed", notes: "", bodyPart: "" });
    }
  };

  //Handle view history for a specific horse
  const handleViewHistory = async (targetHorseId) => {
    if (!targetHorseId) return;
    setShowHistoryModal(true);
    setHistoryLoading(true);
    try {
      const res = await getHorseHealthHistory(targetHorseId);
      setHistoryData(Array.isArray(res?.data) ? res.data : Array.isArray(res) ? res : []);
    } catch (e) {
      console.error(e);
      setHistoryData([]);
    } finally {
      setHistoryLoading(false);
    }
  };

  //Handle form submission to create a new health check
  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    if (!form.horseId || !selectedRaceId) {
      setError("Vui lòng chọn ngựa.");
      return;
    }
    setSubmitting(true);
    try {
      await createHealthCheck({
        horseId: form.horseId,
        raceId: selectedRaceId,
        healthCheckStatus: form.status,
        observations: form.notes || undefined,
      });
      setSuccess("Đã tạo kiểm tra sức khỏe thành công.");
      setShowForm(false);
      setForm({ horseId: "", status: "Passed", notes: "", bodyPart: "" });
      setBodyMapSelected(null);
      loadHealthChecks(selectedRaceId);
    } catch (e) {
      setError(e?.message || "Không thể tạo kiểm tra sức khỏe.");
    } finally {
      setSubmitting(false);
    }
  };

  //Handle approval of a horse for racing
  const handleApprove = async (checkId) => {
    setError("");
    setSuccess("");
    try {
      await approveHorseForRace(checkId);
      setSuccess("Đã phê duyệt ngựa thi đấu.");
      loadHealthChecks(selectedRaceId);
    } catch (e) {
      setError(e?.message || "Không thể phê duyệt.");
    }
  };

  //Handle rejection of a horse for racing
  const handleReject = async (checkId) => {
    const reason = prompt("Nhập lý do từ chối:");
    if (!reason) return;
    setError("");
    setSuccess("");
    try {
      await rejectHorseForRace(checkId, reason);
      setSuccess("Đã từ chối ngựa thi đấu.");
      loadHealthChecks(selectedRaceId);
    } catch (e) {
      setError(e?.message || "Không thể từ chối.");
    }
  };

  //Handle body part selection from the horse body map
  const handleBodyPartClick = (part) => {
    setBodyMapSelected(part);
    setForm((prev) => ({ ...prev, bodyPart: part }));
  };

  //Derive unique assigned races from assignments
  const assignedRaces = useMemo(
    () => [...new Map(assignments.map((a) => [a.raceId, a])).values()],
    [assignments]
  );

  //Filter horse entries
  const availableEntries = useMemo(() => {
    return entries.filter((entry) => {
      const targetHorseId = entry.horseId || entry.HorseId;
      const horseChecks = healthChecks.filter(
        (c) => (c.horseId || c.HorseId) === targetHorseId
      );
      if (horseChecks.length === 0) return true;
      const isFinalized = horseChecks.some(
        (c) => c.approvedToRace || c.ApprovedToRace);
      return !isFinalized;
    });
  }, [entries, healthChecks]);

  const { total, passed, failed, recheck } = useMemo(() => {
    const t = healthChecks.length;
    const p = healthChecks.filter((c) => c.status === "Passed").length;
    const f = healthChecks.filter((c) => c.status === "Failed").length;
    const r = healthChecks.filter((c) => c.status === "RequiresRecheck").length;
    return { total: t, passed: p, failed: f, recheck: r };
  }, [healthChecks]);

  return (
    <div className="rh-wrap">
      <div className="rh-container">
        {/* ════════ Top Bar ════════ */}
        <header className="rh-topbar">
          <div>
            <h1>Kiểm tra sức khoẻ</h1>
            <p className="rh-topbar-sub">
              Kiểm tra ngựa trước cuộc đua để đảm bảo chúng đủ điều kiện thi đấu.
            </p>
          </div>
          {selectedRaceId && (
            <button
              className="rh-btn--gold"
              onClick={() => setShowForm((p) => !p)}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              {showForm ? "Thu gọn" : "+ Kiểm tra mới"}
            </button>
          )}
        </header>

        {/* ════════ KPI Cards ════════ */}
        <div className="rh-kpi-bar">
          <div className="rh-kpi-card">
            <span className="rh-kpi-label">Tổng số</span>
            <div className="rh-kpi-value">{total}</div>
            <span className="rh-kpi-sub">Lượt kiểm tra</span>
          </div>
          <div className="rh-kpi-card">
            <span className="rh-kpi-label">Đạt</span>
            <div className="rh-kpi-value rh-kpi-value--green">{passed}</div>
            <span className="rh-kpi-sub">
              {total > 0 ? Math.round((passed / total) * 100) : 0}% tổng số
            </span>
          </div>
          <div className="rh-kpi-card">
            <span className="rh-kpi-label">Không đạt / Cần kiểm tra lại</span>
            <div className="rh-kpi-value rh-kpi-value--red">{failed}</div>
            <span className="rh-kpi-sub">
              {recheck > 0 ? `${recheck} cần kiểm tra lại` : "Từ chối thi đấu"}
            </span>
          </div>
        </div>

        {/* ════════ Alerts ════════ */}
        {error && <div className="rh-alert rh-alert--error">{error}</div>}
        {success && <div className="rh-alert rh-alert--success">{success}</div>}

        {/* ════════ Race Selector ════════ */}
        <div className="rh-card rh-race-selector">
          <div className="rh-card__header">
            <h3>Chọn cuộc đua</h3>
            {selectedRaceId && (
              <span className="rh-count">{entries.length} ngựa</span>
            )}
          </div>
          <select
            className="rh-select"
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
          <div className="rh-card">
            <div className="rh-empty-state">
              <VetHorseSVG />
              <p>Chọn một cuộc đua để xem và tạo kiểm tra sức khỏe.</p>
            </div>
          </div>
        )}

        {/* ════════ 2-Column Content ════════ */}
        {selectedRaceId && (
          <div className="rh-grid">
            {/* ── Left Column: Thêm mới ── */}
            <div className="rh-card">
              <div className="rh-card__header">
                <h3>Thêm mới</h3>
                {form.bodyPart && (
                  <span className="rh-badge rh-badge--passed">
                    {form.bodyPart}
                  </span>
                )}
              </div>

              {showForm ? (
                <form className="rh-form" onSubmit={handleSubmit}>
                  <div className="rh-form-row">
                    <label className="rh-label rh-label--required">Ngựa</label>
                    <div style={{ display: "flex", gap: "8px", alignItems: "flex-start" }}>
                      <div style={{ flex: 1 }}>
                        <select
                          className="rh-form-input"
                          value={form.horseId}
                          onChange={(e) =>
                            setForm({ ...form, horseId: e.target.value })
                          }
                          required
                        >
                          <option value="">-- Chọn ngựa --</option>
                          {availableEntries.map((entry) => (
                            <option key={entry.horseId} value={entry.horseId}>
                              {entry.horseName}
                              {entry.jockeyName
                                ? ` (Nài: ${entry.jockeyName})`
                                : ""}
                            </option>
                          ))}
                        </select>
                        {entries.length === 0 && (
                          <p className="rh-hint" style={{ marginTop: "4px" }}>
                            Không có ngựa nào trong cuộc đua này.
                          </p>
                        )}
                        {entries.length > 0 && availableEntries.length === 0 && (
                          <p className="rh-hint" style={{ marginTop: "4px" }}>
                            Tất cả ngựa đã được kiểm tra xong.
                          </p>
                        )}
                      </div>
                      
                      {form.horseId && (
                        <button
                          type="button"
                          className="rh-btn"
                          style={{
                            padding: "8px 12px",
                            background: "rgba(255,255,255,0.05)",
                            color: "var(--rh-gold)",
                            border: "1px solid rgba(242, 210, 139, 0.2)",
                            whiteSpace: "nowrap",
                            height: "44px"
                          }}
                          onClick={() => handleViewHistory(form.horseId)}
                        >
                          📋 Lịch sử y tế
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="rh-form-row">
                    <label className="rh-label rh-label--required">
                      Trạng thái
                    </label>
                    <select
                      className="rh-form-input"
                      value={form.status}
                      onChange={(e) =>
                        setForm({ ...form, status: e.target.value })
                      }
                    >
                      {STATUS_OPTIONS.map((s) => (
                        <option key={s} value={s}>
                          {STATUS_MAP[s]}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="rh-form-row">
                    <label className="rh-label">Ghi chú</label>
                    <textarea
                      className="rh-textarea"
                      rows={3}
                      placeholder="Nhập ghi chú về tình trạng sức khỏe..."
                      value={form.notes}
                      onChange={(e) =>
                        setForm({ ...form, notes: e.target.value })
                      }
                    />
                  </div>

                  {/* HorseBodyMap as visual aid */}
                  <div className="rh-body-map-section">
                    <h4>Bản đồ cơ thể ngựa</h4>
                    <p className="rh-hint" style={{ marginBottom: 8 }}>
                      Nhấp vào bộ phận cần kiểm tra để ghi chú.
                    </p>
                    <HorseBodyMap
                      injuredParts={bodyMapData}
                      onPartClick={handleBodyPartClick}
                      selectedPart={bodyMapSelected}
                    />
                  </div>

                  <div className="rh-form-actions">
                    <button
                      type="submit"
                      className="rh-btn rh-btn--primary"
                      disabled={submitting || availableEntries.length === 0}
                    >
                      {submitting ? "Đang gửi..." : "Gửi kiểm tra"}
                    </button>
                    <button
                      type="button"
                      className="rh-btn rh-btn--cancel"
                      onClick={() => setShowForm(false)}
                    >
                      Hủy
                    </button>
                  </div>
                </form>
              ) : (
                /* Prompt to add when form is hidden */
                <div className="rh-empty-list">
                  <BodyMapPromptSVG />
                  <p>Nhấn "+ Kiểm tra mới" để tạo kiểm tra sức khỏe cho ngựa.</p>
                </div>
              )}
            </div>

            {/* ── Right Column: Lịch sử gần đây ── */}
            <div className="rh-card">
              <div className="rh-card__header">
                <h3>Lịch sử gần đây</h3>
                <span className="rh-count">{healthChecks.length}</span>
              </div>

              {loading ? (
                <div className="rh-loading">
                  <div className="rh-skeleton" />
                  <div className="rh-skeleton" />
                  <div className="rh-skeleton" />
                </div>
              ) : healthChecks.length === 0 ? (
                <div className="rh-empty-list">
                  <EmptyHistorySVG />
                  <p>Chưa có kiểm tra sức khỏe nào cho cuộc đua này.</p>
                </div>
              ) : (
                <div className="rh-list">
                  {healthChecks.map((check) => (
                    <div key={check.id} className="rh-item">
                      <div className="rh-item__top" style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: "8px" }}>
                        <strong className="rh-item__name">
                          {check.horseName || "Ngựa #" + check.horseId}
                        </strong>
                        
                        <button
                          type="button"
                          style={{
                            padding: "4px 8px",
                            fontSize: "11px",
                            background: "transparent",
                            color: "var(--rh-gold)",
                            border: "1px solid rgba(242, 210, 139, 0.3)",
                            borderRadius: "4px",
                            cursor: "pointer",
                            transition: "all 0.2s"
                          }}
                          onMouseEnter={(e) => e.currentTarget.style.background = "rgba(242, 210, 139, 0.1)"}
                          onMouseLeave={(e) => e.currentTarget.style.background = "transparent"}
                          onClick={() => handleViewHistory(check.horseId)}
                          title="Xem hồ sơ y tế"
                        >
                          📋 Lịch sử
                        </button>

                        <span
                          className={`rh-badge rh-badge--${check.status?.toLowerCase()}`}
                          style={{ marginLeft: "auto" }}
                        >
                          {STATUS_MAP[check.status] || check.status}
                        </span>
                      </div>

                      {check.jockeyName && (
                        <div className="rh-item__meta">
                          Nài: {check.jockeyName}
                        </div>
                      )}

                      {check.notes && (
                        <p className="rh-item__notes">{check.notes}</p>
                      )}

                      <div className="rh-item__bottom">
                        <span className="rh-item__time">
                          {check.createdAt
                            ? new Date(check.createdAt).toLocaleString(
                              "vi-VN"
                            )
                            : ""}
                        </span>

                        <div className="rh-item__actions">
                          {!check.approvedToRace &&
                            check.status === "Passed" && (
                              <>
                                <button
                                  className="rh-btn rh-btn--sm rh-btn--approve"
                                  onClick={() => handleApprove(check.id)}
                                >
                                  Phê duyệt
                                </button>
                                <button
                                  className="rh-btn rh-btn--sm rh-btn--reject"
                                  onClick={() => handleReject(check.id)}
                                >
                                  Từ chối
                                </button>
                              </>
                            )}
                          {check.approvedToRace && (
                            <span className="rh-badge rh-badge--approved">
                              Đã phê duyệt
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {showHistoryModal && (
          <div style={{ position: "fixed", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: "rgba(0,0,0,0.8)", zIndex: 9999, display: "flex", alignItems: "center", justifyContent: "center", backdropFilter: "blur(4px)" }}>
            <div style={{ background: "#151210", border: "1px solid rgba(238, 229, 212, 0.1)", borderRadius: 16, padding: 24, width: "90%", maxWidth: 650, maxHeight: "80vh", overflowY: "auto", color: "#eee5d4", boxShadow: "0 10px 30px rgba(0,0,0,0.5)" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
                <h3 style={{ margin: 0, color: "#f2d28b", fontSize: "1.25rem", fontWeight: 600 }}>Lịch sử y tế</h3>
                <button type="button" onClick={() => setShowHistoryModal(false)} style={{ background: "transparent", border: "none", color: "#94a3b8", fontSize: 28, cursor: "pointer", padding: 0, lineHeight: 1, transition: "color 0.2s" }} onMouseEnter={(e)=>e.currentTarget.style.color="#f2d28b"} onMouseLeave={(e)=>e.currentTarget.style.color="#94a3b8"}>&times;</button>
              </div>
              
              {historyLoading ? (
                <div style={{ padding: "40px 0", textAlign: "center" }}>
                  <p style={{ color: "#94a3b8" }}>Đang tải dữ liệu...</p>
                </div>
              ) : historyData.length === 0 ? (
                <div style={{ padding: "40px 0", textAlign: "center" }}>
                  <p style={{ color: "#94a3b8" }}>Ngựa này chưa có lịch sử khám bệnh.</p>
                </div>
              ) : (
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid rgba(255,255,255,0.1)", textAlign: "left" }}>
                      <th style={{ padding: "12px 8px", color: "#94a3b8", fontWeight: 600, textTransform: "uppercase", fontSize: 11, letterSpacing: 0.5 }}>Giải / Cuộc đua</th>
                      <th style={{ padding: "12px 8px", color: "#94a3b8", fontWeight: 600, textTransform: "uppercase", fontSize: 11, letterSpacing: 0.5 }}>Trạng thái</th>
                      <th style={{ padding: "12px 8px", color: "#94a3b8", fontWeight: 600, textTransform: "uppercase", fontSize: 11, letterSpacing: 0.5 }}>Ghi chú</th>
                    </tr>
                  </thead>
                  <tbody>
                    {historyData.map((h, i) => (
                      <tr key={h.id || i} style={{ borderBottom: "1px solid rgba(255,255,255,0.05)", transition: "background 0.2s" }} onMouseEnter={(e)=>e.currentTarget.style.background="rgba(255,255,255,0.02)"} onMouseLeave={(e)=>e.currentTarget.style.background="transparent"}>
                        <td style={{ padding: "14px 8px", fontWeight: 500 }}>{h.raceName || h.RaceName || "Khám tổng quát"}</td>
                        <td style={{ padding: "14px 8px" }}>
                          <span className={`rh-badge rh-badge--${(h.status || h.Status)?.toLowerCase()}`}>
                            {STATUS_MAP[h.status || h.Status] || (h.status || h.Status)}
                          </span>
                        </td>
                        <td style={{ padding: "14px 8px", color: "rgba(255,255,255,0.6)", lineHeight: 1.4 }}>
                          {h.notes || h.Notes || h.observations || h.Observations || "-"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default RefereeHealthCheckPage;