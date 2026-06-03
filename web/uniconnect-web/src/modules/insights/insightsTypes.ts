import type {
  AnalyticsReportBundleDto,
  CustomerDto,
  DriverDto,
  FleetModule,
  PlannerSummaryDto,
  SubjectSummaryDto,
  TenantDigestDto,
} from '../../api/types';

export type InsightsApi = {
  digest: TenantDigestDto;
  drivers: DriverDto[];
  customers: CustomerDto[];
  planners: PlannerSummaryDto[];
};

export function defaultInsightsRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

export function insightsRangeQuery(from: string, to: string): string {
  return `from=${from}&to=${to}`;
}

/** Human-readable short label for an event type slug. */
export function formatEventTypeLabel(eventType: string): string {
  const last = eventType.split('.').pop() ?? eventType;
  return last.replace(/_/g, ' ');
}

/** Parse metrics JSON into a compact display string. */
export function formatEventMetrics(metricsJson: string): string | null {
  if (!metricsJson || metricsJson === '{}') return null;
  try {
    const m = JSON.parse(metricsJson) as Record<string, unknown>;
    const parts: string[] = [];
    if (m.automationMode != null) parts.push(String(m.automationMode));
    if (m.ordersPlanned != null) parts.push(`${m.ordersPlanned} orders planned`);
    if (m.computeDurationMs != null) parts.push(`${m.computeDurationMs}ms compute`);
    if (m.routesCreated != null) parts.push(`${m.routesCreated} routes created`);
    if (m.estimatedMinutesBefore != null && m.estimatedMinutesAfter != null) {
      parts.push(`drive ${m.estimatedMinutesBefore}→${m.estimatedMinutesAfter} min`);
    }
    if (parts.length === 0) {
      const keys = Object.keys(m).slice(0, 3);
      if (keys.length === 0) return null;
      return keys.map((k) => `${k}: ${String(m[k])}`).join(', ');
    }
    return parts.join(' · ');
  } catch {
    return null;
  }
}

export function reportDownloadName(report: AnalyticsReportBundleDto): string {
  return `${report.reportType}-${report.subject.id}-${report.from}.json`;
}

export const INSIGHTS_SUBJECT_TYPES: { type: string; label: string; module?: FleetModule }[] = [
  { type: 'driver', label: 'Drivers' },
  { type: 'vehicle', label: 'Vehicles' },
  { type: 'customer', label: 'Customers' },
  { type: 'planner', label: 'Planners', module: 'RoutePlanning' },
];

export type SubjectSummary = SubjectSummaryDto | PlannerSummaryDto;
