import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type { DeliveryDashboardDto, FleetDto } from '../../../api/types';

export function DeliveryDashboardPage() {
  const [dash, setDash] = useState<DeliveryDashboardDto | null>(null);
  const [fleets, setFleets] = useState<FleetDto[]>([]);

  useEffect(() => {
    api.get<DeliveryDashboardDto>('/api/delivery/dashboard').then(setDash);
    api.get<FleetDto[]>('/api/tenants?module=Delivery')
      .then((f) => setFleets(Array.isArray(f) ? f.map(normalizeFleetDto) : []));
  }, []);

  return (
    <div>
      <div className="page-header">
        <h2>Delivery</h2>
        <p>B2B and B2C logistics — autonomous and conventional</p>
      </div>
      {dash && (
        <div className="cards">
          <div className="card"><div className="value">{dash.openOrders}</div><div className="label">Open orders</div></div>
          <div className="card"><div className="value">{dash.inTransit}</div><div className="label">In transit</div></div>
          <div className="card"><div className="value">{dash.b2BOrders}</div><div className="label">B2B</div></div>
          <div className="card"><div className="value">{dash.b2COrders}</div><div className="label">B2C</div></div>
          <div className="card"><div className="value">{dash.autonomousActive}</div><div className="label">AV active</div></div>
          <div className="card"><div className="value">{dash.conventionalActive}</div><div className="label">Conventional active</div></div>
          <div className="card"><div className="value">{dash.activeRoutes}</div><div className="label">Routes in progress</div></div>
          <div className="card"><div className="value">{dash.plannedRoutes}</div><div className="label">Planned routes</div></div>
        </div>
      )}
      {fleets[0] && (
        <p>
          <Link to={`/delivery/fleets/${fleets[0].id}/routes`}>Routes</Link>
          {' · '}
          <Link to={`/delivery/fleets/${fleets[0].id}/orders`}>Orders</Link>
          {' · '}
          <Link to={`/delivery/fleets/${fleets[0].id}/map`}>Map</Link>
        </p>
      )}
    </div>
  );
}
