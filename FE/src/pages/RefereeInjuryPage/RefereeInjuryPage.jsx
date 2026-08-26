import { useState, useEffect, useMemo } from "react";
import {
  getInjuries,
  createInjury,
  markRecovered,
} from "../../services/managementApi";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import { getRaceEntries } from "../../services/refereeApi";
import HorseBodyMap from "../../components/HorseBodyMap/HorseBodyMap3D";
import "./RefereeInjuryPage.css";

const SEVERITY_OPTIONS = [
  { value: "Minor", label: "Nhẹ" },
  { value: "Moderate", label: "Trung bình" },
  { value: "Severe", label: "Nặng" },
  { value: "Critical", label: "Nghiêm trọng" },
];

const INJURY_TYPES = [
  "Fracture",
  "Tendon",
  "Ligament",
  "Muscle",
  "Respiratory",
  "Hoof",
  "Joint",
  "Skin",
  "Other",
];

const BODY_PARTS = [
  "Head",
  "Neck",
  "Shoulder",
  "Back",
  "Hip",
  "Tail",
  "FrontLeg-Left",
  "FrontLeg-Right",
  "HindLeg-Left",
  "HindLeg-Right",
  "Fetlock-Left",
  "Fetlock-Right",
];

const SEVERITY_COLORS = {
  Minor: "#4ade80",
  Moderate: "#facc15",
  Severe: "#fb923c",
  Critical: "#f87171",
};

const SEVERITY_BG = {
  Minor: "rgba(74,222,128,0.15)",
  Moderate: "rgba(250,204,21,0.15)",
  Severe: "rgba(251,146,60,0.15)",
  Critical: "rgba(248,113,113,0.15)",
};

function severityToNumber(s) {
  const map = { Minor: 0, Moderate: 1, Severe: 3, Critical: 4 };
  return map[s] ?? 0;
}

/* ── SVG: empty state (no injuries recorded) ── */
function InjuryEmptySVG() {
  return (
    <svg className="ri-empty-svg" viewBox="0 0 200 120" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M40 90 L160 90" stroke="currentColor" strokeWidth="2" strokeLinecap="round" opacity="0.3" />
      <path d="M60 90 L60 40 C60 30 70 20 80 20 L120 20 C130 20 140 30 140 40 L140 90" stroke="currentColor" strokeWidth="2" fill="none" opacity="0.4" />
      <path d="M90 55 L110 55 M100 45 L100 65" stroke="var(--ri-green)" strokeWidth="3" strokeLinecap="round" opacity="0.8" />
    </svg>
  );
}

