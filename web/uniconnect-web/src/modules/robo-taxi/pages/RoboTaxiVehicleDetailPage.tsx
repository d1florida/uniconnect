import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { OperationalState, RoboTaxiVehicleDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';

export function RoboTaxiVehicleDetailPage() {
  const { vehicleId } = useParams<{ vehicleId: string }>();
  const [vehicle, setVehicle] = useState<RoboTaxiVehicleDto | null>(null);
  const [state, setState] = useState<OperationalState>('Idle');

  useEffect(() => {
    if (vehicleId) {
      api.get<RoboTaxiVehicleDto>(`/api/robo-taxis/vehicles/${vehicleId}`).then((v) => {
        setVehicle(v);
        setState(v.profile.operationalState);
      });
    }
  }, [vehicleId]);

  const updateState = async () => {
    await api.patch(`/api/robo-taxis/vehicles/${vehicleId}/state`, {
      operationalState: state,
      groundedReason: state === 'Grounded' ? 'SafetyReview' : 'None',
    });
    const v = await api.get<RoboTaxiVehicleDto>(`/api/robo-taxis/vehicles/${vehicleId}`);
    setVehicle(v);
  };

  const markers = vehicle?.latestLocation
    ? [{ id: vehicle.id, label: vehicle.licensePlate, lat: vehicle.latestLocation.latitude, lng: vehicle.latestLocation.longitude }]
    : [];

  return (
    <div>
      <div className="page-header">
        <h2>{vehicle?.licensePlate}</h2>
        <p>SW {vehicle?.profile.softwareVersion} · L4</p>
      </div>
      {vehicle?.profile.operationalState === 'Grounded' && (
        <div className="grounded-banner">Vehicle is grounded: {vehicle.profile.groundedReason}</div>
      )}
      <FleetMap markers={markers} />
      <div className="form-row">
        <select value={state} onChange={(e) => setState(e.target.value as OperationalState)}>
          {(['Idle', 'OnTrip', 'Charging', 'Maintenance', 'Grounded'] as OperationalState[]).map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <button onClick={updateState}>Update state</button>
      </div>
    </div>
  );
}
