import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { FleetVehicleTrackingDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { assetCategoryLabel } from '../../../utils/assetLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';

export function FleetTrackingPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [tracking, setTracking] = useState<FleetVehicleTrackingDto[]>([]);

  useEffect(() => {
    if (fleetId) {
      api.get<FleetVehicleTrackingDto[]>(`/api/fleet/tenants/${fleetId}/tracking`).then(setTracking);
    }
  }, [fleetId]);

  const markers = tracking
    .filter((t) => t.latestLocation)
    .map((t) => ({
      id: t.vehicleId,
      label: vehiclePrimaryLabel(t),
      lat: t.latestLocation!.latitude,
      lng: t.latestLocation!.longitude,
      detail: `${assetCategoryLabel(t.category)} · ${t.status}`,
    }));

  return (
    <div>
      <div className="page-header"><h2>Fleet tracking</h2></div>
      <FleetMap markers={markers} />
      <table>
        <thead><tr><th>Vehicle ID</th><th>Plate</th><th>Type</th><th>Status</th></tr></thead>
        <tbody>
          {tracking.map((t) => (
            <tr key={t.vehicleId}>
              <td><strong>{t.vehicleNumber}</strong></td>
              <td>{t.licensePlate}</td>
              <td>{assetCategoryLabel(t.category)}</td>
              <td>{t.status}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
