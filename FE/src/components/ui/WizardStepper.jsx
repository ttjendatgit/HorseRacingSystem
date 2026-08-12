import { colors, typography } from "../../styles/designTokens";

// WizardStepper - Progress indicator cho form wizard
export function WizardStepper({ steps = [], current = 0, onStepClick }) {
  return (
    <div style={{ marginBottom: "32px" }}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          position: "relative",
        }}
      >
        {/* Progress Line Background */}
        <div
          style={{
            position: "absolute",
            top: "16px",
            left: "32px",
            right: "32px",
            height: "2px",
            background: colors.parchment,
            zIndex: 0,
          }}
        />

        {/* Progress Line Fill */}
        <div
          style={{
            position: "absolute",
            top: "16px",
            left: "32px",
            width: `${(current / (steps.length - 1)) * 100}%`,
            height: "2px",
            background: colors.flame,
            zIndex: 1,
            transition: "width 0.3s ease",
          }}
        />

        {/* Steps */}
        {steps.map((step, idx) => {
          const isPast = idx < current;
          const isCurrent = idx === current;

          return (
            <div
              key={step.key}
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                flex: 1,
                position: "relative",
                zIndex: 2,
              }}
            >
              {/* Step Circle */}
              <button
                onClick={() => isPast && onStepClick?.(step.key)}
                disabled={!isPast}
                style={{
                  width: "32px",
                  height: "32px",
                  borderRadius: "50%",
                  background: isPast
                    ? colors.flame
                    : isCurrent
                    ? colors.paper
                    : colors.parchment,
                  border: isCurrent
                    ? `3px solid ${colors.flame}`
                    : "none",
                  color: isPast || isCurrent ? colors.paper : colors.ash,
                  fontSize: typography.body.sizes.md,
                  fontWeight: 700,
                  cursor: isPast ? "pointer" : "default",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  transition: "all 0.2s ease",
                }}
              >
                {isPast ? "✓" : idx + 1}
              </button>

              {/* Step Label */}
              <div
                style={{
                  marginTop: "8px",
                  fontSize: typography.meta.sizes.md,
                  fontWeight: isCurrent ? 700 : 500,
                  color: isPast
                    ? colors.flame
                    : isCurrent
                    ? colors.ink
                    : colors.ash,
                  textAlign: "center",
                }}
              >
                {step.label}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
