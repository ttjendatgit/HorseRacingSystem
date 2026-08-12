import { Link, NavLink } from "react-router-dom";
import { refereeNavItems } from "../../constants/refereeNavigation";
import ProfileDropdown from "../ProfileDropdown";
import NotificationBell from "../NotificationBell/NotificationBell";
import "./RefereeHeader.css";

function RefereeHeader() {
  return (
    <header className="referee-header">
      <Link className="referee-header__brand" to="/referee">
        <img src="/logo.png" alt="RaceMaster" className="header-logo" />
      </Link>
      <nav className="referee-header__nav" aria-label="Referee">
        {refereeNavItems.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => isActive ? "nav-link nav-link--active" : "nav-link"}>{item.label}</NavLink>
        ))}
      </nav>
      <div className="referee-header__actions">
        <NotificationBell />
        <ProfileDropdown profileUrl="/referee/profile" />
      </div>
    </header>
  );
}

export default RefereeHeader;