function RefereeInjuryPage() {
  const [injuries, setInjuries] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [selectedPart, setSelectedPart] = useState(null);
  const [refreshing, setRefreshing] = useState(false);

  const [assignments, setAssignments] = useState([]);
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [entries, setEntries] = useState([]);

  const [form, setForm] = useState({
    horseId: "",
    injuryType: "Fracture",
    description: "",
    severity: "Minor",
    bodyPart: "",
    treatment: "",
  });

  useEffect(() => {
    let ignore = false;
    getMyAssignments()
      .then((data) => {
        if (ignore) return;
        setAssignments(Array.isArray(data?.data) ? data.data : Array.isArray(data) ? data : []);
      })
      .catch(() => { if (!ignore) setAssignments([]); });
    return () => { ignore = true; };
  }, []);

  useEffect(() => {
    if (!selectedRaceId) { setEntries([]); return; }
    let ignore = false;
    getRaceEntries(selectedRaceId)
      .then((data) => {
        if (ignore) return;
        setEntries(Array.isArray(data?.data) ? data.data : Array.isArray(data) ? data : []);
      })
      .catch(() => { if (!ignore) setEntries([]); });
    return () => { ignore = true; };
  }, [selectedRaceId]);

  const assignedRaces = useMemo(
    () => [...new Map(assignments.map((a) => [a.raceId, a])).values()],
    [assignments]
  );

  const loadInjuries = async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setLoading(true);
    setError("");
    try {
      const data = await getInjuries();
      const list = Array.isArray(data?.data)
        ? data.data
        : Array.isArray(data)
          ? data
          : [];
      setInjuries(list);
    } catch (e) {
      setError("Không thể tải danh sách chấn thương: " + e.message);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadInjuries();
  }, []);

  const activeCount = injuries.filter(
    (i) =>
      !i.recoveredAt &&
      i.status !== "Recovered" &&
      i.status !== "ClearedToRace"
  ).length;

  const recoveredCount = injuries.filter(
    (i) =>
      i.recoveredAt ||
      i.status === "Recovered" ||
      i.status === "ClearedToRace"
  ).length;

  const injuredParts = {};
  injuries.forEach((i) => {
    if (i.bodyPart) {
      const sev = severityToNumber(i.severity);
      if (!(i.bodyPart in injuredParts) || sev > injuredParts[i.bodyPart]) {
        injuredParts[i.bodyPart] = sev;
      }
    }
  });

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccessMsg("");
    if (!form.horseId || !form.injuryType || !form.description.trim()) {
      setError("Mã ngựa, loại chấn thương và mô tả là bắt buộc.");
      return;
    }
    try {
      await createInjury({
        horseId: form.horseId,
        injuryType: form.injuryType,
        description: form.description,
        severity: form.severity,
        bodyPart: form.bodyPart || undefined,
        treatment: form.treatment || undefined,
      });
      setSuccessMsg("Đã ghi nhận chấn thương thành công.");
      setShowForm(false);
      setSelectedRaceId("");
      setForm({
        horseId: "",
        injuryType: "Fracture",
        description: "",
        severity: "Minor",
        bodyPart: "",
        treatment: "",
      });
      loadInjuries();
    } catch (e) {
      setError(e.message || "Có lỗi xảy ra.");
    }
  };

  const handleRecover = async (id) => {
    if (!window.confirm("Xác nhận chấn thương này đã bình phục?")) return;
    setError("");
    setSuccessMsg("");
    try {
      await markRecovered(id);
      setSuccessMsg("Đã đánh dấu bình phục thành công.");
      loadInjuries();
    } catch (e) {
      setError(e.message);
    }
  };

  const handlePartClick = (partName) => {
    setSelectedPart(partName === selectedPart ? null : partName);
    if (!showForm) setShowForm(true);
    setForm((p) => ({ ...p, bodyPart: partName }));
  };

  const getStatusLabel = (inj) => {
    if (inj.recoveredAt || inj.status === "Recovered") return "Đã hồi phục";
    if (inj.status === "ClearedToRace") return "Đủ điều kiện";
    return "Đang điều trị";
  };

  const getStatusClass = (inj) => {
    if (inj.recoveredAt || inj.status === "Recovered" || inj.status === "ClearedToRace")
      return "recovered";
    return "active";
  };

  const getSeverityLabel = (s) => {
    const opt = SEVERITY_OPTIONS.find((o) => o.value === s);
    return opt ? opt.label : s;
  };

  const formatDate = (d) => {
    if (!d) return "-";
    try {
      return new Date(d).toLocaleDateString("vi-VN", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
      });
    } catch {
      return "-";
    }
  };

  return (
    <div className="ri-wrap">
      {/* ── Header ── */}
      <div className="ri-header">
        <div className="ri-header-left">
          <h1 className="ri-title">Quản lý chấn thương</h1>
          <p className="ri-sub">Theo dõi và ghi nhận tình trạng chấn thương của ngựa</p>
        </div>
        <div className="ri-header-actions">
          <button
            className="ri-btn ri-btn-outline"
            onClick={() => loadInjuries(true)}
            disabled={refreshing}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={refreshing ? "ri-spin" : ""}>
              <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.92-10.26l5.58 3.69" />
            </svg>
            {refreshing ? "Đang tải..." : "Làm mới"}
          </button>
          <button
            className="ri-btn ri-btn-gold"
            onClick={() => setShowForm(!showForm)}
          >
            {showForm ? "Hủy" : "+ Ghi nhận chấn thương"}
          </button>
        </div>
      </div>

      {error && <div className="ri-alert ri-alert-error">{error}</div>}
      {successMsg && <div className="ri-alert ri-alert-success">{successMsg}</div>}

      {/* ── Compact KPI Row ── */}
      <div className="ri-kpi-row">
        <div className="ri-kpi ri-kpi-gold">
          <span className="ri-kpi-num">{injuries.length}</span>
          <span className="ri-kpi-label">Hồ sơ</span>
        </div>
        <div className="ri-kpi ri-kpi-red">
          <span className="ri-kpi-num">{activeCount}</span>
          <span className="ri-kpi-label">Đang điều trị</span>
        </div>
        <div className="ri-kpi ri-kpi-green">
          <span className="ri-kpi-num">{recoveredCount}</span>
          <span className="ri-kpi-label">Đã phục hồi</span>
        </div>
      </div>

      {/* ── Two-column: Body Map + Injury List ── */}
      <div className="ri-grid">
        {/* Left — Horse Body Map */}
        <div className="ri-body-map-col">
          {!loading && (
            <HorseBodyMap
              injuredParts={injuredParts}
              selectedPart={selectedPart}
              onPartClick={handlePartClick}
            />
          )}
        </div>

        {/* Right — Form Toggle + Injury Table */}
        <div className="ri-list-col">
          {showForm && (
            <form className="ri-form" onSubmit={handleSubmit}>
              <h3 className="ri-form-title">Ghi nhận chấn thương</h3>
              <div className="ri-form-grid">
                <div className="ri-fgroup">
                  <label>Cuộc đua</label>
                  <select
                    value={selectedRaceId}
                    onChange={(e) => {
                      setSelectedRaceId(e.target.value);
                      setForm({ ...form, horseId: "" });
                    }}
                  >
                    <option value="">-- Chọn cuộc đua --</option>
                    {assignedRaces.map((a) => (
                      <option key={a.raceId} value={a.raceId}>
                        {a.raceName || a.raceId}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="ri-fgroup">
                  <label>Ngựa</label>
                  <select
                    value={form.horseId}
                    onChange={(e) =>
                      setForm({ ...form, horseId: e.target.value })
                    }
                    disabled={!selectedRaceId}
                    required
                  >
                    <option value="">-- Chọn ngựa --</option>
                    {entries.map((entry) => (
                      <option key={entry.horseId} value={entry.horseId}>
                        {entry.horseName}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="ri-fgroup">
                  <label>Loại chấn thương</label>
                  <select
                    value={form.injuryType}
                    onChange={(e) =>
                      setForm({ ...form, injuryType: e.target.value })
                    }
                  >
                    {INJURY_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="ri-fgroup">
                  <label>Vị trí cơ thể</label>
                  <select
                    value={form.bodyPart}
                    onChange={(e) =>
                      setForm({ ...form, bodyPart: e.target.value })
                    }
                  >
                    <option value="">-- Chọn --</option>
                    {BODY_PARTS.map((p) => (
                      <option key={p} value={p}>
                        {p}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="ri-fgroup">
                  <label>Mức độ</label>
                  <select
                    value={form.severity}
                    onChange={(e) =>
                      setForm({ ...form, severity: e.target.value })
                    }
                  >
                    {SEVERITY_OPTIONS.map((s) => (
                      <option key={s.value} value={s.value}>
                        {s.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="ri-fgroup ri-fgroup-full">
                <label>Mô tả chi tiết</label>
                <textarea
                  rows={3}
                  placeholder="Mô tả về chấn thương..."
                  value={form.description}
                  onChange={(e) =>
                    setForm({ ...form, description: e.target.value })
                  }
                  required
                />
              </div>
              <div className="ri-fgroup ri-fgroup-full">
                <label>Phương pháp điều trị</label>
                <textarea
                  rows={2}
                  placeholder="Phương pháp điều trị (không bắt buộc)..."
                  value={form.treatment}
                  onChange={(e) =>
                    setForm({ ...form, treatment: e.target.value })
                  }
                />
              </div>
              <button type="submit" className="ri-btn ri-btn-gold ri-btn-full">
                Ghi nhận chấn thương
              </button>
            </form>
          )}

          {/* ── Injury Table ── */}
          {loading ? (
            <div className="ri-loading">Đang tải dữ liệu...</div>
          ) : injuries.length === 0 ? (
            <div className="ri-empty">
              <InjuryEmptySVG />
              <p>Chưa có hồ sơ chấn thương nào.</p>
            </div>
          ) : (
            <div className="ri-table-wrap">
              <table className="ri-table">
                <thead>
                  <tr>
                    <th>Ngựa</th>
                    <th>Ngày</th>
                    <th>Loại chấn thương</th>
                    <th>Vị trí</th>
                    <th>Mức độ</th>
                    <th>Trạng thái</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {injuries.map((inj) => (
                    <tr key={inj.id} className="ri-tr">
                      <td className="ri-cell-name">
                        {inj.horseName || `#${inj.horseId?.slice(0, 8)}`}
                      </td>
                      <td className="ri-cell-date">{formatDate(inj.diagnosedAt)}</td>
                      <td>{inj.injuryType || "-"}</td>
                      <td>{inj.bodyPart || "-"}</td>
                      <td>
                        <span
                          className="ri-badge"
                          style={{
                            background: SEVERITY_BG[inj.severity] || "rgba(255,255,255,0.1)",
                            color: SEVERITY_COLORS[inj.severity] || "#94a3b8",
                            border: `1px solid ${SEVERITY_COLORS[inj.severity] || "#94a3b8"}40`,
                          }}
                        >
                          {getSeverityLabel(inj.severity)}
                        </span>
                      </td>
                      <td>
                        <span
                          className={`ri-status ri-status--${getStatusClass(inj)}`}
                        >
                          {getStatusLabel(inj)}
                        </span>
                      </td>
                      <td>
                        {!inj.recoveredAt &&
                          inj.status !== "Recovered" &&
                          inj.status !== "ClearedToRace" && (
                            <button
                              className="ri-btn ri-btn-sm"
                              onClick={() => handleRecover(inj.id)}
                            >
                              Hồi phục
                            </button>
                          )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default RefereeInjuryPage;
