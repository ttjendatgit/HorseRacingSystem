/* eslint-disable react-refresh/only-export-components */

export const detailBox = { padding: 16, border: "1px solid rgba(231,198,120,.1)", borderRadius: 12, background: "rgba(255, 255, 255, 0.88)" };
export const profileLabel = { display: "block", color: "#657086", fontSize: 11, textTransform: "uppercase", marginBottom: 6 };
export const profileValue = { display: "block", color: "#34415b", fontSize: 14, fontWeight: 600, wordBreak: "break-word" };
export const grid2 = { display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(180px,1fr))", gap: 14 };
export const msgBox = (t) => ({
  padding: "12px 16px",
  borderRadius: 10,
  fontSize: 14,
  fontWeight: 500,
  background: t === "success" ? "#e6f7e6" : "#fde8e8",
  color: t === "success" ? "#1a7d1a" : "#c41e1e",
  border: `1px solid ${t === "success" ? "#b7e6b7" : "#f5c6c6"}`,
});

export const Detail = ({ label: l, value: v }) => (
  <div style={detailBox}>
    <span style={profileLabel}>{l}</span>
    <strong style={profileValue}>{v ?? "-"}</strong>
  </div>
);

export const fieldStyle = { marginBottom: 20 };
export const fieldLabel = { display: "block", fontSize: 13, fontWeight: 600, color: "#34415b", marginBottom: 6 };
export const inputBase = {
  width: "100%", padding: "10px 14px", fontSize: 14, borderRadius: 10,
  border: "1.5px solid rgba(143,100,32,0.2)", background: "#fff",
  color: "#172033", outline: "none", boxSizing: "border-box",
};
export const btnPrimary = {
  padding: "10px 28px", fontSize: 14, fontWeight: 600, borderRadius: 10,
  border: "none", background: "linear-gradient(135deg,#8f6420,#b8862d)", color: "#fff",
  cursor: "pointer", transition: "opacity .2s",
};
export const btnSecondary = {
  padding: "10px 28px", fontSize: 14, fontWeight: 600, borderRadius: 10,
  border: "1.5px solid rgba(143,100,32,0.2)", background: "#fff",
  color: "#8f6420", cursor: "pointer", transition: "all .2s",
};

export function Field({ label: l, value, onChange, type = "text", placeholder, readOnly }) {
  return (
    <div style={fieldStyle}>
      <label style={fieldLabel}>{l}</label>
      <input
        style={inputBase}
        type={type}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        readOnly={readOnly}
        onFocus={(e) => { e.target.style.borderColor = "#8f6420"; }}
        onBlur={(e) => { e.target.style.borderColor = "rgba(143,100,32,0.2)"; }}
      />
    </div>
  );
}

export function StatusBadge({ status }) {
  const s = (status ?? "").toLowerCase();
  const colors = {
    won: { bg: "#e6f7e6", color: "#1a7d1a" },
    lost: { bg: "#fde8e8", color: "#c41e1e" },
    pending: { bg: "#fff3cd", color: "#856404" },
    completed: { bg: "#e6f7e6", color: "#1a7d1a" },
    rejected: { bg: "#fde8e8", color: "#c41e1e" },
  };
  const c = colors[s] ?? { bg: "#e9ecef", color: "#495057" };
  const labels = {
    completed: "Đã chuyển",
    rejected: "Từ chối",
    pending: "Chờ xử lý",
    won: "Thắng",
    lost: "Thua",
  };
  return (
    <span style={{
      display: "inline-block", padding: "3px 10px", borderRadius: 20,
      fontSize: 12, fontWeight: 600, ...c,
    }}>
      {labels[s] ?? status ?? "-"}
    </span>
  );
}

export function ProfileLayout({ profile, roleLabel, walletBalance, tabs, activeTab, setActiveTab, children }) {
  return (
    <div className="spectator-page">
      <div className="sp-profile-layout">
        <aside className="sp-profile-sidebar">
          <div className="sp-avatar-wrap">
            <div className="sp-avatar">
              {(profile.fullName ?? profile.FullName ?? "?")[0].toUpperCase()}
            </div>
            <h3 className="sp-name">{profile.fullName ?? profile.FullName ?? "Người dùng"}</h3>
            <span className="pill">{roleLabel}</span>
          </div>

          {walletBalance !== null && (
            <div style={{ padding: "12px 16px", borderRadius: 12, background: "rgba(143,100,32,0.07)", border: "1px solid rgba(143,100,32,0.12)", textAlign: "center", marginBottom: 4 }}>
              <p style={{ margin: "0 0 2px", fontSize: 11, color: "#657086", textTransform: "uppercase" }}>Số dư ví</p>
              <strong style={{ fontSize: 18, color: "#8f6420" }}>{Number(walletBalance).toLocaleString()} điểm</strong>
            </div>
          )}

          <nav className="sp-sidebar-nav">
            {tabs.map((t) => (
              <button
                key={t.key}
                className={`sp-nav-btn${activeTab === t.key ? " active" : ""}`}
                onClick={() => setActiveTab(t.key)}
              >
                {t.label}
              </button>
            ))}
          </nav>

          <div className="sp-sidebar-footer">
            <p className="muted">Tham gia {profile.createdAt ? new Date(profile.createdAt).toLocaleDateString() : "-"}</p>
          </div>
        </aside>

        <div className="sp-profile-content">
          {children}
        </div>
      </div>
    </div>
  );
}
