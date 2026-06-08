import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { AnalyticsReportBundleDto, PlannerSummaryDto, SubjectSummaryDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { Loading } from '../../../components/Loading';
import { hasModule } from '../../../utils/fleetModules';
import { InsightsDateRangePicker } from '../components/InsightsDateRangePicker';
import { InsightsEventTimeline } from '../components/InsightsEventTimeline';
import { defaultInsightsRange, formatEventMetrics, insightsRangeQuery, reportDownloadName } from '../insightsTypes';

type SubjectKind = 'drivers' | 'customers' | 'planners' | 'vehicles';

const REPORT_TYPES: Record<SubjectKind, string> = {
  drivers: 'driver.performance',
  customers: 'customer.service',
  planners: 'planner.activity',
  vehicles: 'vehicle.performance',
};

export function InsightsSubjectPage({ kind }: { kind: SubjectKind }) {
  const params = useParams();
  const subjectId = params.driverId ?? params.customerId ?? params.plannerId ?? params.vehicleId ?? '';
  const { user } = useAuth();
  const fleetId = user?.fleetId;
  const [range, setRange] = useState(defaultInsightsRange);
  const [summary, setSummary] = useState<SubjectSummaryDto | PlannerSummaryDto | null>(null);
  const [report, setReport] = useState<AnalyticsReportBundleDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!fleetId || !subjectId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    const qs = insightsRangeQuery(range.from, range.to);
    const summaryPath =
      kind === 'planners'
        ? `/api/insights/tenants/${fleetId}/planners/${subjectId}/summary?${qs}`
        : kind === 'drivers'
          ? `/api/insights/tenants/${fleetId}/drivers/${subjectId}/summary?${qs}`
          : kind === 'customers'
            ? `/api/insights/tenants/${fleetId}/customers/${subjectId}/summary?${qs}`
            : `/api/insights/tenants/${fleetId}/vehicles/${subjectId}/summary?${qs}`;

    Promise.all([
      api.get<SubjectSummaryDto | PlannerSummaryDto>(summaryPath).then(setSummary),
      api
        .get<AnalyticsReportBundleDto>(
          `/api/insights/tenants/${fleetId}/reports/${REPORT_TYPES[kind]}?subjectId=${subjectId}&${qs}`,
        )
        .then(setReport),
    ])
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [fleetId, subjectId, kind, range.from, range.to]);

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

  const timelineProps = fleetId && subjectId
    ? {
        tenantId: fleetId,
        from: range.from,
        to: range.to,
        ...(kind === 'drivers' && { driverId: subjectId }),
        ...(kind === 'vehicles' && { vehicleId: subjectId }),
        ...(kind === 'customers' && { customerId: subjectId }),
        ...(kind === 'planners' && { userId: subjectId, domain: 'route_planning' }),
      }
    : null;

  if (!hasModule(user?.modules, 'Insights')) {
    return <p className="error">Insights module is not enabled.</p>;
  }
  if (loading) return <Loading />;
  if (!summary) return <p className="error">{error || 'Not found'}</p>;

  const title =
    'displayName' in summary
      ? summary.displayName
      : 'label' in summary
        ? summary.label
        : 'Subject';

  return (
    <div>
      <div className="page-header">
        <h2>{title}</h2>
        <p className="muted">
          {range.from} – {range.to} · <Link to="/insights">← Insights</Link>
        </p>
      </div>

      <InsightsDateRangePicker
        from={range.from}
        to={range.to}
        onChange={(from, to) => setRange({ from, to })}
      />

      {error && <p className="error">{error}</p>}

      {'kpis' in summary && (
        <section className="card">
          <h3>Summary</h3>
          <p>
            Events: {summary.kpis.eventCount} · Delivered: {summary.kpis.ordersDelivered} · Failed:{' '}
            {summary.kpis.ordersFailed} · Stops: {summary.kpis.stopsCompleted} · Routes: {summary.kpis.routesCompleted}
          </p>
        </section>
      )}

      {'plansRequested' in summary && (
        <section className="card">
          <h3>Planning activity</h3>
          <p>
            Plans requested: {summary.plansRequested} · Accepted: {summary.plansAccepted} · Discarded:{' '}
            {summary.plansDiscarded} · Orders planned: {summary.ordersPlanned}
          </p>
        </section>
      )}

      {report && (
        <section className="card">
          <h3>LLM report bundle</h3>
          <p className="muted">{report.executiveSummary}</p>
          <button type="button" onClick={downloadReport}>Download JSON</button>
        </section>
      )}

      {'notableEvents' in summary && summary.notableEvents.length > 0 && (
        <section className="card">
          <h3>Highlights</h3>
          <ul>
            {summary.notableEvents.map((e) => {
              const metrics = formatEventMetrics(e.metricsJson);
              return (
                <li key={e.id}>
                  <strong>{new Date(e.occurredAt).toLocaleString()}</strong> — {e.narrative ?? e.eventType}
                  {metrics && <span className="muted"> · {metrics}</span>}
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {timelineProps && (
        <section className="card">
          <h3>Event timeline</h3>
          <InsightsEventTimeline {...timelineProps} />
        </section>
      )}
    </div>
  );
}
