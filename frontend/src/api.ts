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

const configuredUrl = import.meta.env.VITE_API_URL?.replace(/\/$/, "");
const devFallback = "http://localhost:5080";

export function getApiBaseUrl(): string {
  if (configuredUrl) return configuredUrl;
  if (import.meta.env.DEV) return devFallback;
  // Production: same-origin /api/* is proxied by Vercel serverless (api/[...path].js)
  if (typeof window !== "undefined") return window.location.origin;
  return "";
}

export function getApiConfigError(): string | null {
  if (import.meta.env.DEV && !configuredUrl) return null;
  return null;
}

async function parseEnvelope<T>(res: Response): Promise<ApiEnvelope<T>> {
  const text = await res.text();
  if (!text) {
    throw new Error(res.ok ? "Empty API response" : `API error (${res.status})`);
  }
  try {
    return JSON.parse(text) as ApiEnvelope<T>;
  } catch {
    if (text.trimStart().startsWith("<!") || text.includes("<html")) {
      throw new Error("API returned HTML instead of JSON. Set API_PROXY_URL on Vercel to your .NET backend URL.");
    }
    throw new Error(res.ok ? "Invalid API response" : `API error (${res.status}): ${text.slice(0, 120)}`);
  }
}

async function call<T>(path: string, init: RequestInit = {}, token?: string): Promise<T> {
  const baseUrl = getApiBaseUrl();
  if (!baseUrl) {
    throw new Error(getApiConfigError() ?? "API URL is not configured.");
  }

  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let res: Response;
  try {
    res = await fetch(`${baseUrl}${path}`, { ...init, headers });
  } catch {
    throw new Error(`Cannot reach API at ${baseUrl}. Check that the backend is running and CORS allows this site.`);
  }

  if (res.status === 401 && token) {
    const stored = localStorage.getItem("agencybill.auth");
    if (stored) {
      const parsed = JSON.parse(stored) as AuthResponse;
      try {
        const refreshed = await api.refresh(parsed.refreshToken);
        localStorage.setItem("agencybill.auth", JSON.stringify(refreshed));
        headers.set("Authorization", `Bearer ${refreshed.accessToken}`);
        const retry = await fetch(`${baseUrl}${path}`, { ...init, headers });
        const retryBody = await parseEnvelope<T>(retry);
        if (!retryBody.success) throw new Error(retryBody.error?.message ?? "Request failed");
        return retryBody.data as T;
      } catch {
        localStorage.removeItem("agencybill.auth");
        throw new Error("Session expired");
      }
    }
  }

  const body = await parseEnvelope<T>(res);
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
