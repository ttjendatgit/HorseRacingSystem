# Referee Assignment Notification System

This directory contains the complete implementation for the Referee Assignment notification feature. It includes React components, API integration functions, and state management hooks.

## 📁 File Structure

```
components/
├── RefereeNotification/
│   ├── RefereeNotificationCard.jsx      # Individual notification card component
│   ├── RefereeNotificationCard.css      # Card styling
│   ├── RefereeNotificationsPanel.jsx    # Container component for all notifications
│   ├── RefereeNotificationsPanel.css    # Panel styling
│   └── README.md                        # This file
│
services/
├── refereeAssignmentApi.js              # API integration functions
│
hooks/
├── useRefereeAssignments.js             # Custom hook for state management
```

## 🚀 Quick Start

### 1. Import the Components

```jsx
import { RefereeNotificationsPanel } from "@/components/RefereeNotification/RefereeNotificationsPanel";
```

### 2. Add to Your Page

```jsx
function RefereesDashboard() {
  return (
    <div className="dashboard">
      <h1>Referee Dashboard</h1>
      <RefereeNotificationsPanel />
      {/* Other dashboard content */}
    </div>
  );
}
```

That's it! The component will automatically:
- Fetch pending assignments on mount
- Display notifications in a list
- Handle Accept/Reject actions
- Update UI based on responses

---

## 📋 Components Overview

### RefereeNotificationCard

**Individual notification card that displays a single referee assignment.**

**Props:**
- `assignment` (Object, required)
  - `id` (number): Assignment ID
  - `matchName` (string): Name of the match
  - `dateTime` (string): ISO date string
  - `tournamentName` (string, optional): Tournament name
  - `status` (string): "pending", "accepted", or "rejected"

- `onResponse` (function, optional)
  - Callback triggered when user clicks Accept/Reject
  - Receives: `{ id, response, status }`

- `onRemove` (function, optional)
  - Callback to remove notification from UI
  - Receives: `assignmentId`

**Example Usage:**

```jsx
import { RefereeNotificationCard } from "@/components/RefereeNotification/RefereeNotificationCard";

function MyComponent() {
  const assignment = {
    id: 1,
    matchName: "Horse A vs Horse B",
    dateTime: "2026-06-15T14:30:00",
    tournamentName: "National Championship",
    status: "pending"
  };

  return (
    <RefereeNotificationCard
      assignment={assignment}
      onResponse={(response) => console.log("Responded:", response)}
      onRemove={(id) => console.log("Removed:", id)}
    />
  );
}
```

---

### RefereeNotificationsPanel

**Container component that manages and displays all pending referee assignments.**

**Features:**
- Automatically fetches assignments on mount
- Displays loading, error, and empty states
- Shows count of pending assignments
- Manages notifications list

**Example Usage:**

```jsx
import { RefereeNotificationsPanel } from "@/components/RefereeNotification/RefereeNotificationsPanel";

function RefereeDashboard() {
  return <RefereeNotificationsPanel />;
}
```

---

## 🎣 Custom Hook: useRefereeAssignments

**Custom React hook for managing referee assignment state.**

**Returns:**
```javascript
{
  assignments: [],           // Array of assignment objects
  isLoading: boolean,        // Loading state while fetching
  error: string|null,        // Error message if fetch fails
  refreshAssignments: fn,    // Function to manually refresh assignments
  removeAssignment: fn,      // Function to remove assignment from list
  updateAssignment: fn       // Function to update assignment details
}
```

**Example Usage:**

```jsx
import { useRefereeAssignments } from "@/hooks/useRefereeAssignments";

function MyComponent() {
  const { assignments, isLoading, error, refreshAssignments } =
    useRefereeAssignments(true); // true = auto-fetch on mount

  return (
    <>
      <button onClick={refreshAssignments}>Refresh</button>
      {isLoading && <p>Loading...</p>}
      {error && <p>Error: {error}</p>}
      {assignments.map(a => <div key={a.id}>{a.matchName}</div>)}
    </>
  );
}
```

---

## 🔌 API Integration

All API functions are in `services/refereeAssignmentApi.js`:

### respondToRefereeAssignment(assignmentId, response)

