import { colors, typography, gradients } from "../../styles/designTokens";
import { StatusPill } from "./Primitives";

// HeroBanner - Full-width hero cho detail page
export function HeroBanner({
  status,
  title,
  subtitle,
  meta = [],
  actions = [],
  imageUrl,
}) {
  const gradient = gradients[status] || gradients.draft;

  return (
    <div
      style={{
        position: "relative",
        height: "240px",
        borderRadius: "20px",
        overflow: "hidden",
        marginBottom: "24px",
        background: imageUrl
          ? `url(${imageUrl}) center/cover no-repeat`
          : gradient,
      }}
    >
      {/* Overlay */}
      <div
        style={{
          position: "absolute",
          inset: 0,
          background:
            "linear-gradient(to top, rgba(26,22,19,0.8), rgba(26,22,19,0.2))",
        }}
      />

      {/* Content */}
      <div
        style={{
          position: "relative",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "flex-end",
          padding: "32px",
        }}
      >
        {/* Top Row - Status + Actions */}
        <div
          style={{
            position: "absolute",
            top: "24px",
            left: "32px",
            right: "32px",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
          }}
        >
          <StatusPill status={status} size="lg" />

          {/* Actions */}
          {actions.length > 0 && (
            <div style={{ display: "flex", gap: "8px" }}>
              {actions.map((action, idx) => (
                <button
                  key={idx}
                  onClick={action.onClick}
                  style={{
                    padding: "8px 16px",
                    borderRadius: "10px",
                    background:
                      action.variant === "icon"
                        ? "rgba(255,255,255,0.9)"
                        : "transparent",
                    border:
                      action.variant === "icon"
                        ? "none"
                        : "1px solid rgba(255,255,255,0.3)",
                    color:
                      action.variant === "icon" ? colors.stone : colors.paper,
                    fontSize: typography.body.sizes.md,
                    fontWeight: 500,
                    cursor: "pointer",
                    fontFamily: typography.body.fontFamily,
                  }}
                >
                  {action.label}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Bottom Row - Title + Meta */}
        <div>
          <h1
            style={{
              margin: "0 0 8px",
              fontSize: typography.display.sizes.xl,
              fontWeight: typography.display.weight,
              color: colors.paper,
              fontFamily: typography.display.fontFamily,
              lineHeight: 1.1,
            }}
          >
            {title}
          </h1>

          {subtitle && (
            <p
              style={{
                margin: "0 0 16px",
                fontSize: typography.body.sizes.lg,
                color: "rgba(255,255,255,0.8)",
                fontFamily: typography.body.fontFamily,
              }}
            >
              {subtitle}
            </p>
          )}

          {/* Meta Info */}
          {meta.length > 0 && (
            <div
              style={{
                display: "flex",
                gap: "24px",
                flexWrap: "wrap",
              }}
            >
              {meta.map((item, idx) => (
                <span
                  key={idx}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "6px",
                    fontSize: typography.body.sizes.md,
                    color: "rgba(255,255,255,0.9)",
                  }}
                >
                  <span>{item.icon}</span>
                  <span>{item.text}</span>
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
