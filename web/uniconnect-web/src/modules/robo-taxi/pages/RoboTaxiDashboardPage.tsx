import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type { FleetDto, RoboTaxiDashboardDto } from '../../../api/types';

export function RoboTaxiDashboardPage() {
  const [dash, setDash] = useState<RoboTaxiDashboardDto | null>(null);
  const [fleets, setFleets] = useState<FleetDto[]>([]);

  useEffect(() => {
    api.get<RoboTaxiDashboardDto>('/api/robo-taxis/dashboard').then(setDash);
    api.get<FleetDto[]>('/api/tenants?module=RoboTaxi')
      .then((f) => setFleets(Array.isArray(f) ? f.map(normalizeFleetDto) : []));
  }, []);

  return (
    <div>
      <div className="page-header">
        <h2>Robo-Taxi</h2>
        <p>Autonomous passenger fleet operations</p>
      </div>
      {dash && (
        <div className="cards">
          <div className="card"><div className="value">{dash.totalVehicles}</div><div className="label">Total AV</div></div>
          <div className="card"><div className="value">{dash.onTrip}</div><div className="label">On trip</div></div>
          <div className="card"><div className="value">{dash.grounded}</div><div className="label">Grounded</div></div>
          <div className="card"><div className="value">{dash.staleLocation}</div><div className="label">Stale GPS</div></div>
        </div>
      )}
      {fleets[0] && (
        <p>
          <Link to={`/robo-taxis/fleets/${fleets[0].id}`}>View {fleets[0].name}</Link>
        </p>
      )}
    </div>
  );
}
