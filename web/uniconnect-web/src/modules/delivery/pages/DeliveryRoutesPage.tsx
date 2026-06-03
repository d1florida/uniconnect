import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryRouteDetailDto, DeliveryRouteDto, DeliveryStopType, DepotDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { routeStatusLabel } from '../deliveryLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';

function toRouteSummary(detail: DeliveryRouteDetailDto): DeliveryRouteDto {
  const deliveryStops = detail.stops.filter((s) => s.stopType !== 'Depot');
  return {
    id: detail.id,
    tenantId: detail.tenantId,
    name: detail.name,
    status: detail.status,
    depotAddress: detail.depotAddress,
    scheduledDate: detail.scheduledDate,
    vehicleId: detail.vehicleId,
    vehicleNumber: detail.vehicleNumber,
    licensePlate: detail.licensePlate,
    driverId: detail.driverId,
    driverName: detail.driverName,
    automationMode: detail.automationMode,
    stopCount: deliveryStops.length,
    completedStops: deliveryStops.filter((s) => s.status === 'Completed').length,
    pendingStops: deliveryStops.filter((s) => s.status === 'Pending').length,
    createdAt: detail.createdAt,
    startedAt: detail.startedAt,
    completedAt: detail.completedAt,
    routePlanRunId: detail.routePlanRunId,
  };
}

interface StopDraft {
  stopType: DeliveryStopType;
  address: string;
  recipientName: string;
  recipientPhone: string;
  parcelDescription: string;
}

const emptyStop = (): StopDraft => ({
  stopType: 'Dropoff',
  address: '',
  recipientName: '',
  recipientPhone: '',
  parcelDescription: '',
});

