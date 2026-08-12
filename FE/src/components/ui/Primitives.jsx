import { colors, typography, radius, shadows, statusColors } from "../../styles/designTokens";

// StatusPill - Hiển thị trạng thái với màu và icon
export function StatusPill({ status, size = "md" }) {
  const config = statusColors[status] || statusColors.draft;
  const isOngoing = status === "ongoing";

  const sizeStyles = {
    sm: { padding: "2px 8px", fontSize: "11px" },
    md: { padding: "4px 12px", fontSize: "12px" },
    lg: { padding: "6px 16px", fontSize: "14px" },
  };

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "6px",
        borderRadius: radius.pill,
        background: config.bg,
        color: config.text,
        fontWeight: 600,
        letterSpacing: "0.5px",
        textTransform: "uppercase",
        animation: isOngoing ? "pulse 2s ease-in-out infinite" : "none",
        ...sizeStyles[size],
      }}
    >
      <span
        style={{
          width: size === "sm" ? "6px" : "8px",
          height: size === "sm" ? "6px" : "8px",
          borderRadius: "50%",
          background: config.text,
          boxShadow: isOngoing ? `0 0 0 2px ${config.bg}` : "none",
        }}
      />
      {config.label}
    </span>
  );
}

// StatPill - Hiển thị thống kê nhỏ với icon
export function StatPill({ icon, value, label, trend }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: "8px",
        padding: "8px 12px",
        borderRadius: radius.pill,
        background: colors.paper,
        border: `1px solid ${colors.parchment}`,
        boxShadow: shadows.card,
      }}
    >
      <span style={{ fontSize: "16px" }}>{icon}</span>
      <div style={{ display: "flex", flexDirection: "column" }}>
        <span
          style={{
            fontSize: typography.body.sizes.md,
            fontWeight: typography.body.weight.medium,
            color: colors.ink,
            lineHeight: 1.2,
          }}
        >
          {value}
        </span>
        <span
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.ash,
            lineHeight: 1.2,
          }}
        >
          {label}
        </span>
      </div>
      {trend && (
        <span
          style={{
            fontSize: typography.meta.sizes.sm,
            color: trend.startsWith("+") ? "#166534" : "#991B1B",
            fontWeight: 600,
          }}
        >
          {trend}
        </span>
      )}
    </div>
  );
}

// Badge - Badge/Tag đơn giản
export function Badge({ children, tone = "neutral", size = "md" }) {
  const tones = {
    neutral: { bg: colors.statusNeutral, text: colors.stone },
    green: { bg: colors.statusGreen, text: "#2D5016" },
    flame: { bg: colors.statusFlame, text: "#7C2D12" },
    red: { bg: colors.statusRed, text: "#7F1D1D" },
  };

  const config = tones[tone] || tones.neutral;

  const sizeStyles = {
    sm: { padding: "2px 8px", fontSize: "11px" },
    md: { padding: "4px 10px", fontSize: "12px" },
    lg: { padding: "6px 14px", fontSize: "14px" },
  };

  return (
    <span
      style={{
        display: "inline-block",
        borderRadius: radius.pill,
        background: config.bg,
        color: config.text,
        fontWeight: 600,
        ...sizeStyles[size],
      }}
    >
      {children}
    </span>
  );
}

// Button - Button với nhiều variants
export function Button({
  children,
  variant = "primary",
  size = "md",
  disabled = false,
  loading = false,
  onClick,
  style: customStyle,
  ...props
}) {
  const variants = {
    primary: {
      background: colors.flame,
      color: colors.paper,
      border: "none",
      hoverBg: colors.crimson,
    },
    secondary: {
      background: "transparent",
      color: colors.flame,
      border: `2px solid ${colors.flame}`,
      hoverBg: "transparent",
    },
    ghost: {
      background: "transparent",
      color: colors.stone,
      border: `1px solid ${colors.parchment}`,
      hoverBg: colors.cream,
    },
    destructive: {
      background: colors.statusRed,
      color: "#7F1D1D",
      border: "none",
      hoverBg: "#E8B0A0",
    },
  };

  const sizeStyles = {
    sm: { padding: "6px 12px", fontSize: "13px" },
    md: { padding: "10px 20px", fontSize: "14px" },
    lg: { padding: "12px 24px", fontSize: "16px" },
  };

  const config = variants[variant] || variants.primary;

  return (
    <button
      onClick={onClick}
      disabled={disabled || loading}
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "8px",
        borderRadius: radius.button,
        background: config.background,
        color: config.color,
        border: config.border,
        fontWeight: 600,
        cursor: disabled || loading ? "not-allowed" : "pointer",
        opacity: disabled ? 0.5 : 1,
        transition: "all 0.2s ease",
        fontFamily: typography.body.fontFamily,
        ...sizeStyles[size],
        ...customStyle,
      }}
      {...props}
    >
      {loading && (
        <span
          style={{
            width: "14px",
            height: "14px",
            border: "2px solid currentColor",
            borderTopColor: "transparent",
            borderRadius: "50%",
            animation: "spin 0.6s linear infinite",
          }}
        />
      )}
      {children}
    </button>
  );
}

