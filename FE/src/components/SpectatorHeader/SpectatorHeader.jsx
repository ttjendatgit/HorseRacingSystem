import { Link, NavLink } from "react-router-dom";
import { spectatorNavItems } from "../../constants/spectatorNavigation";
import ProfileDropdown from "../ProfileDropdown";
import NotificationBell from "../NotificationBell/NotificationBell";
import "./SpectatorHeader.css";

function SpectatorHeader() {
  return (
    <header className="spectator-header">
      <Link className="spectator-header__brand" to="/spectator/tournaments">
        <img src="/logo.png" alt="RaceMaster" className="header-logo" />
      </Link>
      <nav className="spectator-header__nav" aria-label="Spectator">
        {spectatorNavItems.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => isActive ? "nav-link nav-link--active" : "nav-link"}>{item.label}</NavLink>
        ))}
      </nav>
      <div className="spectator-header__actions">
        <NotificationBell />
        <ProfileDropdown profileUrl="/spectator/profile" />
      </div>
    </header>
  );
}

export default SpectatorHeader;
