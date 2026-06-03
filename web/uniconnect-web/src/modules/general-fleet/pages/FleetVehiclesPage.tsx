import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { AssetCategory, VehicleDto } from '../../../api/types';
import { ASSET_CATEGORIES, assetCategoryLabel } from '../../../utils/assetLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';

export function FleetVehiclesPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [vehicles, setVehicles] = useState<VehicleDto[]>([]);
  const [categoryFilter, setCategoryFilter] = useState<AssetCategory | ''>('');
  const [error, setError] = useState('');
  const [form, setForm] = useState({
    vin: '',
    make: '',
    model: '',
    year: 2024,
    category: 'LightVehicle' as AssetCategory,
    vehicleNumber: '',
    licensePlate: '',
    currentMileage: 0,
  });

  const load = () => api.get<VehicleDto[]>(`/api/fleet/tenants/${fleetId}/vehicles`).then(setVehicles);

  useEffect(() => {
    if (fleetId) load().catch((e) => setError(e.message));
  }, [fleetId]);

  const filteredVehicles = useMemo(
    () => (categoryFilter ? vehicles.filter((v) => v.category === categoryFilter) : vehicles),
    [vehicles, categoryFilter],
  );

  const create = async () => {
    if (!form.vin.trim() || !form.make.trim() || !form.model.trim() || !form.vehicleNumber.trim() || !form.licensePlate.trim()) {
      setError('VIN, make, model, vehicle ID, and license plate are required.');
      return;
    }
    try {
      setError('');
      await api.post(`/api/fleet/tenants/${fleetId}/vehicles`, {
        ...form,
        vin: form.vin.trim(),
        make: form.make.trim(),
        model: form.model.trim(),
        vehicleNumber: form.vehicleNumber.trim(),
        licensePlate: form.licensePlate.trim(),
      });
      setForm({
        vin: '',
        make: '',
        model: '',
        year: 2024,
        category: 'LightVehicle',
        vehicleNumber: '',
        licensePlate: '',
        currentMileage: 0,
      });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  };

  const remove = async (vehicle: VehicleDto) => {
    if (!window.confirm(`Delete vehicle ${vehiclePrimaryLabel(vehicle)}? This cannot be undone.`)) return;
    try {
      setError('');
      await api.delete(`/api/fleet/vehicles/${vehicle.id}`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete');
    }
  };

  return (
    <div>
      <div className="page-header">
        <h2>Assets</h2>
        <p className="muted">Fleet vehicles and equipment registered for this tenant.</p>
      </div>
      {error && <p className="error">{error}</p>}
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
      <div className="form-row">
        <select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value as AssetCategory })}>
          {ASSET_CATEGORIES.map((c) => (
            <option key={c} value={c}>{assetCategoryLabel(c)}</option>
          ))}
        </select>
        <input placeholder="VIN" value={form.vin} onChange={(e) => setForm({ ...form, vin: e.target.value })} />
        <input placeholder="Make" value={form.make} onChange={(e) => setForm({ ...form, make: e.target.value })} />
        <input placeholder="Model" value={form.model} onChange={(e) => setForm({ ...form, model: e.target.value })} />
        <input placeholder="Vehicle ID" value={form.vehicleNumber} onChange={(e) => setForm({ ...form, vehicleNumber: e.target.value })} />
        <input placeholder="Plate" value={form.licensePlate} onChange={(e) => setForm({ ...form, licensePlate: e.target.value })} />
        <button type="button" onClick={() => void create()}>Add asset</button>
      </div>
      <table>
        <thead><tr><th>Type</th><th>ID</th><th>Plate</th><th>Asset</th><th>Mileage</th><th></th></tr></thead>
        <tbody>
          {filteredVehicles.map((v) => (
            <tr key={v.id}>
              <td>{assetCategoryLabel(v.category)}</td>
              <td><strong>{v.vehicleNumber}</strong></td>
              <td>{v.licensePlate}</td>
              <td>{v.year} {v.make} {v.model}</td>
              <td>{v.currentMileage}</td>
              <td>
                <Link to={`/fleet/vehicles/${v.id}`}>Detail</Link>
                {' · '}
                <button type="button" className="secondary" onClick={() => void remove(v)}>Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {filteredVehicles.length === 0 && <p className="muted">No assets match this filter.</p>}
    </div>
  );
}
