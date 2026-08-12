import { colors, typography } from "../../styles/designTokens";

// StateStepper - Horizontal timeline visualization cho lifecycle
export function StateStepper({ states = [], currentStatus, onTransition }) {
  const currentIndex = states.findIndex((s) => s.key === currentStatus);

  return (
    <div style={{ padding: "24px 0" }}>
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          position: "relative",
        }}
      >
        {/* Connection Line */}
        <div
          style={{
            position: "absolute",
            top: "16px",
            left: "24px",
            right: "24px",
            height: "2px",
            background: colors.parchment,
            zIndex: 0,
          }}
        />

        {/* Progress Line */}
        <div
          style={{
            position: "absolute",
            top: "16px",
            left: "24px",
            width: currentIndex >= 0 ? `${(currentIndex / (states.length - 1)) * 100}%` : "0%",
            height: "2px",
            background: colors.flame,
            zIndex: 1,
            transition: "width 0.3s ease",
          }}
        />

        {/* States */}
        {states.map((state, idx) => {
          const isPast = idx < currentIndex;
          const isCurrent = idx === currentIndex;
          const isBranch = state.branch; // Cancelled state

          return (
            <div
              key={state.key}
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                flex: isBranch ? "0 0 auto" : "1",
                position: "relative",
                zIndex: 2,
                marginLeft: isBranch ? "24px" : "0",
              }}
            >
              {/* Dot */}
              <button
                onClick={() => onTransition?.(state.key)}
                disabled={!state.transitionable}
                style={{
                  width: isCurrent ? "32px" : "24px",
                  height: isCurrent ? "32px" : "24px",
                  borderRadius: "50%",
                  background: isPast
                    ? colors.flame
                    : isCurrent
                    ? colors.paper
                    : colors.parchment,
                  border: isCurrent
                    ? `3px solid ${colors.flame}`
                    : isPast
                    ? "none"
                    : `2px dashed ${colors.parchment}`,
                  cursor: state.transitionable ? "pointer" : "default",
                  transition: "all 0.2s ease",
                  boxShadow: isCurrent
                    ? `0 0 0 4px ${colors.statusFlame}40`
                    : "none",
                  opacity: isBranch && !isCurrent ? 0.5 : 1,
                }}
              />

              {/* Label */}
              <div
                style={{
                  marginTop: "12px",
                  textAlign: "center",
                  maxWidth: "100px",
                }}
              >
                <div
                  style={{
                    fontSize: typography.meta.sizes.sm,
                    fontWeight: isCurrent ? 700 : 500,
                    color: isPast
                      ? colors.flame
                      : isCurrent
                      ? colors.ink
                      : colors.ash,
                    textDecoration:
                      isBranch && !isCurrent ? "line-through" : "none",
                    opacity: isBranch && !isCurrent ? 0.5 : 1,
                  }}
                >
                  {state.label}
                </div>

                {/* Date */}
                {state.date && (
                  <div
                    style={{
                      fontSize: typography.meta.sizes.sm,
                      color: colors.ash,
                      marginTop: "4px",
                    }}
                  >
                    {state.date}
                  </div>
                )}

                {/* Current Indicator */}
                {isCurrent && (
                  <div
                    style={{
                      fontSize: "10px",
                      fontWeight: 700,
                      color: colors.flame,
                      marginTop: "4px",
                      letterSpacing: "0.5px",
                      textTransform: "uppercase",
                    }}
                  >
                    You are here
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
