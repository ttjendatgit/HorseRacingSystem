// R0.1: HorseHealthCheck has no CreatedAt field — CheckedAt (BE/Models/HorseHealthCheck.cs)
// is the sole authoritative timestamp, and it's what the backend's own
// "latest check per Horse+Race" StartRace-readiness query orders by
// (RaceEntryService.ValidateRaceEntriesForStartAsync). FE must pick the same
// check as "latest" or the two could disagree about a horse's fitness.
export function getLatestHealthCheck(checks) {
  if (!Array.isArray(checks) || checks.length === 0) return null;
  return [...checks].sort((a, b) => {
    const aTime = new Date(a?.checkedAt ?? a?.CheckedAt ?? 0).getTime();
    const bTime = new Date(b?.checkedAt ?? b?.CheckedAt ?? 0).getTime();
    return bTime - aTime;
  })[0];
}
