import { useState } from "react";
import "./App.css";
import Login from "./components/Login";
import Dashboard from "./components/Dashboard";
import type { AuthResponse } from "./api";

function App() {
  const [auth, setAuth] = useState<AuthResponse | null>(() => {
    const saved = localStorage.getItem("ledgerly.auth");
    return saved ? (JSON.parse(saved) as AuthResponse) : null;
  });

  const onAuth = (a: AuthResponse) => {
    localStorage.setItem("ledgerly.auth", JSON.stringify(a));
    setAuth(a);
  };

  const onLogout = () => {
    localStorage.removeItem("ledgerly.auth");
    setAuth(null);
  };

  return (
    <div className="app">
      <header className="header">
        <h1>Ledgerly</h1>
        {auth && <button onClick={onLogout}>Logout</button>}
      </header>
      <main>
        {!auth ? (
          <Login onAuth={onAuth} />
        ) : (
          <Dashboard auth={auth} onLogout={onLogout} />
        )}
      </main>
    </div>
  );
}

export default App;