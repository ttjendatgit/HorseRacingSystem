import { useState } from "react";
import { respondToRefereeAssignment } from "../../services/refereeAssignmentApi";
import "./RefereeNotificationCard.css";

/**
 * RefereeNotificationCard Component
 * Displays a referee assignment notification with Accept/Reject buttons
 *
 * Props:
 * - assignment: Object with { id, matchName, dateTime, tournamentName, status }
 * - onResponse: Callback function triggered after user responds (receives { id, response, status })
 * - onRemove: Optional callback to remove notification from UI after response
 */
export function RefereeNotificationCard({ assignment, onResponse, onRemove }) {
  const [isLoading, setIsLoading] = useState(false);
  const [status, setStatus] = useState(assignment.status || "pending"); // pending, accepted, rejected
  const [error, setError] = useState(null);

  /**
   * Handle Accept button click
   */
  const handleAccept = async () => {
    await handleResponse("Accept");
  };

  /**
   * Handle Reject button click
   */
  const handleReject = async () => {
    await handleResponse("Reject");
  };

  /**
   * Generic response handler
   * @param {string} response - "Accept" or "Reject"
   */
  const handleResponse = async (response) => {
    setIsLoading(true);
    setError(null);

    try {
      // Call the API to respond to the assignment
      await respondToRefereeAssignment(assignment.id, response);

      // Update local status
      const newStatus = response === "Accept" ? "accepted" : "rejected";
      setStatus(newStatus);

      // Trigger parent callback
      if (onResponse) {
        onResponse({
          id: assignment.id,
          response,
          status: newStatus,
        });
      }

      // Optional: Remove from UI after successful response (if callback provided)
      if (onRemove && response === "Accept") {
        // Only remove if accepted, or adjust based on your needs
        setTimeout(() => onRemove(assignment.id), 1000);
      }
    } catch (err) {
      console.error("Error responding to assignment:", err);
      setError(err.message || "Failed to submit response. Please try again.");
    } finally {
      setIsLoading(false);
    }
  };

  // Format date and time
  const formatDateTime = (dateTimeString) => {
    if (!dateTimeString) return "TBD";
    const date = new Date(dateTimeString);
    return date.toLocaleString("vi-VN", {
      month: "short",
      day: "numeric",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  // Don't show if already responded (optional - adjust based on design)
  // if (status !== "pending") {
  //   return null;
  // }

  return (
    <div className="referee-notification-card">
      <div className="notification-header">
        <div className="notification-icon">
          <span>📋</span>
        </div>
        <div className="notification-title">
          <h3>New Referee Assignment</h3>
          {assignment.tournamentName && (
            <p className="tournament-name">{assignment.tournamentName}</p>
          )}
        </div>
        <div className={`notification-status status-${status}`}>
          {status.charAt(0).toUpperCase() + status.slice(1)}
        </div>
      </div>

      <div className="notification-body">
        <div className="assignment-details">
          <p className="match-info">
            <strong>Match:</strong> {assignment.raceName || assignment.matchName || "N/A"}
          </p>
          <p className="date-info">
            <strong>Date & Time:</strong> {formatDateTime(assignment.assignedAt || assignment.dateTime)}
          </p>
        </div>

        {error && <div className="error-message">{error}</div>}
      </div>

      <div className="notification-actions">
        <button
          className="btn btn-accept"
          onClick={handleAccept}
          disabled={isLoading || status !== "pending"}
          title={status !== "pending" ? `Already ${status}` : "Accept assignment"}
        >
          {isLoading && status === "pending" ? "Processing..." : "Accept"}
        </button>
        <button
          className="btn btn-reject"
          onClick={handleReject}
          disabled={isLoading || status !== "pending"}
          title={status !== "pending" ? `Already ${status}` : "Reject assignment"}
        >
          {isLoading && status === "pending" ? "Processing..." : "Reject"}
        </button>
      </div>
    </div>
  );
}

export default RefereeNotificationCard;
