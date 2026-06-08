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
  DriverDto,
  OptimizeSequenceResultDto,
} from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { useAuth } from '../../../auth/AuthContext';
import { hasModule } from '../../../utils/fleetModules';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { routeStatusLabel, formatDriveMinutes, formatNextStopEta, isOperationalRouteStop } from '../deliveryLabels';
import { vehicleSelectLabel } from '../../../utils/vehicleLabels';
import { buildRouteStopMarkers } from '../deliveryMapMarkers';

export function DeliveryRouteDetailPage() {
  const { routeId } = useParams<{ routeId: string }>();
  const { user } = useAuth();
  const [route, setRoute] = useState<DeliveryRouteDetailDto | null>(null);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [drivers, setDrivers] = useState<DriverDto[]>([]);
  const [vehicleId, setVehicleId] = useState('');
  const [driverId, setDriverId] = useState('');
  const [mode, setMode] = useState<AutomationMode>('Conventional');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [optimizeMessage, setOptimizeMessage] = useState('');
  const [optimizing, setOptimizing] = useState(false);
  const [newStop, setNewStop] = useState({ address: '', recipientName: '', parcelDescription: '' });

  const load = async () => {
    if (!routeId) return;
    const r = await api.get<DeliveryRouteDetailDto>(`/api/delivery/routes/${routeId}`);
    setRoute(r);
    if (r.tenantId) {
      const [v, d] = await Promise.all([
        api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${r.tenantId}/vehicles`),
        api.get<DriverDto[]>(`/api/delivery/tenants/${r.tenantId}/drivers`),
      ]);
      setVehicles(v);
      setDrivers(d.filter((x) => x.isActive));
    }
    if (r.vehicleId) setVehicleId(r.vehicleId);
    if (r.driverId) setDriverId(r.driverId);
    if (r.automationMode) setMode(r.automationMode);
  };

  useEffect(() => {
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [routeId]);

  const setStatus = async (status: DeliveryRouteStatus) => {
    setError('');
    try {
      await api.patch(`/api/delivery/routes/${routeId}/status`, { status });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update route status');
    }
  };

  const assign = async () => {
    setError('');
    try {
      await api.post(`/api/delivery/routes/${routeId}/assign`, {
        vehicleId,
        automationMode: mode,
        driverId: mode === 'Conventional' && driverId ? driverId : null,
      });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to assign');
    }
  };

  const completeStop = async (stopId: string, status: DeliveryStopStatus) => {
    await api.patch(`/api/delivery/routes/${routeId}/stops/${stopId}/status`, { status });
    await load();
  };

  const addStop = async () => {
    if (!newStop.address.trim()) return;
    setError('');
    try {
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
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add stop');
    }
  };

  const moveStop = async (stop: DeliveryRouteStopDto, direction: -1 | 1) => {
    if (!route || !routeId) return;
    const delivery = route.stops
      .filter((s) => isOperationalRouteStop(s, route.depotAddress))
      .sort((a, b) => a.sequence - b.sequence);
    const idx = delivery.findIndex((s) => s.id === stop.id);
    const target = idx + direction;
    if (idx < 0 || target < 0 || target >= delivery.length) return;
    const reordered = [...delivery];
    [reordered[idx], reordered[target]] = [reordered[target], reordered[idx]];
    setError('');
    try {
      await api.put(`/api/delivery/routes/${routeId}/stops/reorder`, {
        stopIds: reordered.map((s) => s.id),
      });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to reorder stops');
    }
  };

  const deleteStop = async (stopId: string) => {
    setError('');
    try {
      await api.delete(`/api/delivery/routes/${routeId}/stops/${stopId}`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete stop');
    }
  };

  const optimizeSequence = async () => {
    if (!route || !routeId) return;
    const delivery = route.stops
      .filter((s) => isOperationalRouteStop(s, route.depotAddress))
      .sort((a, b) => a.sequence - b.sequence);
    if (delivery.length < 2) return;

    setOptimizing(true);
    setError('');
    setOptimizeMessage('');
    try {
      const result = await api.post<OptimizeSequenceResultDto>(
        `/api/route-planning/routes/${routeId}/optimize-sequence`,
        { stopIds: delivery.map((s) => s.id) },
      );
      setOptimizeMessage(
        result.estimatedMinutesBefore === result.estimatedMinutesAfter
          ? `Stop order updated — est. drive time ${formatDriveMinutes(result.estimatedMinutesAfter) ?? 'unavailable'} (already optimal)`
          : `Stop order optimized — est. drive time ${formatDriveMinutes(result.estimatedMinutesBefore) ?? '?'} → ${formatDriveMinutes(result.estimatedMinutesAfter) ?? '?'}`,
      );
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to optimize stop order');
    } finally {
      setOptimizing(false);
    }
  };

  if (loading) return <Loading />;
  if (!route) return <ErrorAlert message={error || 'Route not found'} />;

  const editable = route.status === 'Draft' || route.status === 'Planned';
  const deliveryStops = route.stops.filter((s) => isOperationalRouteStop(s, route.depotAddress));
  const canOptimize = hasModule(user?.modules, 'RoutePlanning') && editable && deliveryStops.length >= 2;
  const nextStop = deliveryStops.find((s) => s.status === 'Pending');
  const conventional = vehicles.filter((v) => !v.isAutonomous);
  const autonomous = vehicles.filter((v) => v.isAutonomous);
  const activeDrivers = drivers.filter((d) => d.isActive);
  const canStart =
    !!route.vehicleId &&
    (route.automationMode === 'Autonomous' || !!route.driverId);

  const mapMarkers = buildRouteStopMarkers(route.stops);

  const driveTimeLabel = formatDriveMinutes(route.estimatedDriveMinutes);
  const nextStopEtaLabel = formatNextStopEta(
    route.estimatedMinutesToNextStop,
    route.estimatedNextStopArrivalAt,
  );

  return (
    <div>
      <div className="page-header">
        <h2>{route.name}</h2>
        <p>
          {route.depotAddress} · {route.scheduledDate}
          {driveTimeLabel && <> · Est. drive {driveTimeLabel}</>}
          {' · '}
          <span className="badge badge-conv">{routeStatusLabel(route.status)}</span>
          {route.routePlanRunId && (
            <>
              {' · '}
              <span className="badge badge-av" title={`Plan run ${route.routePlanRunId}`}>
                From route plan · {route.scheduledDate}
              </span>
            </>
          )}
          {route.vehicleNumber && <> · Vehicle {route.vehicleNumber}</>}
          {route.licensePlate && route.licensePlate !== route.vehicleNumber && <> · {route.licensePlate}</>}
          {route.driverName && <> · {route.driverName}</>}
          {' · '}
          <Link to={`/delivery/fleets/${route.tenantId}/routes`}>Back to routes</Link>
        </p>
      </div>
      <ErrorAlert message={error} />
      {optimizeMessage && <p className="plan-hint">{optimizeMessage}</p>}
      {!driveTimeLabel && deliveryStops.length > 0 && (
        <p className="muted">
          Drive time unavailable — ensure the depot and stops have geocoded addresses.
        </p>
      )}

      <div className="form-row">
        {canOptimize && (
          <button type="button" className="secondary" onClick={() => void optimizeSequence()} disabled={optimizing}>
            {optimizing ? 'Optimizing…' : 'Optimize stop order'}
          </button>
        )}
        {route.status === 'Draft' && (
          <button type="button" onClick={() => setStatus('Planned')}>Mark planned</button>
        )}
        {route.status === 'Planned' && (
          <button type="button" onClick={() => setStatus('InProgress')} disabled={!canStart}>
            Start route
          </button>
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
      {route.status === 'Planned' && !canStart && (
        <p className="muted">
          Assign a vehicle{route.automationMode !== 'Autonomous' && ' and driver'} before starting this route.
        </p>
      )}

      {editable && (
        <>
          <h3>Assign vehicle & driver</h3>
          <div className="form-row">
            <select value={mode} onChange={(e) => setMode(e.target.value as AutomationMode)}>
              <option value="Conventional">Conventional</option>
              <option value="Autonomous">Autonomous</option>
            </select>
            <select value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
              <option value="">Select vehicle</option>
              {(mode === 'Conventional' ? conventional : autonomous).map((v) => (
                <option key={v.id} value={v.id}>
                  {vehicleSelectLabel(v)}
                </option>
              ))}
            </select>
            {mode === 'Conventional' && (
              <select value={driverId} onChange={(e) => setDriverId(e.target.value)}>
                <option value="">No driver</option>
                {activeDrivers.map((d) => (
                  <option key={d.id} value={d.id}>{d.displayName}</option>
                ))}
              </select>
            )}
            <button type="button" onClick={() => void assign()} disabled={!vehicleId}>
              Assign
            </button>
          </div>
          {mode === 'Conventional' && activeDrivers.length === 0 && (
            <p className="muted">
              No active drivers. <Link to={`/delivery/fleets/${route.tenantId}/drivers`}>Add drivers</Link>
            </p>
          )}
        </>
      )}

      {route.status === 'InProgress' && nextStop && (
        <div className="next-stop-banner">
          <strong>Next stop:</strong> #{nextStop.sequence} {nextStop.address}
          {nextStopEtaLabel && (
            <span className="muted"> · {nextStopEtaLabel}</span>
          )}
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

      {mapMarkers.length > 0 ? (
        <FleetMap markers={mapMarkers} />
      ) : (
        <p className="muted">Map unavailable — stop coordinates could not be resolved.</p>
      )}

      <h3>Stops ({deliveryStops.length})</h3>
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
          {[...route.stops]
            .filter((s) => isOperationalRouteStop(s, route.depotAddress))
            .sort((a, b) => a.sequence - b.sequence)
            .map((s) => (
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
