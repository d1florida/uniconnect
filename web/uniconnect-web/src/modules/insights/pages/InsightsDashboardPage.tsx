import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import type {
  AnalyticsReportBundleDto,
  CustomerDto,
  DeliveryVehicleDto,
  DriverDto,
  PlannerSummaryDto,
  TenantDigestDto,
} from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { Loading } from '../../../components/Loading';
import { hasModule } from '../../../utils/fleetModules';
import { InsightsDateRangePicker } from '../components/InsightsDateRangePicker';
import { defaultInsightsRange, insightsRangeQuery, reportDownloadName } from '../insightsTypes';
import { vehicleSelectLabel } from '../../../utils/vehicleLabels';

export function InsightsDashboardPage() {
  const { user } = useAuth();
  const fleetId = user?.fleetId;
  const [range, setRange] = useState(defaultInsightsRange);
  const [digest, setDigest] = useState<TenantDigestDto | null>(null);
  const [drivers, setDrivers] = useState<DriverDto[]>([]);
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [planners, setPlanners] = useState<PlannerSummaryDto[]>([]);
  const [report, setReport] = useState<AnalyticsReportBundleDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const hasPlanning = hasModule(user?.modules, 'RoutePlanning');

  useEffect(() => {
    if (!fleetId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError('');
    const qs = insightsRangeQuery(range.from, range.to);
    const requests: Promise<void>[] = [
      api.get<TenantDigestDto>(`/api/insights/tenants/${fleetId}/digest?${qs}`).then(setDigest),
      api.get<DriverDto[]>(`/api/insights/tenants/${fleetId}/drivers`).then(setDrivers),
      api.get<CustomerDto[]>(`/api/insights/tenants/${fleetId}/customers`).then(setCustomers),
      api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${fleetId}/vehicles`).then(setVehicles),
      api
        .get<AnalyticsReportBundleDto>(`/api/insights/tenants/${fleetId}/reports/tenant.operations_digest?subjectId=${fleetId}&${qs}`)
        .then(setReport),
    ];
    if (hasPlanning) {
      requests.push(api.get<PlannerSummaryDto[]>(`/api/insights/tenants/${fleetId}/planners?${qs}`).then(setPlanners));
    }
    Promise.all(requests)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load insights'))
      .finally(() => setLoading(false));
  }, [fleetId, hasPlanning, range.from, range.to]);

  const downloadReport = () => {
    if (!report) return;
    const blob = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = reportDownloadName(report);
    a.click();
    URL.revokeObjectURL(url);
  };

  if (!hasModule(user?.modules, 'Insights')) {
    return <p className="error">Insights module is not enabled for your account.</p>;
  }
  if (loading) return <Loading />;
  if (!fleetId) return <p className="muted">Tenant context required.</p>;

  return (
    <div>
      <div className="page-header">
        <h2>Insights</h2>
        <p className="muted">
          {range.from} – {range.to} · LLM-ready JSON exports
        </p>
      </div>

      <InsightsDateRangePicker
        from={range.from}
        to={range.to}
        onChange={(from, to) => setRange({ from, to })}
      />

      {error && <p className="error">{error}</p>}

      {digest && (
        <section className="card">
          <h3>Operations digest</h3>
          <p>
            Delivered: {digest.delivery.ordersDelivered} · Failed: {digest.delivery.ordersFailed} · Stops completed:{' '}
            {digest.delivery.stopsCompleted} · Routes completed: {digest.delivery.routesCompleted}
          </p>
          {digest.planning && (
            <p>
              Plans requested: {digest.planning.plansRequested} · Accepted: {digest.planning.plansAccepted} · Accept rate:{' '}
              {(digest.planning.acceptRate * 100).toFixed(0)}%
            </p>
          )}
          {report && (
            <div className="form-row">
              <button type="button" onClick={downloadReport}>Download LLM report (JSON)</button>
            </div>
          )}
        </section>
      )}

      <section className="card">
        <h3>Drivers ({drivers.length})</h3>
        <ul>
          {drivers.map((d) => (
            <li key={d.id}>
              <Link to={`/insights/drivers/${d.id}`}>{d.displayName}</Link>
            </li>
          ))}
        </ul>
        {drivers.length === 0 && <p className="muted">No drivers yet.</p>}
      </section>

      <section className="card">
        <h3>Vehicles ({vehicles.length})</h3>
        <ul>
          {vehicles.map((v) => (
            <li key={v.id}>
              <Link to={`/insights/vehicles/${v.id}`}>
                {vehicleSelectLabel(v)}
              </Link>
              {v.isAutonomous && <span className="muted"> · Autonomous</span>}
            </li>
          ))}
        </ul>
        {vehicles.length === 0 && <p className="muted">No vehicles in this fleet.</p>}
      </section>

      <section className="card">
        <h3>Customers ({customers.length})</h3>
        <ul>
          {customers.map((c) => (
            <li key={c.id}>
              <Link to={`/insights/customers/${c.id}`}>{c.name}</Link>
              {(c.deliveryHours || c.deliveryAddress) && (
                <span className="muted">
                  {' — '}
                  {c.deliveryHours ?? c.deliveryAddress}
                </span>
              )}
            </li>
          ))}
        </ul>
        {customers.length === 0 && <p className="muted">No customers yet.</p>}
      </section>

      {hasPlanning && (
        <section className="card">
          <h3>Route planners ({planners.length})</h3>
          <ul>
            {planners.map((p) => (
              <li key={p.userId}>
                <Link to={`/insights/planners/${p.userId}`}>{p.displayName}</Link> — {p.plansRequested} plans,{' '}
                {(p.acceptRate * 100).toFixed(0)}% accepted
              </li>
            ))}
          </ul>
          {planners.length === 0 && <p className="muted">No planning activity in this range.</p>}
        </section>
      )}
    </div>
  );
}
