// Reuses AdminPage.jsx's ConfirmModal CSS classes (hr-modal-overlay/hr-modal/hr-modal__message/
// hr-modal__actions/hr-btn) for visual consistency, but is its own component since ConfirmModal
// itself is not exported from AdminPage.jsx. Shared by TournamentDetail.jsx and
// TournamentManagementPage.jsx (and any other Admin page that wants a popup instead of a
// single-line <p className="admin-notice"> banner) so the modal markup exists in exactly one
// place. Multi-line backend messages (e.g. RaceEntryService's "Entry chưa đủ điều kiện xuất
// phát:\n<ngựa 1> [lý do]\n...") render as a list; a plain single-sentence message (e.g. most
// TournamentAndRoundService.ChangeStatusAsync errors) renders as one paragraph, not a
// one-item list.
export default function ErrorDetailModal({ message, onClose }) {
  if (!message) return null;
  const lines = message.split("\n").map((s) => s.trim()).filter(Boolean);
  return (
    <div className="hr-modal-overlay" role="presentation" onClick={onClose}>
      <div className="hr-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
        {lines.length > 1 ? (
          <ul className="hr-modal__message" style={{ margin: 0, paddingLeft: 18, display: "grid", gap: 6 }}>
            {lines.map((line, i) => <li key={i}>{line}</li>)}
          </ul>
        ) : (
          <p className="hr-modal__message">{lines[0] ?? message}</p>
        )}
        <div className="hr-modal__actions">
          <button type="button" className="hr-btn hr-btn--primary" onClick={onClose}>Đóng</button>
        </div>
      </div>
    </div>
  );
}
