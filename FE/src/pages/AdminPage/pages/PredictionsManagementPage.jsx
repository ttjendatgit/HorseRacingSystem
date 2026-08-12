import { useEffect, useState } from "react";
import { request } from "../../../services/apiClient";

const STATUS_META = {
  pending: { label: "Chờ thanh toán", color: "var(--hr-warning)", bg: "rgba(185,138,69,0.16)" },
  won: { label: "Thắng", color: "var(--hr-success)", bg: "rgba(112,139,104,0.16)" },
  lost: { label: "Thua", color: "var(--hr-danger)", bg: "rgba(201,105,90,0.16)" },
};

const fmtDate = (v) =>
  v
    ? new Date(v).toLocaleString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      })
    : "-";

export default function PredictionsManagementPage() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");

  useEffect(() => {
    request("/api/admin/predictions")
      .then((d) => setData(d?.data ?? d ?? {}))
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  const items = Array.isArray(data?.items) ? data.items : [];
  const summary = data?.summary ?? {};

  const filtered = query.trim()
    ? items.filter((p) =>
        `${p.spectatorName ?? ""} ${p.raceName ?? ""} ${p.horseName ?? ""} ${p.tournamentName ?? ""}`
          .toLowerCase()
          .includes(query.toLowerCase()),
      )
    : items;

  if (loading)
    return <div style={{ padding: 40, textAlign: "center", color: "var(--hr-muted)" }}>Đang tải...</div>;

  return (
    <div style={{ maxWidth: 1100, margin: "0 auto", padding: "24px 32px" }}>
      <h1 style={{ margin: "0 0 8px", fontSize: 28, color: "var(--hr-paper)" }}>Quản lý dự đoán</h1>
      <p style={{ margin: "0 0 24px", fontSize: 13, color: "var(--hr-muted)" }}>
        Tổng quan đặt cược điểm của khán giả và trạng thái thanh toán.
      </p>
      {error && <p style={{ color: "var(--hr-danger)", marginBottom: 16 }}>{error}</p>}

      <div style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", marginBottom: 20 }}>
        {[
          { label: "Tổng dự đoán", value: summary.totalPredictions ?? 0 },
          { label: "Tổng điểm cược", value: (summary.totalBet ?? 0).toLocaleString() },
          { label: "Đã trả thưởng", value: (summary.totalPaid ?? 0).toLocaleString() },
          { label: "Thắng", value: summary.won ?? 0 },
          { label: "Thua", value: summary.lost ?? 0 },
          { label: "Chờ thanh toán", value: summary.pending ?? 0 },
        ].map((s) => (
          <div key={s.label} style={{ borderRadius: 14, border: "1px solid var(--hr-border)", background: "var(--hr-surface)", padding: "14px 16px" }}>
            <span style={{ display: "block", fontSize: 11, color: "var(--hr-muted)", textTransform: "uppercase" }}>{s.label}</span>
            <strong style={{ fontSize: 20, color: "var(--hr-paper)" }}>{s.value}</strong>
          </div>
        ))}
      </div>

      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12, gap: 12 }}>
        <input
          className="hr-field"
          placeholder="Tìm theo khán giả, cuộc đua, ngựa, giải đấu..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          style={{ padding: "10px 14px", borderRadius: 10, border: "1px solid var(--hr-border-soft)", background: "var(--hr-surface-2)", color: "var(--hr-text)", maxWidth: 380, flex: 1 }}
        />
        <span style={{ fontSize: 12, color: "var(--hr-muted)" }}>{filtered.length} dự đoán</span>
      </div>

      <div style={{ overflowX: "auto", border: "1px solid var(--hr-border-soft)", borderRadius: 16 }}>
        <table style={{ width: "100%", borderCollapse: "collapse", background: "var(--hr-surface)", fontSize: 13 }}>
          <thead>
            <tr>
              <th style={th}>Khán giả</th>
              <th style={th}>Cuộc đua</th>
              <th style={th}>Giải đấu</th>
              <th style={th}>Ngựa chọn</th>
              <th style={th}>Điểm cược</th>
              <th style={th}>Tỉ lệ</th>
              <th style={th}>Trả thưởng</th>
              <th style={th}>Trạng thái</th>
              <th style={th}>Ngày đặt</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr><td colSpan={9} style={{ padding: 20, textAlign: "center", color: "var(--hr-muted)" }}>Chưa có dự đoán nào.</td></tr>
            ) : filtered.map((p) => {
              const st = (p.status ?? "").toLowerCase();
              const meta = STATUS_META[st] ?? { label: p.status ?? "-", color: "var(--hr-muted)", bg: "rgba(238,229,212,0.06)" };
              return (
                <tr key={p.id}>
                  <td style={td}><strong>{p.spectatorName ?? "-"}</strong></td>
                  <td style={td}>{p.raceName ?? "-"}</td>
                  <td style={td}>{p.tournamentName ?? "-"}</td>
                  <td style={td}>{p.horseName ?? "-"}</td>
                  <td style={td}>{(p.betAmount ?? 0).toLocaleString()}đ</td>
                  <td style={td}>{Number(p.odds ?? 0).toFixed(2)}x</td>
                  <td style={td}><strong>{(p.payoutAmount ?? 0).toLocaleString()}đ</strong></td>
                  <td style={td}><span style={{ padding: "3px 10px", borderRadius: 999, fontSize: 11, fontWeight: 700, background: meta.bg, color: meta.color }}>{meta.label}</span></td>
                  <td style={td}>{fmtDate(p.createdAt)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const th = { padding: 12, textAlign: "left", borderBottom: "1px solid var(--hr-border-soft)", fontSize: 10, textTransform: "uppercase", letterSpacing: 1, color: "var(--hr-muted)" };
const td = { padding: 12, borderBottom: "1px solid var(--hr-border-soft)", color: "var(--hr-text)" };
