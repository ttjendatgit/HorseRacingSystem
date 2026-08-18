import { apiToUtcDate } from "./vnDateTime";

// Task B Final §3: the ONE shared Owner-registration-eligibility rule, used identically by every
// badge and every registration button/gate across the Owner FE. Never derive this from
// RaceStatus.RegistrationOpen/Closed or Tournament.IsActive — both are unrelated concerns
// (Race-level event progress / server-derived Ongoing flag, respectively).
export const canRegisterTournament = (tournament) => {
  const status = tournament?.statusName ?? tournament?.StatusName;
  const deadline = tournament?.registrationDeadline ?? tournament?.RegistrationDeadline;
  return status === "Published" && !!deadline && apiToUtcDate(deadline) > new Date();
};
