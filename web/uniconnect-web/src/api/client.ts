const base = import.meta.env.VITE_API_BASE_URL ?? '';

let authToken: string | null = null;

export function setAuthToken(token: string | null) {
  authToken = token;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (authToken) headers.Authorization = `Bearer ${authToken}`;

  let res: Response;
  try {
    res = await fetch(`${base}${path}`, {
      headers: { ...headers, ...(init?.headers as Record<string, string>) },
      ...init,
    });
  } catch {
    throw new Error(
      base
        ? `Cannot reach the API at ${base}. Start it with: .\\dev.ps1 or dotnet run --project src/UniConnect.Api`
        : 'Cannot reach the API. Run .\\dev.ps1 from the project root (Postgres + API + web).',
    );
  }
  if (res.status === 401) {
    localStorage.removeItem('uniconnect_token');
    setAuthToken(null);
    window.location.href = '/login';
    throw new Error('Session expired. Please sign in again.');
  }
  if (res.status === 403) {
    const err = await res.json().catch(() => ({ detail: 'Access denied' }));
    throw new Error(err.detail ?? 'Access denied');
  }
  if (res.status === 502) {
    throw new Error(
      'API is not running (502 Bad Gateway). From the project root run: .\\dev.ps1 — or: dotnet run --project src/UniConnect.Api',
    );
  }
  if (res.status === 503) {
    const err = await res.json().catch(() => ({ detail: 'Service unavailable' }));
    throw new Error(
      err.detail?.includes('Database')
        ? err.detail
        : 'Database is not available. Start Docker Desktop, then run: docker compose up -d',
    );
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({} as Record<string, string>));
    const message =
      err.detail ??
      err.title ??
      (typeof err.message === 'string' ? err.message : undefined) ??
      res.statusText ??
      'Request failed';
    throw new Error(message);
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: (path: string) => request<void>(path, { method: 'DELETE' }),
};
