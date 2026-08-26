export const isViolationResolved = (violation) => {
  const penalty = violation?.penalty ?? violation?.Penalty;
  return typeof penalty === "string"
    ? penalty.trim().length > 0
    : Boolean(penalty);
};
