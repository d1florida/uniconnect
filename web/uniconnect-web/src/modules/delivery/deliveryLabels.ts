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

function formatMinutesParts(minutes: number, approximate: boolean): string {
  if (minutes < 60) return `${approximate ? '~' : ''}${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  const prefix = approximate ? '~' : '';
  if (mins === 0) return `${prefix}${hours} hr`;
  return `${prefix}${hours} hr ${mins} min`;
}

export function formatDriveMinutes(minutes?: number | null): string | null {
  if (minutes == null || minutes <= 0 || Number.isNaN(minutes)) return null;
  return formatMinutesParts(Math.round(minutes), true);
}

export function formatWorkMinutes(minutes?: number | null): string {
  if (minutes == null || minutes <= 0 || Number.isNaN(minutes)) return '—';
  return formatMinutesParts(Math.round(minutes), false);
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

const MAX_DEPOT_PICKUP_KM = 0.25;

function haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) ** 2
    + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

export function isDepotPickupAddress(
  pickupAddress: string,
  depotAddress: string,
  pickupCoords?: { latitude?: number; longitude?: number },
  depotCoords?: { latitude?: number; longitude?: number },
): boolean {
  if (normalizeDeliveryAddress(pickupAddress) === normalizeDeliveryAddress(depotAddress)) {
    return true;
  }

  if (
    pickupCoords?.latitude != null
    && pickupCoords?.longitude != null
    && depotCoords?.latitude != null
    && depotCoords?.longitude != null
    && haversineKm(
      pickupCoords.latitude,
      pickupCoords.longitude,
      depotCoords.latitude,
      depotCoords.longitude,
    ) <= MAX_DEPOT_PICKUP_KM
  ) {
    return true;
  }

  return false;
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

export function isOrderHeldForFixedRoute(
  order: Pick<DeliveryOrderDto, 'status' | 'fixedRouteTemplateId'>,
): boolean {
  return order.status === 'Created' && Boolean(order.fixedRouteTemplateId);
}

export function formatHeldUntilShort(heldUntil?: string | null): string | null {
  if (!heldUntil) return null;
  const date = new Date(`${heldUntil}T12:00:00`);
  if (Number.isNaN(date.getTime())) return heldUntil;
  return date.toLocaleDateString(undefined, { weekday: 'short', month: 'numeric', day: 'numeric' });
}

export function orderHoldLabel(
  order: Pick<DeliveryOrderDto, 'fixedRouteTemplateName' | 'heldUntil' | 'fixedRouteTemplateId' | 'status'>,
): string | null {
  if (!isOrderHeldForFixedRoute(order)) return null;
  const when = formatHeldUntilShort(order.heldUntil);
  const route = order.fixedRouteTemplateName ?? 'Fixed route';
  return when ? `Held for ${route} — ${when}` : `Held for ${route}`;
}

const DAY_OF_WEEK_API: Record<string, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
};

export const DAY_OF_WEEK_OPTIONS = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
] as const;

export type DayOfWeekName = (typeof DAY_OF_WEEK_OPTIONS)[number];

export function dayOfWeekToApiValue(day: DayOfWeekName): number {
  return DAY_OF_WEEK_API[day];
}

export function dayNameFromApiValue(value: number): DayOfWeekName | null {
  const entry = Object.entries(DAY_OF_WEEK_API).find(([, v]) => v === value);
  return entry ? (entry[0] as DayOfWeekName) : null;
}

export function daysOfWeekToApiValues(days: readonly DayOfWeekName[]): number[] {
  return days.map(dayOfWeekToApiValue);
}

export function toggleDayOfWeek(days: readonly DayOfWeekName[], day: DayOfWeekName): DayOfWeekName[] {
  return days.includes(day) ? days.filter((d) => d !== day) : [...days, day];
}

export function nextDateForDayOfWeek(dayName: string, fromDate = new Date()): string {
  return nextDateForDays([dayName], fromDate);
}

export function nextDateForDays(dayNames: readonly string[], fromDate = new Date()): string {
  if (dayNames.length === 0) return fromDate.toISOString().slice(0, 10);
  const current = fromDate.getDay();
  const offsets = dayNames
    .map((name) => DAY_OF_WEEK_API[name])
    .filter((target): target is number => target != null)
    .map((target) => (target - current + 7) % 7);
  if (offsets.length === 0) return fromDate.toISOString().slice(0, 10);
  const next = new Date(fromDate);
  next.setDate(next.getDate() + Math.min(...offsets));
  return next.toISOString().slice(0, 10);
}

export function formatDaysOfWeekList(days: readonly string[]): string {
  if (days.length === 0) return '—';
  if (days.length === 1) return `${days[0]}s`;
  if (days.length === 2) return `${days[0]}s and ${days[1]}s`;
  return `${days.slice(0, -1).join(', ')}s, and ${days[days.length - 1]}s`;
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
