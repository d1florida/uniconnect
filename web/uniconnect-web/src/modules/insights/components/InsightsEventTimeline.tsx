import { useEffect, useState } from 'react';
import { api } from '../../../api/client';
import type { OperationalEventDto } from '../../../api/types';
import { Loading } from '../../../components/Loading';
import { formatEventMetrics, formatEventTypeLabel } from '../insightsTypes';

interface InsightsEventTimelineProps {
  tenantId: string;
  from: string;
  to: string;
  driverId?: string;
  vehicleId?: string;
  customerId?: string;
  userId?: string;
  domain?: string;
  limit?: number;
}

export function InsightsEventTimeline({
  tenantId,
  from,
  to,
  driverId,
  vehicleId,
  customerId,
  userId,
  domain,
  limit = 50,
}: InsightsEventTimelineProps) {
  const [events, setEvents] = useState<OperationalEventDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    setLoading(true);
    setError('');
    const params = new URLSearchParams({ from, to });
    if (domain) params.set('domain', domain);
    if (driverId) params.set('driverId', driverId);
    if (vehicleId) params.set('vehicleId', vehicleId);
    if (customerId) params.set('customerId', customerId);
    if (userId) params.set('userId', userId);

    api
      .get<OperationalEventDto[]>(`/api/insights/tenants/${tenantId}/events?${params}`)
      .then((rows) => setEvents(rows.slice(0, limit)))
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load events'))
      .finally(() => setLoading(false));
  }, [tenantId, from, to, driverId, vehicleId, customerId, userId, domain, limit]);

  if (loading) return <Loading />;
  if (error) return <p className="error">{error}</p>;
  if (events.length === 0) return <p className="muted">No events in this date range.</p>;

  return (
    <table className="insights-events-table">
      <thead>
        <tr>
          <th>When</th>
          <th>Type</th>
          <th>Details</th>
        </tr>
      </thead>
      <tbody>
        {events.map((e) => {
          const metrics = formatEventMetrics(e.metricsJson);
          return (
            <tr key={e.id}>
              <td>{new Date(e.occurredAt).toLocaleString()}</td>
              <td>
                <span className="badge badge-conv">{formatEventTypeLabel(e.eventType)}</span>
              </td>
              <td>
                {e.narrative ?? e.eventType}
                {metrics && <span className="muted"> · {metrics}</span>}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
