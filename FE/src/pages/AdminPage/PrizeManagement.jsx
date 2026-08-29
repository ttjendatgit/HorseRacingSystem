import { useEffect, useMemo, useState } from "react";
import { getPrizesByTournament, createPrize, updatePrize, deletePrize, distributePrizes } from "../../services/managementApi";
import { getAdminTournaments } from "../../services/adminApi";
import { getRoundsByTournament } from "../../services/spectatorApi";
import { RaceButton, RaceModalShell } from "../../components/ui/RaceUi";
import {
  isTournamentDraftEditable,
  isTournamentFinished,
  sortPrizesByPosition,
  computeAllocatedTotal,
  computeRemainingBudget,
  computeAllocatedPercentage,
  computeRemainingPercentage,
  isAllocationComplete,
  formatVndCurrency,
  formatPercentage,
  computePreviewAmount,
  validatePrizeForm,
  computePlannedFinalParticipants,
  getMaxRankLabel,
  getDefaultPrizeName,
  PRESET_PERCENTAGES,
} from "../../utils/prizeAllocation";

// ── Prize create/edit form modal (local to this page — a lighter-weight variant of the shared
// Modal in AdminOperations.jsx, kept self-contained so this redesign never has to modify that
// file's exports). PRIZE-V1.2 PART 14/15/16/18: percentage-first form with a "%"-suffixed
// numeric field, quick-fill preset chips (shortcuts only — manual entry is never restricted),
// and a live client-side Amount preview. Two-column grouping on desktop, single column on
// mobile (see .pm-form-grid in AdminPage.css). ──
function PrizeFormModal({ title, initial, prizePool, existingPrizes, editingPrizeId, maxRank, onCancel, onSubmit }) {
  const [position, setPosition] = useState(initial.position);
  const [percentage, setPercentage] = useState(initial.percentage);
  const [name, setName] = useState(initial.name);
  const [sponsorName, setSponsorName] = useState(initial.sponsorName);
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  const previewAmount = computePreviewAmount(prizePool, percentage);

  const submit = async (ev) => {
    ev.preventDefault();
    const validation = validatePrizeForm({ position, percentage, prizePool, existingPrizes, editingPrizeId, maxRank });
    setErrors(validation);
    if (validation.position || validation.percentage) return;

    setSubmitting(true);
    try {
      await onSubmit({ position: Number(position), percentageOfPool: Number(percentage), name, sponsorName: sponsorName || null });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay" role="dialog" aria-modal="true" onClick={onCancel}>
      <div className="admin-modal-panel pm-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button type="button" className="ghost-button" onClick={onCancel}>Đóng</button>
        </div>
        <form className="modal-body" onSubmit={submit}>
          <div className="pm-form-grid">
            <div className="form-group">
              <label htmlFor="prize-position">Hạng thưởng</label>
              <input
                id="prize-position" type="number" min={1} max={maxRank ?? undefined} step={1}
                value={position} onChange={(e) => setPosition(e.target.value)} required
              />
              {errors.position && <p className="admin-notice admin-notice--error" style={{ marginTop: 6 }}>{errors.position}</p>}
            </div>

            <div className="form-group">
              <label htmlFor="prize-percentage">Tỷ lệ phân bổ</label>
              <div className="pm-percent-field">
                <input
                  id="prize-percentage" type="number" min="0.01" max="100" step="0.01"
                  value={percentage} onChange={(e) => setPercentage(e.target.value)} required
                />
                <span className="pm-percent-suffix">%</span>
              </div>
              {errors.percentage && <p className="admin-notice admin-notice--error" style={{ marginTop: 6 }}>{errors.percentage}</p>}
              <div className="pm-preset-chips">
                <span className="pm-preset-chips__label">Gợi ý:</span>
                {PRESET_PERCENTAGES.map((preset) => (
                  <button
                    key={preset} type="button" className="ad-qa-btn"
                    onClick={() => setPercentage(preset)}
                  >
                    {preset}%
                  </button>
                ))}
              </div>
              <p className="pm-preview">
                Tiền thưởng tương ứng: <strong>{formatVndCurrency(previewAmount)}</strong>
              </p>
            </div>
          </div>

          <div className="pm-form-grid">
            <div className="form-group">
              <label htmlFor="prize-name">Tên giải thưởng (tùy chọn)</label>
              <input id="prize-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={getDefaultPrizeName(position || 1)} />
            </div>
            <div className="form-group">
              <label htmlFor="prize-sponsor">Nhà tài trợ (tùy chọn)</label>
              <input id="prize-sponsor" value={sponsorName} onChange={(e) => setSponsorName(e.target.value)} placeholder="Ví dụ: RaceMaster Inc." />
            </div>
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
  const [rounds, setRounds] = useState([]);
  const [loading, setLoading] = useState(false);
  const [msg, setMsg] = useState("");
  const [msgIsError, setMsgIsError] = useState(false);
  const [modal, setModal] = useState(null); // { mode: "create" | "edit", prize? }
  const [confirmDistribute, setConfirmDistribute] = useState(false);
  const [distributing, setDistributing] = useState(false);
  const [distributeResult, setDistributeResult] = useState(null); // { distributed, skipped, errors } | { error }

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
  const finished = isTournamentFinished(tournament);
  // PRIZE-V1.1 PART 1/5: PlannedFinalParticipants — the max valid Prize.Position for this
  // Tournament, needed for both the "Có thể trao thưởng đến" card and the form's Position bound.
  const plannedFinalParticipants = useMemo(
    () => (tournament ? computePlannedFinalParticipants(tournament, rounds) : null),
    [tournament, rounds]
  );

  const load = (tid) => {
    if (!tid) { setPrizes([]); setRounds([]); return; }
    setLoading(true);
    Promise.all([
      getPrizesByTournament(tid).then((d) => setPrizes(Array.isArray(d) ? d : [])),
      getRoundsByTournament(tid).then((d) => setRounds(Array.isArray(d) ? d : [])).catch(() => setRounds([])),
    ])
      .catch((e) => { setMsg(e.message); setMsgIsError(true); })
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(tournamentId); }, [tournamentId]);

  const sorted = sortPrizesByPosition(prizes);
  const allocatedMoney = computeAllocatedTotal(prizes);
  const remainingMoney = computeRemainingBudget(prizePool, prizes);
  const allocatedPercentage = computeAllocatedPercentage(prizes);
  const remainingPercentage = computeRemainingPercentage(prizes);
  const complete = isAllocationComplete(prizes);
  const maxRankLabel = getMaxRankLabel(plannedFinalParticipants);

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

  // PRIZE-V2 (Phase 4): moves real money into Owner wallets — always behind the confirm popup
  // below, never triggered by anything else (no auto-run on Tournament->Finished).
  const runDistribute = async () => {
    setDistributing(true);
    try {
      const result = await distributePrizes(tournamentId);
      setDistributeResult({
        distributed: result?.distributed ?? result?.Distributed ?? [],
        skipped: result?.skipped ?? result?.Skipped ?? [],
        errors: result?.errors ?? result?.Errors ?? [],
      });
    } catch (e) {
      setDistributeResult({ error: e.message });
    } finally {
      setDistributing(false);
      setConfirmDistribute(false);
    }
  };

  return (
    <div>
      <h2>Quản lý giải thưởng</h2>
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>
        Cấu hình cơ cấu giải thưởng theo tỷ lệ phần trăm quỹ thưởng cho từng thứ hạng cuối cùng của giải đấu.
      </p>
      {msg && <p className={`admin-notice ${msgIsError ? "admin-notice--error" : ""}`} role="alert">{msg}</p>}

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
        <div className="pm-empty-state">
          <p className="muted">Vui lòng chọn một giải đấu để xem hoặc cấu hình cơ cấu giải thưởng.</p>
        </div>
      ) : loading ? (
        <p className="muted">Đang tải...</p>
      ) : (
        <>
          <div className="admin-stat-grid" style={{ marginBottom: 12 }}>
            <div className="admin-stat-card">
              <p>Tổng quỹ thưởng</p>
              <h3>{formatVndCurrency(prizePool)}</h3>
            </div>
            <div className="admin-stat-card">
              <p>Đã phân bổ</p>
              <h3>{formatPercentage(allocatedPercentage)}</h3>
              <span className="pm-stat-secondary">{formatVndCurrency(allocatedMoney)}</span>
            </div>
            <div className="admin-stat-card">
              <p>Còn lại</p>
              <h3>{formatPercentage(remainingPercentage)}</h3>
              <span className="pm-stat-secondary">{formatVndCurrency(remainingMoney)}</span>
            </div>
            <div className="admin-stat-card">
              <p>Có thể trao thưởng đến</p>
              <h3>{maxRankLabel ?? "Chưa xác định"}</h3>
            </div>
          </div>

          <p className="pm-help-text" style={{ marginBottom: 16 }}>
            Bạn có thể chọn trao thưởng Top 1, Top 3, Top 5... miễn không vượt quá số người dự kiến vào Vòng chung kết.
          </p>

          {!draftEditable && !finished && (
            <p className="admin-notice" style={{ marginBottom: 16 }}>
              Cơ cấu giải thưởng đã được khóa sau khi giải đấu được công bố.
            </p>
          )}

          {finished && (
            <div className="pm-distribute-bar">
              <p className="admin-notice" style={{ marginBottom: 0 }}>
                Giải đấu đã kết thúc — cơ cấu giải thưởng đã khóa. Trao thưởng sẽ cộng tiền vào ví
                Chủ ngựa đứng đúng thứ hạng chung cuộc theo số tiền đã tính sẵn cho mỗi Hạng.
              </p>
              <RaceButton onClick={() => setConfirmDistribute(true)} disabled={sorted.length === 0}>
                Trao thưởng
              </RaceButton>
            </div>
          )}

          {draftEditable && (
            <div style={{ marginBottom: 16, display: "flex", alignItems: "center", flexWrap: "wrap", gap: 12 }}>
              <button
                className="primary-button"
                onClick={() => setModal({ mode: "create" })}
                disabled={plannedFinalParticipants != null && sorted.length >= plannedFinalParticipants}
              >
                Thêm giải thưởng
              </button>
              {prizes.length > 0 && (
                <span className={`pm-progress-tag ${complete ? "pm-progress-tag--complete" : "pm-progress-tag--incomplete"}`}>
                  {complete ? "Đã phân bổ đủ quỹ thưởng" : "Chưa phân bổ hết"}
                </span>
              )}
            </div>
          )}

          {sorted.length === 0 ? (
            <div className="pm-empty-state">
              <p className="muted">Chưa có giải thưởng nào được cấu hình cho giải đấu này.</p>
            </div>
          ) : (
            <div className="admin-table-wrap">
              <table className="admin-table pm-table">
                <thead>
                  <tr>
                    <th>Hạng</th>
                    <th>Tên giải</th>
                    <th>Tỷ lệ</th>
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
                        <td className="pm-table__name">{p.name ?? p.Name}</td>
                        <td className="pm-table__figure">{formatPercentage(p.percentageOfPool ?? p.PercentageOfPool)}</td>
                        <td className="pm-table__figure">{formatVndCurrency(p.amount ?? p.Amount)}</td>
                        <td className="pm-table__sponsor">{p.sponsorName ?? p.SponsorName ?? "-"}</td>
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
          initial={{ position: sorted.length + 1, percentage: "", name: "", sponsorName: "" }}
          prizePool={prizePool}
          existingPrizes={prizes}
          editingPrizeId={null}
          maxRank={plannedFinalParticipants}
          onCancel={() => setModal(null)}
          onSubmit={submitCreate}
        />
      )}

      {modal?.mode === "edit" && (
        <PrizeFormModal
          title={`Sửa Hạng ${modal.prize.position ?? modal.prize.Position}`}
          initial={{
            position: modal.prize.position ?? modal.prize.Position,
            percentage: modal.prize.percentageOfPool ?? modal.prize.PercentageOfPool,
            name: modal.prize.name ?? modal.prize.Name ?? "",
            sponsorName: modal.prize.sponsorName ?? modal.prize.SponsorName ?? "",
          }}
          prizePool={prizePool}
          existingPrizes={prizes}
          editingPrizeId={modal.prize.id ?? modal.prize.Id}
          maxRank={plannedFinalParticipants}
          onCancel={() => setModal(null)}
          onSubmit={(values) => submitEdit(modal.prize.id ?? modal.prize.Id, values)}
        />
      )}

      {confirmDistribute && (
        <RaceModalShell
          title="Xác nhận trao thưởng"
          description="Hành động này sẽ cộng tiền thật vào ví các Chủ ngựa đoạt giải."
          onClose={() => (distributing ? null : setConfirmDistribute(false))}
          footer={
            <>
              <RaceButton variant="ghost" onClick={() => setConfirmDistribute(false)} disabled={distributing}>
                Hủy
              </RaceButton>
              <RaceButton onClick={runDistribute} loading={distributing}>
                Xác nhận trao thưởng
              </RaceButton>
            </>
          }
        >
          <p>
            Đây là hành động <strong>không thể hoàn tác qua giao diện</strong>. Số tiền của từng Hạng thưởng
            (đã tính sẵn theo cơ cấu đã cấu hình) sẽ được cộng thẳng vào ví của Chủ ngựa đang đứng đúng thứ
            hạng chung cuộc tương ứng. Vui lòng kiểm tra kỹ cơ cấu giải thưởng trước khi xác nhận.
          </p>
        </RaceModalShell>
      )}

      {distributeResult && (
        <RaceModalShell
          title={distributeResult.error ? "Trao thưởng thất bại" : "Kết quả trao thưởng"}
          description={distributeResult.error ? undefined : "Chi tiết từng Hạng thưởng sau khi xử lý."}
          onClose={() => setDistributeResult(null)}
          className={distributeResult.error ? "pm-distribute-modal pm-distribute-modal--danger" : "pm-distribute-modal"}
          footer={<RaceButton onClick={() => setDistributeResult(null)}>Đã hiểu</RaceButton>}
        >
          {distributeResult.error ? (
            <p>{distributeResult.error}</p>
          ) : (
            <div className="pm-distribute-result">
              <section>
                <h4>Đã trao ({distributeResult.distributed.length})</h4>
                {distributeResult.distributed.length === 0 ? (
                  <p className="muted">Không có Hạng thưởng nào được trao trong lần này.</p>
                ) : (
                  <ul className="pm-distribute-list">
                    {distributeResult.distributed.map((d, i) => {
                      const position = d.position ?? d.Position;
                      const horseName = d.horseName ?? d.HorseName;
                      const ownerName = d.ownerName ?? d.OwnerName;
                      const amount = d.amount ?? d.Amount;
                      return (
                        <li key={`d-${position}-${i}`}>
                          <strong>Hạng {position}</strong> — {ownerName || "Chưa rõ Chủ ngựa"} (ngựa {horseName}
                          ): {formatVndCurrency(amount)}
                        </li>
                      );
                    })}
                  </ul>
                )}
              </section>

              {distributeResult.skipped.length > 0 && (
                <section>
                  <h4>Bỏ qua ({distributeResult.skipped.length})</h4>
                  <ul className="pm-distribute-list pm-distribute-list--skipped">
                    {distributeResult.skipped.map((s, i) => {
                      const position = s.position ?? s.Position;
                      const reason = s.reason ?? s.Reason;
                      return (
                        <li key={`s-${position}-${i}`}>
                          <strong>Hạng {position}</strong> — {reason}
                        </li>
                      );
                    })}
                  </ul>
                </section>
              )}

              {distributeResult.errors.length > 0 && (
                <section>
                  <h4>Lỗi ({distributeResult.errors.length})</h4>
                  <ul className="pm-distribute-list pm-distribute-list--error">
                    {distributeResult.errors.map((er, i) => {
                      const position = er.position ?? er.Position;
                      const reason = er.reason ?? er.Reason;
                      return (
                        <li key={`e-${position}-${i}`}>
                          <strong>Hạng {position}</strong> — {reason}
                        </li>
                      );
                    })}
                  </ul>
                </section>
              )}
            </div>
          )}
        </RaceModalShell>
      )}
    </div>
  );
}
