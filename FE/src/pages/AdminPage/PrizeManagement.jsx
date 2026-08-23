import { useEffect, useMemo, useState } from "react";
import { getPrizesByTournament, createPrize, updatePrize, deletePrize } from "../../services/managementApi";
import { getAdminTournaments } from "../../services/adminApi";
import {
  isTournamentDraftEditable,
  sortPrizesByPosition,
  computeAllocatedTotal,
  computeRemainingBudget,
  isAllocationComplete,
  formatVndCurrency,
  validatePrizeForm,
} from "../../utils/prizeAllocation";

// ── Prize create/edit form modal (local to this page — a lighter-weight variant of the shared
// Modal in AdminOperations.jsx, kept self-contained so this redesign never has to modify that
// file's exports) ──
function PrizeFormModal({ title, initial, prizePool, existingPrizes, editingPrizeId, onCancel, onSubmit }) {
  const [position, setPosition] = useState(initial.position);
  const [amount, setAmount] = useState(initial.amount);
  const [name, setName] = useState(initial.name);
  const [sponsorName, setSponsorName] = useState(initial.sponsorName);
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  const submit = async (ev) => {
    ev.preventDefault();
    const validation = validatePrizeForm({ position, amount, prizePool, existingPrizes, editingPrizeId });
    setErrors(validation);
    if (validation.position || validation.amount) return;

    setSubmitting(true);
    try {
      await onSubmit({ position: Number(position), amount: Number(amount), name, sponsorName: sponsorName || null });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay" role="dialog" aria-modal="true" onClick={onCancel}>
      <div className="admin-modal-panel" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 460 }}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button type="button" className="ghost-button" onClick={onCancel}>Đóng</button>
        </div>
        <form className="modal-body" onSubmit={submit}>
          <div className="form-group">
            <label htmlFor="prize-position">Hạng thưởng</label>
            <input
              id="prize-position" type="number" min={1} step={1}
              value={position} onChange={(e) => setPosition(e.target.value)} required
            />
            {errors.position && <p className="admin-notice admin-notice--error" style={{ marginTop: 6 }}>{errors.position}</p>}
          </div>
          <div className="form-group">
            <label htmlFor="prize-amount">Tiền thưởng (VND)</label>
            <input
              id="prize-amount" type="number" min={1} step={1}
              value={amount} onChange={(e) => setAmount(e.target.value)} required
            />
            {errors.amount && <p className="admin-notice admin-notice--error" style={{ marginTop: 6 }}>{errors.amount}</p>}
          </div>
          <div className="form-group">
            <label htmlFor="prize-name">Tên giải thưởng (tùy chọn)</label>
            <input id="prize-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={`Hạng ${position || 1}`} />
          </div>
          <div className="form-group">
            <label htmlFor="prize-sponsor">Nhà tài trợ (tùy chọn)</label>
            <input id="prize-sponsor" value={sponsorName} onChange={(e) => setSponsorName(e.target.value)} placeholder="Ví dụ: RaceMaster Inc." />
          </div>
          <div className="modal-actions" style={{ marginTop: 16 }}>
            <button className="primary-button" type="submit" disabled={submitting}>{submitting ? "Đang lưu..." : "Lưu"}</button>
            <button type="button" className="ghost-button" onClick={onCancel}>Hủy</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function PrizeManagement() {
  const [tournaments, setTournaments] = useState([]);
  const [tournamentId, setTournamentId] = useState("");
  const [prizes, setPrizes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [msg, setMsg] = useState("");
  const [msgIsError, setMsgIsError] = useState(false);
  const [modal, setModal] = useState(null); // { mode: "create" | "edit", prize? }

  useEffect(() => {
    getAdminTournaments()
      .then((d) => setTournaments(Array.isArray(d) ? d : []))
      .catch((e) => { setMsg(e.message); setMsgIsError(true); });
  }, []);

  const tournament = useMemo(
    () => tournaments.find((t) => String(t.id ?? t.Id) === String(tournamentId)) ?? null,
    [tournaments, tournamentId]
  );
  const prizePool = tournament?.prizePool ?? tournament?.PrizePool ?? 0;
  const draftEditable = isTournamentDraftEditable(tournament);

  const load = (tid) => {
    if (!tid) { setPrizes([]); return; }
    setLoading(true);
    getPrizesByTournament(tid)
      .then((d) => setPrizes(Array.isArray(d) ? d : []))
      .catch((e) => { setMsg(e.message); setMsgIsError(true); })
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(tournamentId); }, [tournamentId]);

  const sorted = sortPrizesByPosition(prizes);
  const allocated = computeAllocatedTotal(prizes);
  const remaining = computeRemainingBudget(prizePool, prizes);
  const complete = isAllocationComplete(prizePool, prizes);

  const notify = (text, isError) => { setMsg(text); setMsgIsError(!!isError); };

  const submitCreate = async (values) => {
    try {
      await createPrize({ tournamentId, ...values });
      notify("Đã thêm giải thưởng.", false);
      setModal(null);
      load(tournamentId);
    } catch (e) {
      notify(e.message, true);
      throw e;
    }
  };

  const submitEdit = async (prizeId, values) => {
    try {
      await updatePrize(prizeId, values);
      notify("Đã cập nhật giải thưởng.", false);
      setModal(null);
      load(tournamentId);
    } catch (e) {
      notify(e.message, true);
      throw e;
    }
  };

  const remove = async (prize) => {
    const id = prize.id ?? prize.Id;
    if (!confirm(`Xóa Hạng ${prize.position ?? prize.Position}?`)) return;
    try {
      await deletePrize(id);
      notify("Đã xóa giải thưởng.", false);
      load(tournamentId);
    } catch (e) {
      notify(e.message, true);
    }
  };

  return (
    <div>
      <h2>Quản lý giải thưởng</h2>
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>
        Cấu hình cơ cấu giải thưởng theo thứ hạng cuối cùng của giải đấu.
      </p>
      {msg && <p className={`admin-notice ${msgIsError ? "admin-notice--error" : ""}`}>{msg}</p>}

      <div className="admin-form" style={{ marginBottom: 20 }}>
        <div className="form-group" style={{ margin: 0 }}>
          <label htmlFor="prize-tournament-select">Giải đấu</label>
          <select
            id="prize-tournament-select" className="admin-select"
            value={tournamentId} onChange={(e) => setTournamentId(e.target.value)} required
          >
            <option value="">-- Chọn giải đấu --</option>
            {tournaments.map((t) => (
              <option key={t.id ?? t.Id} value={t.id ?? t.Id}>
                {t.name ?? t.Name} ({t.statusName ?? t.StatusName ?? "Draft"})
              </option>
            ))}
          </select>
        </div>
      </div>

      {!tournamentId ? (
        <p className="muted">Vui lòng chọn một giải đấu để xem hoặc cấu hình cơ cấu giải thưởng.</p>
      ) : loading ? (
        <p className="muted">Đang tải...</p>
      ) : (
        <>
          <div className="admin-stat-grid" style={{ marginBottom: 20 }}>
            <div className="admin-stat-card">
              <p>Tổng quỹ thưởng</p>
              <h3>{formatVndCurrency(prizePool)}</h3>
            </div>
            <div className="admin-stat-card">
              <p>Đã phân bổ</p>
              <h3>{formatVndCurrency(allocated)}</h3>
            </div>
            <div className="admin-stat-card">
              <p>Còn lại</p>
              <h3>{formatVndCurrency(remaining)}</h3>
            </div>
          </div>

          {!draftEditable && (
            <p className="admin-notice" style={{ marginBottom: 16 }}>
              Cơ cấu giải thưởng đã được khóa sau khi giải đấu được công bố.
            </p>
          )}

          {draftEditable && (
            <div style={{ marginBottom: 16 }}>
              <button
                className="primary-button"
                onClick={() => setModal({ mode: "create" })}
              >
                Thêm giải thưởng
              </button>
              {prizes.length > 0 && (
                <span className="muted" style={{ marginLeft: 12 }}>
                  {complete ? "Đã phân bổ đủ quỹ thưởng" : "Chưa phân bổ hết"}
                </span>
              )}
            </div>
          )}

          {sorted.length === 0 ? (
            <p className="muted">Chưa có giải thưởng nào được cấu hình cho giải đấu này.</p>
          ) : (
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Hạng</th>
                    <th>Tên giải</th>
                    <th>Tiền thưởng</th>
                    <th>Nhà tài trợ</th>
                    {draftEditable && <th>Thao tác</th>}
                  </tr>
                </thead>
                <tbody>
                  {sorted.map((p) => {
                    const id = p.id ?? p.Id;
                    const position = p.position ?? p.Position;
                    return (
                      <tr key={id}>
                        <td>Hạng {position}</td>
                        <td>{p.name ?? p.Name}</td>
                        <td>{formatVndCurrency(p.amount ?? p.Amount)}</td>
                        <td>{p.sponsorName ?? p.SponsorName ?? "-"}</td>
                        {draftEditable && (
                          <td>
                            <div className="admin-actions">
                              <button onClick={() => setModal({ mode: "edit", prize: p })}>Sửa</button>
                              <button className="admin-danger" onClick={() => remove(p)}>Xóa</button>
                            </div>
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {modal?.mode === "create" && (
        <PrizeFormModal
          title="Thêm giải thưởng"
          initial={{ position: sorted.length + 1, amount: "", name: "", sponsorName: "" }}
          prizePool={prizePool}
          existingPrizes={prizes}
          editingPrizeId={null}
          onCancel={() => setModal(null)}
          onSubmit={submitCreate}
        />
      )}

      {modal?.mode === "edit" && (
        <PrizeFormModal
          title={`Sửa Hạng ${modal.prize.position ?? modal.prize.Position}`}
          initial={{
            position: modal.prize.position ?? modal.prize.Position,
            amount: modal.prize.amount ?? modal.prize.Amount,
            name: modal.prize.name ?? modal.prize.Name ?? "",
            sponsorName: modal.prize.sponsorName ?? modal.prize.SponsorName ?? "",
          }}
          prizePool={prizePool}
          existingPrizes={prizes}
          editingPrizeId={modal.prize.id ?? modal.prize.Id}
          onCancel={() => setModal(null)}
          onSubmit={(values) => submitEdit(modal.prize.id ?? modal.prize.Id, values)}
        />
      )}
    </div>
  );
}
