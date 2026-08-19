import { useEffect, useState, useCallback, useRef } from "react";
import { updateProfile, changePassword, getProfile, createDeposit, checkDeposit, getDepositHistory } from "../services/authApi";
import { getMyPredictions } from "../services/spectatorApi";
import { getBalance } from "../services/walletApi";
import { saveBankAccount, getBankAccounts, createWithdrawal, getWithdrawalHistory } from "../services/withdrawalApi";
import { Field, Detail, StatusBadge, msgBox, grid2, btnPrimary, btnSecondary, fieldStyle, fieldLabel, inputBase } from "./ProfileCommon";
import "./ProfilePages.css";

// Sử dụng API của vietqr.io để tạo mã QR
const vietQrUrl = (tx, amount, reference) => {
  const acc = encodeURIComponent(tx?.accountNumber ?? tx?.AccountNumber ?? "");
  const bank = encodeURIComponent(tx?.bankCode ?? tx?.BankCode ?? "MB");
  const holder = encodeURIComponent(tx?.accountHolder ?? tx?.AccountHolder ?? "");
  return `https://img.vietqr.io/image/${bank}-${acc}-compact2.png?amount=${Number(amount || 0)}&addInfo=${encodeURIComponent(reference || "")}&accountName=${holder}`;
};

/* ─── page component ─── */

const TABS = [
  { key: "info", label: "Thông tin cá nhân" },
  { key: "password", label: "Mật khẩu & bảo mật" },
  { key: "history", label: "Lịch sử chơi" },
  { key: "deposit", label: "Nạp tiền" },
  { key: "withdrawal", label: "Rút tiền" },
];

