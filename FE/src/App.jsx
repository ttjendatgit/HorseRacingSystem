import { lazy, Suspense, useState, useEffect } from "react";
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useLocation,
  Outlet,
  Link,
} from "react-router-dom";
import Header from "./components/Header/Header";
import Footer from "./components/Footer/Footer";
import SpectatorHeader from "./components/SpectatorHeader/SpectatorHeader";
import JockeyHeader from "./components/JockeyHeader/JockeyHeader";
import OwnerHeader from "./components/OwnerHeader/OwnerHeader";
import RefereeHeader from "./components/RefereeHeader/RefereeHeader";
import AdminHeader from "./components/AdminHeader/AdminHeader";
import { getMyJockeyProfile } from "./services/jockeyApi";
import { getJockeyApprovalDisplay } from "./utils/jockeyApproval";

const HomePage = lazy(() => import("./pages/HomePage/HomePage"));
const TournamentListPage = lazy(() => import("./pages/TournamentListPage/TournamentListPage"));
const TournamentDetailPage = lazy(() => import("./pages/TournamentDetailPage/TournamentDetailPage"));
const RaceSchedulePage = lazy(() => import("./pages/RaceSchedulePage/RaceSchedulePage"));
const LiveResultsPage = lazy(() => import("./pages/LiveResultsPage/LiveResultsPage"));
const LeaderboardPage = lazy(() => import("./pages/LeaderboardPage/LeaderboardPage"));
const SpectatorTournamentListPage = lazy(() => import("./pages/SpectatorTournamentListPage/SpectatorTournamentListPage"));
const SpectatorRaceSchedulePage = lazy(() => import("./pages/SpectatorRaceSchedulePage/SpectatorRaceSchedulePage"));
const SpectatorLiveRankingPage = lazy(() => import("./pages/SpectatorLiveRankingPage/SpectatorLiveRankingPage"));
const SpectatorPredictionFormPage = lazy(() => import("./pages/SpectatorPredictionFormPage/SpectatorPredictionFormPage"));
const SpectatorPredictionResultPage = lazy(() => import("./pages/SpectatorPredictionResultPage/SpectatorPredictionResultPage"));
const SpectatorRewardNotificationsPage = lazy(() => import("./pages/SpectatorRewardNotificationsPage/SpectatorRewardNotificationsPage"));
const JockeyInvitationPage = lazy(() => import("./pages/JockeyInvitationPage/JockeyInvitationPage"));
const JockeyInvitationDetailPage = lazy(() => import("./pages/JockeyInvitationPage/JockeyInvitationDetailPage"));
const JockeyDashboardPage = lazy(() => import("./pages/JockeyDashboardPage/JockeyDashboardPage"));
const JockeySchedulePage = lazy(() => import("./pages/JockeySchedulePage/JockeySchedulePage"));
const JockeyPerformancePage = lazy(() => import("./pages/JockeyPerformancePage/JockeyPerformancePage"));
const OwnerDashboardPage = lazy(() => import("./pages/OwnerDashboardPage/OwnerDashboardPage"));
const OwnerHorseListPage = lazy(() => import("./pages/OwnerHorseListPage/OwnerHorseListPage"));
const OwnerHorseDetailPage = lazy(() => import("./pages/OwnerHorseDetailPage/OwnerHorseDetailPage"));
const OwnerHorseCreatePage = lazy(() => import("./pages/OwnerHorseCreatePage/OwnerHorseCreatePage"));
const OwnerHorseEditPage = lazy(() => import("./pages/OwnerHorseEditPage/OwnerHorseEditPage"));
const OwnerTournamentListPage = lazy(() => import("./pages/OwnerTournamentListPage/OwnerTournamentListPage"));
const OwnerTournamentRegisterPage = lazy(() => import("./pages/OwnerTournamentRegisterPage/OwnerTournamentRegisterPage"));
const OwnerParticipationsPage = lazy(() => import("./pages/OwnerParticipationsPage/OwnerParticipationsPage"));
const OwnerWalletPage = lazy(() => import("./pages/OwnerWalletPage/OwnerWalletPage"));
const OwnerRaceConfirmationPage = lazy(() => import("./pages/OwnerRaceConfirmationPage/OwnerRaceConfirmationPage"));
const RefereeDashboardPage = lazy(() => import("./pages/RefereeDashboardPage/RefereeDashboardPage"));
const RefereeAssignmentPage = lazy(() => import("./pages/RefereeAssignmentPage/RefereeAssignmentPage"));
const RefereeHealthCheckPage = lazy(() => import("./pages/RefereeHealthCheckPage/RefereeHealthCheckPage"));
const GateAssignmentPage = lazy(() => import("./pages/GateAssignmentPage/GateAssignmentPage"));
const RefereeInjuryPage = lazy(() => import("./pages/RefereeInjuryPage/RefereeInjuryPage"));
const RefereeViolationPage = lazy(() => import("./pages/RefereeViolationPage/RefereeViolationPage"));
const RefereeRaceReportPage = lazy(() => import("./pages/RefereeRaceReportPage/RefereeRaceReportPage"));
const RefereeComplaintsPage = lazy(() => import("./pages/RefereeComplaintsPage/RefereeComplaintsPage"));
const OwnerProfilePage = lazy(() => import("./pages/OwnerProfilePage/OwnerProfilePage"));
const JockeyProfilePage = lazy(() => import("./pages/JockeyProfilePage/JockeyProfilePage"));
const RefereeProfilePage = lazy(() => import("./pages/RefereeProfilePage/RefereeProfilePage"));
const SpectatorProfilePage = lazy(() => import("./pages/ProfilePages").then(m => ({ default: m.SpectatorProfilePage })));
const LoginPage = lazy(() => import("./pages/LoginPage/LoginPage"));
const RegisterPage = lazy(() => import("./pages/RegisterPage/RegisterPage"));
const RegisterHorseOwnerPage = lazy(() => import("./pages/RegisterHorseOwnerPage/RegisterHorseOwnerPage"));
const RegisterJockeyPage = lazy(() => import("./pages/RegisterJockeyPage/RegisterJockeyPage"));
const AdminPage = lazy(() => import("./pages/AdminPage/AdminPage"));
import "./pages/OwnerSharedLayout.css";
import "./pages/OwnerHorseFormPage.css";
import "./pages/RefereeSharedLayout.css";
import "./pages/SpectatorSharedLayout.css";
import "./pages/ProfilePages.css";
import "./App.css";

