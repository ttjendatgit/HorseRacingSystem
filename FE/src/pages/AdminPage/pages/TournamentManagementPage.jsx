import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  createTournament,
  deleteTournament,
  getAdminTournaments,
  getTournamentRaces,
  getTournamentRounds,
  updateTournament,
} from "../../../services/adminApi";
import { request } from "../../../services/apiClient";
import {
  RaceButton,
  RaceEmptyState,
  RaceStatusBadge,
  RaceTabs,
} from "../../../components/ui/RaceUi";
import TournamentForm from "../../../components/TournamentForm";
import TournamentDetail from "./TournamentDetail";
import { apiToVNInput, apiToVNDisplay, vnInputToApiUtc, vnNowInput } from "../../../utils/vnDateTime";
import {
  canEditTournamentStructure,
  filterTournamentsByStatusTab,
  getTournamentCardActions,
  getTournamentLifecycleLabel,
  getTournamentStatusTabCounts,
  getTournamentThumbnailUrl,
  normalizeTournamentStatus,
  resolveTournamentPageView,
  TOURNAMENT_STATUS_TABS,
} from "../../../utils/tournamentRegistration";

const formatDateTime = (value) => (value ? apiToVNDisplay(value) : "-");
const inputDate = (days = 0) => vnNowInput(days);

// RaceStatusBadge only has CSS for success/warning/danger/info (RaceUi.css) — anything else
// silently falls back to the plain neutral-gray base style, which is what we want for Draft.
const STATUS_BADGE_VARIANT = {
  Published: "info",
  Ongoing: "warning",
  Finished: "success",
  Cancelled: "danger",
};

function TournamentMedia({ imageUrl, name }) {
  return (
    <div className="tm-card__media">
      {imageUrl ? (
        <img src={imageUrl} alt={name} loading="lazy" />
      ) : (
        <svg className="tm-card__media-placeholder" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" width="26" height="26" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 15.75l4.5-4.5a2.121 2.121 0 013 0l5.25 5.25M13.5 12l1.5-1.5a2.121 2.121 0 013 0l2.25 2.25M3 5.25h18v13.5H3V5.25z" />
          <circle cx="8" cy="9" r="1.25" />
        </svg>
      )}
    </div>
  );
}

function TournamentCard({ item, onView, onEdit, onDelete, onTransition }) {
  const id = item.id ?? item.Id;
  const statusKey = normalizeTournamentStatus(item);
  const isQuiet = statusKey === "Finished" || statusKey === "Cancelled";
  const actions = getTournamentCardActions(item);
  const roundCount = item.roundCount ?? item.RoundCount ?? 0;
  const raceCount = item.raceCount ?? item.RaceCount ?? 0;
  const name = item.name ?? item.Name;

  return (
    <article className={`tm-card${isQuiet ? " tm-card--muted" : ""}`}>
      <TournamentMedia imageUrl={getTournamentThumbnailUrl(item)} name={name} />
      <div className="tm-card__header">
        <RaceStatusBadge variant={STATUS_BADGE_VARIANT[statusKey] ?? "neutral"}>
          {getTournamentLifecycleLabel(item)}
        </RaceStatusBadge>
        <h3 className="tm-card__title">{name}</h3>
      </div>

      <div className="tm-card__body">
        <p className="tm-card__desc">{item.description ?? item.Description ?? "Không có mô tả"}</p>
        <p className="tm-card__dates">
          {formatDateTime(item.startDate ?? item.StartDate)} → {formatDateTime(item.endDate ?? item.EndDate)}
        </p>
        <p className="tm-card__counts">{roundCount} vòng đấu · {raceCount} cuộc đua</p>
      </div>

      <div className="tm-card__footer">
        <RaceButton size="compact" variant="ghost" onClick={() => onView(item)}>
          Xem chi tiết
        </RaceButton>
        {actions.canEdit && (
          <RaceButton size="compact" variant="ghost" onClick={() => onEdit(item)}>
            Sửa
          </RaceButton>
        )}
        {actions.transitions.map((t) => (
          <RaceButton
            key={t.status}
            size="compact"
            variant={t.isPrimary ? "primary" : "ghost"}
            onClick={() => onTransition(id, t.status)}
          >
            {t.label}
          </RaceButton>
        ))}
        {actions.canDelete && (
          <RaceButton size="compact" variant="danger" onClick={() => onDelete(id)}>
            Xóa
          </RaceButton>
        )}
      </div>
    </article>
  );
}

