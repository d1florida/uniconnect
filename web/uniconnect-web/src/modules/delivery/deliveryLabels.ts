import type { DeliveryOrderStatus, DeliveryRouteStatus, RoutePlanRunStatus, DeliveryOrderDto } from '../../api/types';

const ORDER_STATUS_LABELS: Record<DeliveryOrderStatus, string> = {
  Created: 'Ready',
  Assigned: 'Assigned',
  PickedUp: 'Picked up',
  InTransit: 'In transit',
  Delivered: 'Delivered',
  Failed: 'Failed',
  Cancelled: 'Cancelled',
};

const ROUTE_STATUS_LABELS: Record<DeliveryRouteStatus, string> = {
  Draft: 'Draft route',
  Planned: 'Planned',
  InProgress: 'In progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

const PLAN_RUN_STATUS_LABELS: Record<RoutePlanRunStatus, string> = {
  Requested: 'Requested',
  Completed: 'Ready to review',
  Accepted: 'Accepted',
  Discarded: 'Discarded',
  Failed: 'Failed',
};

export function formatDriveMinutes(minutes?: number | null): string | null {
  if (minutes == null || minutes <= 0 || Number.isNaN(minutes)) return null;
  return `~${minutes} min`;
}

export function formatNextStopEta(
  minutes?: number | null,
  arrivalAtUtc?: string | null,
): string | null {
  if (minutes == null || minutes <= 0 || Number.isNaN(minutes)) return null;
  const drive = formatDriveMinutes(minutes);
  if (!arrivalAtUtc) return drive;
  const arrival = new Date(arrivalAtUtc);
  if (Number.isNaN(arrival.getTime())) return drive;
  const time = arrival.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  return `${drive} · ETA ${time}`;
}

export function normalizeDeliveryAddress(address: string): string {
  return address.trim().toLowerCase();
}

export function isDepotPickupAddress(pickupAddress: string, depotAddress: string): boolean {
  return normalizeDeliveryAddress(pickupAddress) === normalizeDeliveryAddress(depotAddress);
}

export function isOperationalRouteStop(
  stop: { stopType: string; address: string },
  depotAddress: string,
): boolean {
  if (stop.stopType === 'Depot') return false;
  if (stop.stopType === 'Pickup' && isDepotPickupAddress(stop.address, depotAddress)) return false;
  return true;
}

export function orderStatusLabel(status: DeliveryOrderStatus): string {
  return ORDER_STATUS_LABELS[status] ?? status;
}

export function canEditOrder(status: DeliveryOrderStatus): boolean {
  return status !== 'Delivered' && status !== 'Cancelled' && status !== 'Failed';
}

export function routeStatusLabel(status: DeliveryRouteStatus): string {
  return ROUTE_STATUS_LABELS[status] ?? status;
}

export function planRunStatusLabel(status: RoutePlanRunStatus): string {
  return PLAN_RUN_STATUS_LABELS[status] ?? status;
}

const VEHICLE_STATUS_LABELS: Record<string, string> = {
  Active: 'Active',
  InShop: 'In shop',
  Retired: 'Retired',
};

export function vehicleStatusLabel(status: string): string {
  return VEHICLE_STATUS_LABELS[status] ?? status;
}

export function hasOrderGeocode(
  order: Pick<
    DeliveryOrderDto,
    'pickupLatitude' | 'pickupLongitude' | 'deliveryLatitude' | 'deliveryLongitude'
  >,
  which: 'pickup' | 'delivery',
): boolean {
  if (which === 'pickup') {
    return order.pickupLatitude != null && order.pickupLongitude != null;
  }
  return order.deliveryLatitude != null && order.deliveryLongitude != null;
}

export function formatGeocodeCoords(lat?: number, lng?: number): string | null {
  if (lat == null || lng == null) return null;
  return `${lat.toFixed(4)}, ${lng.toFixed(4)}`;
}

export function geocodeSourceLabel(source?: string): string | null {
  if (!source) return null;
  if (source.toLowerCase() === 'nominatim') return 'OpenStreetMap';
  if (source.toLowerCase() === 'census') return 'US Census (street-level)';
  if (source.toLowerCase() === 'postal') return 'Approximate (postal code area)';
  if (source.toLowerCase() === 'deterministic') return 'Approximate (unresolved address)';
  return source;
}

export function isApproximateGeocode(source?: string): boolean {
  const normalized = source?.toLowerCase();
  return normalized === 'deterministic' || normalized === 'postal';
}
