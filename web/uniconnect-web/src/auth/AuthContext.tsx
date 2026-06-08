import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, setAuthToken } from '../api/client';
import { normalizeUserProfile } from '../api/normalize';
import type { FleetModule, TenantRole } from '../api/types';
import { hasModule } from '../utils/fleetModules';

export interface UserProfile {
  userId: string;
  email: string;
  displayName: string;
  fleetId?: string;
  fleetName?: string;
  modules: FleetModule[];
  tenantRole?: TenantRole;
  isTenantAdmin: boolean;
  isDriver: boolean;
  driverId?: string;
  isPlatformAdmin: boolean;
}

interface AuthState {
  user: UserProfile | null;
  token: string | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<UserProfile>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);
const TOKEN_KEY = 'uniconnect_token';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY));
  const [loading, setLoading] = useState(!!localStorage.getItem(TOKEN_KEY));

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    setAuthToken(null);
    setToken(null);
    setUser(null);
  }, []);

  useEffect(() => {
    if (!token) {
      setLoading(false);
      return;
    }
    setAuthToken(token);
    api.get<UserProfile>('/api/auth/me')
      .then((profile) => setUser(normalizeUserProfile(profile)))
      .catch(() => logout())
      .finally(() => setLoading(false));
  }, [token, logout]);

  const login = useCallback(async (email: string, password: string) => {
    const res = await api.post<{ token: string; user: UserProfile }>('/api/auth/login', { email, password });
    const profile = normalizeUserProfile(res.user);
    localStorage.setItem(TOKEN_KEY, res.token);
    setAuthToken(res.token);
    setToken(res.token);
    setUser(profile);
    return profile;
  }, []);

  const refreshUser = useCallback(async () => {
    const profile = normalizeUserProfile(await api.get<UserProfile>('/api/auth/me'));
    setUser(profile);
  }, []);

  const value = useMemo(
    () => ({ user, token, loading, login, logout, refreshUser }),
    [user, token, loading, login, logout, refreshUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export function homePathForUser(user: UserProfile): string {
  if (user.isPlatformAdmin) return '/tenants';
  if (hasModule(user.modules, 'General')) return '/';
  if (hasModule(user.modules, 'RoboTaxi')) return '/robo-taxis';
  if (hasModule(user.modules, 'Delivery')) {
    if (user.isDriver && user.fleetId) {
      return `/delivery/fleets/${user.fleetId}/driver-calendar`;
    }
    return '/delivery';
  }
  return '/';
}
