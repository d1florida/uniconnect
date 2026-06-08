import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { hasModule } from '../utils/fleetModules';
import '../App.css';

export function Layout() {
  const { user, logout } = useAuth();
  const fleetId = user?.fleetId;
  const isDriver = user?.isDriver ?? false;

  return (
    <div className="layout">
      <aside className="sidebar">
        <h1>UniConnect</h1>
        {user && (
          <div className="user-info">
            <strong>{user.displayName}</strong>
            <span className="muted">{user.fleetName ?? 'Platform admin'}</span>
          </div>
        )}
        <nav>
          {user?.isPlatformAdmin && (
            <NavLink to="/tenants">Tenants</NavLink>
          )}
          {(user?.isPlatformAdmin || hasModule(user?.modules, 'General')) && (
            <>
              <NavLink to="/" end>General Fleet</NavLink>
              {fleetId && (
                <>
                  <NavLink to={`/fleet/fleets/${fleetId}/vehicles`}>Assets</NavLink>
                  <NavLink to={`/fleet/fleets/${fleetId}/tracking`}>Fleet map</NavLink>
                </>
              )}
            </>
          )}
          {(user?.isPlatformAdmin || hasModule(user?.modules, 'RoboTaxi')) && (
            <>
              <NavLink to="/robo-taxis">Robo-Taxi</NavLink>
              {fleetId && hasModule(user?.modules, 'RoboTaxi') && (
                <NavLink to={`/robo-taxis/fleets/${fleetId}`}>AV fleet</NavLink>
              )}
            </>
          )}
          {(user?.isPlatformAdmin || hasModule(user?.modules, 'Delivery')) && (
            <>
              <NavLink to="/delivery">Delivery</NavLink>
              {fleetId && hasModule(user?.modules, 'Delivery') && (
                <>
                  {!isDriver && (
                    <>
                      <NavLink to={`/delivery/fleets/${fleetId}/orders`}>Orders</NavLink>
                      <NavLink to={`/delivery/fleets/${fleetId}/drivers`}>Drivers</NavLink>
                      <NavLink to={`/delivery/fleets/${fleetId}/vehicles`}>Assets</NavLink>
                      <NavLink to={`/delivery/fleets/${fleetId}/depots`}>Depots</NavLink>
                      <NavLink to={`/delivery/fleets/${fleetId}/customers`}>Customers</NavLink>
                      <NavLink to={`/delivery/fleets/${fleetId}/fixed-routes`}>Fixed routes</NavLink>
                      <NavLink to={`/delivery/fleets/${fleetId}/map`}>Delivery map</NavLink>
                    </>
                  )}
                  <NavLink to={`/delivery/fleets/${fleetId}/routes`}>Routes</NavLink>
                  <NavLink to={`/delivery/fleets/${fleetId}/driver-calendar`}>Calendar</NavLink>
                </>
              )}
              {fleetId && hasModule(user?.modules, 'RoutePlanning') && !isDriver && (
                <NavLink to={`/delivery/fleets/${fleetId}/plan`}>Plan routes</NavLink>
              )}
            </>
          )}
          {hasModule(user?.modules, 'Insights') && (
            <NavLink to="/insights">Insights</NavLink>
          )}
          {fleetId && !isDriver && (
            <NavLink to="/settings">Settings</NavLink>
          )}
        </nav>
        <button type="button" className="secondary logout-btn" onClick={logout}>
          Sign out
        </button>
      </aside>
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}
