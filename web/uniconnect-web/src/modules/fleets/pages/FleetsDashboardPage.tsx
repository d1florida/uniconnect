import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type { FleetDashboardDto, FleetDto } from '../../../api/types';
import { Loading } from '../../../components/Loading';
import { formatContact, formatModules } from '../../../utils/fleetModules';
import { isDemoTenant } from '../../../utils/demoTenants';

export function FleetsDashboardPage() {
  const [dashboard, setDashboard] = useState<FleetDashboardDto | null>(null);
  const [fleets, setFleets] = useState<FleetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    Promise.all([
      api.get<FleetDashboardDto>('/api/tenants/dashboard'),
      api.get<FleetDto[]>('/api/tenants'),
    ])
      .then(([d, f]) => {
        setDashboard(d);
        setFleets(Array.isArray(f) ? f.map(normalizeFleetDto) : []);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed'))
      .finally(() => setLoading(false));
  }, []);

  const load = () =>
    Promise.all([
      api.get<FleetDashboardDto>('/api/tenants/dashboard'),
      api.get<FleetDto[]>('/api/tenants'),
    ]).then(([d, f]) => {
      setDashboard(d);
      setFleets(Array.isArray(f) ? f.map(normalizeFleetDto) : []);
    });

  const remove = async (fleet: FleetDto) => {
    if (isDemoTenant(fleet.id)) return;
    if (!window.confirm(`Delete tenant "${fleet.name}"? All vehicles, orders, and admin users for this tenant will be permanently removed.`)) return;
    try {
      setError('');
      await api.delete(`/api/tenants/${fleet.id}`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete');
    }
  };

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Tenants</h2>
        <Link to="/tenants/new" className="button">New tenant</Link>
      </div>
      {error && <p className="error">{error}</p>}
      {dashboard && (
        <div className="stats-row">
          <div className="stat"><strong>{dashboard.totalTenants}</strong><span>Total</span></div>
          <div className="stat"><strong>{dashboard.generalTenants}</strong><span>General Fleet</span></div>
          <div className="stat"><strong>{dashboard.roboTaxiTenants}</strong><span>Robo-Taxi</span></div>
          <div className="stat"><strong>{dashboard.deliveryTenants}</strong><span>Delivery</span></div>
        </div>
      )}
      <table>
        <thead>
          <tr><th>Name</th><th>Slug</th><th>Contact</th><th>Admin login</th><th>Modules</th><th></th></tr>
        </thead>
        <tbody>
          {fleets.map((f) => (
            <tr key={f.id}>
              <td>{f.name}</td>
              <td>{f.slug}</td>
              <td>{formatContact(f)}</td>
              <td>{f.adminEmail ?? '—'}</td>
              <td>{formatModules(f.modules)}</td>
              <td>
                <Link to={`/tenants/${f.id}`}>Manage</Link>
                {!isDemoTenant(f.id) && (
                  <>
                    {' · '}
                    <button type="button" className="secondary" onClick={() => remove(f)}>Delete</button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
