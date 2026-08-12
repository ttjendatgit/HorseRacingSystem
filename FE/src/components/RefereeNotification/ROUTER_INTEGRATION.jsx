/**
 * ROUTER INTEGRATION EXAMPLE
 *
 * This file shows how to add the Referee Assignment page to your router.
 * Copy the relevant parts to your actual App.jsx or router configuration file.
 */

// ============================================================================
// Option 1: Simple Router Setup (for small apps)
// ============================================================================

/*
// In your App.jsx:

import { BrowserRouter, Routes, Route } from "react-router-dom";
import { RefereeAssignmentPage } from "@/components/RefereeNotification/RefereeAssignmentPage";

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* ... other routes ... */}

        <Route
          path="/referee/assignments"
          element={<RefereeAssignmentPage />}
        />

        {/* ... other routes ... */}
      </Routes>
    </BrowserRouter>
  );
}

export default App;
*/

// ============================================================================
// Option 2: Protected Route (with role-based access)
// ============================================================================

/*
// Create a ProtectedRoute component first:

import { Navigate } from "react-router-dom";

function ProtectedRoute({ children, requiredRole }) {
  const user = JSON.parse(localStorage.getItem("user") || "{}");

  if (!user || user.role !== requiredRole) {
    return <Navigate to="/login" />;
  }

  return children;
}

// Then in App.jsx:

<Route
  path="/referee/assignments"
  element={
    <ProtectedRoute requiredRole="Referee">
      <RefereeAssignmentPage />
    </ProtectedRoute>
  }
/>
*/

// ============================================================================
// Option 3: Nested Routes (for referee dashboard)
// ============================================================================

/*
// Create RefereeDashboard layout:

import { Outlet } from "react-router-dom";

function RefereeDashboard() {
  return (
    <div className="referee-dashboard">
      <Header />
      <Sidebar />
      <main className="main-content">
        <Outlet /> {/* Child routes render here */}
      </main>
    </div>
  );
}

// Then in App.jsx:

<Route path="/referee" element={<RefereeDashboard />}>
  <Route path="assignments" element={<RefereeAssignmentPage />} />
  <Route path="history" element={<RefereeDashboard />} />
  <Route path="profile" element={<RefereeDashboard />} />
</Route>
*/

// ============================================================================
// Option 4: Lazy Loading (for performance)
// ============================================================================

/*
// In App.jsx:

import { lazy, Suspense } from "react";

const RefereeAssignmentPage = lazy(
  () => import("@/components/RefereeNotification/RefereeAssignmentPage")
);

function LoadingFallback() {
  return <div>Loading...</div>;
}

// Then in Routes:

<Route
  path="/referee/assignments"
  element={
    <Suspense fallback={<LoadingFallback />}>
      <RefereeAssignmentPage />
    </Suspense>
  }
/>
*/

// ============================================================================
// Option 5: Complex Router Configuration (recommended for large apps)
// ============================================================================

/*
// Create a separate file: src/routes/index.jsx

import { RefereeAssignmentPage } from "@/components/RefereeNotification";
import { RefereeDashboard } from "@/pages/referee/RefereeDashboard";

export const REFEREE_ROUTES = [
  {
    path: "assignments",
    element: <RefereeAssignmentPage />,
    title: "My Assignments",
  },
  {
    path: "dashboard",
    element: <RefereeDashboard />,
    title: "Dashboard",
  },
];

// In App.jsx:

import { Routes, Route } from "react-router-dom";
import { REFEREE_ROUTES } from "@/routes";
import { ProtectedRoute } from "@/components/ProtectedRoute";

function App() {
  return (
    <Routes>
      {REFEREE_ROUTES.map((route) => (
        <Route
          key={route.path}
          path={`/referee/${route.path}`}
          element={
            <ProtectedRoute requiredRole="Referee">
              {route.element}
            </ProtectedRoute>
          }
        />
      ))}
    </Routes>
  );
}
*/

// ============================================================================
// Option 6: Navigation Menu Integration
// ============================================================================

/*
// Add to your Header/Navigation component:

import { useNavigate } from "react-router-dom";
import { useRefereeAssignments } from "@/hooks/useRefereeAssignments";

function RefereeNav() {
  const navigate = useNavigate();
  const { assignments } = useRefereeAssignments(true);

  const pendingCount = assignments.filter(
    (a) => a.status === "pending"
  ).length;

  return (
    <nav>
      <button
        onClick={() => navigate("/referee/assignments")}
        className={pendingCount > 0 ? "has-notifications" : ""}
      >
        My Assignments
        {pendingCount > 0 && (
          <span className="notification-badge">{pendingCount}</span>
        )}
      </button>
    </nav>
  );
}
*/

// ============================================================================
// Option 7: Conditional Rendering Based on User Role
// ============================================================================

/*
// In App.jsx:

import { useEffect, useState } from "react";

function App() {
  const [user, setUser] = useState(null);

  useEffect(() => {
    const userData = JSON.parse(localStorage.getItem("user") || "null");
    setUser(userData);
  }, []);

  return (
    <Routes>
      {/* Public routes */}
      <Route path="/" element={<Home />} />
      <Route path="/login" element={<Login />} />

      {/* Role-based routes */}
      {user?.role === "Referee" && (
        <>
          <Route path="/referee/assignments" element={<RefereeAssignmentPage />} />
          <Route path="/referee/dashboard" element={<RefereeDashboard />} />
        </>
      )}

      {user?.role === "Admin" && (
        <>
          <Route path="/admin/dashboard" element={<AdminDashboard />} />
        </>
      )}

      {/* Catch-all */}
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}
*/

// ============================================================================
// Full Example: Complete App.jsx with Referee Routes
// ============================================================================

/*
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useEffect, useState } from "react";
import Layout from "@/components/Layout";
import { RefereeAssignmentPage } from "@/components/RefereeNotification";

// Public pages
import Login from "@/pages/Login";
import NotFound from "@/pages/NotFound";

// Referee pages
import RefereeDashboard from "@/pages/referee/RefereeDashboard";

function App() {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  // Check if user is logged in
  useEffect(() => {
    const userData = localStorage.getItem("user");
    if (userData) {
      try {
        setUser(JSON.parse(userData));
      } catch (e) {
        console.error("Failed to parse user data");
      }
    }
    setLoading(false);
  }, []);

  if (loading) {
    return <div>Loading...</div>;
  }

  return (
    <BrowserRouter>
      <Routes>
        {/* Public Routes */}
        <Route path="/login" element={<Login />} />

        {/* Protected Routes */}
        {user ? (
          <Route element={<Layout />}>
            {/* Referee Routes */}
            {user.role === "Referee" && (
              <>
                <Route path="/referee/dashboard" element={<RefereeDashboard />} />
                <Route
                  path="/referee/assignments"
                  element={<RefereeAssignmentPage />}
                />
              </>
            )}

            {/* Add other role routes here */}
          </Route>
        ) : (
          <Route path="/" element={<Navigate to="/login" />} />
        )}

        {/* 404 Route */}
        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
*/

export const ROUTER_EXAMPLES = {
  description: "See commented examples above for router integration patterns",
};
