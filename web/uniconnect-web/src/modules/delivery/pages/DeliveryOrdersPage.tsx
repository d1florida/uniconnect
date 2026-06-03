import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryOrderDto, DepotDto } from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { DepotAddressField } from '../components/DepotAddressField';
import { orderStatusLabel } from '../deliveryLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';
import { OrderGeocodeStatus } from '../components/OrderGeocodeStatus';

export function DeliveryOrdersPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [orders, setOrders] = useState<DeliveryOrderDto[]>([]);
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    pickupAddress: '',
    deliveryAddress: '',
    recipientName: '',
    recipientPhone: '',
    parcelDescription: '',
  });

  const load = () => {
    if (!fleetId) return Promise.resolve();
    return api.get<DeliveryOrderDto[]>(`/api/delivery/tenants/${fleetId}/orders`).then(setOrders);
  };

  useEffect(() => {
    if (!fleetId) return;
    setLoading(true);
    setError('');
    Promise.all([
      load(),
      api.get<DepotDto[]>(`/api/delivery/tenants/${fleetId}/depots`),
    ])
      .then(([, depotList]) => setDepots(depotList))
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [fleetId]);

  const createOrder = async () => {
    if (!fleetId) return;
    setError('');
    try {
      await api.post(`/api/delivery/tenants/${fleetId}/orders`, {
        pickupAddress: form.pickupAddress,
        deliveryAddress: form.deliveryAddress,
        recipientName: form.recipientName,
        recipientPhone: form.recipientPhone,
        parcelDescription: form.parcelDescription,
      });
      setShowForm(false);
      setForm({ pickupAddress: '', deliveryAddress: '', recipientName: '', recipientPhone: '', parcelDescription: '' });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create order');
    }
  };

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Delivery orders</h2>
        <p>
          <Link to={`/delivery/fleets/${fleetId}/routes`}>Routes</Link>
          {' · '}
          <Link to={`/delivery/fleets/${fleetId}/map`}>Map</Link>
        </p>
      </div>
      <ErrorAlert message={error} />
      <div className="tabs">
        <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>+ New order</button>
      </div>

      {showForm && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row">
            <DepotAddressField
              label="Pickup address"
              placeholder="Ship-from address"
              depots={depots}
              fleetId={fleetId}
              value={form.pickupAddress}
              onChange={(pickupAddress) => setForm({ ...form, pickupAddress })}
            />
            <DepotAddressField
              label="Delivery address"
              placeholder="Recipient address"
              depots={depots}
              fleetId={fleetId}
              value={form.deliveryAddress}
              onChange={(deliveryAddress) => setForm({ ...form, deliveryAddress })}
            />
          </div>
          <div className="form-row">
            <input placeholder="Recipient" value={form.recipientName} onChange={(e) => setForm({ ...form, recipientName: e.target.value })} />
            <input placeholder="Phone" value={form.recipientPhone} onChange={(e) => setForm({ ...form, recipientPhone: e.target.value })} />
            <input placeholder="Parcel" value={form.parcelDescription} onChange={(e) => setForm({ ...form, parcelDescription: e.target.value })} />
            <button type="button" onClick={() => void createOrder()} disabled={!form.deliveryAddress.trim() || !form.recipientName.trim()}>
              Create order
            </button>
          </div>
        </div>
      )}

      <table>
        <thead><tr><th>Recipient</th><th>Status</th><th>Geocoding</th><th>Assignment</th><th></th></tr></thead>
        <tbody>
          {orders.map((o) => (
            <tr key={o.id}>
              <td>{o.recipientName}</td>
              <td>{orderStatusLabel(o.status)}</td>
              <td><OrderGeocodeStatus order={o} compact /></td>
              <td>
                {o.assignment
                  ? `${vehiclePrimaryLabel(o.assignment)}${o.assignment.driverName ? ` · ${o.assignment.driverName}` : ''} (${o.assignment.automationMode})`
                  : '—'}
              </td>
              <td><Link to={`/delivery/orders/${o.id}`}>View / edit</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
