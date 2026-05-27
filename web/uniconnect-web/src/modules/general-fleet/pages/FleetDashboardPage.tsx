import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../../auth/AuthContext';
import { api } from '../../../api/client';
import type { FleetVehicleTrackingDto } from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

export function FleetDashboardPage() {
  const { user } = useAuth();
  const [tracking, setTracking] = useState<FleetVehicleTrackingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fleetId = user?.fleetId;

  useEffect(() => {
    if (!fleetId && !user?.isPlatformAdmin) {
      setLoading(false);
      return;
    }
    const id = fleetId ?? '11111111-1111-1111-1111-111111111101';
    api.get<FleetVehicleTrackingDto[]>(`/api/fleet/tenants/${id}/tracking`)
      .then(setTracking)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [fleetId, user?.isPlatformAdmin]);

  const stale = tracking.filter((t) => {
    if (!t.latestLocation) return true;
    return Date.now() - new Date(t.latestLocation.recordedAt).getTime() > 24 * 60 * 60 * 1000;
  }).length;

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Fleet dashboard</h2>
        <p>{user?.fleetName ?? 'Overview'}</p>
      </div>
      <ErrorAlert message={error} />
      <div className="cards">
        <div className="card"><div className="value">{tracking.length}</div><div className="label">Vehicles</div></div>
        <div className="card"><div className="value">{stale}</div><div className="label">Stale GPS (24h)</div></div>
      </div>
      {fleetId && (
        <p>
          <Link to={`/fleet/fleets/${fleetId}/vehicles`}>Manage vehicles</Link>
          {' · '}
          <Link to={`/fleet/fleets/${fleetId}/tracking`}>Fleet map</Link>
        </p>
      )}
      {user?.isPlatformAdmin && !fleetId && (
        <p className="muted">Signed in as platform admin. Use the sidebar to open a module, or sign in as a fleet operator.</p>
      )}
    </div>
  );
}
