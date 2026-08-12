export const LOGIN_ROLE_OPTIONS = [
  { value: "horse_owner", label: "Chủ ngựa" },
  { value: "jockey", label: "Kỵ sĩ" },
  { value: "spectator", label: "Khán giả" },
  { value: "referee", label: "Trọng tài" },
  { value: "admin", label: "Quản trị viên" },
];

export const REGISTER_ROLE_OPTIONS = LOGIN_ROLE_OPTIONS.filter(
  (role) =>
    role.value === "horse_owner" ||
    role.value === "jockey" ||
    role.value === "spectator",
);

export const ROLE_ID_BY_VALUE = {
  horse_owner: 1,
  jockey: 2,
  spectator: 3,
};

const ROLE_BY_API = {
  horseowner: "horse_owner",
  jockey: "jockey",
  spectator: "spectator",
  referee: "referee",
  admin: "admin",
};

const ROLE_BY_ID = {
  1: "horse_owner",
  2: "jockey",
  3: "spectator",
  4: "admin",
  5: "referee",
};

export const LABEL_BY_ROLE = LOGIN_ROLE_OPTIONS.reduce((acc, role) => {
  acc[role.value] = role.label;
  return acc;
}, {});

export const unwrapResponseData = (response) => response?.data ?? response;

export const normalizeApiRole = (value) => {
  if (value && typeof value === "object") {
    const nestedValue = value.value ?? value.name ?? value.role;
    if (nestedValue !== undefined) {
      return normalizeApiRole(nestedValue);
    }
  }

  if (typeof value === "number") {
    return ROLE_BY_ID[value] ?? "";
  }

  const key = String(value || "")
    .trim()
    .toLowerCase();

  if (!key) {
    return "";
  }

  if (/^\d+$/.test(key)) {
    return ROLE_BY_ID[Number(key)] ?? "";
  }

  return ROLE_BY_API[key] ?? "";
};

export const getNormalizedUniqueRoles = (apiRoles) =>
  Array.isArray(apiRoles)
    ? Array.from(
        new Set(apiRoles.map((role) => normalizeApiRole(role)).filter(Boolean)),
      )
    : [];

export const buildLoginRoleOptions = (apiRoles) => {
  const uniqueRoles = getNormalizedUniqueRoles(apiRoles);

  return uniqueRoles.length > 0
    ? LOGIN_ROLE_OPTIONS.filter(
        (role) => uniqueRoles.includes(role.value) || role.value === "admin",
      )
    : LOGIN_ROLE_OPTIONS;
};

export const buildRegisterRoleOptions = (apiRoles) => {
  const uniqueRoles = getNormalizedUniqueRoles(apiRoles).filter(
    (role) => ROLE_ID_BY_VALUE[role],
  );

  return uniqueRoles.length > 0
    ? REGISTER_ROLE_OPTIONS.filter((role) => uniqueRoles.includes(role.value))
    : REGISTER_ROLE_OPTIONS;
};
