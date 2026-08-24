import { useState, useEffect, useMemo } from "react";
import { getRaceEntries, assignGateNumber } from "../../services/refereeApi";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import {
  isRaceGateEditable,
  sortEntriesByGate,
  formatGateLabel,
  isEntryGateAssignable,
  getGateReadinessSummary,
  getGateValidationError,
} from "../../utils/gateAssignment";
import "./GateAssignmentPage.css";

function GateAssignmentPage() {
  const [assignments, setAssignments] = useState([]);
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [inputs, setInputs] = useState({}); // entryId -> draft gate input value
  const [savingEntryId, setSavingEntryId] = useState(null);

  /* ---------- load Confirmed assignments on mount ---------- */
  useEffect(() => {
    let ignore = false;
    const fn = async () => {
      try {
        // Only Confirmed assignments are actionable — filtering server-side means the race
        // selector never even offers a Race this Referee hasn't confirmed for.
        const data = await getMyAssignments("Confirmed");
        if (!ignore) setAssignments(Array.isArray(data) ? data : []);
      } catch (e) {
        if (!ignore) setError("Không thể tải danh sách phân công: " + (e.message || ""));
      }
    };
    fn();
    return () => { ignore = true; };
  }, []);

  /* ---------- load entries when race changes ---------- */
  useEffect(() => {
    if (!selectedRaceId) return;
    let ignore = false;
    setLoading(true);
    const fn = async () => {
      try {
        const data = await getRaceEntries(selectedRaceId);
        const list = Array.isArray(data?.data) ? data.data : Array.isArray(data) ? data : [];
        if (!ignore) {
          setEntries(list);
          setInputs(Object.fromEntries(list.map((e) => [e.entryId ?? e.EntryId, e.gateNumber ?? e.GateNumber ?? ""])));
        }
      } catch (e) {
        if (!ignore) { setEntries([]); setError("Không thể tải danh sách ngựa: " + (e.message || "")); }
      } finally {
        if (!ignore) setLoading(false);
      }
    };
    fn();
    return () => { ignore = true; };
  }, [selectedRaceId]);

  const handleRaceSelect = (raceId) => {
    setSelectedRaceId(raceId);
    setError("");
    setSuccess("");
    setEntries([]);
    setInputs({});
  };

  const selectedAssignment = useMemo(
    () => assignments.find((a) => a.raceId === selectedRaceId),
    [assignments, selectedRaceId]
  );
  const raceStatus = selectedAssignment?.raceStatus;
  const trackCapacity = selectedAssignment?.trackCapacity ?? selectedAssignment?.TrackCapacity ?? null;
  const editable = isRaceGateEditable(raceStatus);
  const sortedEntries = useMemo(() => sortEntriesByGate(entries), [entries]);
  const readiness = useMemo(() => getGateReadinessSummary(entries), [entries]);

  const handleSave = async (entryId) => {
    setError("");
    setSuccess("");
    const raw = inputs[entryId];
    // Track.Capacity (a physical gate count) is the authoritative upper bound — not
    // Race.MaxParticipants. The backend remains authoritative regardless; this only lets the UI
    // show the same range before the round-trip.
    const validation = getGateValidationError(raw, trackCapacity ?? Infinity);
    if (validation) { setError(validation); return; }

    setSavingEntryId(entryId);
    try {
      await assignGateNumber(selectedRaceId, entryId, Number(raw));
      setSuccess("Đã lưu cổng xuất phát.");
      const data = await getRaceEntries(selectedRaceId);
      const list = Array.isArray(data?.data) ? data.data : Array.isArray(data) ? data : [];
      setEntries(list);
      setInputs(Object.fromEntries(list.map((e) => [e.entryId ?? e.EntryId, e.gateNumber ?? e.GateNumber ?? ""])));
    } catch (e) {
      setError(e?.message || "Không thể lưu cổng xuất phát.");
    } finally {
      setSavingEntryId(null);
    }
  };

  return (
    <div className="rg-wrap">
      <div className="rg-container">
        <header className="rg-topbar">
          <div>
            <h1>Phân cổng xuất phát</h1>
            <p className="rg-topbar-sub">
              Gán số cổng xuất phát cho từng ngựa tham gia cuộc đua bạn đang làm trọng tài.
            </p>
          </div>
          {selectedRaceId && readiness.total > 0 && (
            <span className={`rg-badge ${readiness.isComplete ? "rg-badge--complete" : "rg-badge--incomplete"}`}>
              {readiness.assigned}/{readiness.total} đã xếp cổng
            </span>
          )}
        </header>

        {error && <div className="rg-alert rg-alert--error">{error}</div>}
        {success && <div className="rg-alert rg-alert--success">{success}</div>}

        <div className="rg-card rg-race-selector">
          <div className="rg-card__header">
            <h3>Chọn cuộc đua</h3>
          </div>
          <select className="rg-select" value={selectedRaceId} onChange={(e) => handleRaceSelect(e.target.value)}>
            <option value="">-- Chọn một cuộc đua đã xác nhận --</option>
            {assignments.map((a) => (
              <option key={a.raceId} value={a.raceId}>
                {a.raceName || a.raceId}
              </option>
            ))}
          </select>
          {assignments.length === 0 && (
            <p className="rg-hint">Bạn chưa có phân công nào đã được xác nhận.</p>
          )}
        </div>

        {!selectedRaceId && (
          <div className="rg-card">
            <div className="rg-empty-state">
              <p>Chọn một cuộc đua để xem và phân cổng xuất phát.</p>
            </div>
          </div>
        )}

        {selectedRaceId && !editable && (
          <div className="rg-card">
            <p className="rg-locked-notice">
              Cuộc đua đang ở trạng thái "{raceStatus}" — cổng xuất phát đã được khóa và không thể chỉnh sửa.
            </p>
          </div>
        )}

        {selectedRaceId && (
          <div className="rg-card">
            <div className="rg-card__header">
              <h3>Danh sách ngựa tham gia</h3>
              <span className="rg-count">{entries.length}</span>
            </div>
            {editable && (
              <p className="rg-hint" style={{ marginBottom: 12 }}>
                {trackCapacity ? `Cổng hợp lệ: 1–${trackCapacity}` : "Chưa xác định được sức chứa đường đua."}
              </p>
            )}

            {loading ? (
              <div className="rg-loading">
                <div className="rg-skeleton" />
                <div className="rg-skeleton" />
                <div className="rg-skeleton" />
              </div>
            ) : sortedEntries.length === 0 ? (
              <p className="rg-hint">Chưa có ngựa nào trong cuộc đua này.</p>
            ) : (
              <div className="rg-table-wrap">
                <table className="rg-table">
                  <thead>
                    <tr>
                      <th>Ngựa</th>
                      <th>Kỵ sĩ</th>
                      <th>Cổng hiện tại</th>
                      {editable && <th>Cổng mới</th>}
                      {editable && <th></th>}
                    </tr>
                  </thead>
                  <tbody>
                    {sortedEntries.map((entry) => {
                      const entryId = entry.entryId ?? entry.EntryId;
                      const assignable = isEntryGateAssignable(entry);
                      return (
                        <tr key={entryId}>
                          <td>{entry.horseName ?? entry.HorseName}</td>
                          <td>{entry.jockeyName ?? entry.JockeyName ?? "-"}</td>
                          <td className="rg-gate-current">{formatGateLabel(entry.gateNumber ?? entry.GateNumber)}</td>
                          {editable && (
                            <td>
                              {assignable ? (
                                <input
                                  type="number"
                                  min={1}
                                  max={trackCapacity ?? undefined}
                                  className="rg-gate-input"
                                  value={inputs[entryId] ?? ""}
                                  onChange={(e) => setInputs({ ...inputs, [entryId]: e.target.value })}
                                />
                              ) : (
                                <span className="rg-hint">Không tham gia</span>
                              )}
                            </td>
                          )}
                          {editable && (
                            <td>
                              {assignable && (
                                <button
                                  className="rg-btn rg-btn--primary"
                                  disabled={savingEntryId === entryId}
                                  onClick={() => handleSave(entryId)}
                                >
                                  {savingEntryId === entryId ? "Đang lưu..." : "Lưu"}
                                </button>
                              )}
                            </td>
                          )}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default GateAssignmentPage;
