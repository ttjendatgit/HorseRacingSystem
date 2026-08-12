import { request } from "./apiClient";

/**
 * Get assignments for the current referee
 * @param {string} [status] - Optional filter: "pending", "confirmed", etc.
 * @returns {Promise} - List of assignments
 */
export function getMyAssignments(status) {
  const qs = status ? `?status=${encodeURIComponent(status)}` : "";
  return request(`/api/referees/my-assignments${qs}`).then(
    (d) => d?.data ?? d?.Data ?? d
  );
}

/**
 * Get pending referee assignments for the current referee
 * @returns {Promise} - List of pending assignments
 */
export function getPendingRefereeAssignments() {
  return getMyAssignments("Assigned");
}

/**
 * Get all referee assignments (accepted, rejected, pending)
 * @returns {Promise} - List of all assignments
 */
export function getAllRefereeAssignments() {
  return getMyAssignments();
}

/**
 * Respond to a referee assignment (accept or reject)
 * @param {string} assignmentId - The GUID of the referee assignment
 * @param {string} response - "Accept" or "Reject"
 * @returns {Promise} - Response from the API
 */
export function respondToRefereeAssignment(assignmentId, response) {
  return request(`/api/referees/assignments/${assignmentId}/respond`, {
    method: "POST",
    body: JSON.stringify({
      response,
      notes: `Referee ${response.toLowerCase()}ed the assignment.`,
    }),
  });
}
