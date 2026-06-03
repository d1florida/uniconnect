import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryTrackingDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { assetCategoryLabel } from '../../../utils/assetLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';

export function DeliveryFleetMapPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [tracking, setTracking] = useState<DeliveryTrackingDto[]>([]);

  useEffect(() => {
    if (fleetId) api.get<DeliveryTrackingDto[]>(`/api/delivery/tenants/${fleetId}/tracking`).then(setTracking);
  }, [fleetId]);

  const markers = tracking
    .filter((t) => t.latestLocation)
    .map((t) => ({
      id: t.vehicleId,
      label: vehiclePrimaryLabel(t),
      lat: t.latestLocation!.latitude,
      lng: t.latestLocation!.longitude,
      detail: [
        assetCategoryLabel(t.category),
        t.isAutonomous ? 'AV' : null,
        t.activeOrderStatus ?? null,
      ].filter(Boolean).join(' · '),
    }));

  return (
    <div>
      <div className="page-header"><h2>Delivery fleet map</h2></div>
      <FleetMap markers={markers} />
      <table>
        <thead><tr><th>Vehicle ID</th><th>Plate</th><th>Type</th><th>Propulsion</th><th>Active order</th></tr></thead>
        <tbody>
          {tracking.map((t) => (
            <tr key={t.vehicleId}>
              <td><strong>{t.vehicleNumber}</strong></td>
              <td>{t.licensePlate}</td>
              <td>{assetCategoryLabel(t.category)}</td>
              <td>
                {t.isAutonomous && <span className="badge badge-av">AV</span>}
                {!t.isAutonomous && <span className="badge badge-conv">Conventional</span>}
              </td>
              <td>{t.activeOrderStatus ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
