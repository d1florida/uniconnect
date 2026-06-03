import { useEffect, useState } from 'react';

import { Link } from 'react-router-dom';

import { api } from '../../../api/client';

import { normalizeFleetDto } from '../../../api/normalize';

import type { DeliveryDashboardDto, FleetDto } from '../../../api/types';

import { useAuth } from '../../../auth/AuthContext';

import { hasModule } from '../../../utils/fleetModules';



export function DeliveryDashboardPage() {

  const { user } = useAuth();

  const [dash, setDash] = useState<DeliveryDashboardDto | null>(null);

  const [fleets, setFleets] = useState<FleetDto[]>([]);



  useEffect(() => {

    api.get<DeliveryDashboardDto>('/api/delivery/dashboard').then(setDash);

    api.get<FleetDto[]>('/api/tenants?module=Delivery')

      .then((f) => setFleets(Array.isArray(f) ? f.map(normalizeFleetDto) : []));

  }, []);



  const tenantId = user?.fleetId ?? fleets[0]?.id;

  const hasPlanning = hasModule(user?.modules, 'RoutePlanning');



  return (

    <div>

      <div className="page-header">

        <h2>Delivery</h2>

        <p>Logistics — autonomous and conventional</p>

      </div>



      {dash && tenantId && (

        <section className="today-strip">

          <h3>Today</h3>

          <div className="today-strip-items">

            <Link to={`/delivery/fleets/${tenantId}/orders`} className="today-strip-item">

              <span className="value">{dash.ordersReadyToPlan ?? dash.openOrders}</span>

              <span className="label">orders ready to plan</span>

            </Link>

            <Link to={`/delivery/fleets/${tenantId}/routes`} className="today-strip-item">

              <span className="value">{dash.draftRoutes ?? 0}</span>

              <span className="label">draft routes</span>

            </Link>

            <Link to={`/delivery/fleets/${tenantId}/routes`} className="today-strip-item">

              <span className="value">{dash.routesNeedingDrivers ?? 0}</span>

              <span className="label">routes need drivers</span>

            </Link>

            {hasPlanning && (

              <Link to={`/delivery/fleets/${tenantId}/plan`} className="today-strip-item today-strip-action">

                <span className="value">→</span>

                <span className="label">Plan routes</span>

              </Link>

            )}

          </div>

        </section>

      )}



      {dash && (

        <div className="cards">

          <div className="card"><div className="value">{dash.ordersReadyToPlan ?? dash.openOrders}</div><div className="label">Ready orders</div></div>

          <div className="card"><div className="value">{dash.inTransit}</div><div className="label">In transit</div></div>

          <div className="card"><div className="value">{dash.totalOrders}</div><div className="label">Total orders</div></div>

          <div className="card"><div className="value">{dash.autonomousActive}</div><div className="label">AV active</div></div>

          <div className="card"><div className="value">{dash.conventionalActive}</div><div className="label">Conventional active</div></div>

          <div className="card"><div className="value">{dash.activeRoutes}</div><div className="label">Routes in progress</div></div>

          <div className="card"><div className="value">{dash.plannedRoutes}</div><div className="label">Planned routes</div></div>

        </div>

      )}

      {tenantId && (

        <p>

          {hasPlanning && (

            <>

              <Link to={`/delivery/fleets/${tenantId}/plan`}>Plan routes</Link>

              {' · '}

            </>

          )}

          <Link to={`/delivery/fleets/${tenantId}/routes`}>Routes</Link>

          {' · '}

          <Link to={`/delivery/fleets/${tenantId}/orders`}>Orders</Link>

          {' · '}

          <Link to={`/delivery/fleets/${tenantId}/drivers`}>Drivers</Link>
          {' · '}
          <Link to={`/delivery/fleets/${tenantId}/depots`}>Depots</Link>

          {' · '}

          <Link to={`/delivery/fleets/${tenantId}/map`}>Map</Link>

        </p>

      )}

    </div>

  );

}

