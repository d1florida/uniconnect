import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { BusinessAccountDto, DeliveryChannel, DeliveryOrderDto } from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

export function DeliveryOrdersPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [orders, setOrders] = useState<DeliveryOrderDto[]>([]);
  const [accounts, setAccounts] = useState<BusinessAccountDto[]>([]);
  const [tab, setTab] = useState<'All' | DeliveryChannel>('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [channel, setChannel] = useState<DeliveryChannel>('B2C');
  const [form, setForm] = useState({
    pickupAddress: '',
    deliveryAddress: '',
    recipientName: '',
    recipientPhone: '',
    parcelDescription: '',
    businessAccountId: '',
  });

  const load = () => {
    if (!fleetId) return Promise.resolve();
    const q = tab === 'All' ? '' : `?channel=${tab}`;
    return api.get<DeliveryOrderDto[]>(`/api/delivery/tenants/${fleetId}/orders${q}`).then(setOrders);
  };

  useEffect(() => {
    if (!fleetId) return;
    setLoading(true);
    setError('');
    Promise.all([
      load(),
      api.get<BusinessAccountDto[]>(`/api/delivery/tenants/${fleetId}/business-accounts`).then(setAccounts),
    ])
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [fleetId, tab]);

  const createOrder = async () => {
    if (!fleetId) return;
    await api.post(`/api/delivery/tenants/${fleetId}/orders`, {
      channel,
      pickupAddress: form.pickupAddress,
      deliveryAddress: form.deliveryAddress,
      recipientName: form.recipientName,
      recipientPhone: form.recipientPhone,
      parcelDescription: form.parcelDescription,
      businessAccountId: channel === 'B2B' ? form.businessAccountId : null,
    });
    setShowForm(false);
    setForm({ pickupAddress: '', deliveryAddress: '', recipientName: '', recipientPhone: '', parcelDescription: '', businessAccountId: '' });
    await load();
  };

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Delivery orders</h2>
        <p>
          <Link to={`/delivery/fleets/${fleetId}/routes`}>Routes</Link>
          {' · '}
          <Link to={`/delivery/fleets/${fleetId}/accounts`}>B2B accounts</Link>
          {' · '}
          <Link to={`/delivery/fleets/${fleetId}/map`}>Map</Link>
        </p>
      </div>
      <ErrorAlert message={error} />
      <div className="tabs">
        {(['All', 'B2B', 'B2C'] as const).map((t) => (
          <button key={t} type="button" className={tab === t ? 'active' : ''} onClick={() => setTab(t)}>{t}</button>
        ))}
        <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>+ New order</button>
      </div>

      {showForm && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row">
            <select value={channel} onChange={(e) => setChannel(e.target.value as DeliveryChannel)}>
              <option value="B2C">B2C</option>
              <option value="B2B">B2B</option>
            </select>
            {channel === 'B2B' && (
              <select value={form.businessAccountId} onChange={(e) => setForm({ ...form, businessAccountId: e.target.value })}>
                <option value="">Select B2B account</option>
                {accounts.map((a) => (
                  <option key={a.id} value={a.id}>{a.companyName}</option>
                ))}
              </select>
            )}
            <input placeholder="Pickup address" value={form.pickupAddress} onChange={(e) => setForm({ ...form, pickupAddress: e.target.value })} />
            <input placeholder="Delivery address" value={form.deliveryAddress} onChange={(e) => setForm({ ...form, deliveryAddress: e.target.value })} />
            <input placeholder="Recipient" value={form.recipientName} onChange={(e) => setForm({ ...form, recipientName: e.target.value })} />
            <input placeholder="Phone" value={form.recipientPhone} onChange={(e) => setForm({ ...form, recipientPhone: e.target.value })} />
            <input placeholder="Parcel" value={form.parcelDescription} onChange={(e) => setForm({ ...form, parcelDescription: e.target.value })} />
            <button type="button" onClick={createOrder}>Create order</button>
          </div>
        </div>
      )}

      <table>
        <thead><tr><th>Channel</th><th>Recipient</th><th>Status</th><th>Assignment</th><th></th></tr></thead>
        <tbody>
          {orders.map((o) => (
            <tr key={o.id}>
              <td><span className={`badge badge-${o.channel.toLowerCase()}`}>{o.channel}</span></td>
              <td>{o.recipientName}</td>
              <td>{o.status}</td>
              <td>
                {o.assignment
                  ? `${o.assignment.licensePlate} (${o.assignment.automationMode})`
                  : '—'}
              </td>
              <td><Link to={`/delivery/orders/${o.id}`}>Detail</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
