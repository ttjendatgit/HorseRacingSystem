import { useEffect, useState } from "react";
import {
  getPrizes, createPrize, deletePrize,
  getPendingProtests, ruleProtest,
  getPendingTransfers, approveTransfer, rejectTransfer,
  getContracts,
  getInjuries,
} from "../../services/managementApi";
import { getAdminTournaments } from "../../services/adminApi";

const fDate = (v) => v ? new Date(v).toLocaleDateString("vi-VN", { dateStyle: "medium" }) : "-";

// ── Reusable Modal ──
function Modal({ title, children, onClose }) {
  return (
    <div className="modal-overlay" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="owner-modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 500 }}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="ghost-button" onClick={onClose}>Đóng</button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}

// ── Prize Management ──
export function PrizeManagement() {
  const [items, setItems] = useState([]);
  const [msg, setMsg] = useState("");
  const [form, setForm] = useState({ name: "", amount: 0, position: 1, currency: "USD", tournamentId: "", raceId: "", sponsorName: "" });
  const [tournaments, setTournaments] = useState([]);
  const load = () => getPrizes().then((d) => setItems(Array.isArray(d) ? d : [])).catch((e) => setMsg(e.message));
  useEffect(() => { load(); getAdminTournaments().then((d) => setTournaments(Array.isArray(d) ? d : [])); }, []);

  const submit = async (ev) => { ev.preventDefault(); try { await createPrize({ ...form, amount: Number(form.amount), position: Number(form.position), tournamentId: form.tournamentId || null, raceId: form.raceId || null }); setMsg("Đã tạo giải thưởng."); load(); } catch (e) { setMsg(e.message); } };
  const remove = async (id) => { if (!confirm("Xóa?")) return; try { await deletePrize(id); load(); } catch (e) { setMsg(e.message); } };

  return (
    <div>
      <h2>Tiền thưởng</h2>
      <p style={{ color: "#657086", marginBottom: 16 }}>Quản lý phân phối tiền thưởng cho giải đấu và cuộc đua.</p>
      {msg && <p className="admin-notice">{msg}</p>}
      <form onSubmit={submit} className="admin-form" style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 16 }}>
        <input placeholder="Tên giải thưởng" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
        <input type="number" placeholder="Số tiền" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} required />
        <input type="number" placeholder="Vị trí (1,2,3...)" value={form.position} onChange={(e) => setForm({ ...form, position: e.target.value })} />
        <select value={form.tournamentId} onChange={(e) => setForm({ ...form, tournamentId: e.target.value })}>
          <option value="">Giải đấu (tùy chọn)</option>
          {tournaments.map((t) => <option key={t.id ?? t.Id} value={t.id ?? t.Id}>{t.name ?? t.Name}</option>)}
        </select>
        <input placeholder="Nhà tài trợ" value={form.sponsorName} onChange={(e) => setForm({ ...form, sponsorName: e.target.value })} />
        <button className="primary-button" type="submit">Thêm giải thưởng</button>
      </form>
      <div className="admin-table-wrap"><table className="admin-table">
        <thead><tr><th>Tên</th><th>Số tiền</th><th>Vị trí</th><th>Nhà tài trợ</th><th>Đã phân phối</th><th>Thao tác</th></tr></thead>
        <tbody>{items.map((p) => <tr key={p.id}><td>{p.name}</td><td>{p.amount} {p.currency}</td><td>#{p.position}</td><td>{p.sponsorName || "-"}</td><td>{p.isDistributed ? "Có" : "Không"}</td><td><button onClick={() => remove(p.id)}>Xóa</button></td></tr>)}</tbody>
      </table></div>
    </div>
  );
}

// ── Protest Management ──
export function ProtestManagement() {
  const [items, setItems] = useState([]);
  const [msg, setMsg] = useState("");
  const [modal, setModal] = useState(null); // { id, type: "upheld"|"reject" }
  const [modalText, setModalText] = useState("");
  const load = () => getPendingProtests().then((d) => setItems(Array.isArray(d) ? d : [])).catch((e) => setMsg(e.message));
  useEffect(() => { load(); }, []);

  const openRuleModal = (id, type) => { setModal({ id, type }); setModalText(""); };
  const submitRule = async () => {
    if (!modal) return;
    const ruling = modal.type === "upheld"
      ? `Chấp nhận - ${modalText || "Khiếu nại được chấp nhận"}`
      : `Từ chối - ${modalText || "Không đủ bằng chứng"}`;
    try { await ruleProtest(modal.id, { ruling, resolution: modalText }); setMsg(`Khiếu nại đã ${modal.type === "upheld" ? "chấp nhận" : "từ chối"}.`); setModal(null); load(); }
    catch (e) { setMsg(e.message); }
  };

  return (
    <div>
      <h2>Khiếu nại</h2>
      <p style={{ color: "#657086", marginBottom: 16 }}>Xem xét và phán quyết khiếu nại cuộc đua từ chủ sở hữu và kỵ sĩ.</p>
      {msg && <p className="admin-notice">{msg}</p>}

      {modal && (
        <Modal title={modal.type === "upheld" ? "Chấp nhận khiếu nại - Chi tiết" : "Từ chối khiếu nại - Lý do"} onClose={() => setModal(null)}>
          <div className="form-group">
            <label>{modal.type === "upheld" ? "Chi tiết giải quyết" : "Lý do từ chối"}</label>
            <textarea className="form-textarea" rows={4} value={modalText} onChange={(e) => setModalText(e.target.value)}
              placeholder={modal.type === "upheld" ? "Mô tả cách giải quyết..." : "Tại sao khiếu nại này bị từ chối?"} />
          </div>
          <div className="modal-actions" style={{ marginTop: 16 }}>
            <button className="primary-button" onClick={submitRule}>{modal.type === "upheld" ? "Chấp nhận" : "Từ chối"}</button>
            <button className="ghost-button" onClick={() => setModal(null)}>Hủy</button>
          </div>
        </Modal>
      )}

      {items.length === 0 ? <p className="muted">Không có khiếu nại đang chờ.</p> : (
        <div className="admin-card-grid">
          {items.map((p) => (
            <article key={p.id} className="admin-simple-card">
              <span className="badge">{p.status}</span>
              <h3>{p.filedByName || "Không xác định"}</h3>
              <p>Cuộc đua: {p.raceName || p.raceId}</p>
              <p>Chống lại: {p.againstHorseName || p.againstEntryId}</p>
              <p><strong>Lý do:</strong> {p.reason}</p>
              {p.evidence && <p><strong>Bằng chứng:</strong> {p.evidence}</p>}
              <p className="time">Nộp: {fDate(p.filedAt)}</p>
              <div className="admin-actions" style={{ marginTop: 8 }}>
                <button onClick={() => openRuleModal(p.id, "upheld")}>Chấp nhận</button>
                <button className="admin-danger" onClick={() => openRuleModal(p.id, "reject")}>Từ chối</button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Horse Transfer Management ──
export function TransferManagement() {
  const [items, setItems] = useState([]);
  const [msg, setMsg] = useState("");
  const [rejectModal, setRejectModal] = useState(null); // { id }
  const [rejectReason, setRejectReason] = useState("");
  const load = () => getPendingTransfers().then((d) => setItems(Array.isArray(d) ? d : [])).catch((e) => setMsg(e.message));
  useEffect(() => { load(); }, []);

  const approve = async (id) => { try { await approveTransfer(id); setMsg("Đã phê duyệt chuyển nhượng."); load(); } catch (e) { setMsg(e.message); } };
  const submitReject = async () => { if (!rejectModal) return; try { await rejectTransfer(rejectModal.id, rejectReason || "Bị từ chối"); setMsg("Đã từ chối chuyển nhượng."); setRejectModal(null); load(); } catch (e) { setMsg(e.message); } };

  return (
    <div>
      <h2>Chuyển nhượng ngựa</h2>
      <p style={{ color: "#657086", marginBottom: 16 }}>Xem xét và phê duyệt chuyển nhượng quyền sở hữu ngựa.</p>
      {msg && <p className="admin-notice">{msg}</p>}

      {rejectModal && (
        <Modal title="Từ chối chuyển nhượng" onClose={() => setRejectModal(null)}>
          <div className="form-group">
            <label>Lý do từ chối</label>
            <textarea className="form-textarea" rows={3} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} placeholder="Tại sao chuyển nhượng này bị từ chối?" />
          </div>
          <div className="modal-actions" style={{ marginTop: 16 }}>
            <button className="primary-button" onClick={submitReject}>Từ chối chuyển nhượng</button>
            <button className="ghost-button" onClick={() => setRejectModal(null)}>Hủy</button>
          </div>
        </Modal>
      )}

      {items.length === 0 ? <p className="muted">Không có chuyển nhượng đang chờ.</p> : (
        <div className="admin-table-wrap"><table className="admin-table">
          <thead><tr><th>Ngựa</th><th>Từ</th><th>Đến</th><th>Loại</th><th>Giá</th><th>Ngày yêu cầu</th><th>Thao tác</th></tr></thead>
          <tbody>{items.map((t) => <tr key={t.id}><td>{t.horseName || t.horseId}</td><td>{t.fromOwnerName || "-"}</td><td>{t.toOwnerName || "-"}</td><td>{t.transferType}</td><td>{t.price ? `${t.price}` : "-"}</td><td>{fDate(t.requestedAt)}</td><td><div className="admin-actions"><button onClick={() => approve(t.id)}>Phê duyệt</button><button className="admin-danger" onClick={() => setRejectModal({ id: t.id })}>Từ chối</button></div></td></tr>)}</tbody>
        </table></div>
      )}
    </div>
  );
}

// ── Contract Management ──
export function ContractManagement() {
  const [items, setItems] = useState([]);
  const [msg, setMsg] = useState("");
  const load = () => getContracts().then((d) => setItems(Array.isArray(d) ? d : [])).catch((e) => setMsg(e.message));
  useEffect(() => { load(); }, []);

  return (
    <div>
      <h2>Hợp đồng</h2>
      <p style={{ color: "#657086", marginBottom: 16 }}>Hợp đồng và thỏa thuận Chủ sở hữu - Kỵ sĩ.</p>
      {msg && <p className="admin-notice">{msg}</p>}
      {items.length === 0 ? <p className="muted">Không có hợp đồng.</p> : (
        <div className="admin-table-wrap"><table className="admin-table">
          <thead><tr><th>Tiêu đề</th><th>Chủ sở hữu</th><th>Kỵ sĩ</th><th>Ngựa</th><th>Trạng thái</th><th>Thời hạn</th><th>Phí</th></tr></thead>
          <tbody>{items.map((c) => <tr key={c.id}><td>{c.title}</td><td>{c.ownerName || "-"}</td><td>{c.jockeyName || "-"}</td><td>{c.horseName || "-"}</td><td><span className={`status status--${c.status?.toLowerCase()}`}>{c.status}</span></td><td>{fDate(c.startDate)} - {fDate(c.endDate)}</td><td>{c.baseFee ? `$${c.baseFee}` : "-"}</td></tr>)}</tbody>
        </table></div>
      )}
    </div>
  );
}

// ── Injury Management ──
export function InjuryManagement() {
  const [items, setItems] = useState([]);
  const [msg, setMsg] = useState("");
  const load = () => getInjuries().then((d) => setItems(Array.isArray(d) ? d : [])).catch((e) => setMsg(e.message));
  useEffect(() => { load(); }, []);

  return (
    <div>
      <h2>Hồ sơ chấn thương</h2>
      <p style={{ color: "#657086", marginBottom: 16 }}>Theo dõi chấn thương ngựa, điều trị và tình trạng hồi phục.</p>
      {msg && <p className="admin-notice">{msg}</p>}
      {items.length === 0 ? <p className="muted">Không có hồ sơ chấn thương.</p> : (
        <div className="admin-table-wrap"><table className="admin-table">
          <thead><tr><th>Ngựa</th><th>Chấn thương</th><th>Mức độ</th><th>Trạng thái</th><th>Bác sĩ thú y</th><th>Chẩn đoán</th><th>Đã khỏi</th></tr></thead>
          <tbody>{items.map((r) => <tr key={r.id}><td>{r.horseName || r.horseId}</td><td>{r.injuryType}</td><td><span className="status status--inactive">{r.severity}</span></td><td><span className={`status status--${r.status?.toLowerCase()}`}>{r.status}</span></td><td>{r.veterinarianName || "-"}</td><td>{fDate(r.diagnosedAt)}</td><td>{r.clearedToRace ? "Có" : "Không"}</td></tr>)}</tbody>
        </table></div>
      )}
    </div>
  );
}