**Respond to a referee assignment (Accept or Reject)**

```javascript
import { respondToRefereeAssignment } from "@/services/refereeAssignmentApi";

// Accept an assignment
await respondToRefereeAssignment(123, "Accept");

// Reject an assignment
await respondToRefereeAssignment(123, "Reject");
```

### getPendingRefereeAssignments()

**Get all pending referee assignments for current referee**

```javascript
import { getPendingRefereeAssignments } from "@/services/refereeAssignmentApi";

const assignments = await getPendingRefereeAssignments();
```

### getAllRefereeAssignments()

**Get all referee assignments (pending, accepted, rejected)**

```javascript
import { getAllRefereeAssignments } from "@/services/refereeAssignmentApi";

const allAssignments = await getAllRefereeAssignments();
```

---

## 🎨 Styling

All components use CSS modules for styling:

- **RefereeNotificationCard.css**: Individual card styles
  - `.referee-notification-card`: Main card container
  - `.btn-accept` / `.btn-reject`: Action buttons
  - `.status-pending` / `.status-accepted` / `.status-rejected`: Status badges

- **RefereeNotificationsPanel.css**: Panel container styles
  - `.referee-notifications-panel`: Main container
  - `.notifications-header`: Header section
  - `.notifications-list`: List container

### Customizing Styles

You can override styles by editing the CSS files or adding custom styles to your global CSS:

```css
.referee-notification-card {
  /* Your custom styles */
}
```

---

## 🔄 State Management Flow

1. **Component Mount**
   - `RefereeNotificationsPanel` mounts
   - `useRefereeAssignments` hook is initialized
   - `getPendingRefereeAssignments()` API call is made

2. **Display Assignments**
   - Assignments are mapped to `RefereeNotificationCard` components
   - Each card displays match info and Accept/Reject buttons

3. **User Response**
   - User clicks "Accept" or "Reject"
   - `handleResponse()` calls `respondToRefereeAssignment()` API
   - API response updates local state
   - `onResponse` callback is triggered
   - Card status updates in UI

4. **Cleanup**
   - Optional: notification can be removed via `onRemove` callback

---

## ⚠️ Error Handling

Errors are handled gracefully:

1. **API Errors**
   - Caught in the component
   - Error message displayed to user
   - User can retry

2. **Loading States**
   - Loading spinner shown while fetching
   - Buttons disabled while processing response

3. **Empty States**
   - "No pending assignments" message shown when list is empty

---

## 🔌 Backend API Endpoint Requirements

The component expects these backend endpoints:

### GET `/api/referee/assignments/pending`
**Get all pending referee assignments**
- Returns: `Array<Assignment>`

### POST `/api/referee/assign/respond`
**Respond to a referee assignment**
- Body: `{ assignmentId: number, response: "Accept" | "Reject" }`
- Returns: Response object with status confirmation

---

## 📝 TypeScript Support (Optional)

If you want to add TypeScript support, create `types/assignment.ts`:

```typescript
export interface RefereeAssignment {
  id: number;
  matchName: string;
  dateTime: string;
  tournamentName?: string;
  status: "pending" | "accepted" | "rejected";
}

export interface AssignmentResponse {
  id: number;
  response: "Accept" | "Reject";
  status: "accepted" | "rejected";
}
```

Then update the component imports:

```typescript
import { RefereeNotificationCard } from "@/components/RefereeNotification/RefereeNotificationCard";
import type { RefereeAssignment, AssignmentResponse } from "@/types/assignment";
```

---

## 🐛 Troubleshooting

### Assignments not loading
- Check browser console for API errors
- Verify `/api/referee/assignments/pending` endpoint exists
- Ensure authentication token is valid (check localStorage)

### Buttons not responding
- Check network tab in DevTools
- Verify `/api/referee/assign/respond` endpoint exists
- Check error message in the component

### Styling issues
- Clear browser cache
- Check CSS file is imported correctly
- Verify class names match in JSX

---

## 📚 Next Steps

- Add WebSocket support for real-time notifications
- Add notification sound/toast alerts
- Implement assignment filtering/sorting
- Add assignment history view
- Add batch operations (accept/reject multiple)
