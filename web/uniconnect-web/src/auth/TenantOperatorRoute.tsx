import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function TenantOperatorRoute({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) return <p className="loading">Loading…</p>;
  if (!user?.fleetId) return <Navigate to="/" replace />;
  return children;
}
