import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { token, loading } = useAuth();
  const location = useLocation();

  if (loading) return <p className="loading">Loading…</p>;
  if (!token) return <Navigate to="/login" state={{ from: location }} replace />;
  return children;
}
