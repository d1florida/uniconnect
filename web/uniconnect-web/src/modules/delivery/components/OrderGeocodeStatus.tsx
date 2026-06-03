import type { DeliveryOrderDto } from '../../../api/types';
import { formatGeocodeCoords, geocodeSourceLabel, hasOrderGeocode, isApproximateGeocode } from '../deliveryLabels';

interface OrderGeocodeStatusProps {
  order: DeliveryOrderDto;
  compact?: boolean;
}

function GeocodeLine({
  label,
  lat,
  lng,
  source,
}: {
  label: string;
  lat?: number;
  lng?: number;
  source?: string;
}) {
  const coords = formatGeocodeCoords(lat, lng);
  const sourceLabel = geocodeSourceLabel(source);
  const approximate = isApproximateGeocode(source);

  return (
    <div className={coords ? 'order-geocode-line' : 'order-geocode-line order-geocode-pending'}>
      <span className="order-geocode-label">{label}</span>
      {coords ? (
        <span
          className={approximate ? 'order-geocode-approx' : undefined}
          title={
            sourceLabel
              ? `${sourceLabel}${approximate ? ' — use a full street address for accurate routing' : ''}`
              : 'Geocoded for route planning'
          }
        >
          {coords}
          {sourceLabel && approximate && <span className="muted"> · {sourceLabel}</span>}
        </span>
      ) : (
        <span className="muted">Pending</span>
      )}
    </div>
  );
}

export function OrderGeocodeStatus({ order, compact = false }: OrderGeocodeStatusProps) {
  const pickupOk = hasOrderGeocode(order, 'pickup');
  const deliveryOk = hasOrderGeocode(order, 'delivery');
  const pickupApprox = isApproximateGeocode(order.pickupGeocodeSource);
  const deliveryApprox = isApproximateGeocode(order.deliveryGeocodeSource);

  if (compact) {
    const pickupCoords = formatGeocodeCoords(order.pickupLatitude, order.pickupLongitude);
    const deliveryCoords = formatGeocodeCoords(order.deliveryLatitude, order.deliveryLongitude);
    if (pickupOk && deliveryOk) {
      if (pickupApprox || deliveryApprox) {
        return (
          <span
            className="order-geocode-compact order-geocode-approx"
            title={`Pickup: ${pickupCoords}\nDelivery: ${deliveryCoords}\nSome coordinates are approximate.`}
          >
            Approximate
          </span>
        );
      }
      return (
        <span className="order-geocode-compact" title={`Pickup: ${pickupCoords}\nDelivery: ${deliveryCoords}`}>
          Geocoded
        </span>
      );
    }
    if (pickupOk || deliveryOk) {
      return <span className="order-geocode-compact order-geocode-partial">Partial</span>;
    }
    return <span className="order-geocode-compact order-geocode-pending">Pending</span>;
  }

  return (
    <div className="order-geocode-block">
      <GeocodeLine
        label="Pickup"
        lat={order.pickupLatitude}
        lng={order.pickupLongitude}
        source={order.pickupGeocodeSource}
      />
      <GeocodeLine
        label="Delivery"
        lat={order.deliveryLatitude}
        lng={order.deliveryLongitude}
        source={order.deliveryGeocodeSource}
      />
    </div>
  );
}