const getStoredAuthUser = () => {
  const user = localStorage.getItem("authUser");
  if (!user) {
    return null;
  }

  try {
    return JSON.parse(user);
  } catch {
    return null;
  }
};

function RequireAuth({ roles }) {
  const hasAuthToken = Boolean(localStorage.getItem("authToken"));
  const user = getStoredAuthUser();
  if (!hasAuthToken) return <Navigate to="/login" replace />;
  if (roles && user && !roles.includes(user.role)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function JockeyApprovalGuard() {
  const authUser = getStoredAuthUser();
  const isJockey = authUser?.role === "jockey";

  const [status, setStatus] = useState(isJockey ? "loading" : "Approved");
  const [note, setNote] = useState("");
  const location = useLocation();

  useEffect(() => {
    if (!isJockey) return;

    getMyJockeyProfile()
      .then((data) => {
        const appDisplay = getJockeyApprovalDisplay(data);
        setStatus(appDisplay.status);
        setNote(appDisplay.note || "");
      })
      .catch(() => {
        setStatus("error");
      });
  }, [isJockey, location.pathname]);

  if (!isJockey) {
    return <Outlet />;
  }

  if (status === "loading") {
    return <div className="page-loading">Đang tải trạng thái phê duyệt...</div>;
  }

  if (status === "Approved") {
    return <Outlet />;
  }

  const isProfilePath = location.pathname === "/jockey/profile" || location.pathname === "/owner/profile";
  if (isProfilePath) {
    return <Outlet />;
  }

  return (
    <div style={{ padding: "40px 20px", maxWidth: "600px", margin: "80px auto", textAlign: "center", background: "var(--hr-surface, #fff)", borderRadius: "12px", boxShadow: "0 4px 20px rgba(0,0,0,0.08)", border: "1px solid var(--hr-border, #e2e8f0)" }}>
      <h2 style={{ color: status === "Rejected" ? "#ef4444" : "#f59e0b", marginBottom: "16px", fontSize: "24px" }}>
        {status === "Rejected" ? "Hồ sơ kỵ sĩ bị từ chối" : "Hồ sơ đang chờ phê duyệt"}
      </h2>
      <p style={{ color: "#64748b", fontSize: "16px", lineHeight: "1.6", marginBottom: "24px" }}>
        {status === "Rejected"
          ? `Hồ sơ kỵ sĩ của bạn đã bị từ chối phê duyệt. Lý do từ chối: "${note || "Không có lý do cụ thể"}"`
          : "Hồ sơ đăng ký tài khoản kỵ sĩ của bạn đang chờ Admin xác minh và duyệt. Bạn không thể thực hiện các thao tác khác trong thời gian này."}
      </p>
      <div style={{ display: "flex", justifyContent: "center", gap: "16px" }}>
        <Link to="/jockey/profile" className="primary-button" style={{ textDecoration: "none", display: "inline-block", padding: "10px 20px" }}>
          {status === "Rejected" ? "Chỉnh sửa & Gửi lại" : "Xem hồ sơ của tôi"}
        </Link>
      </div>
    </div>
  );
}

function AppLayout() {
  const location = useLocation();
  const authUser = getStoredAuthUser();

  const isAdmin = location.pathname.startsWith("/admin");

  const renderHeader = () => {
    const role = authUser?.role;
    if (role === "spectator") return <SpectatorHeader />;
    if (role === "jockey") return <JockeyHeader />;
    if (role === "horse_owner") return <OwnerHeader />;
    if (role === "referee") return <RefereeHeader />;
    if (role === "admin") return <AdminHeader />;
    return <Header />;
  };

  return (
    <div className="app-shell">
      {renderHeader()}
      <main className="page-wrapper">
        <Suspense fallback={<div className="page-loading">Đang tải...</div>}>
          <Routes>
            {/* Public — không cần đăng nhập */}
            <Route path="/" element={<HomePage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/register/horse-owner" element={<RegisterHorseOwnerPage />} />
            <Route path="/register/jockey" element={<RegisterJockeyPage />} />

            {/* Protected — cần đăng nhập (không giới hạn role) */}
            <Route element={<RequireAuth />}>
              <Route path="/tournaments" element={<TournamentListPage />} />
              <Route path="/tournaments/:id" element={<TournamentDetailPage />} />
              <Route path="/schedule" element={<RaceSchedulePage />} />
              <Route path="/live-results" element={<LiveResultsPage />} />
              <Route path="/leaderboard" element={<LeaderboardPage />} />
              <Route path="/spectator" element={<Navigate to="/" replace />} />
            </Route>

            {/* Spectator */}
            <Route element={<RequireAuth roles={["spectator"]} />}>
              <Route path="/spectator/tournaments" element={<SpectatorTournamentListPage />} />
              <Route path="/spectator/schedule" element={<SpectatorRaceSchedulePage />} />
              <Route path="/spectator/live-ranking" element={<SpectatorLiveRankingPage />} />
              <Route path="/spectator/predictions" element={<SpectatorPredictionFormPage />} />
              <Route path="/spectator/predictions/results" element={<SpectatorPredictionResultPage />} />
              <Route path="/spectator/rewards" element={<SpectatorRewardNotificationsPage />} />
              <Route path="/spectator/profile" element={<SpectatorProfilePage />} />
            </Route>

            {/* Jockey */}
            <Route element={<RequireAuth roles={["jockey"]} />}>
              <Route element={<JockeyApprovalGuard />}>
                <Route path="/jockey" element={<JockeyDashboardPage />} />
                <Route path="/jockey/invitations" element={<JockeyInvitationPage />} />
                <Route path="/jockey/invitations/:id" element={<JockeyInvitationDetailPage />} />
                <Route path="/jockey/schedule" element={<JockeySchedulePage />} />
                <Route path="/jockey/performance" element={<JockeyPerformancePage />} />
                <Route path="/jockey/leaderboard" element={<LeaderboardPage />} />
              </Route>
              <Route path="/jockey/profile" element={<JockeyProfilePage />} />
            </Route>

            {/* Owner — browsing/read routes remain shared with Jockey (unchanged Owner UI) */}
            <Route element={<RequireAuth roles={["horse_owner", "jockey"]} />}>
              <Route element={<JockeyApprovalGuard />}>
                <Route path="/owner" element={<OwnerDashboardPage />} />
                <Route path="/owner/horses" element={<OwnerHorseListPage />} />
                <Route path="/owner/horses/:id" element={<OwnerHorseDetailPage />} />
                <Route path="/owner/tournaments" element={<OwnerTournamentListPage />} />
                <Route path="/owner/schedule" element={<OwnerRaceConfirmationPage />} />
                <Route path="/owner/race-confirmations" element={<Navigate to="/owner/schedule" replace />} />
              </Route>
              <Route path="/owner/profile" element={<OwnerProfilePage />} />
            </Route>

            {/* Owner-only — Task B Final Correction: Create/Edit Horse and Tournament
                registration submission are Owner business territory; Jockey must not reach
                these routes even by direct URL (mirrors backend [Authorize(Roles="HorseOwner,Admin")]
                / [Authorize(Roles="HorseOwner")]). Full Jockey invitation/license flow deferred. */}
            <Route element={<RequireAuth roles={["horse_owner"]} />}>
              <Route path="/owner/horses/new" element={<OwnerHorseCreatePage />} />
              <Route path="/owner/horses/:id/edit" element={<OwnerHorseEditPage />} />
              <Route path="/owner/register-tournament" element={<OwnerTournamentRegisterPage />} />
              <Route path="/owner/participations" element={<OwnerParticipationsPage />} />
              <Route path="/owner/wallet" element={<OwnerWalletPage />} />
            </Route>

            {/* Referee */}
            <Route element={<RequireAuth roles={["referee"]} />}>
              <Route path="/referee" element={<RefereeDashboardPage />} />
              <Route path="/referee/assignments" element={<RefereeAssignmentPage />} />
              <Route path="/referee/health-checks" element={<RefereeHealthCheckPage />} />
              <Route path="/referee/gate-assignment" element={<GateAssignmentPage />} />
              <Route path="/referee/violations" element={<RefereeViolationPage />} />
              <Route path="/referee/reports" element={<RefereeRaceReportPage />} />
              <Route path="/referee/complaints" element={<RefereeComplaintsPage />} />
              <Route path="/referee/injuries" element={<RefereeInjuryPage />} />
              <Route path="/referee/profile" element={<RefereeProfilePage />} />
            </Route>

            {/* Admin */}
            <Route element={<RequireAuth roles={["admin"]} />}>
              <Route path="/admin" element={<AdminPage />} />
              <Route path="/admin/users" element={<AdminPage />} />
              <Route path="/admin/users/:id" element={<AdminPage />} />
              <Route path="/admin/users/:userId/horses/:horseId" element={<AdminPage />} />
              <Route path="/admin/registrations" element={<AdminPage />} />
              <Route path="/admin/roles" element={<AdminPage />} />
              <Route path="/admin/tournaments" element={<AdminPage />} />
              <Route path="/admin/rounds" element={<AdminPage />} />
              <Route path="/admin/races" element={<AdminPage />} />
              <Route path="/admin/prizes" element={<AdminPage />} />
              <Route path="/admin/protests" element={<AdminPage />} />
              <Route path="/admin/race-complaints" element={<AdminPage />} />
              <Route path="/admin/transfers" element={<AdminPage />} />
              <Route path="/admin/contracts" element={<AdminPage />} />
              <Route path="/admin/injuries" element={<AdminPage />} />
              <Route path="/admin/audit" element={<AdminPage />} />
              <Route path="/admin/notifications" element={<AdminPage />} />
              <Route path="/admin/withdrawals" element={<AdminPage />} />
              <Route path="/admin/predictions" element={<AdminPage />} />
              <Route path="/admin/referee-assign" element={<AdminPage />} />
              <Route path="/admin/race-results" element={<AdminPage />} />
              <Route path="/admin/horses" element={<AdminPage />} />
              <Route path="/admin/referees" element={<AdminPage />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
      </main>
      {!isAdmin && <Footer />}
    </div>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AppLayout />
    </BrowserRouter>
  );
}

export default App;
