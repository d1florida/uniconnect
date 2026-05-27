import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { RoboTaxiTrackingDto, RoboTaxiVehicleDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';

const stateColor: Record<string, string> = {
  OnTrip: 'On trip',
  Grounded: 'Grounded',
  Charging: 'Charging',
  Idle: 'Idle',
  Maintenance: 'Maintenance',
};

export function RoboTaxiFleetPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [vehicles, setVehicles] = useState<RoboTaxiVehicleDto[]>([]);
  const [tracking, setTracking] = useState<RoboTaxiTrackingDto[]>([]);

  useEffect(() => {
    if (!fleetId) return;
    api.get<RoboTaxiVehicleDto[]>(`/api/robo-taxis/tenants/${fleetId}/vehicles`).then(setVehicles);
    api.get<RoboTaxiTrackingDto[]>(`/api/robo-taxis/tenants/${fleetId}/tracking`).then(setTracking);
  }, [fleetId]);

  const markers = tracking
    .filter((t) => t.latestLocation)
    .map((t) => ({
      id: t.vehicleId,
      label: t.licensePlate,
      lat: t.latestLocation!.latitude,
      lng: t.latestLocation!.longitude,
      detail: stateColor[t.operationalState] ?? t.operationalState,
    }));

  return (
    <div>
      <div className="page-header"><h2>AV Fleet</h2></div>
      <FleetMap markers={markers} />
      <table>
        <thead><tr><th>Plate</th><th>State</th><th>Battery</th><th></th></tr></thead>
        <tbody>
          {vehicles.map((v) => (
            <tr key={v.id}>
              <td>{v.licensePlate}</td>
              <td>
                <span className={v.profile.operationalState === 'Grounded' ? 'badge badge-grounded' : 'badge badge-av'}>
                  {v.profile.operationalState}
                </span>
              </td>
              <td>{v.profile.batteryPercent ?? '—'}%</td>
              <td><Link to={`/robo-taxis/vehicles/${v.id}`}>Detail</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
