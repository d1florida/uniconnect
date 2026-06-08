import { useState } from 'react';
import type { LocationMapTarget } from '../../../components/LocationMapModal';
import { LocationMapModal } from '../../../components/LocationMapModal';
import type { DeliveryOrderDto } from '../../../api/types';
import { formatGeocodeCoords, geocodeSourceLabel, hasOrderGeocode, isApproximateGeocode } from '../deliveryLabels';

interface GeocodeOverride {
  address?: string;
  lat?: number;
  lng?: number;
  pending?: boolean;
}

interface OrderGeocodeStatusProps {
  order: DeliveryOrderDto;
  compact?: boolean;
  pickupOverride?: GeocodeOverride;
}

function GeocodeLine({
  label,
  lat,
  lng,
  source,
  address,
  formattedAddress,
  onShowMap,
}: {
  label: string;
  lat?: number;
  lng?: number;
  source?: string;
  address?: string;
  formattedAddress?: string;
  onShowMap: (target: LocationMapTarget) => void;
}) {
  const coords = formatGeocodeCoords(lat, lng);
  const sourceLabel = geocodeSourceLabel(source);
  const approximate = isApproximateGeocode(source);
  const showFormatted = Boolean(
    formattedAddress?.trim()
    && address?.trim()
    && formattedAddress.trim().toLowerCase() !== address.trim().toLowerCase(),
  );
  const mapDetail = formattedAddress?.trim() || address;

  const openMap = () => {
    if (lat == null || lng == null) return;
    onShowMap({
      label,
      lat,
      lng,
      detail: mapDetail,
    });
  };

  return (
    <div className={coords ? 'order-geocode-line' : 'order-geocode-line order-geocode-pending'}>
      <span className="order-geocode-label">{label}</span>
      {coords ? (
        <span className="order-geocode-value">
          {address?.trim() && (
            <span className="order-geocode-address" title="Current saved address">
              {address}
            </span>
          )}
          <span
            className={approximate ? 'order-geocode-approx' : undefined}
            title={
              sourceLabel
                ? `${sourceLabel}${approximate ? ' — use a full street address for accurate routing' : ''}`
                : 'Geocoded for route planning'
            }
          >
            {coords}
            {sourceLabel && <span className="muted"> · {sourceLabel}</span>}
          </span>
          {showFormatted && (
            <span className="order-geocode-formatted muted" title="Standardized address from geocoder">
              {formattedAddress}
            </span>
          )}
          <button type="button" className="btn-map-link" onClick={openMap} title={`Show ${label} on map`}>
            Map
          </button>
        </span>
      ) : (
        <span className="muted">{address?.trim() ? `${address} · Pending geocode` : 'Pending'}</span>
      )}
    </div>
  );
}

export function OrderGeocodeStatus({ order, compact = false, pickupOverride }: OrderGeocodeStatusProps) {
  const [mapTarget, setMapTarget] = useState<LocationMapTarget | null>(null);
  const showMap = (target: LocationMapTarget) => setMapTarget(target);
  const closeMap = () => setMapTarget(null);
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
    <>
      <div className="order-geocode-block">
        <GeocodeLine
          label="Pickup"
          lat={pickupOverride?.lat ?? order.pickupLatitude}
          lng={pickupOverride?.lng ?? order.pickupLongitude}
          source={pickupOverride?.pending ? undefined : order.pickupGeocodeSource}
          address={pickupOverride?.address ?? order.pickupAddress}
          formattedAddress={pickupOverride?.pending ? undefined : order.pickupFormattedAddress}
          onShowMap={showMap}
        />
        {pickupOverride?.pending && (
          <p className="muted order-details-hint" style={{ marginTop: '0.25rem' }}>
            Pickup coordinates refresh when you save changes.
          </p>
        )}
        <GeocodeLine
          label="Delivery"
          lat={order.deliveryLatitude}
          lng={order.deliveryLongitude}
          source={order.deliveryGeocodeSource}
          address={order.deliveryAddress}
          formattedAddress={order.deliveryFormattedAddress}
          onShowMap={showMap}
        />
      </div>
      <LocationMapModal target={mapTarget} onClose={closeMap} />
    </>
  );
}
