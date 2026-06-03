import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type {
  AutomationMode,
  DeliveryOrderDto,
  DeliveryOrderStatus,
  DeliveryVehicleDto,
  DepotDto,
  DriverDto,
} from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { DepotAddressField } from '../components/DepotAddressField';
import { OrderGeocodeStatus } from '../components/OrderGeocodeStatus';
import { canEditOrder, orderStatusLabel } from '../deliveryLabels';
import { vehiclePrimaryLabel, vehicleSelectLabel } from '../../../utils/vehicleLabels';

const NEXT_STATUS: Partial<Record<DeliveryOrderStatus, DeliveryOrderStatus>> = {
  Created: 'Assigned',
  Assigned: 'PickedUp',
  PickedUp: 'InTransit',
  InTransit: 'Delivered',
};

type OrderForm = {
  pickupAddress: string;
  deliveryAddress: string;
  recipientName: string;
  recipientPhone: string;
  parcelDescription: string;
};

function orderToForm(order: DeliveryOrderDto): OrderForm {
  return {
    pickupAddress: order.pickupAddress,
    deliveryAddress: order.deliveryAddress,
    recipientName: order.recipientName,
    recipientPhone: order.recipientPhone,
    parcelDescription: order.parcelDescription,
  };
}

export function DeliveryOrderDetailPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const [order, setOrder] = useState<DeliveryOrderDto | null>(null);
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [drivers, setDrivers] = useState<DriverDto[]>([]);
  const [vehicleId, setVehicleId] = useState('');
  const [driverId, setDriverId] = useState('');
  const [mode, setMode] = useState<AutomationMode>('Conventional');
  const [editForm, setEditForm] = useState<OrderForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async () => {
    if (!orderId) return;
    const o = await api.get<DeliveryOrderDto>(`/api/delivery/orders/${orderId}`);
    setOrder(o);
    setEditForm(orderToForm(o));
    if (o.tenantId) {
      const [v, d, depotList] = await Promise.all([
        api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${o.tenantId}/vehicles`),
        api.get<DriverDto[]>(`/api/delivery/tenants/${o.tenantId}/drivers`),
        api.get<DepotDto[]>(`/api/delivery/tenants/${o.tenantId}/depots`),
      ]);
      setVehicles(v);
      setDrivers(d.filter((x) => x.isActive));
      setDepots(depotList);
    }
    if (o.assignment) {
      setVehicleId(o.assignment.vehicleId);
      setDriverId(o.assignment.driverId ?? '');
      setMode(o.assignment.automationMode);
    }
  };

  useEffect(() => {
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [orderId]);

  const assign = async () => {
    setError('');
    try {
      await api.post(`/api/delivery/orders/${orderId}/assign`, {
        vehicleId,
        automationMode: mode,
        driverId: mode === 'Conventional' && driverId ? driverId : null,
      });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to assign');
    }
  };

  const advanceStatus = async (status: DeliveryOrderStatus) => {
    await api.patch(`/api/delivery/orders/${orderId}/status`, { status });
    await load();
  };

  const saveOrder = async () => {
    if (!orderId || !editForm) return;
    setSaving(true);
    setError('');
    try {
      await api.put(`/api/delivery/orders/${orderId}`, editForm);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save order');
    } finally {
      setSaving(false);
    }
  };

  const resetEditForm = () => {
    if (order) setEditForm(orderToForm(order));
  };

  if (loading) return <Loading />;
  const conventional = vehicles.filter((v) => !v.isAutonomous);
  const autonomous = vehicles.filter((v) => v.isAutonomous);
  const next = order ? NEXT_STATUS[order.status] : undefined;
  const activeDrivers = drivers.filter((d) => d.isActive);
  const editable = order ? canEditOrder(order.status) : false;
  const editDirty = order && editForm
    ? JSON.stringify(editForm) !== JSON.stringify(orderToForm(order))
    : false;

  return (
    <div>
      <div className="page-header">
        <h2>Order</h2>
        <p>
          {order?.pickupAddress} → {order?.deliveryAddress}
          {order && (
            <> · <Link to={`/delivery/fleets/${order.tenantId}/orders`}>Back to orders</Link></>
          )}
        </p>
      </div>
      <ErrorAlert message={error} />
      {order && editForm && (
        <>
          <p>
            <span className="badge badge-conv">{orderStatusLabel(order.status)}</span>
            {' '}
            {order.parcelDescription}
          </p>

          <OrderGeocodeStatus order={order} />

          {editable ? (
            <div className="card" style={{ marginTop: '1rem' }}>
              <h3>Edit order</h3>
              <div className="form-row">
                <DepotAddressField
                  label="Pickup address"
                  placeholder="Ship-from address"
                  depots={depots}
                  fleetId={order.tenantId}
                  value={editForm.pickupAddress}
                  onChange={(pickupAddress) => setEditForm({ ...editForm, pickupAddress })}
                />
                <DepotAddressField
                  label="Delivery address"
                  placeholder="Recipient address"
                  depots={depots}
                  fleetId={order.tenantId}
                  value={editForm.deliveryAddress}
                  onChange={(deliveryAddress) => setEditForm({ ...editForm, deliveryAddress })}
                />
              </div>
              <div className="form-row">
                <input
                  placeholder="Recipient"
                  value={editForm.recipientName}
                  onChange={(e) => setEditForm({ ...editForm, recipientName: e.target.value })}
                />
                <input
                  placeholder="Phone"
                  value={editForm.recipientPhone}
                  onChange={(e) => setEditForm({ ...editForm, recipientPhone: e.target.value })}
                />
                <input
                  placeholder="Parcel"
                  value={editForm.parcelDescription}
                  onChange={(e) => setEditForm({ ...editForm, parcelDescription: e.target.value })}
                />
              </div>
              <div className="form-row">
                <button
                  type="button"
                  onClick={() => void saveOrder()}
                  disabled={saving || !editForm.deliveryAddress.trim() || !editForm.recipientName.trim() || !editDirty}
                >
                  {saving ? 'Saving…' : 'Save changes'}
                </button>
                <button
                  type="button"
                  className="secondary"
                  onClick={resetEditForm}
                  disabled={saving || !editDirty}
                >
                  Reset
                </button>
              </div>
              <p className="muted">Address changes trigger re-geocoding for route planning.</p>
            </div>
          ) : (
            <p className="muted">This order is closed and can no longer be edited.</p>
          )}

          {order.assignment && (
            <p className="muted">
              Assigned: {vehiclePrimaryLabel(order.assignment)}
              {order.assignment.driverName && <> · Driver: {order.assignment.driverName}</>}
              {' · '}
              {order.assignment.automationMode}
            </p>
          )}

          {next && (
            <div className="form-row">
              <button type="button" onClick={() => advanceStatus(next)}>Advance to {next}</button>
              {order.status !== 'Cancelled' && order.status !== 'Delivered' && (
                <button type="button" className="secondary" onClick={() => advanceStatus('Cancelled')}>Cancel</button>
              )}
            </div>
          )}

          <h3>Assign vehicle & driver</h3>
          <div className="form-row">
            <select value={mode} onChange={(e) => setMode(e.target.value as AutomationMode)}>
              <option value="Conventional">Conventional</option>
              <option value="Autonomous">Autonomous</option>
            </select>
            <select value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
              <option value="">Select vehicle</option>
              {(mode === 'Conventional' ? conventional : autonomous).map((v) => (
                <option key={v.id} value={v.id}>{vehicleSelectLabel(v)}</option>
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
            <button type="button" onClick={() => void assign()} disabled={!vehicleId}>Assign</button>
          </div>
          {mode === 'Conventional' && activeDrivers.length === 0 && (
            <p className="muted">
              No active drivers. <Link to={`/delivery/fleets/${order.tenantId}/drivers`}>Add drivers</Link>
            </p>
          )}
        </>
      )}
    </div>
  );
}
