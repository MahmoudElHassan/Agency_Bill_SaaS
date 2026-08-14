import { useEffect, useState } from "react";
import { api, type AuthResponse, type ClientDto, type InvoiceDto } from "../api";

const STATUS_LABEL = ["Draft", "Sent", "Paid", "Overdue", "Void"];

export default function Dashboard({ auth }: { auth: AuthResponse; onLogout: () => void }) {
  const [me, setMe] = useState<{ email: string; fullName: string; tenantName: string; plan: string } | null>(null);
  const [clients, setClients] = useState<ClientDto[]>([]);
  const [invoices, setInvoices] = useState<InvoiceDto[]>([]);
  const [tab, setTab] = useState<"clients" | "invoices" | "billing">("clients");
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      const [m, c, i] = await Promise.all([
        api.me(auth.accessToken),
        api.listClients(auth.accessToken),
        api.listInvoices(auth.accessToken)
      ]);
      setMe(m);
      setClients(c);
      setInvoices(i.items);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  useEffect(() => { load(); }, []);

  return (
    <div>
      {me && (
        <p className="muted">
          Signed in as <strong>{me.fullName}</strong> · {me.tenantName} · plan <strong>{me.plan}</strong>
        </p>
      )}
      {error && <p className="error">{error}</p>}
      <nav className="tabs">
        <button className={tab === "clients" ? "active" : ""} onClick={() => setTab("clients")}>Clients ({clients.length})</button>
        <button className={tab === "invoices" ? "active" : ""} onClick={() => setTab("invoices")}>Invoices ({invoices.length})</button>
        <button className={tab === "billing" ? "active" : ""} onClick={() => setTab("billing")}>Billing</button>
      </nav>

      {tab === "clients" && <ClientsTab auth={auth} clients={clients} onChange={load} />}
      {tab === "invoices" && <InvoicesTab auth={auth} clients={clients} invoices={invoices} onChange={load} />}
      {tab === "billing" && <BillingTab auth={auth} />}
    </div>
  );
}

function ClientsTab({ auth, clients, onChange }: { auth: AuthResponse; clients: ClientDto[]; onChange: () => Promise<void> }) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.createClient(auth.accessToken, { name, email, currency: "USD" });
      setName(""); setEmail("");
      await onChange();
    } finally { setBusy(false); }
  };
  return (
    <section className="card">
      <h2>Clients</h2>
      <form onSubmit={submit} className="row">
        <input placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} required />
        <input placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        <button type="submit" disabled={busy}>Add</button>
      </form>
      <table>
        <thead><tr><th>Name</th><th>Email</th><th>Currency</th></tr></thead>
        <tbody>
          {clients.map(c => <tr key={c.id}><td>{c.name}</td><td>{c.email}</td><td>{c.currency}</td></tr>)}
        </tbody>
      </table>
    </section>
  );
}

function InvoicesTab({ auth, clients, invoices, onChange }: { auth: AuthResponse; clients: ClientDto[]; invoices: InvoiceDto[]; onChange: () => Promise<void> }) {
  const [clientId, setClientId] = useState(clients[0]?.id ?? "");
  const [desc, setDesc] = useState("");
  const [qty, setQty] = useState("1");
  const [price, setPrice] = useState("100");
  const [tax, setTax] = useState("0");
  const [busy, setBusy] = useState(false);

  useEffect(() => { if (!clientId && clients[0]) setClientId(clients[0].id); }, [clients]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.createInvoice(auth.accessToken, {
        clientId,
        issueDate: new Date().toISOString(),
        dueDate: new Date(Date.now() + 14 * 86400000).toISOString(),
        currency: "USD",
        lines: [{ description: desc, quantity: parseFloat(qty), unitPrice: parseFloat(price), taxRate: parseFloat(tax) }]
      });
      setDesc(""); setQty("1"); setPrice("100"); setTax("0");
      await onChange();
    } finally { setBusy(false); }
  };

  const send = async (id: string) => {
    await api.sendInvoice(auth.accessToken, id);
    await onChange();
  };

  return (
    <section className="card">
      <h2>Invoices</h2>
      <form onSubmit={submit} className="row">
        <select value={clientId} onChange={(e) => setClientId(e.target.value)} required>
          {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <input placeholder="Description" value={desc} onChange={(e) => setDesc(e.target.value)} required />
        <input type="number" min="0" step="0.01" value={qty} onChange={(e) => setQty(e.target.value)} placeholder="Qty" />
        <input type="number" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} placeholder="Price" />
        <input type="number" min="0" step="0.01" value={tax} onChange={(e) => setTax(e.target.value)} placeholder="Tax %" />
        <button type="submit" disabled={busy}>Create</button>
      </form>
      <table>
        <thead><tr><th>Number</th><th>Client</th><th>Total</th><th>Status</th><th></th></tr></thead>
        <tbody>
          {invoices.map(i => (
            <tr key={i.id}>
              <td>{i.number}</td>
              <td>{i.clientName}</td>
              <td>{i.total.toFixed(2)} {i.currency}</td>
              <td>{STATUS_LABEL[i.status]}</td>
              <td>
                {i.status === 0 && <button onClick={() => send(i.id)}>Send</button>}
                {i.publicPayToken && <a href={`${import.meta.env.VITE_API_URL ?? "http://localhost:5080"}/api/public/invoices/${i.publicPayToken}`} target="_blank">Pay link</a>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function BillingTab({ auth }: { auth: AuthResponse }) {
  const [plans, setPlans] = useState<{ code: string; name: string; pricePerMonth: number; features: string[] }[]>([]);
  const [status, setStatus] = useState<{ plan: string; status: string } | null>(null);
  useEffect(() => {
    api.listPlans().then(setPlans).catch(() => {});
    api.billingStatus(auth.accessToken).then(setStatus).catch(() => {});
  }, []);
  return (
    <section className="card">
      <h2>Billing</h2>
      {status && <p>Current plan: <strong>{status.plan}</strong> ({status.status})</p>}
      <div className="plans">
        {plans.map(p => (
          <div key={p.code} className="plan">
            <h3>{p.name}</h3>
            <p className="price">${p.pricePerMonth}/mo</p>
            <ul>{p.features.map(f => <li key={f}>{f}</li>)}</ul>
          </div>
        ))}
      </div>
    </section>
  );
}