// Input - Input với validation
export function Input({
  label,
  error,
  hint,
  size = "md",
  style: customStyle,
  ...props
}) {
  const sizeStyles = {
    sm: { padding: "8px 12px", fontSize: "13px" },
    md: { padding: "10px 14px", fontSize: "14px" },
    lg: { padding: "12px 16px", fontSize: "16px" },
  };

  return (
    <div style={{ marginBottom: "16px" }}>
      {label && (
        <label
          style={{
            display: "block",
            fontSize: typography.body.sizes.sm,
            fontWeight: typography.body.weight.medium,
            color: colors.ink,
            marginBottom: "6px",
          }}
        >
          {label}
        </label>
      )}
      <div style={{ position: "relative" }}>
        <input
          style={{
            width: "100%",
            borderRadius: radius.input,
            border: `1px solid ${error ? colors.flame : colors.parchment}`,
            background: colors.paper,
            color: colors.ink,
            fontFamily: typography.body.fontFamily,
            outline: "none",
            transition: "border-color 0.2s ease",
            ...sizeStyles[size],
            ...customStyle,
          }}
          {...props}
        />
        {error && (
          <span
            style={{
              position: "absolute",
              right: "12px",
              top: "50%",
              transform: "translateY(-50%)",
              color: colors.flame,
              fontSize: "16px",
            }}
          >
            ⚠
          </span>
        )}
      </div>
      {error && (
        <p
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.flame,
            marginTop: "4px",
            marginBottom: 0,
          }}
        >
          {error}
        </p>
      )}
      {hint && !error && (
        <p
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.ash,
            marginTop: "4px",
            marginBottom: 0,
          }}
        >
          {hint}
        </p>
      )}
    </div>
  );
}

// Textarea - Textarea với validation
export function Textarea({
  label,
  error,
  hint,
  rows = 4,
  style: customStyle,
  ...props
}) {
  return (
    <div style={{ marginBottom: "16px" }}>
      {label && (
        <label
          style={{
            display: "block",
            fontSize: typography.body.sizes.sm,
            fontWeight: typography.body.weight.medium,
            color: colors.ink,
            marginBottom: "6px",
          }}
        >
          {label}
        </label>
      )}
      <textarea
        rows={rows}
        style={{
          width: "100%",
          borderRadius: radius.input,
          border: `1px solid ${error ? colors.flame : colors.parchment}`,
          background: colors.paper,
          color: colors.ink,
          fontFamily: typography.body.fontFamily,
          fontSize: typography.body.sizes.md,
          padding: "10px 14px",
          outline: "none",
          transition: "border-color 0.2s ease",
          resize: "vertical",
          ...customStyle,
        }}
        {...props}
      />
      {error && (
        <p
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.flame,
            marginTop: "4px",
            marginBottom: 0,
          }}
        >
          {error}
        </p>
      )}
      {hint && !error && (
        <p
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.ash,
            marginTop: "4px",
            marginBottom: 0,
          }}
        >
          {hint}
        </p>
      )}
    </div>
  );
}

// Select - Select dropdown
export function Select({
  label,
  error,
  hint,
  options = [],
  size = "md",
  style: customStyle,
  ...props
}) {
  const sizeStyles = {
    sm: { padding: "8px 12px", fontSize: "13px" },
    md: { padding: "10px 14px", fontSize: "14px" },
    lg: { padding: "12px 16px", fontSize: "16px" },
  };

  return (
    <div style={{ marginBottom: "16px" }}>
      {label && (
        <label
          style={{
            display: "block",
            fontSize: typography.body.sizes.sm,
            fontWeight: typography.body.weight.medium,
            color: colors.ink,
            marginBottom: "6px",
          }}
        >
          {label}
        </label>
      )}
      <select
        style={{
          width: "100%",
          borderRadius: radius.input,
          border: `1px solid ${error ? colors.flame : colors.parchment}`,
          background: colors.paper,
          color: colors.ink,
          fontFamily: typography.body.fontFamily,
          outline: "none",
          transition: "border-color 0.2s ease",
          cursor: "pointer",
          ...sizeStyles[size],
          ...customStyle,
        }}
        {...props}
      >
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
      {error && (
        <p
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.flame,
            marginTop: "4px",
            marginBottom: 0,
          }}
        >
          {error}
        </p>
      )}
      {hint && !error && (
        <p
          style={{
            fontSize: typography.meta.sizes.sm,
            color: colors.ash,
            marginTop: "4px",
            marginBottom: 0,
          }}
        >
          {hint}
        </p>
      )}
    </div>
  );
}
