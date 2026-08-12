import { Link } from "react-router-dom";
import { colors, typography, spacing } from "../../styles/designTokens";

// PageLayout - Wrapper cho mọi trang với breadcrumb + title + actions
export function PageLayout({
  breadcrumb = [],
  title,
  subtitle,
  primaryAction,
  children,
}) {
  return (
    <div
      style={{
        maxWidth: "1400px",
        margin: "0 auto",
        padding: `${spacing.xl} ${spacing["2xl"]}`,
      }}
    >
      {/* Breadcrumb */}
      {breadcrumb.length > 0 && (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "8px",
            marginBottom: spacing.lg,
            fontSize: typography.meta.sizes.md,
            color: colors.ash,
          }}
        >
          {breadcrumb.map((item, idx) => (
            <span key={idx} style={{ display: "flex", alignItems: "center", gap: "8px" }}>
              {idx > 0 && <span>›</span>}
              {item.to ? (
                <Link
                  to={item.to}
                  style={{
                    color: colors.flame,
                    textDecoration: "none",
                    fontWeight: 500,
                  }}
                >
                  {item.label}
                </Link>
              ) : (
                <span style={{ color: colors.ink, fontWeight: 500 }}>
                  {item.label}
                </span>
              )}
            </span>
          ))}
        </div>
      )}

      {/* Header */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          marginBottom: spacing["2xl"],
          flexWrap: "wrap",
          gap: spacing.lg,
        }}
      >
        <div>
          <h1
            style={{
              margin: "0 0 8px",
              fontSize: typography.display.sizes.xl,
              fontWeight: typography.display.weight,
              color: colors.ink,
              fontFamily: typography.display.fontFamily,
              lineHeight: 1.1,
            }}
          >
            {title}
          </h1>
          {subtitle && (
            <p
              style={{
                margin: 0,
                fontSize: typography.body.sizes.lg,
                color: colors.stone,
                fontFamily: typography.body.fontFamily,
              }}
            >
              {subtitle}
            </p>
          )}
        </div>

        {primaryAction && (
          <button
            onClick={primaryAction.onClick}
            style={{
              padding: "12px 24px",
              borderRadius: "10px",
              background: colors.flame,
              border: "none",
              color: colors.paper,
              fontSize: typography.body.sizes.md,
              fontWeight: 600,
              cursor: "pointer",
              fontFamily: typography.body.fontFamily,
              display: "flex",
              alignItems: "center",
              gap: "8px",
            }}
          >
            {primaryAction.label}
          </button>
        )}
      </div>

      {/* Content */}
      {children}
    </div>
  );
}

// TwoColumnLayout - Layout cho detail page (main + sidebar)
export function TwoColumnLayout({ main, sidebar }) {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "2fr 1fr",
        gap: spacing["2xl"],
        alignItems: "flex-start",
      }}
    >
      {/* Main Content */}
      <div>{main}</div>

      {/* Sidebar */}
      <div
        style={{
          position: "sticky",
          top: spacing.xl,
          maxHeight: "calc(100vh - 48px)",
          overflowY: "auto",
        }}
      >
        {sidebar}
      </div>
    </div>
  );
}

// TabBar - Tabs navigation
export function TabBar({ tabs = [], active, onChange }) {
  return (
    <div
      style={{
        display: "flex",
        gap: "4px",
        borderBottom: `2px solid ${colors.parchment}`,
        marginBottom: spacing.xl,
      }}
    >
      {tabs.map((tab) => (
        <button
          key={tab.key}
          onClick={() => onChange?.(tab.key)}
          style={{
            padding: "12px 20px",
            background: "transparent",
            border: "none",
            borderBottom:
              active === tab.key
                ? `3px solid ${colors.flame}`
                : "3px solid transparent",
            color: active === tab.key ? colors.flame : colors.stone,
            fontSize: typography.body.sizes.md,
            fontWeight: active === tab.key ? 700 : 500,
            cursor: "pointer",
            fontFamily: typography.body.fontFamily,
            display: "flex",
            alignItems: "center",
            gap: "6px",
            marginBottom: "-2px",
            transition: "all 0.2s ease",
          }}
        >
          <span>{tab.label}</span>
          {tab.count !== undefined && (
            <span
              style={{
                padding: "2px 8px",
                borderRadius: "999px",
                background:
                  active === tab.key ? colors.flame : colors.parchment,
                color: active === tab.key ? colors.paper : colors.stone,
                fontSize: typography.meta.sizes.sm,
                fontWeight: 600,
              }}
            >
              {tab.count}
            </span>
          )}
        </button>
      ))}
    </div>
  );
}
