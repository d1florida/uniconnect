import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryTrackingDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';

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
      label: t.licensePlate,
      lat: t.latestLocation!.latitude,
      lng: t.latestLocation!.longitude,
      detail: `${t.isAutonomous ? 'AV' : 'Van'}${t.activeOrderStatus ? ` · ${t.activeOrderStatus}` : ''}`,
    }));

  return (
    <div>
      <div className="page-header"><h2>Delivery fleet map</h2></div>
      <FleetMap markers={markers} />
      <table>
        <thead><tr><th>Plate</th><th>Type</th><th>Active order</th></tr></thead>
        <tbody>
          {tracking.map((t) => (
            <tr key={t.vehicleId}>
              <td>{t.licensePlate}</td>
              <td><span className={t.isAutonomous ? 'badge badge-av' : 'badge badge-conv'}>{t.isAutonomous ? 'Autonomous' : 'Conventional'}</span></td>
              <td>{t.activeOrderStatus ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
