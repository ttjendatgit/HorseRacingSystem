import { useState } from "react";
import { colors, typography, radius, shadows } from "../../styles/designTokens";

// CommandBar - Search + filters + view toggle + bulk actions
export function CommandBar({
  searchValue,
  onSearchChange,
  searchPlaceholder = "Tìm kiếm...",
  filters = [],
  views = ["grid", "table"],
  activeView,
  onViewChange,
  selectedCount = 0,
  bulkActions = [],
}) {
  const [showBulkMenu, setShowBulkMenu] = useState(false);

  return (
    <div
      style={{
        display: "flex",
        gap: "12px",
        alignItems: "center",
        padding: "12px 16px",
        background: colors.paper,
        borderRadius: radius.card,
        boxShadow: shadows.card,
        marginBottom: "24px",
        flexWrap: "wrap",
        position: "sticky",
        top: "0",
        zIndex: 10,
      }}
    >
      {/* Search */}
      <div style={{ flex: "1 1 200px", position: "relative" }}>
        <span
          style={{
            position: "absolute",
            left: "12px",
            top: "50%",
            transform: "translateY(-50%)",
            fontSize: "16px",
            color: colors.ash,
          }}
        >
          🔍
        </span>
        <input
          type="text"
          value={searchValue}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder={searchPlaceholder}
          style={{
            width: "100%",
            padding: "8px 12px 8px 36px",
            borderRadius: radius.input,
            border: `1px solid ${colors.parchment}`,
            background: colors.cream,
            fontSize: typography.body.sizes.md,
            color: colors.ink,
            outline: "none",
            fontFamily: typography.body.fontFamily,
          }}
        />
      </div>

      {/* Filters */}
      {filters.map((filter) => (
        <select
          key={filter.key}
          value={filter.value}
          onChange={(e) => filter.onChange(e.target.value)}
          style={{
            padding: "8px 12px",
            borderRadius: radius.input,
            border: `1px solid ${colors.parchment}`,
            background: colors.paper,
            fontSize: typography.body.sizes.md,
            color: colors.ink,
            cursor: "pointer",
            fontFamily: typography.body.fontFamily,
          }}
        >
          <option value="">{filter.label}</option>
          {filter.options.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      ))}

      {/* View Toggle */}
      {views.length > 1 && (
        <div
          style={{
            display: "flex",
            borderRadius: radius.input,
            overflow: "hidden",
            border: `1px solid ${colors.parchment}`,
          }}
        >
          {views.map((view) => (
            <button
              key={view}
              onClick={() => onViewChange?.(view)}
              style={{
                padding: "8px 12px",
                background: activeView === view ? colors.flame : colors.paper,
                color: activeView === view ? colors.paper : colors.stone,
                border: "none",
                cursor: "pointer",
                fontSize: "16px",
              }}
            >
              {view === "grid" ? "▦" : "☰"}
            </button>
          ))}
        </div>
      )}

      {/* Bulk Actions */}
      {selectedCount > 0 && bulkActions.length > 0 && (
        <div style={{ position: "relative" }}>
          <button
            onClick={() => setShowBulkMenu(!showBulkMenu)}
            style={{
              padding: "8px 16px",
              borderRadius: radius.button,
              background: colors.cream,
              border: `1px solid ${colors.parchment}`,
              color: colors.ink,
              fontSize: typography.body.sizes.md,
              fontWeight: 500,
              cursor: "pointer",
              fontFamily: typography.body.fontFamily,
              display: "flex",
              alignItems: "center",
              gap: "6px",
            }}
          >
            <span>☑</span>
            <span>{selectedCount} đã chọn</span>
            <span>▾</span>
          </button>

          {showBulkMenu && (
            <div
              style={{
                position: "absolute",
                top: "100%",
                right: "0",
                marginTop: "4px",
                background: colors.paper,
                borderRadius: radius.button,
                boxShadow: shadows.modal,
                padding: "8px 0",
                minWidth: "160px",
                zIndex: 20,
              }}
            >
              {bulkActions.map((action, idx) => (
                <button
                  key={idx}
                  onClick={() => {
                    action.onClick?.();
                    setShowBulkMenu(false);
                  }}
                  style={{
                    display: "block",
                    width: "100%",
                    padding: "8px 16px",
                    background: "transparent",
                    border: "none",
                    textAlign: "left",
                    fontSize: typography.body.sizes.md,
                    color: action.destructive ? colors.flame : colors.ink,
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
                  {action.label}
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
