import { useState } from "react";
import { api, type AuthResponse } from "../api";

type Mode = "login" | "register";

export default function Login({ onAuth }: { onAuth: (a: AuthResponse) => void }) {
  const [mode, setMode] = useState<Mode>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [tenantName, setTenantName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const auth = mode === "login"
        ? await api.login({ email, password })
        : await api.register({ email, password, fullName, tenantName });
      onAuth(auth);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card">
      <h2>{mode === "login" ? "Log in" : "Create your agency"}</h2>
      <form onSubmit={submit}>
        {mode === "register" && (
          <>
            <label>Full name<input value={fullName} onChange={(e) => setFullName(e.target.value)} required /></label>
            <label>Agency name<input value={tenantName} onChange={(e) => setTenantName(e.target.value)} required /></label>
          </>
        )}
        <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required /></label>
        <label>Password<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} /></label>
        {error && <p className="error">{error}</p>}
        <button type="submit" disabled={busy}>{busy ? "…" : mode === "login" ? "Log in" : "Register"}</button>
      </form>
      <p>
        {mode === "login" ? "New here? " : "Already have an account? "}
        <a onClick={() => setMode(mode === "login" ? "register" : "login")}>
          {mode === "login" ? "Create an agency" : "Log in"}
        </a>
      </p>
    </section>
  );
}