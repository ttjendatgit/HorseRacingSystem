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
