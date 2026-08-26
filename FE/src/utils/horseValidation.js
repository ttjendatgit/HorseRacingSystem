const parseDateInput = (value) => {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
};

const calculateAge = (dateOfBirth, today = new Date()) => {
  let age = today.getFullYear() - dateOfBirth.getFullYear();
  const hasNotHadBirthday =
    today.getMonth() < dateOfBirth.getMonth() ||
    (today.getMonth() === dateOfBirth.getMonth() &&
      today.getDate() < dateOfBirth.getDate());

  if (hasNotHadBirthday) {
    age -= 1;
  }

  return age;
};

export const sanitizeDigitsOnly = (value) =>
  String(value ?? "").replace(/\D/g, "");

export const isPositiveIntegerInput = (value) => {
  const text = String(value ?? "");
  return /^[0-9]+$/.test(text) && Number(text) > 0;
};

export const preventInvalidIntegerKey = (event) => {
  const allowedNavigationKeys = new Set([
    "Backspace",
    "Delete",
    "Tab",
    "Escape",
    "Enter",
    "ArrowLeft",
    "ArrowRight",
    "ArrowUp",
    "ArrowDown",
    "Home",
    "End",
  ]);

  if (allowedNavigationKeys.has(event.key) || event.ctrlKey || event.metaKey) {
    return;
  }

  if (!/^[0-9]$/.test(event.key)) {
    event.preventDefault();
  }
};

export const validateHorseMeasurements = ({ weight, height }) => {
  const errors = {};

  if (!isPositiveIntegerInput(weight)) {
    errors.weight = "Cân nặng phải lớn hơn 0";
  }

  if (!isPositiveIntegerInput(height)) {
    errors.height = "Chiều cao phải lớn hơn 0";
  }

  return errors;
};

export const hasHorseMeasurementErrors = (errors) =>
  Boolean(errors?.weight || errors?.height);

export const validateHorseStats = ({
  dateOfBirth,
  age,
  totalRaces,
  totalWins,
}) => {
  if (dateOfBirth) {
    const birthDate = parseDateInput(dateOfBirth);
    const expectedAge = calculateAge(birthDate);

    if (expectedAge < 0) {
      return "Date of birth cannot be in the future.";
    }

    if (age !== expectedAge) {
      return `Age must be ${expectedAge} based on the date of birth.`;
    }
  }

  if (totalWins > totalRaces) {
    return "Total wins cannot be greater than total races.";
  }

  return "";
};