export function SpectatorProfilePage() {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("info");
  const [msg, setMsg] = useState(null); // { type, text }

  /* personal info */
  const [editMode, setEditMode] = useState(false);
  const [info, setInfo] = useState({ fullName: "", phoneNumber: "" });

  /* password */
  const [pw, setPw] = useState({ currentPassword: "", newPassword: "", confirmNewPassword: "" });

  /* history */
  const [predictions, setPredictions] = useState([]);
  const [histLoading, setHistLoading] = useState(false);

  /* withdrawal */
  const [wdAccounts, setWdAccounts] = useState([]);
  const [wdHistory, setWdHistory] = useState([]);
  const [wdLoading, setWdLoading] = useState(false);
  const [showBankForm, setShowBankForm] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [bankForm, setBankForm] = useState({ bankName: "", accountNumber: "", accountHolder: "" });
  const [wdAmount, setWdAmount] = useState("");
  const [wdSelectedAccount, setWdSelectedAccount] = useState("");

  /* deposit */
  const [depositAmount, setDepositAmount] = useState("");
  const [depositTx, setDepositTx] = useState(null);
  const [depositStatus, setDepositStatus] = useState("idle");
  const [depositHistory, setDepositHistory] = useState([]);
  const [depositHistoryLoading, setDepositHistoryLoading] = useState(false);
  const [showDepositHistory, setShowDepositHistory] = useState(false);
  const depositSince = useRef(null);
  const pollRef = useRef(null);

  /* wallet */
  const [walletBalance, setWalletBalance] = useState(null);

  const handleCreateDeposit = async () => {
    if (!Number(depositAmount) || Number(depositAmount) <= 0) {
      showMsg("error", "Vui lòng nhập số tiền hợp lệ.");
      return;
    }
    try {
      const res = await createDeposit(Number(depositAmount));
      const d = res?.data ?? res;
      setDepositTx(d);
      setDepositStatus("qr");
      depositSince.current = new Date();
      loadDepositHistory();

      // Bắt đầu tự động kiểm tra mỗi 3 giây
      let retries = 0;
      const MAX_RETRIES = 100; // ~5 phút
      const currentTxId = d?.transactionId;

      setDepositStatus("polling");
      pollRef.current = setInterval(async () => {
        retries++;
        if (retries > MAX_RETRIES) {
          clearInterval(pollRef.current);
          pollRef.current = null;
          setDepositStatus("idle");
          showMsg("error", "Hết thời gian chờ thanh toán. Vui lòng thử lại hoặc liên hệ hỗ trợ.");
          return;
        }
        try {
          if (currentTxId) {
            const checkRes = await checkDeposit(currentTxId);
            const cd = checkRes?.data ?? checkRes;
            if (cd?.completed) {
              clearInterval(pollRef.current);
              pollRef.current = null;
              setDepositStatus("success");
              loadDepositHistory();
              // Refresh wallet balance
              try {
                const bal = await getBalance();
                const b = bal?.data ?? bal;
                setWalletBalance(b?.balance ?? b?.Balance ?? 0);
              } catch { /* ignore */ }
            }
          }
        } catch (err) { console.error("Deposit check failed:", err); }
      }, 3000);
    } catch (e) {
      showMsg("error", e?.message ?? "Tạo yêu cầu nạp tiền thất bại.");
    }
  };

  // Cleanup polling on unmount
  useEffect(() => {
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, []);

  /* ── load profile ── */
  useEffect(() => {
    getProfile()
      .then((d) => {
        const p = d?.data ?? d;
        setProfile(p);
        setInfo({
          fullName: p.fullName ?? p.FullName ?? "",
          phoneNumber: p.phoneNumber ?? p.PhoneNumber ?? "",
        });
      })
      .catch(() => { /* empty */ })
      .finally(() => setLoading(false));

    getBalance()
      .then((d) => {
        const b = d?.data ?? d;
        setWalletBalance(b?.balance ?? b?.Balance ?? 0);
      })
      .catch(() => { /* empty */ });
  }, []);

  /* ── load predictions when switching to history tab ── */
  useEffect(() => {
    if (activeTab !== "history") return;
    setHistLoading(true);
    getMyPredictions()
      .then((d) => setPredictions(d?.data ?? d ?? []))
      .catch(() => { /* empty */ })
      .finally(() => setHistLoading(false));
  }, [activeTab]);

  /* ── load bank accounts + withdrawal history ── */
  useEffect(() => {
    if (activeTab !== "withdrawal") return;
    setWdLoading(true);
    Promise.all([
      getBankAccounts().then((d) => setWdAccounts(d?.data ?? d ?? [])),
      getWithdrawalHistory().then((d) => setWdHistory(d?.data ?? d ?? [])),
    ]).catch(() => { /* empty */ }).finally(() => setWdLoading(false));
  }, [activeTab]);

  const loadDepositHistory = async () => {
    setDepositHistoryLoading(true);
    try {
      const response = await getDepositHistory();
      const data = response?.data ?? response?.Data ?? response ?? [];
      setDepositHistory(Array.isArray(data) ? data : []);
    } catch (err) {
      console.error("Load deposit history failed:", err);
      setDepositHistory([]);
    } finally {
      setDepositHistoryLoading(false);
    }
  };

  /* ── load deposit history when switching to deposit tab ── */
  useEffect(() => {
    if (activeTab !== "deposit") return;
    loadDepositHistory();
  }, [activeTab]);

  const showMsg = useCallback((type, text) => {
    setMsg({ type, text });
    setTimeout(() => setMsg(null), 4000);
  }, []);

  /* ── handlers ── */
  const saveInfo = async () => {
    try {
      const res = await updateProfile({ fullName: info.fullName, phoneNumber: info.phoneNumber });
      const d = res?.data ?? res;
      setProfile((prev) => ({ ...prev, ...d }));
      setEditMode(false);
      // Cập nhật authUser trong localStorage để navbar hiển thị đúng tên
      try {
        const stored = JSON.parse(localStorage.getItem("authUser") || "{}");
        stored.fullName = info.fullName;
        localStorage.setItem("authUser", JSON.stringify(stored));
      } catch { /* empty */ }
      showMsg("success", "Cập nhật hồ sơ thành công!");
    } catch (e) {
      showMsg("error", e?.message ?? "Cập nhật thất bại.");
    }
  };

  const savePassword = async () => {
    if (pw.newPassword !== pw.confirmNewPassword) {
      showMsg("error", "Mật khẩu mới không khớp.");
      return;
    }
    if (pw.newPassword.length < 6) {
      showMsg("error", "Mật khẩu phải có ít nhất 6 ký tự.");
      return;
    }
    try {
      await changePassword({
        currentPassword: pw.currentPassword,
        newPassword: pw.newPassword,
        confirmNewPassword: pw.confirmNewPassword,
      });
      setPw({ currentPassword: "", newPassword: "", confirmNewPassword: "" });
      showMsg("success", "Đổi mật khẩu thành công!");
    } catch (e) {
      showMsg("error", e?.message ?? "Đổi mật khẩu thất bại.");
    }
  };

  const submitWithdrawal = async () => {
    if (!wdSelectedAccount || !Number(wdAmount) || Number(wdAmount) <= 0) {
      showMsg("error", "Vui lòng chọn tài khoản và nhập số tiền.");
      return;
    }
    try {
      await createWithdrawal({ bankAccountId: wdSelectedAccount, amount: Number(wdAmount) });
      setWdAmount("");
      showMsg("success", "Yêu cầu rút tiền đã được gửi. Admin sẽ xử lý trong thời gian sớm nhất.");
      // Refresh
      getWithdrawalHistory().then((d) => setWdHistory(d?.data ?? d ?? []));
      getBalance().then((d) => { const b = d?.data ?? d; setWalletBalance(b?.balance ?? b?.Balance ?? 0); });
    } catch (e) {
      showMsg("error", e?.message ?? "Gửi yêu cầu thất bại.");
    }
  };

  const saveBankInfo = async () => {
    if (!bankForm.bankName || !bankForm.accountNumber || !bankForm.accountHolder) {
      showMsg("error", "Vui lòng nhập đầy đủ thông tin.");
      return;
    }
    // Kiểm tra chủ tài khoản trùng với tài khoản cũ
    if (wdAccounts.length > 0) {
      const existingHolder = (wdAccounts[0].accountHolder ?? wdAccounts[0].AccountHolder ?? "").toLowerCase().trim();
      const newHolder = bankForm.accountHolder.trim().toLowerCase();
      if (existingHolder && newHolder !== existingHolder) {
        showMsg("error", `Chủ tài khoản phải là "${wdAccounts[0].accountHolder ?? wdAccounts[0].AccountHolder}" giống với tài khoản đã lưu.`);
        return;
      }
    }
    try {
      await saveBankAccount(bankForm);
      setShowBankForm(false);
      setBankForm({ bankName: "", accountNumber: "", accountHolder: "" });
      getBankAccounts().then((d) => setWdAccounts(d?.data ?? d ?? []));
      showMsg("success", "Đã lưu tài khoản ngân hàng!");
    } catch (e) {
      showMsg("error", e?.message ?? "Lưu thất bại.");
    }
  };

  /* ── total winnings for withdrawal tab ── */
  const totalWon = predictions
    .filter((p) => (p.status ?? p.Status ?? "").toLowerCase() === "won")
    .reduce((sum, p) => sum + Number(p.payoutAmount ?? p.PayoutAmount ?? 0), 0);

  if (loading) return <div className="spectator-page"><p>Đang tải...</p></div>;
  if (!profile) return <div className="spectator-page"><p>Không tìm thấy hồ sơ.</p></div>;

  return (
    <div className="spectator-page">
      <div className="sp-profile-layout">
        {/* ─── sidebar ─── */}
        <aside className="sp-profile-sidebar">
          <div className="sp-avatar-wrap">
            <div className="sp-avatar">
              {(profile.fullName ?? profile.FullName ?? "?")[0].toUpperCase()}
            </div>
            <h3 className="sp-name">{profile.fullName ?? profile.FullName ?? "Người dùng"}</h3>
            <span className="pill">Khán giả</span>
          </div>

          {walletBalance !== null && (
            <div style={{ padding: "12px 16px", borderRadius: 12, background: "rgba(143,100,32,0.07)", border: "1px solid rgba(143,100,32,0.12)", textAlign: "center", marginBottom: 4 }}>
              <p style={{ margin: "0 0 2px", fontSize: 11, color: "#657086", textTransform: "uppercase" }}>Số dư ví</p>
              <strong style={{ fontSize: 18, color: "#8f6420" }}>{Number(walletBalance).toLocaleString()} điểm</strong>
            </div>
          )}

          <nav className="sp-sidebar-nav">
            {TABS.map((t) => (
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

        {/* ─── content ─── */}
        <div className="sp-profile-content">
          {msg && <div style={msgBox(msg.type)}>{msg.text}</div>}

          {/* Personal Info */}
          {activeTab === "info" && (
            <section className="sp-section">
              <div className="sp-section-header">
                <h2>Thông tin cá nhân</h2>
                {!editMode ? (
                  <button style={btnSecondary} onClick={() => setEditMode(true)}>Chỉnh sửa</button>
                ) : (
                  <div style={{ display: "flex", gap: 10 }}>
                    <button style={btnSecondary} onClick={() => { setEditMode(false); setInfo({ fullName: profile.fullName ?? profile.FullName ?? "", phoneNumber: profile.phoneNumber ?? profile.PhoneNumber ?? "" }); }}>Huỷ</button>
                    <button style={btnPrimary} onClick={saveInfo}>Lưu</button>
                  </div>
                )}
              </div>

              <div className="sp-card">
                <Field label="Họ và tên" value={info.fullName} onChange={(e) => setInfo((p) => ({ ...p, fullName: e.target.value }))} readOnly={!editMode} placeholder="Nhập họ tên" />
                <Field label="Email" value={profile.email ?? profile.Email ?? ""} readOnly placeholder="Email" />
                <Field label="Số điện thoại" value={info.phoneNumber} onChange={(e) => setInfo((p) => ({ ...p, phoneNumber: e.target.value }))} readOnly={!editMode} placeholder="Nhập số điện thoại" />
              </div>

              {!editMode && (
                <div style={grid2}>
                  <Detail label="Vai trò" value="Khán giả" />
                  <Detail label="Ngày tham gia" value={profile.createdAt ? new Date(profile.createdAt).toLocaleDateString() : "-"} />
                </div>
              )}
            </section>
          )}

          {/* Password & Security */}
          {activeTab === "password" && (
            <section className="sp-section">
              <div className="sp-section-header">
                <h2>Mật khẩu & bảo mật</h2>
              </div>

              <div className="sp-card">
                <Field label="Mật khẩu hiện tại" type="password" value={pw.currentPassword} onChange={(e) => setPw((p) => ({ ...p, currentPassword: e.target.value }))} placeholder="Nhập mật khẩu hiện tại" />
                <Field label="Mật khẩu mới" type="password" value={pw.newPassword} onChange={(e) => setPw((p) => ({ ...p, newPassword: e.target.value }))} placeholder="Nhập mật khẩu mới (ít nhất 6 ký tự)" />
                <Field label="Xác nhận mật khẩu mới" type="password" value={pw.confirmNewPassword} onChange={(e) => setPw((p) => ({ ...p, confirmNewPassword: e.target.value }))} placeholder="Nhập lại mật khẩu mới" />
                <div style={{ marginTop: 8 }}>
                  <button style={btnPrimary} onClick={savePassword}>Đổi mật khẩu</button>
                </div>
              </div>
            </section>
          )}

          {/* Play History */}
          {activeTab === "history" && (
            <section className="sp-section">
              <div className="sp-section-header">
                <h2>Lịch sử chơi</h2>
              </div>

              <div className="sp-card" style={{ overflowX: "auto" }}>
                {histLoading ? (
                  <p>Đang tải...</p>
                ) : predictions.length === 0 ? (
                  <p className="muted" style={{ textAlign: "center", padding: "40px 0" }}>Chưa có lịch sử dự đoán.</p>
                ) : (
                  <table className="sp-history-table">
                    <thead>
                      <tr>
                        <th>Ngày</th>
                        <th>Cuộc đua</th>
                        <th>Ngựa chọn</th>
                        <th>Tiền cược</th>
                        <th>Trạng thái</th>
                        <th>Tiền thưởng</th>
                      </tr>
                    </thead>
                    <tbody>
                      {predictions.map((p) => {
                        const row = p;
                        return (
                          <tr key={row.id ?? row.Id}>
                            <td>{row.createdAt ? new Date(row.createdAt).toLocaleDateString() : "-"}</td>
                            <td>{row.raceName ?? row.RaceName ?? "-"}</td>
                            <td>{row.predictedHorseName ?? row.PredictedHorseName ?? "-"}</td>
                            <td>{(row.betAmount ?? row.BetAmount ?? 0).toLocaleString()}</td>
                            <td><StatusBadge status={row.status ?? row.Status} /></td>
                            <td>{(row.payoutAmount ?? row.PayoutAmount ?? 0).toLocaleString()}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                )}
              </div>
            </section>
          )}

          {/* Deposit */}
          {activeTab === "deposit" && (
            <section className="sp-section">
              <div className="sp-section-header">
                <h2>Nạp tiền</h2>
                <button
                  style={{ ...btnSecondary, fontSize: 13, padding: "6px 16px" }}
                  onClick={() => {
                    setShowDepositHistory((v) => !v);
                    if (!showDepositHistory) loadDepositHistory();
                  }}
                >
                  {showDepositHistory ? "Ẩn lịch sử" : "Lịch sử nạp tiền"}
                </button>
              </div>

              <div className="sp-card">
                {depositStatus === "idle" && (
                  <>
                    <Field
                      label="Số tiền nạp"
                      type="number"
                      value={depositAmount}
                      onChange={(e) => setDepositAmount(e.target.value)}
                      placeholder="Nhập số tiền muốn nạp"
                    />
                    <div style={{ display: "flex", gap: 8, marginBottom: 16, flexWrap: "wrap" }}>
                      {[10000, 20000, 50000, 100000, 200000, 500000].map((amt) => (
                        <button
                          key={amt}
                          type="button"
                          onClick={() => setDepositAmount(String(amt))}
                          style={{
                            padding: "6px 14px", borderRadius: 20, fontSize: 13, fontWeight: 600,
                            border: `1.5px solid ${Number(depositAmount) === amt ? "#8f6420" : "rgba(143,100,32,0.2)"}`,
                            background: Number(depositAmount) === amt ? "rgba(143,100,32,0.12)" : "#fff",
                            color: Number(depositAmount) === amt ? "#8f6420" : "#34415b",
                            cursor: "pointer", fontFamily: "inherit", transition: "all .15s",
                          }}
                        >
                          {amt.toLocaleString()}đ
                        </button>
                      ))}
                    </div>
                    <div style={{ marginTop: 8 }}>
                      <button style={btnPrimary} onClick={handleCreateDeposit} disabled={!Number(depositAmount) || Number(depositAmount) <= 0}>
                        Tạo mã QR
                      </button>
                    </div>
                  </>
                )}

                {depositStatus === "qr" && depositTx && (
                  <div style={{ display: "grid", gridTemplateColumns: "auto 1fr", gap: 24, alignItems: "start" }}>
                    <div>
                      <img
                        src={vietQrUrl(depositTx, depositAmount, depositTx.reference)}
                        alt="QR chuyển khoản"
                        style={{ width: 300, height: "auto", borderRadius: 12, border: "1px solid rgba(143,100,32,0.15)" }}
                      />
                      <p style={{ margin: "8px 0 0", fontSize: 12, color: "#856404", background: "#fff3cd", padding: "8px 14px", borderRadius: 8 }}>
                        Số tiền và nội dung chuyển khoản đã điền sẵn trên QR. Nếu ngân hàng không tự điền, hãy nhập đúng nội dung bên phải.
                      </p>
                    </div>
                    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                      <div style={{ padding: "16px 20px", borderRadius: 12, background: "rgba(255,255,255,0.88)", border: "1.5px solid rgba(231,198,120,0.25)" }}>
                        <p style={{ margin: "0 0 4px", fontSize: 12, color: "#657086", textTransform: "uppercase" }}>Số tiền</p>
                        <strong style={{ fontSize: 22, color: "#172033" }}>{Number(depositTx.amount).toLocaleString()} VNĐ</strong>
                      </div>
                      <div
                        role="button" tabIndex={0}
                        style={{ padding: "16px 20px", borderRadius: 12, background: "rgba(255,255,255,0.88)", border: "1.5px solid rgba(231,198,120,0.25)", cursor: "pointer" }}
                        onClick={() => { navigator.clipboard.writeText(depositTx.reference); showMsg("success", "Đã sao chép nội dung!"); }}
                        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); navigator.clipboard.writeText(depositTx.reference); showMsg("success", "Đã sao chép nội dung!"); } }}
                        title="Click để sao chép"
                      >
                        <p style={{ margin: "0 0 4px", fontSize: 12, color: "#657086", textTransform: "uppercase" }}>Nội dung chuyển khoản</p>
                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                          <strong style={{ fontSize: 16, color: "#8f6420", fontFamily: "monospace" }}>{depositTx.reference}</strong>
                          <span style={{ fontSize: 14, color: "#657086" }}>&#x2398;</span>
                        </div>
                      </div>
                      <p style={{ margin: 0, fontSize: 13, color: "#856404", background: "#fff3cd", padding: "8px 14px", borderRadius: 8 }}>
                        Đang chờ thanh toán...
                      </p>
                    </div>
                  </div>
                )}

                {depositStatus === "polling" && (
                  <div style={{ display: "grid", gridTemplateColumns: "auto 1fr", gap: 24, alignItems: "start" }}>
                    <div>
                      <img
                        src={vietQrUrl(depositTx, depositAmount, depositTx.reference)}
                        alt="QR chuyển khoản"
                        style={{ width: 300, height: "auto", borderRadius: 12, border: "1px solid rgba(143,100,32,0.15)" }}
                      />
                      <p style={{ margin: "8px 0 0", fontSize: 12, color: "#856404", background: "#fff3cd", padding: "8px 14px", borderRadius: 8 }}>
                        Số tiền và nội dung chuyển khoản đã điền sẵn trên QR. Nếu ngân hàng không tự điền, hãy nhập đúng nội dung bên phải.
                      </p>
                    </div>
                    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                      <div style={{ padding: "16px 20px", borderRadius: 12, background: "rgba(255,255,255,0.88)", border: "1.5px solid rgba(231,198,120,0.25)" }}>
                        <p style={{ margin: "0 0 4px", fontSize: 12, color: "#657086", textTransform: "uppercase" }}>Số tiền</p>
                        <strong style={{ fontSize: 22, color: "#172033" }}>{Number(depositTx.amount).toLocaleString()} VNĐ</strong>
                      </div>
                      <div
                        role="button" tabIndex={0}
                        style={{ padding: "16px 20px", borderRadius: 12, background: "rgba(255,255,255,0.88)", border: "1.5px solid rgba(231,198,120,0.25)", cursor: "pointer" }}
                        onClick={() => { navigator.clipboard.writeText(depositTx.reference); showMsg("success", "Đã sao chép nội dung!"); }}
                        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); navigator.clipboard.writeText(depositTx.reference); showMsg("success", "Đã sao chép nội dung!"); } }}
                        title="Click để sao chép"
                      >
                        <p style={{ margin: "0 0 4px", fontSize: 12, color: "#657086", textTransform: "uppercase" }}>Nội dung chuyển khoản</p>
                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                          <strong style={{ fontSize: 16, color: "#8f6420", fontFamily: "monospace" }}>{depositTx.reference}</strong>
                          <span style={{ fontSize: 14, color: "#657086" }}>&#x2398;</span>
                        </div>
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 14px", background: "#fff3cd", borderRadius: 8 }}>
                        <span style={{ display: "inline-block", width: 14, height: 14, borderRadius: "50%", border: "2px solid #8f6420", borderTopColor: "transparent", animation: "sp-spin 0.8s linear infinite" }} />
                        <span style={{ fontSize: 13, color: "#856404" }}>Đang kiểm tra thanh toán...</span>
                      </div>
                      <button style={{ ...btnPrimary, marginTop: 4, fontSize: 12, padding: "6px 16px" }} onClick={async () => {
                        try {
                          const checkRes = await checkDeposit(depositTx?.transactionId);
                          const cd = checkRes?.data ?? checkRes;
                          if (cd?.completed) {
                            clearInterval(pollRef.current);
                            pollRef.current = null;
                            setDepositStatus("success");
                            try {
                              const bal = await getBalance();
                              const b = bal?.data ?? bal;
                              setWalletBalance(b?.balance ?? b?.Balance ?? 0);
                            } catch { /* ignore */ }
                          }
                        } catch (err) { console.error("Manual check failed:", err); }
                      }}>
                        Kiểm tra ngay
                      </button>
                      <button style={{ ...btnSecondary, marginTop: 4 }} onClick={() => { setDepositStatus("idle"); setDepositTx(null); }}>
                        Nhập số tiền khác
                      </button>
                    </div>
                  </div>
                )}

                {depositStatus === "success" && depositTx && (
                  <div style={{ textAlign: "center", padding: "20px 0" }}>
                    <div style={{ width: 56, height: 56, borderRadius: "50%", background: "#e6f7e6", display: "grid", placeItems: "center", margin: "0 auto 12px" }}>
                      <span style={{ color: "#1a7d1a", fontSize: 28 }}>&#10003;</span>
                    </div>
                    <h3 style={{ color: "#1a7d1a", margin: "0 0 8px" }}>Thanh toán thành công!</h3>
                    <p style={{ fontSize: 14, color: "#657086", margin: 0 }}>
                      Đã nhận <strong style={{ color: "#172033" }}>{Number(depositTx.amount).toLocaleString()} VNĐ</strong>
                    </p>
                    <button style={{ ...btnSecondary, marginTop: 16 }} onClick={() => { setDepositStatus("idle"); setDepositTx(null); setDepositAmount(""); }}>
                      Nạp thêm
                    </button>
                  </div>
                )}

                {showDepositHistory && (
                  <div style={{ marginTop: 24, borderTop: "1px solid rgba(143,100,32,0.12)", paddingTop: 20 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                      <h3 style={{ margin: 0, color: "#172033", fontSize: 16 }}>Lịch sử nạp tiền</h3>
                      <span style={{ fontSize: 12, color: "#657086" }}>Mới nhất trước</span>
                    </div>
                  {depositHistoryLoading ? (
                    <p className="muted">Đang tải...</p>
                  ) : depositHistory.length === 0 ? (
                    <p className="muted" style={{ textAlign: "center", padding: "20px 0" }}>Chưa có giao dịch nạp tiền nào.</p>
                  ) : (
                    <div style={{ overflowX: "auto" }}>
                      <table style={{ width: "100%", borderCollapse: "collapse" }}>
                        <thead>
                          <tr style={{ textAlign: "left", color: "#657086", fontSize: 12 }}>
                            <th style={{ padding: "8px 10px", borderBottom: "1px solid rgba(143,100,32,0.12)" }}>Ngày</th>
                            <th style={{ padding: "8px 10px", borderBottom: "1px solid rgba(143,100,32,0.12)" }}>Mã tham chiếu</th>
                            <th style={{ padding: "8px 10px", borderBottom: "1px solid rgba(143,100,32,0.12)" }}>Số tiền</th>
                            <th style={{ padding: "8px 10px", borderBottom: "1px solid rgba(143,100,32,0.12)" }}>Trạng thái</th>
                          </tr>
                        </thead>
                        <tbody>
                          {[...depositHistory]
                            .sort((a, b) => new Date(b.createdAt ?? b.CreatedAt ?? 0) - new Date(a.createdAt ?? a.CreatedAt ?? 0))
                            .map((item) => (
                            <tr key={item.id ?? item.Id}>
                              <td style={{ padding: "10px", borderBottom: "1px solid rgba(143,100,32,0.08)", color: "#34415b" }}>
                                {item.createdAt ? new Date(item.createdAt).toLocaleString("vi-VN") : "—"}
                              </td>
                              <td style={{ padding: "10px", borderBottom: "1px solid rgba(143,100,32,0.08)", color: "#34415b", fontFamily: "monospace" }}>
                                {item.reference ?? item.Reference ?? "—"}
                              </td>
                              <td style={{ padding: "10px", borderBottom: "1px solid rgba(143,100,32,0.08)", color: "#8f6420", fontWeight: 600 }}>
                                {Number(item.amount ?? item.Amount ?? 0).toLocaleString()}đ
                              </td>
                              <td style={{ padding: "10px", borderBottom: "1px solid rgba(143,100,32,0.08)" }}>
                                <span style={{
                                  display: "inline-block",
                                  padding: "4px 8px",
                                  borderRadius: 999,
                                  fontSize: 12,
                                  fontWeight: 600,
                                  color: (item.status ?? item.Status ?? "pending").toLowerCase() === "completed" ? "#1a7d1a" : "#8f6420",
                                  background: (item.status ?? item.Status ?? "pending").toLowerCase() === "completed" ? "rgba(26,125,26,0.1)" : "rgba(143,100,32,0.12)"
                                }}>
                                  {(item.status ?? item.Status ?? "pending") === "completed" ? "Hoàn tất" : "Đang chờ"}
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
                )}
              </div>
            </section>
          )}

          {/* Withdrawal */}
          {activeTab === "withdrawal" && (
            <section className="sp-section">
              <div className="sp-section-header">
                <h2>Rút tiền</h2>
                <button
                  style={{ ...btnSecondary, fontSize: 13, padding: "6px 16px" }}
                  onClick={() => {
                    setShowHistory(!showHistory);
                    if (!showHistory) {
                      getWithdrawalHistory().then((d) => setWdHistory(d?.data ?? d ?? []));
                    }
                  }}
                >
                  {showHistory ? "Ẩn lịch sử" : "Lịch sử rút tiền"}
                </button>
              </div>

              {wdLoading ? (
                <p>Đang tải...</p>
              ) : (
                <>
                  {/* Tài khoản ngân hàng */}
                  <div className="sp-card" style={{ marginBottom: 16 }}>
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
                      <h3 style={{ margin: 0, color: "#172033", fontSize: 16 }}>Tài khoản ngân hàng</h3>
                      {!showBankForm && wdAccounts.length === 0 && (
                        <button style={btnSecondary} onClick={() => setShowBankForm(true)}>Thêm tài khoản</button>
                      )}
                    </div>

                    {showBankForm || wdAccounts.length === 0 ? (
                      <div>
                        <div style={fieldStyle}>
                          <label style={fieldLabel}>Ngân hàng</label>
                          <select
                            value={bankForm.bankName}
                            onChange={(e) => setBankForm((p) => ({ ...p, bankName: e.target.value }))}
                            style={{
                              ...inputBase,
                              appearance: "none",
                              backgroundImage: "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%23657086' d='M6 8L1 3h10z'/%3E%3C/svg%3E\")",
                              backgroundRepeat: "no-repeat",
                              backgroundPosition: "right 12px center",
                              cursor: "pointer",
                            }}
                          >
                            <option value="">-- Chọn ngân hàng --</option>
                            <option value="TP Bank">TP Bank</option>
                            <option value="Vietcombank">Vietcombank</option>
                            <option value="VietinBank">VietinBank</option>
                            <option value="BIDV">BIDV</option>
                            <option value="Techcombank">Techcombank</option>
                            <option value="MB Bank">MB Bank</option>
                            <option value="ACB">ACB</option>
                            <option value="VP Bank">VP Bank</option>
                            <option value="Sacombank">Sacombank</option>
                            <option value="HDBank">HDBank</option>
                            <option value="VIB">VIB</option>
                            <option value="MSB">MSB</option>
                            <option value="OCB">OCB</option>
                            <option value="Nam A Bank">Nam A Bank</option>
                            <option value="PVcomBank">PVcomBank</option>
                            <option value="SHB">SHB</option>
                            <option value="SCB">SCB</option>
                            <option value="Eximbank">Eximbank</option>
                            <option value="Agribank">Agribank</option>
                            <option value="Dong A Bank">Dong A Bank</option>
                          </select>
                        </div>
                        <Field label="Số tài khoản" value={bankForm.accountNumber} onChange={(e) => setBankForm((p) => ({ ...p, accountNumber: e.target.value }))} placeholder="Số tài khoản" />
                        <Field label="Chủ tài khoản" value={bankForm.accountHolder} onChange={(e) => setBankForm((p) => ({ ...p, accountHolder: e.target.value }))} placeholder="Tên chủ tài khoản" />
                        <div style={{ display: "flex", gap: 10, marginTop: 8 }}>
                          <button style={btnPrimary} onClick={saveBankInfo}>Lưu</button>
                          {wdAccounts.length > 0 && (
                            <button style={btnSecondary} onClick={() => { setShowBankForm(false); setBankForm({ bankName: "", accountNumber: "", accountHolder: "" }); }}>Huỷ</button>
                          )}
                        </div>
                      </div>
                    ) : (
                      <div>
                        {wdAccounts.map((acc) => (
                          <div key={acc.id ?? acc.Id} role="button" tabIndex={0} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "10px 14px", borderRadius: 10, marginBottom: 8, border: `1.5px solid ${wdSelectedAccount === (acc.id ?? acc.Id) ? "#8f6420" : "rgba(143,100,32,0.12)"}`, background: wdSelectedAccount === (acc.id ?? acc.Id) ? "rgba(143,100,32,0.06)" : "#fff", cursor: "pointer" }}
                            onClick={() => setWdSelectedAccount(acc.id ?? acc.Id)}
                            onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setWdSelectedAccount(acc.id ?? acc.Id); } }}
                          >
                            <div>
                              <strong style={{ color: "#172033", fontSize: 14, display: "block" }}>{acc.bankName ?? acc.BankName}</strong>
                              <span style={{ color: "#657086", fontSize: 13 }}>
                                {acc.accountNumber ?? acc.AccountNumber} - {acc.accountHolder ?? acc.AccountHolder}
                              </span>
                            </div>
                            {wdSelectedAccount === (acc.id ?? acc.Id) && (
                              <span style={{ color: "#8f6420", fontSize: 18 }}>&#10003;</span>
                            )}
                          </div>
                        ))}
                        <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                          <button style={{ ...btnSecondary, fontSize: 13, padding: "6px 16px" }} onClick={() => setShowBankForm(true)}>Thêm tài khoản khác</button>
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Form rút tiền */}
                  {wdAccounts.length > 0 && (
                    <div className="sp-card" style={{ marginBottom: 16 }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
                        <h3 style={{ margin: 0, color: "#172033" }}>Yêu cầu rút tiền</h3>
                        {totalWon > 0 && (
                          <span style={{ fontSize: 13, color: "#1a7d1a", background: "#e6f7e6", padding: "4px 12px", borderRadius: 20 }}>
                            Tổng thắng: {totalWon.toLocaleString()} điểm
                          </span>
                        )}
                      </div>
                      <Field label="Số điểm rút" type="number" value={wdAmount} onChange={(e) => setWdAmount(e.target.value)} placeholder="Nhập số điểm muốn rút (1.000đ = 1 điểm)" />
                      <div style={{ marginTop: 8 }}>
                        <button style={btnPrimary} onClick={submitWithdrawal}>Gửi yêu cầu</button>
                      </div>
                    </div>
                  )}

                  {/* Lịch sử rút tiền */}
                  {showHistory && (
                    <div className="sp-card">
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                        <h3 style={{ margin: 0, color: "#172033", fontSize: 16 }}>Lịch sử rút tiền</h3>
                        <span style={{ fontSize: 12, color: "#657086" }}>Mới nhất trước</span>
                      </div>
                      {wdHistory.length === 0 ? (
                        <p className="muted" style={{ textAlign: "center", padding: "20px 0" }}>Chưa có yêu cầu rút tiền nào.</p>
                      ) : (
                        <div style={{ overflowX: "auto" }}>
                          <table className="sp-history-table">
                            <thead>
                              <tr>
                                <th>Ngày</th>
                                <th>Ngân hàng</th>
                                <th>Số tài khoản</th>
                                <th>Số tiền</th>
                                <th>Trạng thái</th>
                              </tr>
                            </thead>
                            <tbody>
                              {[...wdHistory]
                                .sort((a, b) => new Date(b.createdAt ?? b.CreatedAt ?? 0) - new Date(a.createdAt ?? a.CreatedAt ?? 0))
                                .map((w) => (
                                <tr key={w.id ?? w.Id}>
                                  <td>{w.createdAt ? new Date(w.createdAt).toLocaleString("vi-VN") : "-"}</td>
                                  <td>{w.bankName ?? w.BankName ?? "-"}</td>
                                  <td>{w.accountNumber ?? w.AccountNumber ?? "-"}</td>
                                  <td>{(w.amount ?? w.Amount ?? 0).toLocaleString()} điểm</td>
                                  <td><StatusBadge status={w.status ?? w.Status} /></td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </div>
                  )}
                </>
              )}
            </section>
          )}
        </div>
      </div>
    </div>
  );
}
