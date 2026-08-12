import { Link, NavLink } from "react-router-dom";
import { useNavigate } from "react-router-dom";
import "./Header.css";

const navItems = [
  { to: "/", label: "Trang chủ", end: true },
  { to: "/tournaments", label: "Giải đấu" },
  { to: "/schedule", label: "Lịch đua" },
  { to: "/live-results", label: "Kết quả trực tiếp" },
  { to: "/leaderboard", label: "Bảng xếp hạng" },
];

function Header({ isLoggedIn = false }) {
  const navigate = useNavigate();

  const handleLogout = () => {
    localStorage.removeItem("authToken");
    localStorage.removeItem("authUser");
    navigate("/");
  };

  return (
    <header className="site-header">
      <Link className="site-header__brand" to="/">
        <img src="/logo.png" alt="RaceMaster" className="header-logo" />
      </Link>

      <nav className="site-header__nav" aria-label="Primary">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            className={({ isActive }) =>
              isActive ? "nav-link nav-link--active" : "nav-link"
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div className="site-header__actions">
        {isLoggedIn ? (
          <button className="ghost-button btn" onClick={handleLogout}>
            Đăng xuất
          </button>
        ) : (
          <>
            <Link className="ghost-button btn" to="/login">
              Đăng nhập
            </Link>
            <Link className="primary-button btn" to="/register">
              Đăng ký
            </Link>
          </>
        )}
      </div>
    </header>
  );
}

export default Header;
