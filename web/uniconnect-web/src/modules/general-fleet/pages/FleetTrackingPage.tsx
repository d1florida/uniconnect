import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { FleetVehicleTrackingDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';

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
      label: t.licensePlate,
      lat: t.latestLocation!.latitude,
      lng: t.latestLocation!.longitude,
      detail: `Status: ${t.status}`,
    }));

  return (
    <div>
      <div className="page-header"><h2>Fleet tracking</h2></div>
      <FleetMap markers={markers} />
    </div>
  );
}
