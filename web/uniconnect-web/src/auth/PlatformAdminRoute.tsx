import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function PlatformAdminRoute({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) return <p className="loading">Loading…</p>;
  if (!user?.isPlatformAdmin) return <Navigate to="/" replace />;
  return children;
}
