import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { AutomationMode, DeliveryOrderDto, DeliveryOrderStatus, DeliveryVehicleDto } from '../../../api/types';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

const NEXT_STATUS: Partial<Record<DeliveryOrderStatus, DeliveryOrderStatus>> = {
  Created: 'Assigned',
  Assigned: 'PickedUp',
  PickedUp: 'InTransit',
  InTransit: 'Delivered',
};

export function DeliveryOrderDetailPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const [order, setOrder] = useState<DeliveryOrderDto | null>(null);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [vehicleId, setVehicleId] = useState('');
  const [mode, setMode] = useState<AutomationMode>('Conventional');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async () => {
    if (!orderId) return;
    const o = await api.get<DeliveryOrderDto>(`/api/delivery/orders/${orderId}`);
    setOrder(o);
    if (o.tenantId) {
      const v = await api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${o.tenantId}/vehicles`);
      setVehicles(v);
    }
  };

  useEffect(() => {
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [orderId]);

  const assign = async () => {
    await api.post(`/api/delivery/orders/${orderId}/assign`, { vehicleId, automationMode: mode });
    await load();
  };

  const advanceStatus = async (status: DeliveryOrderStatus) => {
    await api.patch(`/api/delivery/orders/${orderId}/status`, { status });
    await load();
  };

  if (loading) return <Loading />;
  const conventional = vehicles.filter((v) => !v.isAutonomous);
  const autonomous = vehicles.filter((v) => v.isAutonomous);
  const next = order ? NEXT_STATUS[order.status] : undefined;

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
      {order && (
        <>
          <p>
            <span className={`badge badge-${order.channel.toLowerCase()}`}>{order.channel}</span>
            {' '}
            <span className="badge badge-conv">{order.status}</span>
            {' '}
            {order.parcelDescription}
          </p>

          {next && (
            <div className="form-row">
              <button type="button" onClick={() => advanceStatus(next)}>Advance to {next}</button>
              {order.status !== 'Cancelled' && order.status !== 'Delivered' && (
                <button type="button" className="secondary" onClick={() => advanceStatus('Cancelled')}>Cancel</button>
              )}
            </div>
          )}

          <h3>Assign vehicle</h3>
          <div className="form-row">
            <select value={mode} onChange={(e) => setMode(e.target.value as AutomationMode)}>
              <option value="Conventional">Conventional</option>
              <option value="Autonomous">Autonomous</option>
            </select>
            <select value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
              <option value="">Select vehicle</option>
              {(mode === 'Conventional' ? conventional : autonomous).map((v) => (
                <option key={v.id} value={v.id}>{v.licensePlate} — {v.make} {v.model}</option>
              ))}
            </select>
            <button type="button" onClick={assign} disabled={!vehicleId}>Assign</button>
          </div>
        </>
      )}
    </div>
  );
}
