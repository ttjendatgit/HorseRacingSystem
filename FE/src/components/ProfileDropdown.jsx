import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { getBalance } from "../services/walletApi";

const getInitials = (name) => (name || "Người dùng").split(" ").map((w) => w[0]).join("").slice(0, 2).toUpperCase();

export default function ProfileDropdown({ profileUrl }) {
  const [open, setOpen] = useState(false);
  const [user, setUser] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    try {
      const stored = JSON.parse(localStorage.getItem("authUser") || "null");
      setUser(stored);
    } catch { setUser(null); }
  }, []);

  const [balance, setBalance] = useState(null);
  useEffect(() => {
    const stored = JSON.parse(localStorage.getItem("authUser") || "null");
    if (stored?.role === "spectator" || stored?.role === "jockey" || stored?.role === "horse_owner") {
      getBalance()
        .then((d) => { const b = d?.data ?? d; setBalance(b?.balance ?? b?.Balance ?? null); })
        .catch(() => {});
    }
  }, []);

  const handleLogout = () => {
    localStorage.removeItem("authToken");
    localStorage.removeItem("authUser");
    localStorage.removeItem("refreshToken");
    navigate("/login");
  };

  const fullName = user?.fullName || user?.email || "Người dùng";
  const email = user?.email || "";
  const role = user?.role || "người dùng";

  const ROLE_LABELS = {
    horse_owner: "Chủ Ngựa",
    jockey: "Kỵ Sĩ",
    spectator: "Khán giả",
    referee: "Trọng tài",
    admin: "Quản trị viên",
  };

  return (
    <div className="profile-dropdown" style={{ position: "relative" }}>
      <button
        onClick={() => setOpen(!open)}
        style={{
          display: "flex", alignItems: "center", gap: 10,
          background: "rgba(238,229,212,0.05)",
          backdropFilter: "blur(8px)", WebkitBackdropFilter: "blur(8px)",
          border: "1px solid rgba(184,134,59,.25)", borderRadius: 9,
          padding: "6px 14px 6px 6px", cursor: "pointer", color: "#eee5d4",
        }}
      >
        <span style={{
          width: 32, height: 32, borderRadius: 8,
          background: "linear-gradient(135deg, #d1a75c, #b8863b)",
          display: "flex", alignItems: "center", justifyContent: "center",
          color: "#18120c", fontWeight: 700, fontSize: 13,
        }}>
          {getInitials(fullName)}
        </span>
        <span style={{ fontSize: 13, textAlign: "left" }}>
          <span style={{ display: "block", color: "#eee5d4", lineHeight: 1.2 }}>{fullName}</span>
          <span style={{ display: "block", color: "rgba(238,229,212,0.55)", fontSize: 11, lineHeight: 1.2 }}>{ROLE_LABELS[role] || role}</span>
        </span>
      </button>

      {open && (
        <>
          <div style={{ position: "fixed", inset: 0, zIndex: 99 }} onClick={() => setOpen(false)} />
          <div style={{
            position: "absolute", right: 0, top: "calc(100% + 8px)", zIndex: 100,
            minWidth: 220, background: "#1a1511", border: "1px solid rgba(184,134,59,.18)",
            borderRadius: 10, padding: 16, boxShadow: "0 16px 36px rgba(0,0,0,0.35)",
          }}>
            <div style={{ marginBottom: 12, paddingBottom: 12, borderBottom: "1px solid rgba(238,229,212,.08)" }}>
              <p style={{ color: "#eee5d4", fontWeight: 600, margin: 0, fontSize: 14 }}>{fullName}</p>
              <p style={{ color: "#aa9d8a", margin: "4px 0 0", fontSize: 12, wordBreak: "break-all" }}>{email}</p>
            </div>
            {balance !== null && (
              <div style={{ padding: "8px 10px", marginBottom: 8, borderRadius: 6, background: "rgba(184,134,59,0.08)", border: "1px solid rgba(184,134,59,0.14)" }}>
                <p style={{ margin: 0, fontSize: 11, color: "#aa9d8a", textTransform: "uppercase" }}>Số dư ví</p>
                <p style={{ margin: "2px 0 0", fontSize: 15, fontWeight: 700, color: "#c9a563" }}>
                  {Number(balance).toLocaleString()} điểm
                </p>
              </div>
            )}
            {profileUrl && (
              <Link to={profileUrl} onClick={() => setOpen(false)} style={{
                display: "block", padding: "8px 10px", borderRadius: 6, color: "#e5d9c6",
                textDecoration: "none", fontSize: 13, marginBottom: 4,
              }} onMouseOver={(e) => e.target.style.background = "rgba(184,134,59,.10)"} onMouseOut={(e) => e.target.style.background = "none"}>
                Hồ sơ
              </Link>
            )}
            {role === "jockey" && (
              <Link to="/jockey/wallet" onClick={() => setOpen(false)} style={{
                display: "block", padding: "8px 10px", borderRadius: 6, color: "#e5d9c6",
                textDecoration: "none", fontSize: 13, marginBottom: 4,
              }} onMouseOver={(e) => e.target.style.background = "rgba(184,134,59,.10)"} onMouseOut={(e) => e.target.style.background = "none"}>
                Ví của tôi
              </Link>
            )}
            <button onClick={handleLogout} style={{
              display: "block", width: "100%", textAlign: "left", padding: "8px 10px",
              borderRadius: 6, border: "none", background: "none", color: "#c9695a",
              cursor: "pointer", fontSize: 13, marginTop: 2,
            }} onMouseOver={(e) => e.target.style.background = "rgba(201,105,90,.10)"} onMouseOut={(e) => e.target.style.background = "none"}>
              Đăng xuất
            </button>
          </div>
        </>
      )}
    </div>
  );
}
