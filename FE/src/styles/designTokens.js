// Design Tokens - Editorial Sport Command Center
// Based on PROMPT_QUAN_LY_GIAI_DAU_VA_CUOC_DUA.md

export const colors = {
  // Backgrounds
  cream: "#FBF7F0",
  paper: "#FFFFFF",
  parchment: "#E8DFCE",

  // Text
  ink: "#1A1613",
  stone: "#6B6259",
  ash: "#9B928A",

  // Accents
  flame: "#C6452C",
  marigold: "#E6A54A",
  crimson: "#9C3226",

  // Status colors
  statusNeutral: "#E8DFCE",
  statusGreen: "#C8E0C0",
  statusFlame: "#E6A54A",
  statusRed: "#E8B0A0",
};

export const typography = {
  // Display (headings lớn)
  display: {
    fontFamily: "'Playfair Display', Georgia, serif",
    sizes: {
      xl: "40px",
      lg: "32px",
      md: "28px",
    },
    weight: 600,
  },

  // Section (headings nhỏ)
  section: {
    fontFamily: "Inter, system-ui, sans-serif",
    sizes: {
      lg: "22px",
      md: "18px",
    },
    weight: 600,
  },

  // Body
  body: {
    fontFamily: "Inter, system-ui, sans-serif",
    sizes: {
      lg: "16px",
      md: "14px",
      sm: "13px",
    },
    weight: {
      normal: 400,
      medium: 500,
    },
  },

  // Meta / caption
  meta: {
    fontFamily: "Inter, system-ui, sans-serif",
    sizes: {
      md: "13px",
      sm: "12px",
    },
    weight: 400,
  },

  // Mono (for GUIDs, codes)
  mono: {
    fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
    sizes: {
      md: "14px",
      sm: "12px",
    },
  },
};

export const spacing = {
  xs: "4px",
  sm: "8px",
  md: "12px",
  lg: "16px",
  xl: "24px",
  "2xl": "32px",
  "3xl": "48px",
  "4xl": "64px",
};

export const radius = {
  card: "16px",
  button: "10px",
  input: "10px",
  pill: "999px",
};

export const shadows = {
  card: "0 1px 2px rgba(26,22,19,0.04)",
  cardHover: "0 8px 24px rgba(26,22,19,0.08)",
  modal: "0 24px 48px rgba(26,22,19,0.18)",
};

// Status mappings
export const statusColors = {
  draft: {
    bg: colors.statusNeutral,
    text: colors.stone,
    label: "Bản nháp",
  },
  published: {
    bg: colors.statusGreen,
    text: "#2D5016",
    label: "Đã công bố",
  },
  ongoing: {
    bg: colors.statusFlame,
    text: "#7C2D12",
    label: "Đang diễn ra",
  },
  finished: {
    bg: colors.statusNeutral,
    text: colors.stone,
    label: "Đã kết thúc",
  },
  cancelled: {
    bg: colors.statusRed,
    text: "#7F1D1D",
    label: "Đã hủy",
  },
  // Race statuses
  scheduled: {
    bg: "rgba(37,99,235,0.1)",
    text: "#2563eb",
    label: "Đã lên lịch",
  },
  inprogress: {
    bg: "rgba(245,158,11,0.1)",
    text: "#f59e0b",
    label: "Đang diễn ra",
  },
  registrationopen: {
    bg: "rgba(16,185,129,0.1)",
    text: "#059669",
    label: "Chuẩn bị",
  },
  registrationclosed: {
    bg: "rgba(37,99,235,0.1)",
    text: "#1d4ed8",
    label: "Chuẩn bị",
  },
};

// Gradient presets for hero banners
export const gradients = {
  draft: `linear-gradient(135deg, ${colors.stone} 0%, ${colors.parchment} 100%)`,
  published: `linear-gradient(135deg, ${colors.marigold} 0%, ${colors.cream} 100%)`,
  ongoing: `linear-gradient(135deg, ${colors.flame} 0%, ${colors.marigold} 100%)`,
  finished: `linear-gradient(135deg, ${colors.ink} 0%, ${colors.stone} 100%)`,
};
