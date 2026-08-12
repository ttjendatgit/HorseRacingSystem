/**
 * INTEGRATION GUIDE: How to Use the Referee Assignment System in Your App
 * =========================================================================
 *
 * This guide shows how to integrate the Referee Assignment notification system
 * into your existing React application.
 */

// ============================================================================
// 1. BASIC SETUP - Add to Your Router
// ============================================================================

/*
// In your router configuration file (e.g., src/pages/index.jsx or src/App.jsx):

import { RefereeAssignmentPage } from "@/components/RefereeNotification/RefereeAssignmentPage";

const routes = [
  // ... other routes
  {
    path: "/referee/assignments",
    element: <RefereeAssignmentPage />,
    requiredRole: "Referee", // Add role check if needed
  },
];
*/

// ============================================================================
// 2. ADD TO REFEREE DASHBOARD
// ============================================================================

/*
// In your referee dashboard or home page:

import { RefereeNotificationsPanel } from "@/components/RefereeNotification/RefereeNotificationsPanel";

export function RefereeDashboard() {
  return (
    <div className="dashboard">
      <h1>Welcome, Referee!</h1>

//       {/* Add the notification panel at the top */
//       <RefereeNotificationsPanel />

//       {/* Your other dashboard content */}
//       <section>Other content...</section>
//     </div>
//   );
// }
// */

// ============================================================================
// 3. USE THE HOOK DIRECTLY
// ============================================================================

/*
// If you need more control, use the hook directly:

import { useRefereeAssignments } from "@/hooks/useRefereeAssignments";
import { RefereeNotificationCard } from "@/components/RefereeNotification/RefereeNotificationCard";

export function MyCustomComponent() {
  const { assignments, isLoading, error, removeAssignment } =
    useRefereeAssignments(true);

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div>
      {assignments.map(assignment => (
        <RefereeNotificationCard
          key={assignment.id}
          assignment={assignment}
          onRemove={(id) => removeAssignment(id)}
        />
      ))}
    </div>
  );
}
*/

// ============================================================================
// 4. DISPLAY IN NOTIFICATION BADGE
// ============================================================================

/*
// Show pending count in a badge:

import { useRefereeAssignments } from "@/hooks/useRefereeAssignments";

export function Header() {
  const { assignments } = useRefereeAssignments(true);
  const pendingCount = assignments.filter(a => a.status === "pending").length;

  return (
    <header>
      <nav>
        <a href="/referee/assignments">
          Assignments
          {pendingCount > 0 && (
            <span className="badge">{pendingCount}</span>
          )}
        </a>
      </nav>
    </header>
  );
}
*/

// ============================================================================
// 5. ADD TO NAVIGATION MENU
// ============================================================================

/*
// In your referee navigation menu:

export function RefereeMenu() {
  return (
    <nav>
      <ul>
        <li><a href="/referee/dashboard">Dashboard</a></li>
        <li><a href="/referee/assignments">📋 My Assignments</a></li>
        <li><a href="/referee/history">History</a></li>
        <li><a href="/referee/profile">Profile</a></li>
      </ul>
    </nav>
  );
}
*/

// ============================================================================
// 6. ADVANCED: REAL-TIME UPDATES WITH WEBSOCKET (Optional)
// ============================================================================

/*
// Create a WebSocket handler for real-time notifications:

// services/refereeAssignmentApi.js (add this):

export function subscribeToAssignments(onNewAssignment) {
  const token = localStorage.getItem("authToken");
  const ws = new WebSocket(
    `ws://localhost:5226/api/referee/assignments/subscribe?token=${token}`
  );

  ws.onmessage = (event) => {
    const assignment = JSON.parse(event.data);
    onNewAssignment(assignment);
  };

  ws.onerror = (error) => {
    console.error("WebSocket error:", error);
  };

  return ws;
}

// Then use in your component:

import { useEffect, useRef } from "react";
import { subscribeToAssignments } from "@/services/refereeAssignmentApi";
import { useRefereeAssignments } from "@/hooks/useRefereeAssignments";

export function RefereeAssignmentListener() {
  const { assignments, refreshAssignments } = useRefereeAssignments(true);
  const wsRef = useRef(null);

  useEffect(() => {
    wsRef.current = subscribeToAssignments((newAssignment) => {
      console.log("New assignment received:", newAssignment);
      refreshAssignments(); // Refresh the list
      // Optional: Show a toast notification
    });

    return () => {
      if (wsRef.current) {
        wsRef.current.close();
      }
    };
  }, [refreshAssignments]);

  return null; // This is just a listener component
}

// Add to your root App component:
// <RefereeAssignmentListener />
*/

// ============================================================================
// 7. ENVIRONMENT VARIABLES
// ============================================================================

