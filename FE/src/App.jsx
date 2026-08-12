import { lazy, Suspense } from "react";
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useLocation,
  Outlet,
} from "react-router-dom";
import Header from "./components/Header/Header";
import Footer from "./components/Footer/Footer";
import SpectatorHeader from "./components/SpectatorHeader/SpectatorHeader";
import JockeyHeader from "./components/JockeyHeader/JockeyHeader";
import OwnerHeader from "./components/OwnerHeader/OwnerHeader";
import RefereeHeader from "./components/RefereeHeader/RefereeHeader";
import AdminHeader from "./components/AdminHeader/AdminHeader";

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
const OwnerRaceConfirmationPage = lazy(() => import("./pages/OwnerRaceConfirmationPage/OwnerRaceConfirmationPage"));
const RefereeDashboardPage = lazy(() => import("./pages/RefereeDashboardPage/RefereeDashboardPage"));
const RefereeAssignmentPage = lazy(() => import("./pages/RefereeAssignmentPage/RefereeAssignmentPage"));
const RefereeHealthCheckPage = lazy(() => import("./pages/RefereeHealthCheckPage/RefereeHealthCheckPage"));
const RefereeInjuryPage = lazy(() => import("./pages/RefereeInjuryPage/RefereeInjuryPage"));
const RefereeViolationPage = lazy(() => import("./pages/RefereeViolationPage/RefereeViolationPage"));
const RefereeRaceReportPage = lazy(() => import("./pages/RefereeRaceReportPage/RefereeRaceReportPage"));
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
              <Route path="/jockey" element={<JockeyDashboardPage />} />
              <Route path="/jockey/invitations" element={<JockeyInvitationPage />} />
              <Route path="/jockey/invitations/:id" element={<JockeyInvitationDetailPage />} />
              <Route path="/jockey/schedule" element={<JockeySchedulePage />} />
              <Route path="/jockey/performance" element={<JockeyPerformancePage />} />
              <Route path="/jockey/leaderboard" element={<LeaderboardPage />} />
              <Route path="/jockey/profile" element={<JockeyProfilePage />} />
            </Route>

            {/* Owner */}
            <Route element={<RequireAuth roles={["horse_owner", "jockey"]} />}>
              <Route path="/owner" element={<OwnerDashboardPage />} />
              <Route path="/owner/horses" element={<OwnerHorseListPage />} />
              <Route path="/owner/horses/new" element={<OwnerHorseCreatePage />} />
              <Route path="/owner/horses/:id" element={<OwnerHorseDetailPage />} />
              <Route path="/owner/horses/:id/edit" element={<OwnerHorseEditPage />} />
              <Route path="/owner/tournaments" element={<OwnerTournamentListPage />} />
              <Route path="/owner/register-tournament" element={<OwnerTournamentRegisterPage />} />
              <Route path="/owner/schedule" element={<OwnerRaceConfirmationPage />} />
              <Route path="/owner/race-confirmations" element={<Navigate to="/owner/schedule" replace />} />
              <Route path="/owner/profile" element={<OwnerProfilePage />} />
            </Route>

            {/* Referee */}
            <Route element={<RequireAuth roles={["referee"]} />}>
              <Route path="/referee" element={<RefereeDashboardPage />} />
              <Route path="/referee/assignments" element={<RefereeAssignmentPage />} />
              <Route path="/referee/health-checks" element={<RefereeHealthCheckPage />} />
              <Route path="/referee/violations" element={<RefereeViolationPage />} />
              <Route path="/referee/reports" element={<RefereeRaceReportPage />} />
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
