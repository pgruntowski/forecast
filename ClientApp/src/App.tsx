import React, { useEffect, useMemo, useRef, useState } from "react";
import { PublicClientApplication, InteractionRequiredAuthError, AccountInfo } from "@azure/msal-browser";
import * as XLSX from "xlsx";

// =====================
// Quick ENV config
// =====================
const API_BASE = import.meta.env.VITE_API_BASE ?? "http://localhost:5000"; // e.g. https://trecom-api.azurewebsites.net
const AAD_AUTHORITY = import.meta.env.VITE_AAD_AUTHORITY ?? "https://login.microsoftonline.com/<tenant>";
const AAD_CLIENT_ID = import.meta.env.VITE_AAD_CLIENT_ID ?? "<frontend-client-id>";
const AAD_API_SCOPE = import.meta.env.VITE_AAD_API_SCOPE ?? "api://<backend-app-id-uri>/.default";

// =====================
// MSAL (Bearer) setup
// =====================
const msal = new PublicClientApplication({
    auth: { clientId: AAD_CLIENT_ID, authority: AAD_AUTHORITY, redirectUri: window.location.origin },
    cache: { cacheLocation: "sessionStorage" },
});

async function ensureLoggedIn(): Promise<AccountInfo> {
    const existing = msal.getAllAccounts()[0];
    if (existing) return existing;
    const r = await msal.loginPopup({ scopes: [AAD_API_SCOPE] });
    return r.account!;
}

async function acquireApiToken(): Promise<string> {
    const account = await ensureLoggedIn();
    const request = { account, scopes: [AAD_API_SCOPE] } as const;
    try {
        const r = await msal.acquireTokenSilent(request);
        return r.accessToken;
    } catch (e) {
        if (e instanceof InteractionRequiredAuthError) {
            const r = await msal.acquireTokenPopup(request);
            return r.accessToken;
        }
        throw e;
    }
}

