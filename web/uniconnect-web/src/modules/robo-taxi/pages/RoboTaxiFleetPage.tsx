import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { AssetCategory, RoboTaxiTrackingDto, RoboTaxiVehicleDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { ASSET_CATEGORIES, assetCategoryLabel } from '../../../utils/assetLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';

const stateColor: Record<string, string> = {
  OnTrip: 'On trip',
  Grounded: 'Grounded',
  Charging: 'Charging',
  Idle: 'Idle',
  Maintenance: 'Maintenance',
};

export function RoboTaxiFleetPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [vehicles, setVehicles] = useState<RoboTaxiVehicleDto[]>([]);
  const [tracking, setTracking] = useState<RoboTaxiTrackingDto[]>([]);
  const [categoryFilter, setCategoryFilter] = useState<AssetCategory | ''>('');

  useEffect(() => {
    if (!fleetId) return;
    api.get<RoboTaxiVehicleDto[]>(`/api/robo-taxis/tenants/${fleetId}/vehicles`).then(setVehicles);
    api.get<RoboTaxiTrackingDto[]>(`/api/robo-taxis/tenants/${fleetId}/tracking`).then(setTracking);
  }, [fleetId]);

  const filteredVehicles = useMemo(
    () => (categoryFilter ? vehicles.filter((v) => v.category === categoryFilter) : vehicles),
    [vehicles, categoryFilter],
  );

  const markers = tracking
    .filter((t) => t.latestLocation)
    .map((t) => ({
      id: t.vehicleId,
      label: vehiclePrimaryLabel(t),
      lat: t.latestLocation!.latitude,
      lng: t.latestLocation!.longitude,
      detail: `${assetCategoryLabel(t.category)} · ${stateColor[t.operationalState] ?? t.operationalState}`,
    }));

  return (
    <div>
      <div className="page-header"><h2>AV Fleet</h2></div>
      <FleetMap markers={markers} />
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
      <table>
        <thead><tr><th>Type</th><th>ID</th><th>Plate</th><th>State</th><th>Battery</th><th></th></tr></thead>
        <tbody>
          {filteredVehicles.map((v) => (
            <tr key={v.id}>
              <td>{assetCategoryLabel(v.category)}</td>
              <td><strong>{v.vehicleNumber}</strong></td>
              <td>{v.licensePlate}</td>
              <td>
                <span className={v.profile.operationalState === 'Grounded' ? 'badge badge-grounded' : 'badge badge-av'}>
                  {v.profile.operationalState}
                </span>
              </td>
              <td>{v.profile.batteryPercent ?? '—'}%</td>
              <td><Link to={`/robo-taxis/vehicles/${v.id}`}>Detail</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
      {filteredVehicles.length === 0 && <p className="muted">No assets match this filter.</p>}
    </div>
  );
}
