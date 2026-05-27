import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryRouteDto, DeliveryStopType } from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

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
  const { fleetId } = useParams<{ fleetId: string }>();
  const [routes, setRoutes] = useState<DeliveryRouteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [depotAddress, setDepotAddress] = useState('2500 Distribution Way, San Francisco, CA');
  const [scheduledDate, setScheduledDate] = useState(new Date().toISOString().slice(0, 10));
  const [stops, setStops] = useState<StopDraft[]>([emptyStop(), emptyStop(), emptyStop()]);

  const load = () => {
    if (!fleetId) return Promise.resolve();
    return api.get<DeliveryRouteDto[]>(`/api/delivery/tenants/${fleetId}/routes`).then(setRoutes);
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
    if (!fleetId) return;
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
    if (!name.trim() || deliveryStops.length === 0) {
      setError('Route name and at least one stop address are required.');
      return;
    }
    setError('');
    await api.post(`/api/delivery/tenants/${fleetId}/routes`, {
      name: name.trim(),
      depotAddress: depotAddress.trim(),
      scheduledDate,
      stops: deliveryStops,
    });
    setShowForm(false);
    setName('');
    setStops([emptyStop(), emptyStop(), emptyStop()]);
    await load();
  };

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
            <input
              placeholder="Depot / start address"
              value={depotAddress}
              onChange={(e) => setDepotAddress(e.target.value)}
              style={{ minWidth: '100%' }}
            />
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
            <button type="button" onClick={createRoute}>
              Create route
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
            <th>Progress</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {routes.map((r) => (
            <tr key={r.id}>
              <td>{r.name}</td>
              <td>{r.scheduledDate}</td>
              <td><span className="badge badge-conv">{r.status}</span></td>
              <td>{r.licensePlate ?? '—'}</td>
              <td>
                {r.completedStops}/{r.completedStops + r.pendingStops} stops
              </td>
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
