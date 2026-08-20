import { useState } from "react";
import "./App.css";
import Login from "./components/Login";
import Dashboard from "./components/Dashboard";
import { getApiConfigError, api, type AuthResponse } from "./api";

const AUTH_KEY = "agencybill.auth";

function App() {
  const [auth, setAuth] = useState<AuthResponse | null>(() => {
    const saved = localStorage.getItem(AUTH_KEY);
    return saved ? (JSON.parse(saved) as AuthResponse) : null;
  });

  const configError = getApiConfigError();

  const onAuth = (a: AuthResponse) => {
    localStorage.setItem(AUTH_KEY, JSON.stringify(a));
    setAuth(a);
  };

  const onLogout = async () => {
    if (auth?.refreshToken) {
      try {
        await api.logout(auth.refreshToken);
      } catch {
        // still clear local session
      }
    }
    localStorage.removeItem(AUTH_KEY);
    setAuth(null);
  };

  return (
    <div className="app">
      <header className="header">
        <h1>AgencyBill</h1>
        {auth && <button onClick={onLogout}>Logout</button>}
      </header>
      <main>
        {configError && <p className="error">{configError}</p>}
        {!auth ? (
          <Login onAuth={onAuth} disabled={!!configError} />
        ) : (
          <Dashboard auth={auth} onLogout={onLogout} />
        )}
      </main>
    </div>
  );
}

export default App;
