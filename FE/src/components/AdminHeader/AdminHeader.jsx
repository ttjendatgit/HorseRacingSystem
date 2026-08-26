import { Link, NavLink } from "react-router-dom";
import ProfileDropdown from "../ProfileDropdown";
import NotificationBell from "../NotificationBell/NotificationBell";
import "./AdminHeader.css";

const adminNavItems = [
  { to: "/admin", label: "Tổng quan", end: true },
  { to: "/admin/users", label: "Người dùng" },
  { to: "/admin/tournaments", label: "Giải đấu" },
  { to: "/admin/audit", label: "Hệ thống" },
];

function AdminHeader() {
  return (
    <header className="admin-header">
      <div className="admin-header__inner">
        <Link className="admin-header__brand" to="/admin">
          <img
            src="/images/home-legacy/hr-logo-approved-exact.svg"
            alt="RaceMaster"
            className="header-logo"
          />
        </Link>
        <nav className="admin-header__nav" aria-label="Admin">
          {adminNavItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => isActive ? "nav-link nav-link--active" : "nav-link"}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="admin-header__actions">
          <NotificationBell />
          <ProfileDropdown profileUrl="/admin" />
        </div>
      </div>
    </header>
  );
}

export default AdminHeader;
