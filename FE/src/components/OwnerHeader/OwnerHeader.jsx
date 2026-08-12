import { Link, NavLink } from "react-router-dom";
import { ownerNavItems } from "../../constants/ownerNavigation";
import ProfileDropdown from "../ProfileDropdown";
import NotificationBell from "../NotificationBell/NotificationBell";
import "./OwnerHeader.css";

function OwnerHeader() {
  return (
    <header className="owner-header">
      <Link className="owner-header__brand" to="/owner">
        <img src="/logo.png" alt="RaceMaster" className="header-logo" />
      </Link>
      <nav className="owner-header__nav" aria-label="Horse Owner">
        {ownerNavItems.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => isActive ? "nav-link nav-link--active" : "nav-link"}>{item.label}</NavLink>
        ))}
      </nav>
      <div className="owner-header__actions">
        <NotificationBell />
        <ProfileDropdown profileUrl="/owner/profile" />
      </div>
    </header>
  );
}

export default OwnerHeader;
