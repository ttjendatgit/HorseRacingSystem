import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getPendingProtests, getProtests, markProtestUnderReview, ruleProtest,
  getPendingTransfers, approveTransfer, rejectTransfer,
  getContracts,
  getInjuries,
} from "../../services/managementApi";
import {
  buildRuleProtestPayload,
  filterProtestsByTab,
  getAvailableProtestActions,
  getDefaultProtestTab,
  getProtestStatusDetails,
  getProtestTabCounts,
} from "../../utils/protestDisplay";

const fDate = (v) => v ? new Date(v).toLocaleDateString("vi-VN", { dateStyle: "medium" }) : "-";

// ── Reusable Modal ──
function Modal({ title, children, onClose }) {
  return (
    <div className="modal-overlay" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="admin-modal-panel" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 500 }}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="ghost-button" onClick={onClose}>Đóng</button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}

// ── Protest Management ──
function LegacyProtestManagement() {
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
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>Xem xét và phán quyết khiếu nại cuộc đua từ chủ sở hữu và kỵ sĩ.</p>
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

// ── Protest Management ──
export function ProtestManagement() {
  const navigate = useNavigate();
  const [items, setItems] = useState([]);
  const [msg, setMsg] = useState("");
  const [showResultCta, setShowResultCta] = useState(false);
  const [activeTab, setActiveTab] = useState("pending");
  const [modal, setModal] = useState(null); // { id, outcome: "Upheld"|"Rejected" }
  const [modalText, setModalText] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const load = async () => {
    setIsLoading(true);
    try {
      const data = await getProtests();
      const nextItems = Array.isArray(data) ? data : [];
      setItems(nextItems);
      setActiveTab((current) => current || getDefaultProtestTab(getProtestTabCounts(nextItems)));
    } catch (e) {
      setMsg(e.message);
      setShowResultCta(false);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const counts = useMemo(() => getProtestTabCounts(items), [items]);
  const visibleItems = useMemo(() => filterProtestsByTab(items, activeTab), [items, activeTab]);
  const tabs = [
    { key: "pending", label: "Chờ xử lý", count: counts.pending },
    { key: "underReview", label: "Đang xem xét", count: counts.underReview },
    { key: "resolved", label: "Đã xử lý", count: counts.resolved },
    { key: "all", label: "Tất cả", count: counts.all },
  ];

  const openRuleModal = (id, outcome) => {
    setModal({ id, outcome });
    setModalText("");
  };

  const startReview = async (id) => {
    try {
      await markProtestUnderReview(id);
      setMsg("Khiếu nại đã được chuyển sang đang xem xét.");
      setShowResultCta(false);
      load();
    } catch (e) {
      setMsg(e.message);
      setShowResultCta(false);
    }
  };

  const submitRule = async () => {
    if (!modal) return;
    try {
      await ruleProtest(modal.id, buildRuleProtestPayload(modal.outcome, modalText));
      setMsg(modal.outcome === "Upheld"
        ? "Khiếu nại được chấp nhận. Kết quả cuộc đua phải được chỉnh sửa và gửi lại trước khi có thể xác nhận chính thức."
        : "Khiếu nại đã bị bác.");
      setShowResultCta(modal.outcome === "Upheld");
      setModal(null);
      load();
    } catch (e) {
      setMsg(e.message);
      setShowResultCta(false);
    }
  };

  return (
    <div>
      <h2>Khiáº¿u náº¡i</h2>
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>Xem xÃ©t vÃ  phÃ¡n quyáº¿t khiáº¿u náº¡i cuá»™c Ä‘ua tá»« chá»§ sá»Ÿ há»¯u vÃ  ká»µ sÄ©.</p>
      {msg && (
        <div className="admin-notice" style={{ display: "flex", gap: 12, alignItems: "center", justifyContent: "space-between", flexWrap: "wrap" }}>
          <span>{msg}</span>
          {showResultCta && (
            <button type="button" className="ghost-button" onClick={() => navigate("/admin/race-results")}>
              Đi đến kết quả cuộc đua
            </button>
          )}
        </div>
      )}

      <div className="admin-actions" style={{ marginBottom: 16 }}>
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            className={activeTab === tab.key ? "primary-button" : ""}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label} ({tab.count})
          </button>
        ))}
      </div>

      {modal && (
        <Modal title={modal.outcome === "Upheld" ? "Chấp nhận khiếu nại" : "Bác khiếu nại"} onClose={() => setModal(null)}>
          <div className="form-group">
            <label>Ghi chú quyết định</label>
            <textarea
              className="form-textarea"
              rows={4}
              value={modalText}
              onChange={(e) => setModalText(e.target.value)}
              placeholder={modal.outcome === "Upheld" ? "Mô tả cách giải quyết..." : "Lý do bác khiếu nại..."}
            />
          </div>
          <div className="modal-actions" style={{ marginTop: 16 }}>
            <button className="primary-button" onClick={submitRule}>{modal.outcome === "Upheld" ? "Chấp nhận" : "Bác khiếu nại"}</button>
            <button className="ghost-button" onClick={() => setModal(null)}>Há»§y</button>
          </div>
        </Modal>
      )}

      {isLoading ? <p className="muted">Đang tải khiếu nại...</p> : visibleItems.length === 0 ? <p className="muted">Không có khiếu nại trong nhóm này.</p> : (
        <div className="admin-card-grid">
          {visibleItems.map((p) => {
            const id = p.id ?? p.Id;
            const status = getProtestStatusDetails(p.status ?? p.Status);
            const actions = getAvailableProtestActions(status.status);
            return (
              <article key={id} className="admin-simple-card">
                <span className={`status status--${status.variant}`}>{status.label}</span>
                <h3>{p.filedByName || p.FiledByName || "Không xác định"}</h3>
                <p>Cuá»™c Ä‘ua: {p.raceName || p.RaceName || p.raceId || p.RaceId}</p>
                <p>Chá»‘ng láº¡i: {p.againstHorseName || p.AgainstHorseName || p.againstEntryId || p.AgainstEntryId}</p>
                <p><strong>LÃ½ do:</strong> {p.reason || p.Reason}</p>
                {(p.evidence || p.Evidence) && <p><strong>Báº±ng chá»©ng:</strong> {p.evidence || p.Evidence}</p>}
                <p className="time">Ná»™p: {fDate(p.filedAt || p.FiledAt)}</p>
                {actions.length > 0 && (
                  <div className="admin-actions" style={{ marginTop: 8 }}>
                    {actions.includes("underReview") && <button onClick={() => startReview(id)}>Bắt đầu xem xét</button>}
                    {actions.includes("upheld") && <button onClick={() => openRuleModal(id, "Upheld")}>Chấp nhận</button>}
                    {actions.includes("rejected") && <button className="admin-danger" onClick={() => openRuleModal(id, "Rejected")}>Bác khiếu nại</button>}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}

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
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>Xem xét và phê duyệt chuyển nhượng quyền sở hữu ngựa.</p>
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
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>Hợp đồng và thỏa thuận Chủ sở hữu - Kỵ sĩ.</p>
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
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>Theo dõi chấn thương ngựa, điều trị và tình trạng hồi phục.</p>
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
