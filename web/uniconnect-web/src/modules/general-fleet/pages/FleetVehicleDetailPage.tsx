import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { MaintenanceRecordDto, MaintenanceStatus, ServiceType, VehicleDto, VehicleLocationDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

export function FleetVehicleDetailPage() {
  const { vehicleId } = useParams<{ vehicleId: string }>();
  const navigate = useNavigate();
  const [vehicle, setVehicle] = useState<VehicleDto | null>(null);
  const [maintenance, setMaintenance] = useState<MaintenanceRecordDto[]>([]);
  const [locations, setLocations] = useState<VehicleLocationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [lat, setLat] = useState('37.775');
  const [lng, setLng] = useState('-122.418');
  const [maintForm, setMaintForm] = useState({
    serviceType: 'OilChange' as ServiceType,
    performedOn: new Date().toISOString().slice(0, 10),
    mileageAtService: 0,
    cost: 0,
    notes: '',
    status: 'Completed' as MaintenanceStatus,
  });

  const load = useCallback(async () => {
    if (!vehicleId) return;
    setLoading(true);
    setError('');
    try {
      const [v, m, locs] = await Promise.all([
        api.get<VehicleDto>(`/api/fleet/vehicles/${vehicleId}`),
        api.get<MaintenanceRecordDto[]>(`/api/fleet/vehicles/${vehicleId}/maintenance`),
        api.get<VehicleLocationDto[]>(`/api/fleet/vehicles/${vehicleId}/locations`),
      ]);
      setVehicle(v);
      setMaintenance(m);
      setLocations(locs);
      setMaintForm((f) => ({ ...f, mileageAtService: v.currentMileage }));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load');
    } finally {
      setLoading(false);
    }
  }, [vehicleId]);

  useEffect(() => { load(); }, [load]);

  const recordLocation = async () => {
    await api.post(`/api/fleet/vehicles/${vehicleId}/locations`, {
      latitude: parseFloat(lat),
      longitude: parseFloat(lng),
    });
    await load();
  };

  const addMaintenance = async () => {
    await api.post(`/api/fleet/vehicles/${vehicleId}/maintenance`, {
      ...maintForm,
      performedOn: maintForm.performedOn,
    });
    await load();
  };

  const remove = async () => {
    if (!vehicleId || !vehicle) return;
    if (!window.confirm(`Delete ${vehicle.licensePlate}? This cannot be undone.`)) return;
    try {
      setError('');
      await api.delete(`/api/fleet/vehicles/${vehicleId}`);
      navigate(`/fleet/fleets/${vehicle.tenantId}/vehicles`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete');
    }
  };

  if (loading) return <Loading />;
  const markers = vehicle?.latestLocation
    ? [{ id: vehicle.id, label: vehicle.licensePlate, lat: vehicle.latestLocation.latitude, lng: vehicle.latestLocation.longitude }]
    : [];

  return (
    <div>
      <div className="page-header">
        <h2>{vehicle?.licensePlate ?? 'Vehicle'}</h2>
        <p>
          {vehicle && `${vehicle.year} ${vehicle.make} ${vehicle.model}`}
          {vehicle && (
            <> · <Link to={`/fleet/fleets/${vehicle.tenantId}/vehicles`}>Back to fleet</Link></>
          )}
        </p>
        {vehicle && (
          <button type="button" className="secondary" onClick={remove}>Delete vehicle</button>
        )}
      </div>
      <ErrorAlert message={error} />
      <FleetMap markers={markers} />
      <div className="form-row" style={{ marginTop: '1rem' }}>
        <input value={lat} onChange={(e) => setLat(e.target.value)} placeholder="Lat" />
        <input value={lng} onChange={(e) => setLng(e.target.value)} placeholder="Lng" />
        <button type="button" onClick={recordLocation}>Record location</button>
      </div>

      <h3>Log maintenance</h3>
      <div className="form-row">
        <select value={maintForm.serviceType} onChange={(e) => setMaintForm({ ...maintForm, serviceType: e.target.value as ServiceType })}>
          {(['OilChange', 'TireRotation', 'BrakeService', 'Inspection', 'Repair', 'Other'] as ServiceType[]).map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <input type="date" value={maintForm.performedOn} onChange={(e) => setMaintForm({ ...maintForm, performedOn: e.target.value })} />
        <input type="number" placeholder="Mileage" value={maintForm.mileageAtService} onChange={(e) => setMaintForm({ ...maintForm, mileageAtService: +e.target.value })} />
        <input type="number" placeholder="Cost" value={maintForm.cost} onChange={(e) => setMaintForm({ ...maintForm, cost: +e.target.value })} />
        <select value={maintForm.status} onChange={(e) => setMaintForm({ ...maintForm, status: e.target.value as MaintenanceStatus })}>
          <option value="Scheduled">Scheduled</option>
          <option value="Completed">Completed</option>
          <option value="Cancelled">Cancelled</option>
        </select>
        <input placeholder="Notes" value={maintForm.notes} onChange={(e) => setMaintForm({ ...maintForm, notes: e.target.value })} />
        <button type="button" onClick={addMaintenance}>Add record</button>
      </div>

      <h3>Maintenance history</h3>
      <table>
        <thead><tr><th>Service</th><th>Date</th><th>Status</th><th>Cost</th><th>Notes</th></tr></thead>
        <tbody>
          {maintenance.map((m) => (
            <tr key={m.id}>
              <td>{m.serviceType}</td>
              <td>{m.performedOn}</td>
              <td>{m.status}</td>
              <td>${m.cost}</td>
              <td>{m.notes}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <h3>Location history</h3>
      <table>
        <thead><tr><th>Time</th><th>Lat</th><th>Lng</th><th>Speed</th></tr></thead>
        <tbody>
          {locations.map((l) => (
            <tr key={l.id}>
              <td>{new Date(l.recordedAt).toLocaleString()}</td>
              <td>{l.latitude}</td>
              <td>{l.longitude}</td>
              <td>{l.speedKph ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
