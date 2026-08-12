import { useState } from "react";
import { colors, typography, radius, shadows, gradients } from "../../styles/designTokens";
import { StatusPill } from "./Primitives";

// MegaCard - Card chính trong list với hero gradient và context menu
export function MegaCard({
  status,
  title,
  meta = [],
  stats = [],
  menu = [],
  primaryAction,
  secondaryAction,
  onClick,
}) {
  const [showMenu, setShowMenu] = useState(false);

  const gradient = gradients[status] || gradients.draft;

  return (
    <div
      role={onClick ? "button" : undefined}
      tabIndex={onClick ? 0 : undefined}
      style={{
        borderRadius: radius.card,
        background: colors.paper,
        boxShadow: shadows.card,
        overflow: "hidden",
        transition: "box-shadow 0.2s ease",
        cursor: onClick ? "pointer" : "default",
      }}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.target !== e.currentTarget) return;
        if ((e.key === "Enter" || e.key === " ") && onClick) {
          e.preventDefault();
          onClick?.();
        }
      }}
      onMouseEnter={(e) => {
        e.currentTarget.style.boxShadow = shadows.cardHover;
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.boxShadow = shadows.card;
        setShowMenu(false);
      }}
    >
      {/* Hero Banner */}
      <div
        style={{
          height: "120px",
          background: gradient,
          position: "relative",
          padding: "16px",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
        }}
      >
        <StatusPill status={status} size="md" />

        {/* Context Menu Button */}
        {menu.length > 0 && (
          <button
            onClick={(e) => {
              e.stopPropagation();
              setShowMenu(!showMenu);
            }}
            style={{
              background: "rgba(255,255,255,0.9)",
              border: "none",
              borderRadius: "50%",
              width: "32px",
              height: "32px",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              cursor: "pointer",
              fontSize: "18px",
              color: colors.stone,
              opacity: showMenu ? 1 : 0,
              transition: "opacity 0.2s ease",
            }}
          >
            ⋯
          </button>
        )}

        {/* Context Menu Dropdown */}
        {showMenu && (
          <div
            role="menu"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => { if (e.key === "Escape") setShowMenu(false); }}
            style={{
              position: "absolute",
              top: "52px",
              right: "16px",
              background: colors.paper,
              borderRadius: radius.button,
              boxShadow: shadows.modal,
              padding: "8px 0",
              minWidth: "160px",
              zIndex: 10,
            }}
          >
            {menu.map((item, idx) => (
              <button
                key={idx}
                onClick={() => {
                  item.onClick?.();
                  setShowMenu(false);
                }}
                style={{
                  display: "block",
                  width: "100%",
                  padding: "8px 16px",
                  background: "transparent",
                  border: "none",
                  textAlign: "left",
                  fontSize: typography.body.sizes.md,
                  color: item.destructive ? colors.flame : colors.ink,
                  cursor: "pointer",
                  fontFamily: typography.body.fontFamily,
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = colors.cream;
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = "transparent";
                }}
              >
                {item.label}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Content */}
      <div style={{ padding: "20px" }}>
        <h3
          style={{
            margin: "0 0 8px",
            fontSize: typography.display.sizes.md,
            fontWeight: typography.display.weight,
            color: colors.ink,
            fontFamily: typography.display.fontFamily,
            lineHeight: 1.2,
          }}
        >
          {title}
        </h3>

        {/* Meta Info */}
        {meta.length > 0 && (
          <div
            style={{
              display: "flex",
              gap: "12px",
              marginBottom: "16px",
              flexWrap: "wrap",
            }}
          >
            {meta.map((item, idx) => (
              <span
                key={idx}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "4px",
                  fontSize: typography.meta.sizes.md,
                  color: colors.stone,
                }}
              >
                <span>{item.icon}</span>
                <span>{item.text}</span>
              </span>
            ))}
          </div>
        )}

        {/* Stats Row */}
        {stats.length > 0 && (
          <div
            style={{
              display: "flex",
              gap: "8px",
              marginBottom: "16px",
              flexWrap: "wrap",
            }}
          >
            {stats.map((stat, idx) => (
              <div
                key={idx}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "6px",
                  padding: "6px 12px",
                  borderRadius: radius.pill,
                  background: colors.cream,
                  fontSize: typography.meta.sizes.md,
                  color: colors.stone,
                }}
              >
                <strong style={{ color: colors.ink }}>{stat.value}</strong>
                <span>{stat.label}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Footer Actions */}
      {(primaryAction || secondaryAction) && (
        <div
          style={{
            padding: "16px 20px",
            borderTop: `1px solid ${colors.parchment}`,
            display: "flex",
            gap: "8px",
            justifyContent: "flex-end",
          }}
        >
          {secondaryAction && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                secondaryAction.onClick?.();
              }}
              style={{
                padding: "8px 16px",
                borderRadius: radius.button,
                background: "transparent",
                border: `1px solid ${colors.parchment}`,
                color: colors.stone,
                fontSize: typography.body.sizes.md,
                fontWeight: 500,
                cursor: "pointer",
                fontFamily: typography.body.fontFamily,
              }}
            >
              {secondaryAction.label}
            </button>
          )}
          {primaryAction && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                primaryAction.onClick?.();
              }}
              style={{
                padding: "8px 16px",
                borderRadius: radius.button,
                background: colors.flame,
                border: "none",
                color: colors.paper,
                fontSize: typography.body.sizes.md,
                fontWeight: 600,
                cursor: "pointer",
                fontFamily: typography.body.fontFamily,
              }}
            >
              {primaryAction.label}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
