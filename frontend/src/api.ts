export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userId: string;
  tenantId: string;
  role: string;
}

export interface ApiEnvelope<T> {
  success: boolean;
  data?: T;
  error?: { code: string; message: string };
}

export interface ClientDto {
  id: string;
  name: string;
  email: string;
  address?: string;
  currency: string;
  createdAt: string;
}

export interface InvoiceLineDto {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
}

export interface InvoiceDto {
  id: string;
  number: string;
  clientId: string;
  clientName: string;
  issueDate: string;
  dueDate: string;
  status: number;
  currency: string;
  subtotal: number;
  tax: number;
  total: number;
  publicPayToken: string;
  paidAt?: string | null;
  lines: InvoiceLineDto[];
}

export interface PlanDto {
  code: string;
  name: string;
  pricePerMonth: number;
  stripePriceId?: string | null;
  features: string[];
}

const BASE_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5080";

async function call<T>(path: string, init: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);
  const res = await fetch(`${BASE_URL}${path}`, { ...init, headers });
  if (res.status === 401 && token) {
    const stored = localStorage.getItem("ledgerly.auth");
    if (stored) {
      const parsed = JSON.parse(stored) as AuthResponse;
      try {
        const refreshed = await api.refresh(parsed.refreshToken);
        localStorage.setItem("ledgerly.auth", JSON.stringify(refreshed));
        headers.set("Authorization", `Bearer ${refreshed.accessToken}`);
        const retry = await fetch(`${BASE_URL}${path}`, { ...init, headers });
        const retryBody = (await retry.json()) as ApiEnvelope<T>;
        if (!retryBody.success) throw new Error(retryBody.error?.message ?? "Request failed");
        return retryBody.data as T;
      } catch {
        localStorage.removeItem("ledgerly.auth");
        throw new Error("Session expired");
      }
    }
  }
  const body = (await res.json()) as ApiEnvelope<T>;
  if (!body.success) {
    throw new Error(body.error?.message ?? "Request failed");
  }
  return body.data as T;
}

export const api = {
  register: (req: { email: string; password: string; fullName: string; tenantName: string }) =>
    call<AuthResponse>("/api/auth/register", { method: "POST", body: JSON.stringify(req) }),
  login: (req: { email: string; password: string }) =>
    call<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify(req) }),
  refresh: (refreshToken: string) =>
    call<AuthResponse>("/api/auth/refresh", { method: "POST", body: JSON.stringify({ refreshToken }) }),
  logout: (refreshToken: string) =>
    call<void>("/api/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) }),
  me: (token: string) => call<{ email: string; fullName: string; role: string; tenantName: string; plan: string }>("/api/auth/me", {}, token),
  listClients: (token: string) => call<ClientDto[]>("/api/clients", {}, token),
  createClient: (token: string, req: { name: string; email: string; address?: string; currency: string }) =>
    call<ClientDto>("/api/clients", { method: "POST", body: JSON.stringify(req) }, token),
  listInvoices: (token: string) => call<{ items: InvoiceDto[]; total: number }>("/api/invoices", {}, token),
  createInvoice: (token: string, req: unknown) =>
    call<InvoiceDto>("/api/invoices", { method: "POST", body: JSON.stringify(req) }, token),
  sendInvoice: (token: string, id: string) =>
    call<InvoiceDto>(`/api/invoices/${id}/send`, { method: "POST" }, token),
  listPlans: () => call<PlanDto[]>("/api/billing/plans"),
  billingStatus: (token: string) =>
    call<{ plan: string; status: string; stripeCustomerId?: string }>("/api/billing/status", {}, token)
};