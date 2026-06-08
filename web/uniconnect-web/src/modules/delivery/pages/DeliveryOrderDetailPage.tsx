import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type {
  AddressCheckResultDto,
  AutomationMode,
  CheckAddressesResultDto,
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
import {
  canEditOrder,
  geocodeSourceLabel,
  isApproximateGeocode,
  isDepotPickupAddress,
  orderHoldLabel,
  orderStatusLabel,
} from '../deliveryLabels';
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
  externalRef: string;
};

function orderToForm(order: DeliveryOrderDto): OrderForm {
  return {
    pickupAddress: order.pickupAddress,
    deliveryAddress: order.deliveryAddress,
    recipientName: order.recipientName,
    recipientPhone: order.recipientPhone,
    parcelDescription: order.parcelDescription,
    externalRef: order.externalRef ?? '',
  };
}

function formToPayload(form: OrderForm): OrderForm {
  return {
    pickupAddress: form.pickupAddress.trim(),
    deliveryAddress: form.deliveryAddress.trim(),
    recipientName: form.recipientName.trim(),
    recipientPhone: form.recipientPhone.trim(),
    parcelDescription: form.parcelDescription.trim(),
    externalRef: form.externalRef.trim(),
  };
}

function addressForField(check: AddressCheckResultDto, current: string): string {
  const standardized = check.standardizedAddress?.trim();
  if (standardized) return standardized;
  const suggested = check.suggestedAddress?.trim();
  if (suggested && check.resolved) return suggested;
  return current;
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
  const [selectedDepot, setSelectedDepot] = useState<DepotDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [checkingAddresses, setCheckingAddresses] = useState(false);
  const [addressCheck, setAddressCheck] = useState<{
    pickup?: AddressCheckResultDto;
    delivery?: AddressCheckResultDto;
  } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const editFormRef = useRef(editForm);
  editFormRef.current = editForm;

  const load = async () => {
    if (!orderId) return;
    const o = await api.get<DeliveryOrderDto>(`/api/delivery/orders/${orderId}`);
    setOrder(o);
    setEditForm(orderToForm(o));
    setSelectedDepot(null);
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
    const form = editFormRef.current;
    if (!orderId || !form) return;
    const payload = formToPayload(form);
    setSaving(true);
    setError('');
    try {
      const updated = await api.put<DeliveryOrderDto>(`/api/delivery/orders/${orderId}`, payload);
      setOrder(updated);
      setEditForm(orderToForm(updated));
      setSelectedDepot(null);
      setAddressCheck(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save order');
    } finally {
      setSaving(false);
    }
  };

  const resetEditForm = () => {
    if (order) setEditForm(orderToForm(order));
    setSelectedDepot(null);
    setAddressCheck(null);
  };

  const updateEditForm = (patch: Partial<OrderForm>) => {
    setEditForm((prev) => (prev ? { ...prev, ...patch } : prev));
    if ('pickupAddress' in patch || 'deliveryAddress' in patch) {
      setAddressCheck(null);
    }
  };

  const checkAddresses = async () => {
    if (!order || !editForm) return;
    const pickupAddress = editForm.pickupAddress.trim();
    const deliveryAddress = editForm.deliveryAddress.trim();
    if (!pickupAddress || !deliveryAddress) return;

    setCheckingAddresses(true);
    setError('');
    try {
      const result = await api.post<CheckAddressesResultDto>(
        `/api/delivery/tenants/${order.tenantId}/check-addresses`,
        { pickupAddress, deliveryAddress },
      );
      setEditForm((prev) =>
        prev
          ? {
              ...prev,
              pickupAddress: addressForField(result.pickup, prev.pickupAddress),
              deliveryAddress: addressForField(result.delivery, prev.deliveryAddress),
            }
          : prev,
      );
      setAddressCheck({ pickup: result.pickup, delivery: result.delivery });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to check addresses');
    } finally {
      setCheckingAddresses(false);
    }
  };

  const addressCheckHintClass = (check?: AddressCheckResultDto) => {
    if (!check) return 'order-address-hint muted';
    if (!check.resolved) return 'order-address-hint order-address-hint-error';
    if (isApproximateGeocode(check.geocodeSource)) return 'order-address-hint order-address-hint-warn';
    return 'order-address-hint order-address-hint-ok';
  };

  const addressCheckHintText = (check?: AddressCheckResultDto) => {
    if (!check) return null;
    const source = geocodeSourceLabel(check.geocodeSource);
    const applied = check.standardizedAddress?.trim()
      ? ' — updated in field above'
      : '';
    const detail = source ? `${check.message} (${source})` : check.message;
    return `${detail}${applied}`;
  };

  if (loading) return <Loading />;
  const conventional = vehicles.filter((v) => !v.isAutonomous);
  const autonomous = vehicles.filter((v) => v.isAutonomous);
  const next = order ? NEXT_STATUS[order.status] : undefined;
  const activeDrivers = drivers.filter((d) => d.isActive);
  const editable = order ? canEditOrder(order.status) : false;
  const editDirty = order && editForm
    ? JSON.stringify(formToPayload(editForm)) !== JSON.stringify(formToPayload(orderToForm(order)))
    : false;
  const displayPickup = editForm?.pickupAddress ?? order?.pickupAddress ?? '';
  const displayDelivery = editForm?.deliveryAddress ?? order?.deliveryAddress ?? '';
  const pickupCoordsForMatching = selectedDepot
    ? { latitude: selectedDepot.latitude, longitude: selectedDepot.longitude }
    : { latitude: order?.pickupLatitude, longitude: order?.pickupLongitude };
  const pickupGeocodeOverride = editDirty && selectedDepot
    ? {
        address: editForm?.pickupAddress,
        lat: selectedDepot.latitude,
        lng: selectedDepot.longitude,
        pending: true,
      }
    : undefined;
  const pickupMatchesSavedGeocode = Boolean(
    order?.pickupFormattedAddress
    && order.pickupLatitude != null
    && order.pickupLongitude != null
    && isDepotPickupAddress(
      editForm?.pickupAddress ?? '',
      order.pickupFormattedAddress,
      { latitude: order.pickupLatitude, longitude: order.pickupLongitude },
      { latitude: order.pickupLatitude, longitude: order.pickupLongitude },
    ),
  );
  const showPickupStandardizedHint = Boolean(
    order?.pickupFormattedAddress
    && editForm
    && !addressCheck?.pickup
    && !pickupMatchesSavedGeocode
    && order.pickupFormattedAddress.trim().toLowerCase()
      !== editForm.pickupAddress.trim().toLowerCase(),
  );

  return (
    <div>
      <div className="page-header">
        <h2>Order</h2>
        <p>
          {displayPickup} → {displayDelivery}
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
            {orderHoldLabel(order) && (
              <>
                {' '}
                <span className="badge badge-held">{orderHoldLabel(order)}</span>
              </>
            )}
            {' '}
            {order.parcelDescription}
            {order.externalRef && (
              <> · Ref: {order.externalRef}</>
            )}
          </p>

          <div className="card order-details-card">
            <h3>{editable ? 'Edit order' : 'Order details'}</h3>

            <div className="order-address-fields">
              {editable ? (
                <>
                  <DepotAddressField
                    label="Pickup address"
                    placeholder="Ship-from address"
                    depots={depots}
                    fleetId={order.tenantId}
                    multiline
                    value={editForm.pickupAddress}
                    pickupLatitude={pickupCoordsForMatching.latitude}
                    pickupLongitude={pickupCoordsForMatching.longitude}
                    onChange={(pickupAddress) => {
                      setSelectedDepot(null);
                      updateEditForm({ pickupAddress });
                    }}
                    onDepotSelect={setSelectedDepot}
                  />
                  {addressCheck?.pickup ? (
                    <p className={addressCheckHintClass(addressCheck.pickup)}>
                      {addressCheckHintText(addressCheck.pickup)}
                    </p>
                  ) : showPickupStandardizedHint && (
                    <p className="order-address-hint muted" title="Last saved standardized address">
                      Previously standardized: {order.pickupFormattedAddress}
                    </p>
                  )}
                  <DepotAddressField
                    label="Delivery address"
                    placeholder="Recipient address"
                    depots={depots}
                    fleetId={order.tenantId}
                    showDepotPicker={false}
                    multiline
                    value={editForm.deliveryAddress}
                    onChange={(deliveryAddress) => updateEditForm({ deliveryAddress })}
                  />
                  {addressCheck?.delivery ? (
                    <p className={addressCheckHintClass(addressCheck.delivery)}>
                      {addressCheckHintText(addressCheck.delivery)}
                    </p>
                  ) : (
                    order.deliveryFormattedAddress &&
                    order.deliveryFormattedAddress.trim().toLowerCase()
                      !== editForm.deliveryAddress.trim().toLowerCase() && (
                      <p className="order-address-hint muted" title="Last saved standardized address">
                        Previously standardized: {order.deliveryFormattedAddress}
                      </p>
                    )
                  )}
                </>
              ) : (
                <>
                  <div className="order-address-readonly">
                    <span className="order-address-readonly-label">Pickup address</span>
                    <p>{order.pickupAddress}</p>
                    {order.pickupFormattedAddress &&
                      order.pickupFormattedAddress.trim().toLowerCase()
                        !== order.pickupAddress.trim().toLowerCase() && (
                      <p className="muted order-address-hint">Standardized: {order.pickupFormattedAddress}</p>
                    )}
                  </div>
                  <div className="order-address-readonly">
                    <span className="order-address-readonly-label">Delivery address</span>
                    <p>{order.deliveryAddress}</p>
                    {order.deliveryFormattedAddress &&
                      order.deliveryFormattedAddress.trim().toLowerCase()
                        !== order.deliveryAddress.trim().toLowerCase() && (
                      <p className="muted order-address-hint">Standardized: {order.deliveryFormattedAddress}</p>
                    )}
                  </div>
                </>
              )}
            </div>

            {editable ? (
              <>
                <div className="form-row">
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => void checkAddresses()}
                    disabled={
                      checkingAddresses
                      || saving
                      || !editForm.pickupAddress.trim()
                      || !editForm.deliveryAddress.trim()
                    }
                  >
                    {checkingAddresses ? 'Checking addresses…' : 'Check addresses'}
                  </button>
                </div>
                <div className="form-row">
                  <input
                    placeholder="Recipient"
                    value={editForm.recipientName}
                    onChange={(e) => updateEditForm({ recipientName: e.target.value })}
                  />
                  <input
                    placeholder="Phone"
                    value={editForm.recipientPhone}
                    onChange={(e) => updateEditForm({ recipientPhone: e.target.value })}
                  />
                  <input
                    placeholder="Parcel"
                    value={editForm.parcelDescription}
                    onChange={(e) => updateEditForm({ parcelDescription: e.target.value })}
                  />
                  <input
                    placeholder="External ref"
                    value={editForm.externalRef}
                    onChange={(e) => updateEditForm({ externalRef: e.target.value })}
                  />
                </div>
                <p className="muted order-details-hint">
                  Use Check addresses to standardize pickup and delivery before saving. Saving still runs geocoding for route planning.
                </p>
                <div className="form-row order-details-actions">
                  <button
                    type="button"
                    onClick={() => void saveOrder()}
                    disabled={
                      saving
                      || !editForm.pickupAddress.trim()
                      || !editForm.deliveryAddress.trim()
                      || !editForm.recipientName.trim()
                      || !editDirty
                    }
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
              </>
            ) : (
              <>
                <div className="form-row order-details-meta">
                  <span><strong>Recipient:</strong> {order.recipientName}</span>
                  <span><strong>Phone:</strong> {order.recipientPhone || '—'}</span>
                  <span><strong>Parcel:</strong> {order.parcelDescription || '—'}</span>
                  <span><strong>Ref:</strong> {order.externalRef || '—'}</span>
                </div>
                <p className="muted">This order is closed and can no longer be edited.</p>
              </>
            )}
          </div>

          <OrderGeocodeStatus order={order} pickupOverride={pickupGeocodeOverride} />

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
