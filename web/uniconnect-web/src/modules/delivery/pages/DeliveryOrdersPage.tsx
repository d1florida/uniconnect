import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { CustomerDto, DeliveryOrderDto, DepotDto } from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { DepotAddressField } from '../components/DepotAddressField';
import { isOrderHeldForFixedRoute, orderHoldLabel, orderStatusLabel } from '../deliveryLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';
import { OrderGeocodeStatus } from '../components/OrderGeocodeStatus';

type OrderFilter = 'all' | 'daily' | 'held';

export function DeliveryOrdersPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const filterParam = searchParams.get('filter');
  const orderFilter: OrderFilter =
    filterParam === 'held' ? 'held' : filterParam === 'daily' ? 'daily' : 'all';
  const [orders, setOrders] = useState<DeliveryOrderDto[]>([]);
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({
    customerId: '',
    pickupAddress: '',
    deliveryAddress: '',
    recipientName: '',
    recipientPhone: '',
    parcelDescription: '',
    externalRef: '',
  });
  const selectedCustomer = customers.find((c) => c.id === form.customerId);

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
      api.get<CustomerDto[]>(`/api/delivery/tenants/${fleetId}/customers`),
    ])
      .then(([, depotList, customerList]) => {
        setDepots(depotList);
        setCustomers(customerList);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [fleetId]);

  useEffect(() => {
    if (!showForm || depots.length === 0) return;
    setForm((prev) => {
      if (prev.pickupAddress.trim()) return prev;
      const defaultDepot = depots.find((d) => d.isDefault) ?? depots[0];
      return defaultDepot ? { ...prev, pickupAddress: defaultDepot.address } : prev;
    });
  }, [showForm, depots]);

  const createOrder = async () => {
    if (!fleetId || creating) return;
    setError('');
    setCreating(true);
    try {
      await api.post(`/api/delivery/tenants/${fleetId}/orders`, {
        pickupAddress: form.pickupAddress,
        deliveryAddress: form.deliveryAddress,
        recipientName: form.recipientName,
        recipientPhone: form.recipientPhone,
        parcelDescription: form.parcelDescription,
        customerId: form.customerId || null,
        externalRef: form.externalRef.trim() || null,
      });
      setShowForm(false);
      setForm({ customerId: '', pickupAddress: '', deliveryAddress: '', recipientName: '', recipientPhone: '', parcelDescription: '', externalRef: '' });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create order');
    } finally {
      setCreating(false);
    }
  };

  const filteredOrders = orders.filter((o) => {
    if (orderFilter === 'held') return isOrderHeldForFixedRoute(o);
    if (orderFilter === 'daily') return o.status === 'Created' && !o.fixedRouteTemplateId;
    return true;
  });

  const setOrderFilter = (next: OrderFilter) => {
    if (next === 'all') setSearchParams({});
    else setSearchParams({ filter: next });
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
        <button type="button" className={orderFilter === 'all' ? 'active' : ''} onClick={() => setOrderFilter('all')}>
          All ({orders.length})
        </button>
        <button type="button" className={orderFilter === 'daily' ? 'active' : ''} onClick={() => setOrderFilter('daily')}>
          Daily ready ({orders.filter((o) => o.status === 'Created' && !o.fixedRouteTemplateId).length})
        </button>
        <button type="button" className={orderFilter === 'held' ? 'active' : ''} onClick={() => setOrderFilter('held')}>
          Held ({orders.filter((o) => isOrderHeldForFixedRoute(o)).length})
        </button>
        <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>+ New order</button>
      </div>

      {showForm && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row" style={{ marginBottom: '0.75rem' }}>
            <label>
              Customer{' '}
              <select
                value={form.customerId}
                onChange={(e) => {
                  const customerId = e.target.value;
                  const customer = customers.find((c) => c.id === customerId);
                  setForm((prev) => ({
                    ...prev,
                    customerId,
                    recipientName: customer?.name ?? prev.recipientName,
                    recipientPhone: customer?.phone ?? prev.recipientPhone,
                    deliveryAddress: customer?.deliveryAddress ?? prev.deliveryAddress,
                    externalRef: customer?.externalRef ?? prev.externalRef,
                  }));
                }}
              >
                <option value="">— New recipient —</option>
                {customers.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}{c.deliveryAddress ? ` — ${c.deliveryAddress}` : ''}</option>
                ))}
              </select>
            </label>
            {selectedCustomer?.deliveryWindowStart && selectedCustomer?.deliveryWindowEnd && (
              <span className="muted" style={{ alignSelf: 'center' }}>
                Open hours: {selectedCustomer.deliveryWindowStart} – {selectedCustomer.deliveryWindowEnd}
                {selectedCustomer.noDeliveryStart && selectedCustomer.noDeliveryEnd
                  ? ` (no deliveries ${selectedCustomer.noDeliveryStart}–${selectedCustomer.noDeliveryEnd})`
                  : ''}
              </span>
            )}
          </div>
          <div className="order-address-fields">
            <DepotAddressField
              label="Pickup address"
              placeholder="Ship-from address"
              depots={depots}
              fleetId={fleetId}
              multiline
              value={form.pickupAddress}
              onChange={(pickupAddress) => setForm((prev) => ({ ...prev, pickupAddress }))}
            />
            <DepotAddressField
              label="Delivery address"
              placeholder="Recipient address"
              depots={depots}
              fleetId={fleetId}
              showDepotPicker={false}
              multiline
              value={form.deliveryAddress}
              onChange={(deliveryAddress) => setForm((prev) => ({ ...prev, deliveryAddress }))}
            />
          </div>
          <div className="form-row">
            <input placeholder="Recipient" value={form.recipientName} onChange={(e) => setForm({ ...form, recipientName: e.target.value })} />
            <input placeholder="Phone" value={form.recipientPhone} onChange={(e) => setForm({ ...form, recipientPhone: e.target.value })} />
            <input placeholder="Parcel" value={form.parcelDescription} onChange={(e) => setForm({ ...form, parcelDescription: e.target.value })} />
            <input placeholder="External ref" value={form.externalRef} onChange={(e) => setForm({ ...form, externalRef: e.target.value })} />
            <button
              type="button"
              onClick={() => void createOrder()}
              disabled={creating || !form.deliveryAddress.trim() || !form.recipientName.trim()}
            >
              {creating ? 'Creating…' : 'Create order'}
            </button>
          </div>
        </div>
      )}

      <table>
        <thead><tr><th>Recipient</th><th>Ref</th><th>Status</th><th>Geocoding</th><th>Assignment</th><th></th></tr></thead>
        <tbody>
          {filteredOrders.map((o) => {
            const holdLabel = orderHoldLabel(o);
            return (
            <tr key={o.id}>
              <td>{o.recipientName}</td>
              <td>{o.externalRef ?? '—'}</td>
              <td>
                {orderStatusLabel(o.status)}
                {holdLabel && (
                  <>
                    <br />
                    <span className="badge badge-held">{holdLabel}</span>
                  </>
                )}
              </td>
              <td><OrderGeocodeStatus order={o} compact /></td>
              <td>
                {o.assignment
                  ? `${vehiclePrimaryLabel(o.assignment)}${o.assignment.driverName ? ` · ${o.assignment.driverName}` : ''} (${o.assignment.automationMode})`
                  : '—'}
              </td>
              <td><Link to={`/delivery/orders/${o.id}`}>View / edit</Link></td>
            </tr>
          );
          })}
        </tbody>
      </table>
      {filteredOrders.length === 0 && (
        <p className="muted">No orders match this filter.</p>
      )}
    </div>
  );
}