/*
// In your .env.local file:

VITE_API_BASE_URL=http://localhost:5226
VITE_ENABLE_NOTIFICATIONS=true
VITE_REFRESH_INTERVAL=30000  // Refresh every 30 seconds

// Then use in the hook:
*/

import { useEffect } from "react";
import { getPendingRefereeAssignments } from "@/services/refereeAssignmentApi";

/*
export function useRefereeAssignmentsWithAutoRefresh() {
  const { assignments, refreshAssignments } = useRefereeAssignments(true);

  const refreshInterval =
    import.meta.env.VITE_REFRESH_INTERVAL || 30000;

  useEffect(() => {
    const interval = setInterval(
      refreshAssignments,
      refreshInterval
    );

    return () => clearInterval(interval);
  }, [refreshAssignments, refreshInterval]);

  return { assignments };
}
*/

// ============================================================================
// 8. ERROR HANDLING & RECOVERY
// ============================================================================

/*
// Create an error boundary for better error handling:

class RefereeAssignmentErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true };
  }

  componentDidCatch(error, errorInfo) {
    console.error("Referee assignment error:", error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-container">
          <h2>Failed to load assignments</h2>
          <button onClick={() => window.location.reload()}>
            Reload Page
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}

// Use it:
// <RefereeAssignmentErrorBoundary>
//   <RefereeNotificationsPanel />
// </RefereeAssignmentErrorBoundary>
*/

// ============================================================================
// 9. TESTING
// ============================================================================

/*
// Example test for the component:

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RefereeNotificationCard } from "@/components/RefereeNotification/RefereeNotificationCard";

test("renders notification card with accept and reject buttons", () => {
  const assignment = {
    id: 1,
    matchName: "Horse A vs Horse B",
    dateTime: "2026-06-15T14:30:00",
    tournamentName: "Championship",
    status: "pending"
  };

  render(
    <RefereeNotificationCard assignment={assignment} />
  );

  expect(screen.getByText("Horse A vs Horse B")).toBeInTheDocument();
  expect(screen.getByText("Accept")).toBeInTheDocument();
  expect(screen.getByText("Reject")).toBeInTheDocument();
});

test("calls onResponse when accept button is clicked", async () => {
  const mockOnResponse = jest.fn();
  const assignment = {
    id: 1,
    matchName: "Test Match",
    dateTime: "2026-06-15T14:30:00",
    status: "pending"
  };

  render(
    <RefereeNotificationCard
      assignment={assignment}
      onResponse={mockOnResponse}
    />
  );

  const acceptBtn = screen.getByText("Accept");
  await userEvent.click(acceptBtn);

  expect(mockOnResponse).toHaveBeenCalled();
});
*/

// ============================================================================
// 10. STYLING CUSTOMIZATION
// ============================================================================

/*
// Create a custom theme CSS file:
// src/styles/referee-theme.css

:root {
  --notification-primary: #10b981;
  --notification-danger: #ef4444;
  --notification-warning: #f59e0b;
  --notification-bg: #f9fafb;
  --notification-border: #e5e7eb;
  --notification-text: #1f2937;
}

.referee-notification-card {
  --primary-color: var(--notification-primary);
  --danger-color: var(--notification-danger);
}

// Then import in your main CSS:
// import "./styles/referee-theme.css";
*/

// ============================================================================
// 11. LOCALIZATION (i18n)
// ============================================================================

/*
// If you have i18n setup, create translation keys:

// locales/en/referee.json
{
  "assignments": {
    "title": "My Assignments",
    "noPending": "No pending assignments",
    "loading": "Loading assignments...",
    "accept": "Accept",
    "reject": "Reject",
    "status": {
      "pending": "Pending",
      "accepted": "Accepted",
      "rejected": "Rejected"
    }
  }
}

// Then use:
import { useTranslation } from "react-i18next";

export function RefereeNotificationCard({ assignment }) {
  const { t } = useTranslation("referee");

  return (
    <button className="btn btn-accept">
      {t("assignments.accept")}
    </button>
  );
}
*/

// ============================================================================
// 12. PERFORMANCE OPTIMIZATION
// ============================================================================

/*
// Use React.memo to prevent unnecessary re-renders:

import { memo } from "react";

const MemoizedNotificationCard = memo(
  ({ assignment, onResponse, onRemove }) => (
    <RefereeNotificationCard
      assignment={assignment}
      onResponse={onResponse}
      onRemove={onRemove}
    />
  ),
  (prevProps, nextProps) => {
    // Custom comparison: only re-render if assignment or status changed
    return (
      prevProps.assignment.id === nextProps.assignment.id &&
      prevProps.assignment.status === nextProps.assignment.status
    );
  }
);

export default MemoizedNotificationCard;
*/

export const INTEGRATION_EXAMPLES = {
  description: "See commented examples above for integration patterns",
};