export default function TournamentManagementPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState("all");
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState("");
  const [editingStatus, setEditingStatus] = useState(null);
  const [message, setMessage] = useState("");
  // PRIZE-V1.1 PART 12: shown only when Publish fails specifically because of Prize
  // configuration — detected from the backend's own error text, not a separate API call.
  const [showPrizeCta, setShowPrizeCta] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [selectedT, setSelectedT] = useState(null);
  const [form, setForm] = useState({ name: "", description: "", venue: "", startDate: inputDate(7), endDate: inputDate(14), prizePool: 0, imageUrl: "", minParticipants: 3, maxParticipants: 10, maxRounds: 1 });
  const isDraft = canEditTournamentStructure(editingStatus);

  const load = () => {
    setLoading(true);
    getAdminTournaments()
      .then((data) => {
        setItems(Array.isArray(data) ? data : []);
        setError("");
      })
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  };
  useEffect(() => { load(); }, []);

  const counts = useMemo(() => getTournamentStatusTabCounts(items), [items]);
  const visibleItems = useMemo(() => filterTournamentsByStatusTab(items, activeTab), [items, activeTab]);
  const tabs = TOURNAMENT_STATUS_TABS.map((tab) => ({ ...tab, count: counts[tab.value] }));

  const handleUpload = async (file) => {
    if (!file) return;
    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const res = await request("/api/auth/upload-document", { method: "POST", body: formData });
      const d = res?.data ?? res;
      setForm((prev) => ({ ...prev, imageUrl: d?.url ?? "" }));
    } catch (e) {
      setMessage("Tải ảnh thất bại: " + (e.message ?? ""));
    }
    setUploading(false);
  };

  const submit = async (event) => {
    event.preventDefault();
    try {
      // Phase4B: when editing a Published tournament, omit immutable fields from the payload
      // so the BE never sees them. Draft edits send everything.
      const isPublished = editingId && editingStatus === 1; // TournamentStatus.Published == 1
      const payload = {
        name: form.name,
        description: form.description,
        imageUrl: form.imageUrl,
      };
      if (!isPublished) {
        payload.startDate = vnInputToApiUtc(form.startDate);
        payload.endDate = vnInputToApiUtc(form.endDate);
        payload.minParticipants = Number(form.minParticipants);
        payload.maxParticipants = Number(form.maxParticipants);
      }
      // V0.1 micro-fix: MaxRounds is structural (drives V0 Final identity) and is locked for
      // EVERY non-Draft status (Published, Ongoing, Finished, Cancelled) — not just Published.
      if (isDraft) {
        payload.maxRounds = Number(form.maxRounds);
      }
      if (editingId) await updateTournament(editingId, payload);
      else {
        payload.startDate = vnInputToApiUtc(form.startDate);
        payload.endDate = vnInputToApiUtc(form.endDate);
        payload.minParticipants = Number(form.minParticipants);
        payload.maxParticipants = Number(form.maxParticipants);
        payload.maxRounds = Number(form.maxRounds);
        await createTournament(payload);
      }
      setMessage(`Giải đấu ${editingId ? "đã cập nhật" : "đã tạo"} thành công.`);
      setShowForm(false); setEditingId(""); setEditingStatus(null); load();
    } catch (err) { setMessage(err.message); }
  };

  const edit = (item) => {
    setEditingId(item.id ?? item.Id);
    setEditingStatus(item.status ?? item.Status ?? null);
    setForm({
      name: item.name ?? item.Name ?? "",
      description: item.description ?? item.Description ?? "",
      startDate: apiToVNInput(item.startDate ?? item.StartDate),
      endDate: apiToVNInput(item.endDate ?? item.EndDate),
      imageUrl: item.imageUrl ?? item.ImageUrl ?? "",
      minParticipants: item.minParticipants ?? item.MinParticipants ?? 3,
      maxParticipants: item.maxParticipants ?? item.MaxParticipants ?? 10,
      maxRounds: item.maxRounds ?? item.MaxRounds ?? 1,
    });
    setShowForm(true);
  };

  const remove = async (id) => {
    if (!window.confirm("Xóa giải đấu này?")) return;
    try { await deleteTournament(id); setMessage("Đã xóa giải đấu."); load(); } catch (err) { setMessage(err.message); }
  };

  const viewT = (item) => { setMessage(""); setSelectedT(item); };

  // TournamentStatus.Cancelled backend enum value (BE/Models/Enums.cs) — NextTransitionDto.Status
  // serializes as the raw int (no global string enum converter).
  const CANCELLED_STATUS = 4;

  const [cancellingTournamentId, setCancellingTournamentId] = useState(null);
  const [cancelReasonText, setCancelReasonText] = useState("");

  const changeStatus = async (id, newStatus) => {
    setShowPrizeCta(false);
    if (newStatus === CANCELLED_STATUS) {
      setCancellingTournamentId(id);
      setCancelReasonText("");
      return;
    }
    try {
      const body = { newStatus };
      await request(`/api/tournaments/${id}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      setMessage("Đã cập nhật trạng thái giải đấu.");
      load();
    } catch (err) {
      setMessage(err.message);
      setShowPrizeCta(typeof err.message === "string" && err.message.includes("giải thưởng"));
    }
  };

  const confirmCancelTournament = async () => {
    if (!cancellingTournamentId) return;
    const reasonStr = cancelReasonText.trim();
    if (!reasonStr) {
      alert("Lý do hủy giải đấu không được để trống.");
      return;
    }
    const id = cancellingTournamentId;
    try {
      await request(`/api/tournaments/${id}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ newStatus: CANCELLED_STATUS, reason: reasonStr }),
      });
      setMessage("Đã hủy giải đấu.");
      setCancellingTournamentId(null);
      load();
    } catch (err) {
      setMessage(err.message);
    }
  };

  if (resolveTournamentPageView(selectedT) === "detail") {
    return (
      <TournamentDetail
        t={selectedT}
        onBack={() => { setMessage(""); setSelectedT(null); }}
        message={message}          // ADD
        setMessage={setMessage}
        getTournamentRaces={getTournamentRaces}
        getTournamentRounds={getTournamentRounds}
      />
    );
  }

  return (
    <>
      <section className="admin-title">
        <div>
          <span>Quản lý giải đấu</span>
          <h1>Giải đấu</h1>
          <p>Tạo và quản lý vòng đấu, cuộc đua và vòng đời giải đấu.</p>
        </div>
        <RaceButton onClick={() => { setEditingId(""); setShowForm(true); }}>Tạo giải đấu</RaceButton>
      </section>

      {message && <p className="admin-notice">{message}</p>}
      {error && <p className="admin-notice admin-notice--error">{error}</p>}
      {showPrizeCta && (
        <RaceButton variant="ghost" size="compact" style={{ marginBottom: 16 }} onClick={() => navigate("/admin/prizes")}>
          Đi tới cấu hình giải thưởng
        </RaceButton>
      )}

      {showForm && !editingId && (
        <TournamentForm
          onClose={() => setShowForm(false)}
          onSuccess={() => {
            setShowForm(false);
            setMessage("Giải đấu đã tạo thành công.");
            load();
          }}
        />
      )}
      {showForm && editingId && <form className="admin-form" onSubmit={submit}>
        {editingStatus === 1 && <p style={{ color: "var(--hr-gold-soft)", fontSize: 13, marginBottom: 8 }}>⚠ Giải đấu đã công bố — chỉ có thể sửa Tên, Mô tả và Ảnh bìa.</p>}
        <input placeholder="Tên giải đấu" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        <input placeholder="Mô tả" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
          Thời gian bắt đầu *
          <input type="datetime-local" required value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} min={inputDate(0)} disabled={editingStatus === 1} />
        </label>
        <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
          Thời gian kết thúc *
          <input type="datetime-local" required value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} min={inputDate(0)} disabled={editingStatus === 1} />
        </label>
        {editingStatus !== 1 && <p style={{ margin: "-8px 0 8px", fontSize: 12, color: "var(--hr-muted)" }}>Giải đấu có thể bắt đầu và kết thúc trong cùng một ngày, miễn thời gian kết thúc sau thời gian bắt đầu.</p>}
        <input type="number" placeholder="Số người tham gia tối thiểu" required min="3" value={form.minParticipants} onChange={(e) => setForm({ ...form, minParticipants: e.target.value })} disabled={editingStatus === 1} />
        <input type="number" placeholder="Số người tham gia tối đa" required min="1" value={form.maxParticipants} onChange={(e) => setForm({ ...form, maxParticipants: e.target.value })} disabled={editingStatus === 1} />
        <label style={{ display: "block", fontSize: 13, color: "var(--hr-muted)", marginBottom: 4 }}>
          Số vòng đấu *
          <input type="number" required min="1" step="1" value={form.maxRounds} onChange={(e) => setForm({ ...form, maxRounds: e.target.value })} disabled={!isDraft} />
        </label>
        <label style={{ fontSize: 13, color: "var(--hr-muted)" }}>Ảnh bìa giải đấu (tỉ lệ 3:1, đề xuất 1200×400px):
          <input type="file" accept="image/*" onChange={(e) => { const f = e.target.files?.[0]; if (f) handleUpload(f); }} style={{ display: "block", marginTop: 4 }} />
          {uploading ? <span style={{ color: "var(--hr-gold-soft)", fontSize: 12 }}>Đang tải ảnh...</span> : null}
        </label>
        {form.imageUrl && <img src={form.imageUrl} alt="Ảnh bìa giải đấu xem trước" style={{ width: 120, borderRadius: 8, marginTop: 4 }} />}
        <div style={{ display: "flex", gap: 8 }}>
          <button className="primary-button" disabled={uploading}>Lưu giải đấu</button>
          <button type="button" className="ghost-button" onClick={() => { setShowForm(false); setEditingId(""); setEditingStatus(null); }}>Hủy</button>
        </div>
      </form>}

      <div className="tm-toolbar">
        <RaceTabs
          tabs={tabs}
          activeValue={activeTab}
          onChange={setActiveTab}
          ariaLabel="Lọc giải đấu theo vòng đời"
          idPrefix="tm-tab"
          panelId="tm-panel"
        />
      </div>

      <section id="tm-panel" role="tabpanel" aria-labelledby={`tm-tab-${activeTab}`}>
        {loading ? (
          <p className="rr-state-card"><strong>Đang tải giải đấu</strong> <span>Vui lòng chờ trong giây lát.</span></p>
        ) : visibleItems.length === 0 ? (
          <RaceEmptyState
            title="Không có giải đấu trong nhóm này"
            description={items.length === 0 ? "Chưa có giải đấu nào — bắt đầu bằng cách tạo giải đấu đầu tiên." : "Chọn tab khác hoặc tạo giải đấu mới."}
          />
        ) : (
          <div className="tm-grid">
            {visibleItems.map((item) => (
              <TournamentCard
                key={item.id ?? item.Id}
                item={item}
                onView={viewT}
                onEdit={edit}
                onDelete={remove}
                onTransition={changeStatus}
              />
            ))}
          </div>
        )}
      </section>

      {cancellingTournamentId && (
        <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.65)", zIndex: 9999, display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
          <div style={{ width: "100%", maxWidth: 460, background: "var(--hr-surface, #1e293b)", border: "1px solid var(--hr-border, #334155)", borderRadius: 12, padding: 20, boxShadow: "0 20px 25px -5px rgba(0,0,0,0.5)" }}>
            <h3 style={{ margin: "0 0 12px", color: "var(--hr-paper, #f8fafc)", fontSize: 16 }}>❌ Hủy Giải Đấu</h3>
            <p style={{ margin: "0 0 12px", fontSize: 13, color: "var(--hr-muted, #94a3b8)" }}>
              Vui lòng nhập rõ lý do hủy giải đấu này:
            </p>
            <textarea
              style={{ width: "100%", height: 90, padding: 10, borderRadius: 8, border: "1px solid var(--hr-border, #475569)", background: "var(--hr-bg-deep, #0f172a)", color: "#f8fafc", fontSize: 13, resize: "none" }}
              placeholder="Nhập lý do hủy giải đấu..."
              value={cancelReasonText}
              onChange={(e) => setCancelReasonText(e.target.value)}
            />
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button
                type="button"
                className="ghost-button"
                style={{ padding: "6px 14px", fontSize: 12 }}
                onClick={() => setCancellingTournamentId(null)}
              >
                Hủy
              </button>
              <button
                type="button"
                style={{ padding: "6px 14px", fontSize: 12, borderRadius: 6, border: "none", background: "#ef4444", color: "#fff", fontWeight: 700, cursor: "pointer" }}
                onClick={confirmCancelTournament}
              >
                Xác nhận hủy giải
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
