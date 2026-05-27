import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type {
  AutomationMode,
  DeliveryRouteDetailDto,
  DeliveryRouteStatus,
  DeliveryRouteStopDto,
  DeliveryStopStatus,
  DeliveryVehicleDto,
} from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

const BASE_LAT = 37.7749;
const BASE_LNG = -122.4194;

export function DeliveryRouteDetailPage() {
  const { routeId } = useParams<{ routeId: string }>();
  const [route, setRoute] = useState<DeliveryRouteDetailDto | null>(null);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [vehicleId, setVehicleId] = useState('');
  const [mode, setMode] = useState<AutomationMode>('Conventional');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [newStop, setNewStop] = useState({ address: '', recipientName: '', parcelDescription: '' });

  const load = async () => {
    if (!routeId) return;
    const r = await api.get<DeliveryRouteDetailDto>(`/api/delivery/routes/${routeId}`);
    setRoute(r);
    if (r.tenantId) {
      const v = await api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${r.tenantId}/vehicles`);
      setVehicles(v);
    }
  };

  useEffect(() => {
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [routeId]);

  const setStatus = async (status: DeliveryRouteStatus) => {
    await api.patch(`/api/delivery/routes/${routeId}/status`, { status });
    await load();
  };

  const assign = async () => {
    await api.post(`/api/delivery/routes/${routeId}/assign`, { vehicleId, automationMode: mode });
    await load();
  };

  const completeStop = async (stopId: string, status: DeliveryStopStatus) => {
    await api.patch(`/api/delivery/routes/${routeId}/stops/${stopId}/status`, { status });
    await load();
  };

  const addStop = async () => {
    if (!newStop.address.trim()) return;
    await api.post(`/api/delivery/routes/${routeId}/stops`, {
      stopType: 'Dropoff',
      address: newStop.address.trim(),
      recipientName: newStop.recipientName.trim() || null,
      recipientPhone: null,
      parcelDescription: newStop.parcelDescription.trim() || null,
      notes: null,
    });
    setNewStop({ address: '', recipientName: '', parcelDescription: '' });
    await load();
  };

  const moveStop = async (stop: DeliveryRouteStopDto, direction: -1 | 1) => {
    if (!route) return;
    const delivery = route.stops.filter((s) => s.stopType !== 'Depot');
    const idx = delivery.findIndex((s) => s.id === stop.id);
    const target = idx + direction;
    if (target < 0 || target >= delivery.length) return;
    const reordered = [...delivery];
    [reordered[idx], reordered[target]] = [reordered[target], reordered[idx]];
    await api.put(`/api/delivery/routes/${routeId}/stops/reorder`, {
      stopIds: reordered.map((s) => s.id),
    });
    await load();
  };

  const deleteStop = async (stopId: string) => {
    await api.delete(`/api/delivery/routes/${routeId}/stops/${stopId}`);
    await load();
  };

  if (loading) return <Loading />;
  if (!route) return <ErrorAlert message="Route not found" />;

  const editable = route.status === 'Draft' || route.status === 'Planned';
  const deliveryStops = route.stops.filter((s) => s.stopType !== 'Depot');
  const nextStop = deliveryStops.find((s) => s.status === 'Pending');
  const conventional = vehicles.filter((v) => !v.isAutonomous);
  const autonomous = vehicles.filter((v) => v.isAutonomous);

  const mapMarkers = route.stops
    .filter((s) => s.stopType !== 'Depot')
    .map((s, i) => ({
      id: s.id,
      label: `#${s.sequence} ${s.recipientName ?? s.address.slice(0, 24)}`,
      lat: BASE_LAT + i * 0.008,
      lng: BASE_LNG + i * 0.006,
      detail: `${s.stopType} · ${s.status}`,
    }));

  if (route.stops.some((s) => s.stopType === 'Depot')) {
    mapMarkers.unshift({
      id: 'depot',
      label: 'Depot',
      lat: BASE_LAT,
      lng: BASE_LNG,
      detail: route.depotAddress,
    });
  }

  return (
    <div>
      <div className="page-header">
        <h2>{route.name}</h2>
        <p>
          {route.depotAddress} · {route.scheduledDate}
          {' · '}
          <span className="badge badge-conv">{route.status}</span>
          {route.licensePlate && <> · {route.licensePlate}</>}
          {' · '}
          <Link to={`/delivery/fleets/${route.tenantId}/routes`}>Back to routes</Link>
        </p>
      </div>
      <ErrorAlert message={error} />

      <div className="form-row">
        {route.status === 'Draft' && (
          <button type="button" onClick={() => setStatus('Planned')}>Mark planned</button>
        )}
        {route.status === 'Planned' && (
          <button type="button" onClick={() => setStatus('InProgress')}>Start route</button>
        )}
        {route.status === 'InProgress' && deliveryStops.every((s) => s.status !== 'Pending') && (
          <button type="button" onClick={() => setStatus('Completed')}>Complete route</button>
        )}
        {route.status !== 'Completed' && route.status !== 'Cancelled' && (
          <button type="button" className="secondary" onClick={() => setStatus('Cancelled')}>
            Cancel route
          </button>
        )}
      </div>

      {editable && (
        <>
          <h3>Assign vehicle</h3>
          <div className="form-row">
            <select value={mode} onChange={(e) => setMode(e.target.value as AutomationMode)}>
              <option value="Conventional">Conventional</option>
              <option value="Autonomous">Autonomous</option>
            </select>
            <select value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
              <option value="">Select vehicle</option>
              {(mode === 'Conventional' ? conventional : autonomous).map((v) => (
                <option key={v.id} value={v.id}>
                  {v.licensePlate} — {v.make} {v.model}
                </option>
              ))}
            </select>
            <button type="button" onClick={assign} disabled={!vehicleId}>
              Assign
            </button>
          </div>
        </>
      )}

      {route.status === 'InProgress' && nextStop && (
        <div className="next-stop-banner">
          <strong>Next stop:</strong> #{nextStop.sequence} {nextStop.address}
          <div className="form-row">
            <button type="button" onClick={() => completeStop(nextStop.id, 'Completed')}>
              Complete stop
            </button>
            <button type="button" className="secondary" onClick={() => completeStop(nextStop.id, 'Skipped')}>
              Skip
            </button>
          </div>
        </div>
      )}

      <FleetMap markers={mapMarkers} center={[BASE_LAT, BASE_LNG]} zoom={12} />

      <h3>Stops ({route.stops.length})</h3>
      <table className="stops-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Type</th>
            <th>Address</th>
            <th>Recipient</th>
            <th>Parcel</th>
            <th>Status</th>
            {editable && <th>Actions</th>}
          </tr>
        </thead>
        <tbody>
          {route.stops.map((s) => (
            <tr key={s.id} className={s.id === nextStop?.id ? 'stop-current' : ''}>
              <td>{s.sequence}</td>
              <td>{s.stopType}</td>
              <td>{s.address}</td>
              <td>{s.recipientName ?? '—'}</td>
              <td>{s.parcelDescription ?? '—'}</td>
              <td>
                <span className={`badge ${s.status === 'Completed' ? 'badge-av' : s.status === 'Skipped' ? 'badge-grounded' : 'badge-conv'}`}>
                  {s.status}
                </span>
              </td>
              {editable && s.stopType !== 'Depot' && (
                <td className="stop-actions">
                  <button type="button" className="secondary" onClick={() => moveStop(s, -1)}>
                    ↑
                  </button>
                  <button type="button" className="secondary" onClick={() => moveStop(s, 1)}>
                    ↓
                  </button>
                  <button type="button" className="secondary" onClick={() => deleteStop(s.id)}>
                    Delete
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>

      {editable && (
        <div className="route-form-panel">
          <h3>Add stop</h3>
          <div className="form-row">
            <input placeholder="Address" value={newStop.address} onChange={(e) => setNewStop({ ...newStop, address: e.target.value })} />
            <input placeholder="Recipient" value={newStop.recipientName} onChange={(e) => setNewStop({ ...newStop, recipientName: e.target.value })} />
            <input placeholder="Parcel" value={newStop.parcelDescription} onChange={(e) => setNewStop({ ...newStop, parcelDescription: e.target.value })} />
            <button type="button" onClick={addStop}>
              Add
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
