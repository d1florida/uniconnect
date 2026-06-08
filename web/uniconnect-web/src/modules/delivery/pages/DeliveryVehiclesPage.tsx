import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { AssetCategory, DeliveryVehicleDto, DepotDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { ASSET_CATEGORIES, assetCategoryLabel } from '../../../utils/assetLabels';
import { vehicleStatusLabel } from '../deliveryLabels';

export function DeliveryVehiclesPage() {
  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();
  const { user } = useAuth();
  const fleetId = fleetIdParam ?? user?.fleetId;

  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [categoryFilter, setCategoryFilter] = useState<AssetCategory | ''>('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [savingId, setSavingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!fleetId) return;
    const [vehicleList, depotList] = await Promise.all([
      api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${fleetId}/vehicles`),
      api.get<DepotDto[]>(`/api/delivery/tenants/${fleetId}/depots`),
    ]);
    setVehicles(vehicleList);
    setDepots(depotList);
  }, [fleetId]);

  useEffect(() => {
    if (!fleetId) {
      setLoading(false);
      return;
    }
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load vehicles'))
      .finally(() => setLoading(false));
  }, [fleetId, load]);

  const filteredVehicles = useMemo(
    () => (categoryFilter ? vehicles.filter((v) => v.category === categoryFilter) : vehicles),
    [vehicles, categoryFilter],
  );

  const assignHomeDepot = async (vehicleId: string, homeDepotId: string) => {
    if (!fleetId) return;
    setSavingId(vehicleId);
    setError('');
    try {
      const updated = await api.patch<DeliveryVehicleDto>(
        `/api/delivery/tenants/${fleetId}/vehicles/${vehicleId}/home-depot`,
        { homeDepotId: homeDepotId || null },
      );
      setVehicles((list) => list.map((v) => (v.id === vehicleId ? updated : v)));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update home depot');
    } finally {
      setSavingId(null);
    }
  };

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Assets</h2>
        <p className="muted">
          Assign each asset to a home depot so route planning uses the right fleet at each location.
          {' · '}
          <Link to="/delivery">← Delivery</Link>
        </p>
      </div>
      <ErrorAlert message={error} />
      <div className="form-row" style={{ marginBottom: '1rem' }}>
        <label>
          Filter by type
          <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value as AssetCategory | '')}>
            <option value="">All types</option>
            {ASSET_CATEGORIES.map((c) => (
              <option key={c} value={c}>{assetCategoryLabel(c)}</option>
            ))}
          </select>
        </label>
      </div>
      {depots.length === 0 && (
        <p className="plan-hint">
          Add a depot first.{' '}
          {fleetId && <Link to={`/delivery/fleets/${fleetId}/depots`}>Manage depots</Link>}
        </p>
      )}
      <table>
        <thead>
          <tr>
            <th>Type</th>
            <th>Vehicle ID</th>
            <th>Plate</th>
            <th>Vehicle</th>
            <th>Status</th>
            <th>Home depot</th>
          </tr>
        </thead>
        <tbody>
          {filteredVehicles.map((v) => (
            <tr key={v.id}>
              <td>{assetCategoryLabel(v.category)}</td>
              <td>
                <Link to={`/fleet/vehicles/${v.id}`}><strong>{v.vehicleNumber}</strong></Link>
              </td>
              <td>{v.licensePlate}</td>
              <td>
                {v.make} {v.model}
                {v.isAutonomous && <span className="muted"> · Autonomous</span>}
              </td>
              <td>
                <span className={(v.status ?? 'Active') === 'Active' ? 'badge badge-conv' : 'badge badge-muted'}>
                  {vehicleStatusLabel(v.status ?? 'Active')}
                </span>
              </td>
              <td>
                <select
                  value={v.homeDepotId ?? ''}
                  disabled={depots.length === 0 || savingId === v.id}
                  onChange={(e) => void assignHomeDepot(v.id, e.target.value)}
                >
                  <option value="">— Unassigned —</option>
                  {depots.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
                {savingId === v.id && <span className="muted"> Saving…</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {filteredVehicles.length === 0 && <p className="muted">No vehicles match this filter.</p>}
    </div>
  );
}