export function DeliveryRoutesPage() {
  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();
  const { user } = useAuth();
  const fleetId = fleetIdParam ?? user?.fleetId;
  const [routes, setRoutes] = useState<DeliveryRouteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [savedMessage, setSavedMessage] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [depotId, setDepotId] = useState('');
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [scheduledDate, setScheduledDate] = useState(new Date().toISOString().slice(0, 10));
  const [stops, setStops] = useState<StopDraft[]>([emptyStop(), emptyStop(), emptyStop()]);

  const load = () => {
    if (!fleetId) return Promise.resolve();
    return Promise.all([
      api.get<DeliveryRouteDto[]>(`/api/delivery/tenants/${fleetId}/routes`),
      api.get<DepotDto[]>(`/api/delivery/tenants/${fleetId}/depots`),
    ]).then(([routeList, depotList]) => {
      setRoutes(routeList);
      setDepots(depotList);
      if (depotList.length > 0) {
        const defaultDepot = depotList.find((d) => d.isDefault) ?? depotList[0];
        setDepotId((current) => current || defaultDepot.id);
      }
    });
  };

  useEffect(() => {
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [fleetId]);

  const addStopRow = () => setStops([...stops, emptyStop()]);
  const removeStopRow = (i: number) => setStops(stops.filter((_, idx) => idx !== i));
  const updateStop = (i: number, patch: Partial<StopDraft>) => {
    setStops(stops.map((s, idx) => (idx === i ? { ...s, ...patch } : s)));
  };

  const createRoute = async () => {
    if (!fleetId) {
      setError('Tenant context is missing. Open Routes from the Delivery section in the sidebar.');
      return;
    }
    const deliveryStops = stops
      .filter((s) => s.address.trim())
      .map((s) => ({
        stopType: s.stopType,
        address: s.address.trim(),
        recipientName: s.recipientName.trim() || null,
        recipientPhone: s.recipientPhone.trim() || null,
        parcelDescription: s.parcelDescription.trim() || null,
        notes: null,
      }));
    if (!name.trim() || deliveryStops.length === 0 || !depotId) {
      setError('Route name, depot, and at least one stop address are required.');
      return;
    }
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      const created = await api.post<DeliveryRouteDetailDto>(`/api/delivery/tenants/${fleetId}/routes`, {
        name: name.trim(),
        depotId: depotId || null,
        scheduledDate,
        stops: deliveryStops,
      });
      const summary = toRouteSummary(created);
      setRoutes((prev) => [summary, ...prev.filter((r) => r.id !== summary.id)]);
      setShowForm(false);
      setName('');
      setStops([emptyStop(), emptyStop(), emptyStop()]);
      setSavedMessage(`Route "${summary.name}" created.`);
      try {
        await load();
      } catch {
        // List refresh failed but create succeeded — summary already shown.
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create route');
    } finally {
      setSaving(false);
    }
  };

  if (!fleetId && !loading) {
    return (
      <div>
        <div className="page-header"><h2>Delivery routes</h2></div>
        <p className="error">Tenant context is missing. Use the Routes link under Delivery in the sidebar.</p>
      </div>
    );
  }

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Delivery routes</h2>
        <p>
          Multi-stop runs for a single driver and vehicle
          {fleetId && (
            <>
              {' '}
              · <Link to={`/delivery/fleets/${fleetId}/orders`}>Orders</Link>
              {' · '}
              <Link to={`/delivery/fleets/${fleetId}/map`}>Map</Link>
            </>
          )}
        </p>
      </div>
      <ErrorAlert message={error} />
      {savedMessage && <p className="muted">{savedMessage}</p>}

      <div className="form-row">
        <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>
          + New route
        </button>
      </div>

      {showForm && (
        <div className="route-form-panel">
          <div className="form-row">
            <input placeholder="Route name (e.g. Morning SF drops)" value={name} onChange={(e) => setName(e.target.value)} />
            <input type="date" value={scheduledDate} onChange={(e) => setScheduledDate(e.target.value)} />
          </div>
          <div className="form-row">
            <label style={{ flex: 1 }}>
              Depot
              {depots.length > 0 ? (
                <select value={depotId} onChange={(e) => setDepotId(e.target.value)} style={{ width: '100%' }}>
                  {depots.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name} — {d.address}
                    </option>
                  ))}
                </select>
              ) : (
                <span className="muted">
                  {' '}
                  No depots.{' '}
                  <Link to={`/delivery/fleets/${fleetId}/depots`}>Add a depot</Link>
                </span>
              )}
            </label>
          </div>
          <h3>Stops</h3>
          {stops.map((s, i) => (
            <div key={i} className="stop-draft-row">
              <span className="stop-seq">{i + 1}</span>
              <select value={s.stopType} onChange={(e) => updateStop(i, { stopType: e.target.value as DeliveryStopType })}>
                <option value="Dropoff">Dropoff</option>
                <option value="Pickup">Pickup</option>
              </select>
              <input placeholder="Address" value={s.address} onChange={(e) => updateStop(i, { address: e.target.value })} />
              <input placeholder="Recipient" value={s.recipientName} onChange={(e) => updateStop(i, { recipientName: e.target.value })} />
              <input placeholder="Phone" value={s.recipientPhone} onChange={(e) => updateStop(i, { recipientPhone: e.target.value })} />
              <input placeholder="Parcel" value={s.parcelDescription} onChange={(e) => updateStop(i, { parcelDescription: e.target.value })} />
              {stops.length > 1 && (
                <button type="button" className="secondary" onClick={() => removeStopRow(i)}>
                  Remove
                </button>
              )}
            </div>
          ))}
          <div className="form-row">
            <button type="button" className="secondary" onClick={addStopRow}>
              Add stop
            </button>
            <button type="button" onClick={() => void createRoute()} disabled={saving}>
              {saving ? 'Creating…' : 'Create route'}
            </button>
          </div>
        </div>
      )}

      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Date</th>
            <th>Status</th>
            <th>Vehicle</th>
            <th>Driver</th>
            <th>Progress</th>
            <th>Source</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {routes.map((r) => (
            <tr key={r.id}>
              <td>{r.name}</td>
              <td>{r.scheduledDate}</td>
              <td><span className="badge badge-conv">{routeStatusLabel(r.status)}</span></td>
              <td>{r.vehicleNumber ? vehiclePrimaryLabel({ vehicleNumber: r.vehicleNumber, licensePlate: r.licensePlate }) : '—'}</td>
              <td>{r.driverName ?? '—'}</td>
              <td>
                {r.completedStops}/{r.completedStops + r.pendingStops} stops
              </td>
              <td>{r.routePlanRunId ? <span className="badge badge-av">From plan</span> : 'Manual'}</td>
              <td>
                <Link to={`/delivery/routes/${r.id}`}>Open</Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {routes.length === 0 && !showForm && <p className="muted">No routes yet. Create a morning run with 10–15 dropoff stops.</p>}
    </div>
  );
}
