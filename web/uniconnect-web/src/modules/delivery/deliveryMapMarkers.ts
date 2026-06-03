import type { DeliveryOrderDto, DeliveryRouteStopDto, PlannedStopDto, RoutePlanRunDto } from '../../api/types';
import type { MapMarker } from '../../components/FleetMap';

export function coordsForPlannedStop(
  stop: PlannedStopDto,
  orders: DeliveryOrderDto[],
): { lat?: number; lng?: number } {
  if (stop.latitude != null && stop.longitude != null) {
    return { lat: stop.latitude, lng: stop.longitude };
  }

  if (stop.orderId) {
    const order = orders.find((o) => o.id === stop.orderId);
    if (order) {
      if (stop.stopType === 'Pickup' && order.pickupLatitude != null && order.pickupLongitude != null) {
        return { lat: order.pickupLatitude, lng: order.pickupLongitude };
      }
      if (stop.stopType === 'Dropoff' && order.deliveryLatitude != null && order.deliveryLongitude != null) {
        return { lat: order.deliveryLatitude, lng: order.deliveryLongitude };
      }
    }
  }

  return {};
}

export function displayPlannedStopAddress(stop: PlannedStopDto, orders: DeliveryOrderDto[]): string {
  if (stop.address?.trim()) return stop.address;

  if (!stop.orderId) return stop.address;
  const order = orders.find((o) => o.id === stop.orderId);
  if (!order) return stop.address;
  if (stop.stopType === 'Pickup') return order.pickupAddress;
  if (stop.stopType === 'Dropoff') return order.deliveryAddress;
  return stop.address;
}

export function displayPlannedStopRecipient(stop: PlannedStopDto, orders: DeliveryOrderDto[]): string | undefined {
  if (stop.recipientName?.trim()) return stop.recipientName;
  if (!stop.orderId) return stop.recipientName;
  const order = orders.find((o) => o.id === stop.orderId);
  return order?.recipientName ?? stop.recipientName;
}

export function displayPlannedStopParcel(stop: PlannedStopDto, orders: DeliveryOrderDto[]): string | undefined {
  if (stop.parcelDescription?.trim()) return stop.parcelDescription;
  if (!stop.orderId) return stop.parcelDescription;
  const order = orders.find((o) => o.id === stop.orderId);
  return order?.parcelDescription ?? stop.parcelDescription;
}

export function buildPlanMapMarkers(plan: RoutePlanRunDto, orders: DeliveryOrderDto[]): MapMarker[] {
  const markers: MapMarker[] = [];

  if (plan.depotLatitude != null && plan.depotLongitude != null) {
    markers.push({
      id: 'depot',
      label: 'Depot',
      lat: plan.depotLatitude,
      lng: plan.depotLongitude,
      detail: plan.depotAddress,
    });
  }

  plan.proposals.forEach((proposal, routeIndex) => {
    proposal.stops.forEach((stop) => {
      const { lat, lng } = coordsForPlannedStop(stop, orders);
      if (lat == null || lng == null) return;

      markers.push({
        id: `${routeIndex}-${stop.sequence}-${stop.address}`,
        label: `R${routeIndex + 1} #${stop.sequence} ${stop.stopType}`,
        lat,
        lng,
        detail: displayPlannedStopAddress(stop, orders),
      });
    });
  });

  return markers;
}

export function buildRouteStopMarkers(stops: DeliveryRouteStopDto[]): MapMarker[] {
  const markers: MapMarker[] = [];
  for (const stop of stops) {
    if (stop.latitude == null || stop.longitude == null) continue;
    markers.push({
      id: stop.id,
      label:
        stop.stopType === 'Depot'
          ? 'Depot'
          : `#${stop.sequence} ${stop.recipientName ?? stop.address.slice(0, 24)}`,
      lat: stop.latitude,
      lng: stop.longitude,
      detail: `${stop.stopType} · ${stop.status}`,
    });
  }
  return markers;
}
