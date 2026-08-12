import { RefereeNotificationCard } from "./RefereeNotificationCard";
import { useRefereeAssignments } from "../../hooks/useRefereeAssignments";
import "./RefereeNotificationsPanel.css";

/**
 * RefereeNotificationsPanel Component
 * Container component that displays all pending referee assignment notifications
 *
 * Usage:
 * <RefereeNotificationsPanel />
 */
export function RefereeNotificationsPanel() {
  const { assignments, isLoading, error, removeAssignment, updateAssignment } =
    useRefereeAssignments(true);

  /**
   * Handle when referee responds to an assignment
   */
  const handleAssignmentResponse = (responseData) => {
    const { id, status } = responseData;

    // Update the assignment status in state
    updateAssignment(id, {
      status,
      respondedAt: new Date().toISOString(),
    });

    // Optional: Show a toast/notification to user
  };

  /**
   * Handle removing notification from UI
   */
  const handleRemoveNotification = (assignmentId) => {
    removeAssignment(assignmentId);
  };

  // Show loading state
  if (isLoading && assignments.length === 0) {
    return (
      <div className="referee-notifications-panel loading">
        <p>Đang tải phân công...</p>
      </div>
    );
  }

  // Show error state
  if (error && assignments.length === 0) {
    return (
      <div className="referee-notifications-panel error">
        <p>Không thể tải phân công: {error}</p>
        <button onClick={() => window.location.reload()}>Thử lại</button>
      </div>
    );
  }

  // Show empty state
  if (assignments.length === 0) {
    return (
      <div className="referee-notifications-panel empty">
        <p>Không có phân công trọng tài đang chờ</p>
      </div>
    );
  }

  // Show pending assignments
  return (
    <div className="referee-notifications-panel">
      <div className="notifications-header">
        <h2>Phân công trọng tài ({assignments.length})</h2>
      </div>

      <div className="notifications-list">
        {assignments.map((assignment) => (
          <RefereeNotificationCard
            key={assignment.id}
            assignment={assignment}
            onResponse={handleAssignmentResponse}
            onRemove={handleRemoveNotification}
          />
        ))}
      </div>
    </div>
  );
}

export default RefereeNotificationsPanel;
