import { createContext, useContext, useState, useCallback } from "react";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    try { return JSON.parse(localStorage.getItem("authUser")); } catch { return null; }
  });
  const [token, setToken] = useState(() => localStorage.getItem("authToken"));

  const login = useCallback((userData, authToken) => {
    localStorage.setItem("authUser", JSON.stringify(userData));
    localStorage.setItem("authToken", authToken);
    setUser(userData);
    setToken(authToken);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem("authUser");
    localStorage.removeItem("authToken");
    localStorage.removeItem("refreshToken");
    setUser(null);
    setToken(null);
  }, []);

  const isAuthenticated = Boolean(token && user);
  const hasRole = useCallback((role) => user?.role === role || user?.role === role?.toLowerCase(), [user]);

  return (
    <AuthContext.Provider value={{ user, token, isAuthenticated, hasRole, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
