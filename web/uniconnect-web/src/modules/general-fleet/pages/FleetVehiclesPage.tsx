import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { VehicleDto } from '../../../api/types';

export function FleetVehiclesPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [vehicles, setVehicles] = useState<VehicleDto[]>([]);
  const [error, setError] = useState('');
  const [form, setForm] = useState({ vin: '', make: '', model: '', year: 2024, licensePlate: '', currentMileage: 0 });

  const load = () => api.get<VehicleDto[]>(`/api/fleet/tenants/${fleetId}/vehicles`).then(setVehicles);

  useEffect(() => {
    if (fleetId) load().catch((e) => setError(e.message));
  }, [fleetId]);

  const create = async () => {
    if (!form.vin.trim() || !form.make.trim() || !form.model.trim() || !form.licensePlate.trim()) {
      setError('VIN, make, model, and license plate are required.');
      return;
    }
    try {
      setError('');
      await api.post(`/api/fleet/tenants/${fleetId}/vehicles`, {
        ...form,
        vin: form.vin.trim(),
        make: form.make.trim(),
        model: form.model.trim(),
        licensePlate: form.licensePlate.trim(),
      });
      setForm({ vin: '', make: '', model: '', year: 2024, licensePlate: '', currentMileage: 0 });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  };

  const remove = async (vehicle: VehicleDto) => {
    if (!window.confirm(`Delete ${vehicle.licensePlate}? This cannot be undone.`)) return;
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
      <div className="page-header"><h2>Vehicles</h2></div>
      {error && <p className="error">{error}</p>}
      <div className="form-row">
        <input placeholder="VIN" value={form.vin} onChange={(e) => setForm({ ...form, vin: e.target.value })} />
        <input placeholder="Make" value={form.make} onChange={(e) => setForm({ ...form, make: e.target.value })} />
        <input placeholder="Model" value={form.model} onChange={(e) => setForm({ ...form, model: e.target.value })} />
        <input placeholder="Plate" value={form.licensePlate} onChange={(e) => setForm({ ...form, licensePlate: e.target.value })} />
        <button onClick={create}>Add vehicle</button>
      </div>
      <table>
        <thead><tr><th>Plate</th><th>Vehicle</th><th>Mileage</th><th></th></tr></thead>
        <tbody>
          {vehicles.map((v) => (
            <tr key={v.id}>
              <td>{v.licensePlate}</td>
              <td>{v.year} {v.make} {v.model}</td>
              <td>{v.currentMileage}</td>
              <td>
                <Link to={`/fleet/vehicles/${v.id}`}>Detail</Link>
                {' · '}
                <button type="button" className="secondary" onClick={() => remove(v)}>Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