async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
    const token = await acquireApiToken();
    const headers: Record<string, string> = {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        ...(init.headers as Record<string, string> | undefined),
    };
    const res = await fetch(`${API_BASE}${path}`, { ...init, headers });
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}: ${await res.text()}`);
    if (res.status === 204) return undefined as unknown as T;
    return (await res.json()) as T;
}

// =====================
// Types matching backend DTOs (adapt as needed)
// =====================
export type UserDto = {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    role: string;
    isActive: boolean;
    createdAt: string;
    updatedAt?: string | null;
};

export type ProjectDto = {
    projectId: string;
    version: number;
    effectiveAt: string; // ISO
    amId: string;
    clientId: string;
    marketId: number;
    name: string;
    statusId: number;
    value: number; // gross? adapt
    margin: number;
    probabilityPercent: number;
    dueQuarter: string; // "2025 Q1"
    invoiceMonth?: string | null; // "2025-01"
    paymentQuarter?: string | null;
    vendorId?: string | null;
    architectureId?: number | null;
    comment?: string | null;
    isCanceled: boolean;
    weightedMargin: number; // server sends rounded
    participants: string[]; // user ids
};

export type CreateProjectDto = {
    effectiveAt?: string | null; // ISO
    amId: string;
    clientId: string;
    marketId: number;
    name: string;
    statusId: number;
    value: number;
    margin: number;
    probabilityPercent?: number | null;
    dueQuarter: string;
    invoiceMonth?: string | null;
    paymentQuarter?: string | null;
    vendorId?: string | null;
    architectureId?: number | null;
    comment?: string | null;
    participants?: string[]; // user ids
};

export type AddProjectRevisionDto = Partial<Omit<CreateProjectDto, "participants">> & {
    isCanceled?: boolean;
};

// Dictionaries (expected shapes; adjust if different)
export type DictItemInt = { id: number; name: string; autoProbabilityPercent?: number | null };
export type DictItemGuid = { id: string; name: string };

// =====================
// Utilities
// =====================
function currentQuarterString(d = new Date()): string {
    const y = d.getUTCFullYear();
    const q = Math.floor(d.getUTCMonth() / 3) + 1;
    return `${y} Q${q}`;
}

function quarterOffset(qs: string, delta: number): string {
    // qs: "YYYY Qn"
    const m = qs.match(/^(\d{4})\s*Q([1-4])$/);
    if (!m) return qs;
    let y = parseInt(m[1], 10);
    let q = parseInt(m[2], 10) + delta;
    while (q > 4) { q -= 4; y++; }
    while (q < 1) { q += 4; y--; }
    return `${y} Q${q}`;
}

const fmtPln = (v: number) => new Intl.NumberFormat("pl-PL", { style: "currency", currency: "PLN", maximumFractionDigits: 0 }).format(v);

// =====================
// API wrappers
// =====================
const api = {
    me: () => apiFetch<{ Name?: string; Email?: string; AppUserId?: string; Roles: string[] }>("/api/me"),

    users: (params: { role?: string; search?: string; skip?: number; take?: number; includeInactive?: boolean }) => {
        const q = new URLSearchParams();
        if (params.role) q.set("role", params.role);
        if (params.search) q.set("search", params.search);
        if (params.skip != null) q.set("skip", String(params.skip));
        if (params.take != null) q.set("take", String(params.take));
        if (params.includeInactive) q.set("includeInactive", "true");
        return apiFetch<UserDto[]>(`/api/users?${q.toString()}`);
    },

    // Projects
    projectsCurrent: (params: {
        amId?: string; userId?: string; marketId?: number; statusId?: number; clientId?: string; vendorId?: string; architectureId?: number;
        dueQuarter?: string; invoiceMonth?: string; hideCanceled?: boolean; search?: string; skip?: number; take?: number;
    }) => {
        const q = new URLSearchParams();
        const set = (k: string, v: any) => (v !== undefined && v !== null && v !== "" ? q.set(k, String(v)) : undefined);
        Object.entries(params).forEach(([k, v]) => set(k, v as any));
        return apiFetch<ProjectDto[]>(`/api/projects/current?${q.toString()}`);
    },
    projectHistory: (projectId: string) => apiFetch<ProjectDto[]>(`/api/projects/${projectId}/history`),
    projectCreate: (dto: CreateProjectDto) => apiFetch<string>(`/api/projects`, { method: "POST", body: JSON.stringify(dto) }),
    projectAddRevision: (projectId: string, dto: AddProjectRevisionDto) => apiFetch<number>(`/api/projects/${projectId}/revisions`, { method: "POST", body: JSON.stringify(dto) }),
    projectsSummary: (params: { asOf?: string; amId?: string; userId?: string; marketId?: number; statusId?: number; clientId?: string; vendorId?: string; architectureId?: number; dueQuarter?: string; invoiceMonth?: string; hideCanceled?: boolean; }) => {
        const q = new URLSearchParams();
        const set = (k: string, v: any) => (v !== undefined && v !== null && v !== "" ? q.set(k, String(v)) : undefined);
        Object.entries(params).forEach(([k, v]) => set(k, v as any));
        return apiFetch<{ count: number; valueSum: number; marginSum: number; weightedMarginSum: number }>(`/api/projects/summary?${q.toString()}`);
    },

    // Dictionaries — adjust to your actual endpoints
    markets: () => apiFetch<DictItemInt[]>(`/api/dictionaries/markets`).catch(() => []),
    statuses: () => apiFetch<DictItemInt[]>(`/api/dictionaries/statuses`).catch(() => []),
    architectures: () => apiFetch<DictItemInt[]>(`/api/dictionaries/architectures`).catch(() => []),
    vendors: () => apiFetch<DictItemGuid[]>(`/api/vendors`).catch(() => []),
    vendorCreate: (name: string) => apiFetch<DictItemGuid>(`/api/vendors`, { method: "POST", body: JSON.stringify({ name }) }),
    clients: () => apiFetch<DictItemGuid[]>(`/api/clients`).catch(() => []),
};

// =====================
// Core App
// =====================
export default function App() {
    const [me, setMe] = useState<{ Name?: string; Email?: string; AppUserId?: string; Roles: string[] } | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [tab, setTab] = useState<"projects" | "users">("projects");

    useEffect(() => {
        (async () => {
            try { setMe(await api.me()); } catch (e: any) { setError(e.message ?? String(e)); }
        })();
    }, []);

    if (error) {
        return (
            <div className="min-h-screen flex items-center justify-center p-8">
                <div className="max-w-lg w-full bg-white rounded-2xl shadow p-6 space-y-4">
                    <h1 className="text-xl font-semibold">Błąd</h1>
                    <p className="text-red-600 whitespace-pre-wrap">{error}</p>
                    <button className="rounded-xl border px-3 py-2" onClick={() => window.location.reload()}>Odśwież</button>
                </div>
            </div>
        );
    }

    if (!me) {
        return (
            <div className="min-h-screen flex items-center justify-center p-8">
                <div className="max-w-md w-full bg-white rounded-2xl shadow p-6 space-y-4 text-center">
                    <h1 className="text-2xl font-semibold">Trecom Forecast</h1>
                    <p className="text-gray-600">Logowanie przez Microsoft</p>
                    <button
                        className="rounded-xl px-4 py-2 bg-black text-white"
                        onClick={async () => { await ensureLoggedIn(); window.location.reload(); }}
                    >Zaloguj</button>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gray-50">
            <header className="bg-white shadow">
                <div className="max-w-7xl mx-auto px-4 py-4 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                        <span className="text-xl font-semibold">Trecom Forecast</span>
                        <nav className="ml-6 flex gap-2">
                            <button onClick={() => setTab("projects")} className={`px-3 py-2 rounded-xl ${tab === 'projects' ? 'bg-black text-white' : 'border'}`}>Projekty</button>
                            <button onClick={() => setTab("users")} className={`px-3 py-2 rounded-xl ${tab === 'users' ? 'bg-black text-white' : 'border'}`}>Użytkownicy</button>
                        </nav>
                    </div>
                    <div className="flex items-center gap-3 text-sm">
                        <span className="text-gray-600">{me.Name ?? me.Email}</span>
                        <button className="px-3 py-2 rounded-xl border" onClick={async () => { await msal.logoutPopup(); window.location.reload(); }}>Wyloguj</button>
                    </div>
                </div>
            </header>

            {tab === "projects" ? <ProjectsPage me={me} /> : <UsersPage />}
        </div>
    );
}

// =====================
// Users Page
// =====================
function UsersPage() {
    const [search, setSearch] = useState("");
    const [role, setRole] = useState("");
    const [includeInactive, setIncludeInactive] = useState(false);
    const [page, setPage] = useState(0);
    const pageSize = 20;

    const [loading, setLoading] = useState(false);
    const [items, setItems] = useState<UserDto[]>([]);
    const [error, setError] = useState<string | null>(null);

    const load = async () => {
        setLoading(true); setError(null);
        try {
            const data = await api.users({ role: role || undefined, search: search || undefined, includeInactive, skip: page * pageSize, take: pageSize });
            setItems(data);
        } catch (e: any) { setError(e.message ?? String(e)); }
        finally { setLoading(false); }
    };

    useEffect(() => { load(); /* eslint-disable-next-line */ }, [role, includeInactive, page]);

    return (
        <div className="max-w-7xl mx-auto p-6 space-y-4">
            <div className="bg-white p-4 rounded-2xl shadow grid md:grid-cols-4 gap-3">
                <input className="border rounded-xl px-3 py-2 md:col-span-2" placeholder="Szukaj (imię, nazwisko, email)" value={search} onChange={(e) => setSearch(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && load()} />
                <select className="border rounded-xl px-3 py-2" value={role} onChange={(e) => setRole(e.target.value)}>
                    <option value="">Rola: dowolna</option>
                    <option value="AM">AM</option>
                    <option value="TeamLeader">TeamLeader</option>
                    <option value="Board">Board</option>
                    <option value="Admin">Admin</option>
                </select>
                <label className="flex items-center gap-2"><input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />Pokaż nieaktywne</label>
                <div className="md:col-start-4 flex gap-2">
                    <button className="border rounded-xl px-3 py-2" onClick={() => { setPage(0); load(); }} disabled={loading}>{loading ? "Ładowanie…" : "Filtruj"}</button>
                </div>
            </div>

            {error && <div className="text-red-600">{error}</div>}

            <div className="bg-white rounded-2xl shadow overflow-auto">
                <table className="w-full table-auto">
                    <thead className="bg-gray-100 text-left">
                        <tr>
                            <th className="px-3 py-2">Imię</th>
                            <th className="px-3 py-2">Nazwisko</th>
                            <th className="px-3 py-2">Email</th>
                            <th className="px-3 py-2">Rola</th>
                            <th className="px-3 py-2">Aktywny</th>
                        </tr>
                    </thead>
                    <tbody>
                        {items.map(u => (
                            <tr key={u.id} className="border-t">
                                <td className="px-3 py-2">{u.firstName}</td>
                                <td className="px-3 py-2">{u.lastName}</td>
                                <td className="px-3 py-2">{u.email}</td>
                                <td className="px-3 py-2">{u.role}</td>
                                <td className="px-3 py-2">{u.isActive ? "✔" : "—"}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <div className="flex items-center gap-2">
                <button className="border rounded-xl px-3 py-2" onClick={() => setPage(p => Math.max(0, p - 1))}>Poprzednia</button>
                <span>Strona {page + 1}</span>
                <button className="border rounded-xl px-3 py-2" onClick={() => setPage(p => p + 1)}>Następna</button>
            </div>
        </div>
    );
}

// =====================
// Projects Page
// =====================
function ProjectsPage({ me }: { me: { AppUserId?: string; Roles: string[] } }) {
    const [dicts, setDicts] = useState<{ markets: DictItemInt[]; statuses: DictItemInt[]; architectures: DictItemInt[]; vendors: DictItemGuid[]; clients: DictItemGuid[]; users: UserDto[] }>({ markets: [], statuses: [], architectures: [], vendors: [], clients: [], users: [] });

    const [filters, setFilters] = useState<{ amId?: string; userId?: string; marketId?: number; statusId?: number; clientId?: string; vendorId?: string; architectureId?: number; dueQuarter?: string; invoiceMonth?: string; hideCanceled?: boolean; search?: string; }>({
        hideCanceled: true,
        dueQuarter: currentQuarterString(),
    });
    const [page, setPage] = useState(0);
    const pageSize = 100;

    const [items, setItems] = useState<ProjectDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

   // const isAM = me.Roles.includes("AM");
    const isLeader = me.Roles.includes("TeamLeader");
    const isBoard = me.Roles.includes("Board") || me.Roles.includes("Admin") || me.Roles.includes("SuperAdmin");

    // Load dictionaries once
    useEffect(() => {
        (async () => {
            const [markets, statuses, architectures, vendors, clients] = await Promise.all([
                api.markets(), api.statuses(), api.architectures(), api.vendors(), api.clients()
            ]);
            const users = await api.users({ take: 500, skip: 0, includeInactive: false });
            setDicts({ markets, statuses, architectures, vendors, clients, users });
        })();
    }, []);

    const load = async () => {
        setLoading(true); setError(null);
        try {
            const data = await api.projectsCurrent({ ...filters, skip: page * pageSize, take: pageSize });
            setItems(data);
        } catch (e: any) { setError(e.message ?? String(e)); }
        finally { setLoading(false); }
    };

    useEffect(() => { load(); /* eslint-disable-next-line */ }, [filters.dueQuarter, filters.hideCanceled, filters.marketId, filters.statusId, filters.vendorId, filters.architectureId, filters.clientId, filters.search, filters.amId, filters.userId, page]);

    // Aggregates for visible page
    const sums = useMemo(() => {
        const valueSum = items.reduce((a, b) => a + (b.value ?? 0), 0);
        const marginSum = items.reduce((a, b) => a + (b.margin ?? 0), 0);
        const weightedMarginSum = items.reduce((a, b) => a + (b.weightedMargin ?? (b.margin * (b.probabilityPercent ?? 0) / 100)), 0);
        return { valueSum, marginSum, weightedMarginSum };
    }, [items]);

    // Summary API (server-side accurate for the same filters)
    const [summary, setSummary] = useState<{ count: number; valueSum: number; marginSum: number; weightedMarginSum: number } | null>(null);
    useEffect(() => { (async () => { setSummary(await api.projectsSummary({ ...filters })); })(); }, [filters]);

    // Create project dialog state
    const [openCreate, setOpenCreate] = useState(false);

    return (
        <div className="max-w-7xl mx-auto p-6 space-y-4">
            {/* Filters */}
            <div className="bg-white p-4 rounded-2xl shadow grid md:grid-cols-6 gap-3 items-end">
                <input className="border rounded-xl px-3 py-2 md:col-span-2" placeholder="Szukaj globalnie" value={filters.search ?? ""} onChange={(e) => setFilters(f => ({ ...f, search: e.target.value }))} onKeyDown={(e) => e.key === 'Enter' && (setPage(0), load())} />

                <select className="border rounded-xl px-3 py-2" value={filters.marketId ?? ""} onChange={(e) => setFilters(f => ({ ...f, marketId: e.target.value ? Number(e.target.value) : undefined }))}>
                    <option value="">Rynek</option>
                    {dicts.markets.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
                </select>

                <select className="border rounded-xl px-3 py-2" value={filters.statusId ?? ""} onChange={(e) => setFilters(f => ({ ...f, statusId: e.target.value ? Number(e.target.value) : undefined }))}>
                    <option value="">Status</option>
                    {dicts.statuses.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                </select>

                <select className="border rounded-xl px-3 py-2" value={filters.architectureId ?? ""} onChange={(e) => setFilters(f => ({ ...f, architectureId: e.target.value ? Number(e.target.value) : undefined }))}>
                    <option value="">Architektura</option>
                    {dicts.architectures.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select>

                <select className="border rounded-xl px-3 py-2" value={filters.vendorId ?? ""} onChange={(e) => setFilters(f => ({ ...f, vendorId: e.target.value || undefined }))}>
                    <option value="">Vendor</option>
                    {dicts.vendors.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
                </select>

                <select className="border rounded-xl px-3 py-2" value={filters.dueQuarter ?? ""} onChange={(e) => setFilters(f => ({ ...f, dueQuarter: e.target.value || undefined }))}>
                    {(() => {
                        const base = currentQuarterString();
                        const opts = [-2, -1, 0, 1, 2].map(d => quarterOffset(base, d));
                        return opts.map(o => <option key={o} value={o}>{o}</option>);
                    })()}
                </select>

                <div className="flex items-center gap-2">
                    <input id="hideCanceled" type="checkbox" checked={filters.hideCanceled ?? true} onChange={(e) => setFilters(f => ({ ...f, hideCanceled: e.target.checked }))} />
                    <label htmlFor="hideCanceled">Ukryj anulowane</label>
                </div>

                {(isLeader || isBoard) && (
                    <select className="border rounded-xl px-3 py-2" value={filters.amId ?? ""} onChange={(e) => setFilters(f => ({ ...f, amId: e.target.value || undefined }))}>
                        <option value="">AM: wszyscy</option>
                        {dicts.users.filter(u => u.role === "AM").map(u => <option key={u.id} value={u.id}>{u.lastName} {u.firstName}</option>)}
                    </select>
                )}

                <button className="border rounded-xl px-3 py-2" onClick={() => { setPage(0); load(); }}>Filtruj</button>
                <button className="border rounded-xl px-3 py-2" onClick={() => setOpenCreate(true)}>+ Nowy projekt</button>
                <ExportExcelButton rows={items} dicts={dicts} />
            </div>

            {/* Aggregates */}
            <div className="grid md:grid-cols-4 gap-3">
                <KpiCard title="Wartość (strona)" value={fmtPln(sums.valueSum)} />
                <KpiCard title="Marża (strona)" value={fmtPln(sums.marginSum)} />
                <KpiCard title="Marża ważona (strona)" value={fmtPln(sums.weightedMarginSum)} />
                <KpiCard title="Wynik (API)" value={summary ? `${summary.count} / ${fmtPln(summary.marginSum)} / ${fmtPln(summary.weightedMarginSum)}` : "…"} subtitle="count / marża / ważona" />
            </div>

            {/* Table */}
            <div className="bg-white rounded-2xl shadow overflow-auto">
                <table className="w-full table-auto">
                    <thead className="bg-gray-100 text-left text-sm">
                        <tr>
                            <th className="px-3 py-2">AM</th>
                            <th className="px-3 py-2">Klient</th>
                            <th className="px-3 py-2">Rynek</th>
                            <th className="px-3 py-2">Projekt</th>
                            <th className="px-3 py-2">Status</th>
                            <th className="px-3 py-2">Wartość</th>
                            <th className="px-3 py-2">Marża</th>
                            <th className="px-3 py-2">Prawdop.</th>
                            <th className="px-3 py-2">Marża ważona</th>
                            <th className="px-3 py-2">Termin</th>
                            <th className="px-3 py-2">FV</th>
                            <th className="px-3 py-2">Płatność</th>
                            <th className="px-3 py-2">Vendor</th>
                            <th className="px-3 py-2">Architektura</th>
                            <th className="px-3 py-2">Komentarz</th>
                            <th className="px-3 py-2">Akcje</th>
                        </tr>
                    </thead>
                    <tbody>
                        {items.map(p => <ProjectRow key={`${p.projectId}:${p.version}`} p={p} dicts={dicts} onChanged={load} />)}
                    </tbody>
                    <tfoot className="bg-gray-50">
                        <tr>
                            <td className="px-3 py-2 font-semibold" colSpan={5}>Suma (strona):</td>
                            <td className="px-3 py-2 font-semibold">{fmtPln(sums.valueSum)}</td>
                            <td className="px-3 py-2 font-semibold">{fmtPln(sums.marginSum)}</td>
                            <td className="px-3 py-2" />
                            <td className="px-3 py-2 font-semibold">{fmtPln(sums.weightedMarginSum)}</td>
                            <td colSpan={6} />
                        </tr>
                    </tfoot>
                </table>
            </div>

            {/* Pagination */}
            <div className="flex items-center gap-2">
                <button className="border rounded-xl px-3 py-2" onClick={() => setPage(p => Math.max(0, p - 1))}>Poprzednia</button>
                <span>Strona {page + 1}</span>
                <button className="border rounded-xl px-3 py-2" onClick={() => setPage(p => p + 1)}>Następna</button>
            </div>

            {openCreate && (
                <ProjectCreateDialog onClose={() => setOpenCreate(false)} dicts={dicts} onCreated={async () => { setOpenCreate(false); setPage(0); await load(); }} me={me} />
            )}
        </div>
    );
}

function KpiCard({ title, value, subtitle }: { title: string; value: string; subtitle?: string }) {
    return (
        <div className="bg-white rounded-2xl shadow p-4">
            <div className="text-sm text-gray-500">{title}</div>
            <div className="text-2xl font-semibold">{value}</div>
            {subtitle && <div className="text-xs text-gray-400">{subtitle}</div>}
        </div>
    );
}

function nameById<T extends { id: any; name: string }>(list: T[], id: any) {
    return list.find(x => String(x.id) === String(id))?.name ?? "—";
}

function ProjectRow({ p, dicts, onChanged }: { p: ProjectDto; dicts: { markets: DictItemInt[]; statuses: DictItemInt[]; architectures: DictItemInt[]; vendors: DictItemGuid[]; clients: DictItemGuid[]; users: UserDto[] }; onChanged: () => void; }) {
    const probability = p.probabilityPercent ?? 0;
    const weighted = p.weightedMargin ?? (p.margin * probability / 100);
    return (
        <tr className="border-t align-top">
            <td className="px-3 py-2">{nameById(dicts.users.map(u => ({ id: u.id, name: `${u.lastName} ${u.firstName}` })), p.amId)}</td>
            <td className="px-3 py-2">{nameById(dicts.clients, p.clientId)}</td>
            <td className="px-3 py-2">{nameById(dicts.markets, p.marketId)}</td>
            <td className="px-3 py-2 font-medium">{p.name}</td>
            <td className="px-3 py-2">{nameById(dicts.statuses, p.statusId)}</td>
            <td className="px-3 py-2 whitespace-nowrap">{fmtPln(p.value)}</td>
            <td className="px-3 py-2 whitespace-nowrap">{fmtPln(p.margin)}</td>
            <td className="px-3 py-2">{probability}%</td>
            <td className="px-3 py-2 whitespace-nowrap">{fmtPln(weighted)}</td>
            <td className="px-3 py-2">{p.dueQuarter}</td>
            <td className="px-3 py-2">{p.invoiceMonth ?? "—"}</td>
            <td className="px-3 py-2">{p.paymentQuarter ?? "—"}</td>
            <td className="px-3 py-2">{nameById(dicts.vendors, p.vendorId)}</td>
            <td className="px-3 py-2">{nameById(dicts.architectures, p.architectureId)}</td>
            <td className="px-3 py-2 max-w-[24rem]">{p.comment}</td>
            <td className="px-3 py-2">
                <AddRevisionInline p={p} dicts={dicts} onChanged={onChanged} />
            </td>
        </tr>
    );
}

function AddRevisionInline({ p, dicts, onChanged }: { p: ProjectDto; dicts: { markets: DictItemInt[]; statuses: DictItemInt[]; architectures: DictItemInt[]; vendors: DictItemGuid[]; clients: DictItemGuid[]; users: UserDto[] }; onChanged: () => void; }) {
    const [open, setOpen] = useState(false);
    const [dto, setDto] = useState<AddProjectRevisionDto>({
        name: p.name,
        statusId: p.statusId,
        value: p.value,
        margin: p.margin,
        probabilityPercent: p.probabilityPercent,
        dueQuarter: p.dueQuarter,
        invoiceMonth: p.invoiceMonth ?? undefined,
        paymentQuarter: p.paymentQuarter ?? undefined,
        vendorId: p.vendorId ?? undefined,
        architectureId: p.architectureId ?? undefined,
        comment: p.comment ?? undefined,
        isCanceled: p.isCanceled,
    });

    // auto probability from status if provided
    useEffect(() => {
        const s = dicts.statuses.find(s => s.id === dto.statusId);
        if (s && s.autoProbabilityPercent != null) {
            setDto(d => ({ ...d, probabilityPercent: s.autoProbabilityPercent! }));
        }
    }, [dto.statusId]);

    const save = async () => {
        await api.projectAddRevision(p.projectId, dto);
        setOpen(false);
        onChanged();
    };

    return (
        <div>
            <button className="border rounded-xl px-2 py-1 text-sm" onClick={() => setOpen(true)}>+ Rewizja</button>
            {open && (
                <div className="fixed inset-0 bg-black/20 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-2xl shadow w-full max-w-2xl p-4 space-y-3">
                        <div className="flex items-center justify-between">
                            <div className="text-lg font-semibold">Nowa rewizja</div>
                            <button className="border rounded-xl px-2 py-1" onClick={() => setOpen(false)}>Zamknij</button>
                        </div>

                        <div className="grid md:grid-cols-2 gap-3">
                            <div>
                                <label className="text-sm">Nazwa</label>
                                <input className="border rounded-xl w-full px-3 py-2" value={dto.name ?? ""} onChange={e => setDto(d => ({ ...d, name: e.target.value }))} />
                            </div>

                            <div>
                                <label className="text-sm">Status</label>
                                <select className="border rounded-xl w-full px-3 py-2" value={dto.statusId ?? ""} onChange={e => setDto(d => ({ ...d, statusId: e.target.value ? Number(e.target.value) : undefined }))}>
                                    {dicts.statuses.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                                </select>
                            </div>

                            <NumberInput label="Wartość [PLN]" value={dto.value ?? 0} onChange={v => setDto(d => ({ ...d, value: v }))} />
                            <NumberInput label="Marża [PLN]" value={dto.margin ?? 0} onChange={v => setDto(d => ({ ...d, margin: v }))} />

                            <div>
                                <label className="text-sm">Prawdopodobieństwo [%]</label>
                                <input type="number" min={0} max={100} className="border rounded-xl w-full px-3 py-2" value={dto.probabilityPercent ?? 0} onChange={e => setDto(d => ({ ...d, probabilityPercent: Number(e.target.value) }))} />
                            </div>

                            <div>
                                <label className="text-sm">Termin (kwartał)</label>
                                <select className="border rounded-xl w-full px-3 py-2" value={dto.dueQuarter ?? currentQuarterString()} onChange={e => setDto(d => ({ ...d, dueQuarter: e.target.value }))}>
                                    {(() => {
                                        const base = currentQuarterString();
                                        const opts = [-2, -1, 0, 1, 2, 3, 4].map(d => quarterOffset(base, d));
                                        return opts.map(o => <option key={o} value={o}>{o}</option>);
                                    })()}
                                </select>
                            </div>

                            <div>
                                <label className="text-sm">Termin FV (YYYY-MM)</label>
                                <input placeholder="2025-01" className="border rounded-xl w-full px-3 py-2" value={dto.invoiceMonth ?? ""} onChange={e => setDto(d => ({ ...d, invoiceMonth: e.target.value || undefined }))} />
                            </div>

                            <div>
                                <label className="text-sm">Termin płatności (kwartał)</label>
                                <input placeholder="2025 Q2" className="border rounded-xl w-full px-3 py-2" value={dto.paymentQuarter ?? ""} onChange={e => setDto(d => ({ ...d, paymentQuarter: e.target.value || undefined }))} />
                            </div>

                            <div>
                                <label className="text-sm">Vendor</label>
                                <select className="border rounded-xl w-full px-3 py-2" value={dto.vendorId ?? ""} onChange={e => setDto(d => ({ ...d, vendorId: e.target.value || undefined }))}>
                                    <option value="">—</option>
                                    {dicts.vendors.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
                                </select>
                            </div>

                            <div>
                                <label className="text-sm">Architektura</label>
                                <select className="border rounded-xl w-full px-3 py-2" value={dto.architectureId ?? ""} onChange={e => setDto(d => ({ ...d, architectureId: e.target.value ? Number(e.target.value) : undefined }))}>
                                    <option value="">—</option>
                                    {dicts.architectures.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                                </select>
                            </div>
                        </div>

                        <div>
                            <label className="text-sm">Komentarz</label>
                            <textarea className="border rounded-xl w-full px-3 py-2" value={dto.comment ?? ""} onChange={e => setDto(d => ({ ...d, comment: e.target.value || undefined }))} />
                        </div>

                        <label className="flex items-center gap-2"><input type="checkbox" checked={dto.isCanceled ?? false} onChange={e => setDto(d => ({ ...d, isCanceled: e.target.checked }))} />Anulowany</label>

                        <div className="flex justify-end gap-2">
                            <button className="border rounded-xl px-3 py-2" onClick={() => setOpen(false)}>Anuluj</button>
                            <button className="bg-black text-white rounded-xl px-3 py-2" onClick={save}>Zapisz rewizję</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

function NumberInput({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
    const [txt, setTxt] = useState(String(value ?? 0));
    useEffect(() => { setTxt(String(value ?? 0)); }, [value]);
    return (
        <div>
            <label className="text-sm">{label}</label>
            <input className="border rounded-xl w-full px-3 py-2" value={txt} onChange={(e) => {
                setTxt(e.target.value);
                const n = Number(e.target.value.replace(/\s/g, "").replace(",", "."));
                if (!isNaN(n)) onChange(n);
            }} />
        </div>
    );
}

function ProjectCreateDialog({ onClose, dicts, onCreated, me }: { onClose: () => void; onCreated: () => void; me: { AppUserId?: string; Roles: string[] }; dicts: { markets: DictItemInt[]; statuses: DictItemInt[]; architectures: DictItemInt[]; vendors: DictItemGuid[]; clients: DictItemGuid[]; users: UserDto[] } }) {
    const defaultAM = useMemo(() => me.AppUserId ?? dicts.users.find(u => u.role === "AM")?.id ?? "", [me, dicts.users]);
    const [dto, setDto] = useState<CreateProjectDto>({
        amId: defaultAM,
        clientId: dicts.clients[0]?.id ?? "",
        marketId: dicts.markets[0]?.id ?? 1,
        name: "",
        statusId: dicts.statuses[0]?.id ?? 1,
        value: 0,
        margin: 0,
        probabilityPercent: dicts.statuses[0]?.autoProbabilityPercent ?? 0,
        dueQuarter: currentQuarterString(),
        invoiceMonth: "",
        paymentQuarter: "",
        vendorId: dicts.vendors[0]?.id,
        architectureId: dicts.architectures[0]?.id,
        comment: "",
        participants: [],
    });

    // auto probability from status
    useEffect(() => {
        const s = dicts.statuses.find(s => s.id === dto.statusId);
        if (s && s.autoProbabilityPercent != null) {
            setDto(d => ({ ...d, probabilityPercent: s.autoProbabilityPercent! }));
        }
    }, [dto.statusId]);

    const [newVendor, setNewVendor] = useState("");
    const addVendor = async () => {
        if (!newVendor.trim()) return;
        const v = await api.vendorCreate(newVendor.trim());
        dicts.vendors.push(v);
        setDto(d => ({ ...d, vendorId: v.id }));
        setNewVendor("");
    };

    const [saving, setSaving] = useState(false);
    const save = async () => {
        setSaving(true);
        try {
            const payload: CreateProjectDto = {
                ...dto,
                effectiveAt: new Date().toISOString(),
                invoiceMonth: dto.invoiceMonth || undefined,
                paymentQuarter: dto.paymentQuarter || undefined,
                vendorId: dto.vendorId || undefined,
                architectureId: dto.architectureId ?? undefined,
                participants: dto.participants ?? [],
            };
            await api.projectCreate(payload);
            onCreated();
        } finally { setSaving(false); }
    };

    return (
        <div className="fixed inset-0 bg-black/20 flex items-center justify-center p-4 z-50">
            <div className="bg-white rounded-2xl shadow w-full max-w-3xl p-4 space-y-3">
                <div className="flex items-center justify-between">
                    <div className="text-lg font-semibold">Nowy projekt</div>
                    <button className="border rounded-xl px-2 py-1" onClick={onClose}>Zamknij</button>
                </div>

                <div className="grid md:grid-cols-2 gap-3">
                    <div>
                        <label className="text-sm">AM</label>
                        <select className="border rounded-xl w-full px-3 py-2" value={dto.amId} onChange={e => setDto(d => ({ ...d, amId: e.target.value }))}>
                            {dicts.users.filter(u => u.role === "AM").map(u => <option key={u.id} value={u.id}>{u.lastName} {u.firstName}</option>)}
                        </select>
                    </div>

                    <div>
                        <label className="text-sm">Klient</label>
                        <select className="border rounded-xl w-full px-3 py-2" value={dto.clientId} onChange={e => setDto(d => ({ ...d, clientId: e.target.value }))}>
                            {dicts.clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                        </select>
                    </div>

                    <div>
                        <label className="text-sm">Rynek</label>
                        <select className="border rounded-xl w-full px-3 py-2" value={dto.marketId} onChange={e => setDto(d => ({ ...d, marketId: Number(e.target.value) }))}>
                            {dicts.markets.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
                        </select>
                    </div>

                    <div>
                        <label className="text-sm">Nazwa projektu</label>
                        <input className="border rounded-xl w-full px-3 py-2" value={dto.name} onChange={e => setDto(d => ({ ...d, name: e.target.value }))} />
                    </div>

                    <div>
                        <label className="text-sm">Status</label>
                        <select className="border rounded-xl w-full px-3 py-2" value={dto.statusId} onChange={e => setDto(d => ({ ...d, statusId: Number(e.target.value) }))}>
                            {dicts.statuses.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                        </select>
                    </div>

                    <NumberInput label="Wartość [PLN]" value={dto.value} onChange={v => setDto(d => ({ ...d, value: v }))} />
                    <NumberInput label="Marża [PLN]" value={dto.margin} onChange={v => setDto(d => ({ ...d, margin: v }))} />

                    <div>
                        <label className="text-sm">Prawdopodobieństwo [%]</label>
                        <input type="number" min={0} max={100} className="border rounded-xl w-full px-3 py-2" value={dto.probabilityPercent ?? 0} onChange={e => setDto(d => ({ ...d, probabilityPercent: Number(e.target.value) }))} />
                    </div>

                    <div>
                        <label className="text-sm">Termin (kwartał)</label>
                        <select className="border rounded-xl w-full px-3 py-2" value={dto.dueQuarter} onChange={e => setDto(d => ({ ...d, dueQuarter: e.target.value }))}>
                            {(() => {
                                const base = currentQuarterString();
                                const opts = [-2, -1, 0, 1, 2, 3, 4].map(d => quarterOffset(base, d));
                                return opts.map(o => <option key={o} value={o}>{o}</option>);
                            })()}
                        </select>
                    </div>

                    <div>
                        <label className="text-sm">Termin FV (YYYY-MM)</label>
                        <input placeholder="2025-01" className="border rounded-xl w-full px-3 py-2" value={dto.invoiceMonth ?? ""} onChange={e => setDto(d => ({ ...d, invoiceMonth: e.target.value }))} />
                    </div>

                    <div>
                        <label className="text-sm">Termin płatności (kwartał)</label>
                        <input placeholder="2025 Q2" className="border rounded-xl w-full px-3 py-2" value={dto.paymentQuarter ?? ""} onChange={e => setDto(d => ({ ...d, paymentQuarter: e.target.value }))} />
                    </div>

                    <div>
                        <label className="text-sm">Vendor</label>
                        <div className="flex gap-2">
                            <select className="border rounded-xl px-3 py-2 flex-1" value={dto.vendorId ?? ""} onChange={e => setDto(d => ({ ...d, vendorId: e.target.value || undefined }))}>
                                <option value="">—</option>
                                {dicts.vendors.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
                            </select>
                            <input className="border rounded-xl px-3 py-2 w-40" placeholder="Nowy vendor" value={newVendor} onChange={e => setNewVendor(e.target.value)} />
                            <button className="border rounded-xl px-3 py-2" onClick={addVendor}>Dodaj</button>
                        </div>
                    </div>

                    <div>
                        <label className="text-sm">Architektura</label>
                        <select className="border rounded-xl w-full px-3 py-2" value={dto.architectureId ?? ""} onChange={e => setDto(d => ({ ...d, architectureId: e.target.value ? Number(e.target.value) : undefined }))}>
                            <option value="">—</option>
                            {dicts.architectures.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                        </select>
                    </div>

                    <div className="md:col-span-2">
                        <label className="text-sm">Uczestnicy</label>
                        <MultiSelect options={dicts.users.map(u => ({ value: u.id, label: `${u.lastName} ${u.firstName}` }))} values={new Set(dto.participants ?? [])} onChange={(set) => setDto(d => ({ ...d, participants: Array.from(set) }))} />
                    </div>
                </div>

                <div>
                    <label className="text-sm">Komentarz</label>
                    <textarea className="border rounded-xl w-full px-3 py-2" value={dto.comment ?? ""} onChange={e => setDto(d => ({ ...d, comment: e.target.value }))} />
                </div>

                <div className="flex justify-end gap-2">
                    <button className="border rounded-xl px-3 py-2" onClick={onClose}>Anuluj</button>
                    <button className="bg-black text-white rounded-xl px-3 py-2" onClick={save} disabled={saving}>{saving ? "Zapisywanie…" : "Utwórz"}</button>
                </div>
            </div>
        </div>
    );
}

function MultiSelect({ options, values, onChange }: { options: { value: string; label: string }[]; values: Set<string>; onChange: (v: Set<string>) => void }) {
    const [query, setQuery] = useState("");
    const filtered = options.filter(o => o.label.toLowerCase().includes(query.toLowerCase()));
    return (
        <div className="border rounded-2xl p-2">
            <input className="border rounded-xl px-3 py-2 w-full" placeholder="Szukaj…" value={query} onChange={(e) => setQuery(e.target.value)} />
            <div className="max-h-48 overflow-auto mt-2 grid grid-cols-2 gap-1">
                {filtered.map(o => (
                    <label key={o.value} className={`px-2 py-1 rounded-xl border flex items-center gap-2 ${values.has(o.value) ? 'bg-gray-100' : ''}`}>
                        <input type="checkbox" checked={values.has(o.value)} onChange={(e) => {
                            const next = new Set(values);
                            if (e.target.checked) next.add(o.value); else next.delete(o.value);
                            onChange(next);
                        }} />
                        <span className="truncate">{o.label}</span>
                    </label>
                ))}
            </div>
        </div>
    );
}

function ExportExcelButton({ rows, dicts }: { rows: ProjectDto[]; dicts: { markets: DictItemInt[]; statuses: DictItemInt[]; architectures: DictItemInt[]; vendors: DictItemGuid[]; clients: DictItemGuid[]; users: UserDto[] } }) {
    const ref = useRef<HTMLButtonElement>(null);
    const onExport = () => {
        // Build worksheet
        const header = [
            "AM", "Klient", "Rynek", "Projekt", "Status", "Wartość", "Marża", "Prawdopodobieństwo", "Marża ważona", "Termin", "FV", "Płatność", "Vendor", "Architektura", "Komentarz"
        ];
        const data = rows.map((p, i) => {
            const A = nameById(dicts.users.map(u => ({ id: u.id, name: `${u.lastName} ${u.firstName}` })), p.amId);
            const B = nameById(dicts.clients, p.clientId);
            const C = nameById(dicts.markets, p.marketId);
            const D = p.name;
            const E = nameById(dicts.statuses, p.statusId);
            const F = p.value;
            const G = p.margin;
            const H = p.probabilityPercent;
            // Formula for weighted margin: =G[row+2]*H[row+2]/100 (1-based + header)
            const row = i + 2;
            const I = { f: `G${row}*H${row}/100` } as any;
            const J = p.dueQuarter;
            const K = p.invoiceMonth ?? "";
            const L = p.paymentQuarter ?? "";
            const M = nameById(dicts.vendors, p.vendorId);
            const N = nameById(dicts.architectures, p.architectureId);
            const O = p.comment ?? "";
            return [A, B, C, D, E, F, G, H, I, J, K, L, M, N, O];
        });

        const ws = XLSX.utils.aoa_to_sheet([header, ...data]);

        // Totals at bottom
        const totalRowIdx = rows.length + 2;
        XLSX.utils.sheet_add_aoa(ws, [["Suma:", , , , , { f: `SUM(F2:F${totalRowIdx - 1})` }, { f: `SUM(G2:G${totalRowIdx - 1})` }, , { f: `SUM(I2:I${totalRowIdx - 1})` }]], { origin: `A${totalRowIdx}` });

        // Create workbook
        const wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, "Forecast");
        XLSX.writeFile(wb, `forecast_${new Date().toISOString().slice(0, 10)}.xlsx`);
    };

    return <button ref={ref} className="border rounded-xl px-3 py-2" onClick={onExport}>Eksport do Excel</button>;
}
