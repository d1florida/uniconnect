import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryOrderDto, PlanRunExplanationDto, RoutePlanRunDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { planRunStatusLabel, formatDriveMinutes, formatWorkMinutes } from '../deliveryLabels';
import { buildPlanMapMarkers, displayPlannedStopAddress, displayPlannedStopParcel, displayPlannedStopRecipient } from '../deliveryMapMarkers';

interface PlanProposalReviewProps {
  plan: RoutePlanRunDto;
  orders: DeliveryOrderDto[];
  tenantId?: string;
  accepting?: boolean;
  onAccept: () => void;
  onDiscard: () => void;
}

function orderLabel(orderId: string | undefined, orders: DeliveryOrderDto[]): string {
  if (!orderId) return '';
  const order = orders.find((o) => o.id === orderId);
  if (!order) return 'Order';
  return order.recipientName || order.deliveryAddress.slice(0, 32);
}

function formatCustomerWindow(stop: RoutePlanRunDto['proposals'][number]['stops'][number]): string | null {
  const parts: string[] = [];
  if (stop.deliveryOpenStart || stop.deliveryOpenEnd) {
    parts.push(`open ${stop.deliveryOpenStart ?? '—'}–${stop.deliveryOpenEnd ?? '—'}`);
  }
  if (stop.noDeliveryStart && stop.noDeliveryEnd) {
    parts.push(`no delivery ${stop.noDeliveryStart}–${stop.noDeliveryEnd}`);
  }
  return parts.length > 0 ? parts.join(', ') : null;
}

function explainEmptyPlan(plan: RoutePlanRunDto): string {
  if (plan.ordersRequested === 0) {
    return 'No orders were in Ready status when this plan was generated. Assigned or in-progress orders are not included.';
  }
  if (plan.ordersPlanned === 0) {
    return `${plan.ordersRequested} order(s) were ready, but none had geocoded pickup and delivery addresses. Check addresses on the Orders page and generate the plan again.`;
  }
  if (plan.proposalCount === 0 && plan.ordersUnassigned === plan.ordersRequested) {
    return 'Orders were geocoded but could not be grouped into routes. Confirm at least one asset is Active under Delivery → Assets (or General Fleet → Assets).';
  }
  return 'No draft routes were proposed. Review the readiness checklist above and try generating again.';
}

export function PlanProposalReview({ plan, orders, tenantId, accepting, onAccept, onDiscard }: PlanProposalReviewProps) {
  const [expanded, setExpanded] = useState<Record<number, boolean>>({});
  const [livePlan, setLivePlan] = useState(plan);
  const [liveOrders, setLiveOrders] = useState(orders);
  const [explanation, setExplanation] = useState<PlanRunExplanationDto | null>(null);
  const [explaining, setExplaining] = useState(false);

  useEffect(() => {
    setLivePlan(plan);
    setLiveOrders(orders);
  }, [plan, orders]);

  useEffect(() => {
    let cancelled = false;

    async function refresh() {
      try {
        const requests: [Promise<RoutePlanRunDto>, Promise<DeliveryOrderDto[] | null>] = [
          api.get<RoutePlanRunDto>(`/api/route-planning/plan-runs/${plan.id}`),
          tenantId
            ? api.get<DeliveryOrderDto[]>(`/api/delivery/tenants/${tenantId}/orders`)
            : Promise.resolve(null),
        ];
        const [freshPlan, freshOrders] = await Promise.all(requests);
        if (cancelled) return;
        setLivePlan(freshPlan);
        if (freshOrders) setLiveOrders(freshOrders);
      } catch {
        // Keep parent-provided snapshot when refresh fails.
      }
    }

    void refresh();
    return () => {
      cancelled = true;
    };
  }, [plan.id, tenantId]);

  const proposals = livePlan.proposals ?? [];
  const mapMarkers = buildPlanMapMarkers(livePlan, liveOrders);

  const toggleRoute = (index: number) => {
    setExpanded((prev) => ({ ...prev, [index]: !prev[index] }));
  };

  const dropoffCount = proposals.reduce(
    (sum, p) => sum + p.stops.filter((s) => s.stopType === 'Dropoff').length,
    0,
  );
  const totalDriveMinutes = proposals.reduce((sum, p) => sum + (p.estimatedMinutes ?? 0), 0);
  const totalDriveLabel = formatDriveMinutes(totalDriveMinutes);
  const totalWindowViolations = proposals.reduce((sum, p) => sum + (p.windowViolationCount ?? 0), 0);

  const loadExplanation = async () => {
    setExplaining(true);
    try {
      setExplanation(
        await api.post<PlanRunExplanationDto>(`/api/route-planning/plan-runs/${livePlan.id}/explain`, {}),
      );
    } catch {
      setExplanation({
        planRunId: livePlan.id,
        explanation: 'Could not generate an explanation for this plan.',
        usedAi: false,
      });
    } finally {
      setExplaining(false);
    }
  };

  return (
    <section className="card plan-proposal">
      <h3>Proposal review</h3>
      <p className="muted">
        {livePlan.proposalCount} draft route{livePlan.proposalCount === 1 ? '' : 's'} · {dropoffCount} order
        {dropoffCount === 1 ? '' : 's'} planned
        {totalDriveLabel && <> · {totalDriveLabel} total est. drive</>}
        {livePlan.ordersUnassigned > 0 && (
          <> · {livePlan.ordersUnassigned} order{livePlan.ordersUnassigned === 1 ? '' : 's'} could not be assigned</>
        )}
        {totalWindowViolations > 0 && (
          <> · {totalWindowViolations} customer window warning{totalWindowViolations === 1 ? '' : 's'}</>
        )}
        {livePlan.computeDurationMs != null && <> · computed in {livePlan.computeDurationMs}ms</>}
      </p>

      {livePlan.ordersUnassigned > 0 && (
        <p className="plan-hint">
          Unassigned orders stay <strong>Ready</strong> — add vehicles/drivers, extend shifts, or run another plan.
        </p>
      )}

      <div className="form-row" style={{ marginBottom: '0.75rem' }}>
        <button type="button" onClick={() => void loadExplanation()} disabled={explaining}>
          {explaining ? 'Explaining…' : 'Explain this plan'}
        </button>
        {explanation?.usedAi && <span className="muted">AI summary</span>}
      </div>
      {explanation && <p className="plan-hint">{explanation.explanation}</p>}

      {proposals.length === 0 ? (
        <p className="plan-hint">{explainEmptyPlan(livePlan)}</p>
      ) : (
      <>
      {mapMarkers.length > 0 && <FleetMap markers={mapMarkers} />}
      <div className="plan-routes">
        {proposals.map((proposal, index) => {
          const isOpen = expanded[index] ?? true;
          const deliveryStops = [...proposal.stops]
            .filter((s) => s.stopType !== 'Depot')
            .sort((a, b) => a.sequence - b.sequence);
          const orderCount = deliveryStops.filter((s) => s.stopType === 'Dropoff').length;

          const exceedsShift = proposal.shiftAvailableMinutes != null
            && proposal.estimatedMinutes > proposal.shiftAvailableMinutes;
          const routeWarnings = proposal.warnings ?? [];
          const violationCount = proposal.windowViolationCount ?? 0;

          return (
            <div key={index} className="plan-route-card">
              <button type="button" className="plan-route-header" onClick={() => toggleRoute(index)}>
                <span className="plan-route-title">
                  Route {index + 1}: {proposal.vehicleLabel ?? 'No vehicle assigned'}
                  {proposal.driverLabel ? ` · ${proposal.driverLabel}` : ''}
                </span>
                <span className="muted">
                  {orderCount} order{orderCount === 1 ? '' : 's'} · {deliveryStops.length} stops
                  {formatDriveMinutes(proposal.estimatedMinutes)
                    ? ` · ${formatDriveMinutes(proposal.estimatedMinutes)} est. drive`
                    : ' · drive time unavailable'}
                  {proposal.shiftWindow && proposal.shiftAvailableMinutes != null && (
                    <> · shift {proposal.shiftWindow} ({formatWorkMinutes(proposal.shiftAvailableMinutes)} avail.)</>
                  )}
                  {exceedsShift && ' · exceeds shift'}
                  {violationCount > 0 && ` · ${violationCount} window warning${violationCount === 1 ? '' : 's'}`}
                  {proposal.estimatedRouteStart && ` · depart ${proposal.estimatedRouteStart}`}
                </span>
                <span className="plan-route-chevron">{isOpen ? '▾' : '▸'}</span>
              </button>

              {isOpen && (
                <>
                {routeWarnings.length > 0 && (
                  <ul className="plan-window-warnings">
                    {routeWarnings.map((warning) => (
                      <li key={warning}>{warning}</li>
                    ))}
                  </ul>
                )}
                <table className="plan-stops-table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Type</th>
                      <th>ETA</th>
                      <th>Address</th>
                      <th>Parcel</th>
                      <th>Order</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deliveryStops.map((stop) => {
                      const recipient = displayPlannedStopRecipient(stop, liveOrders);
                      const parcel = displayPlannedStopParcel(stop, liveOrders);
                      const customerWindow = stop.stopType === 'Dropoff' ? formatCustomerWindow(stop) : null;
                      const stopWarnings = stop.windowWarnings ?? [];
                      return (
                      <tr key={`${stop.sequence}-${stop.address}`} className={stopWarnings.length > 0 ? 'plan-stop-warning' : undefined}>
                        <td>{stop.sequence}</td>
                        <td>{stop.stopType}</td>
                        <td>
                          {stop.estimatedArrival ?? '—'}
                          {stopWarnings.length > 0 && (
                            <span className="plan-stop-warning-label" title={stopWarnings.join(' ')}> !</span>
                          )}
                        </td>
                        <td>
                          {displayPlannedStopAddress(stop, liveOrders)}
                          {recipient && (
                            <span className="muted"> · {recipient}</span>
                          )}
                          {customerWindow && (
                            <div className="muted plan-stop-window">{customerWindow}</div>
                          )}
                          {stopWarnings.map((warning) => (
                            <div key={warning} className="plan-stop-warning-text">{warning}</div>
                          ))}
                        </td>
                        <td>{parcel ?? '—'}</td>
                        <td>
                          {stop.orderId ? (
                            <Link to={`/delivery/orders/${stop.orderId}`}>
                              {orderLabel(stop.orderId, liveOrders)}
                            </Link>
                          ) : (
                            '—'
                          )}
                        </td>
                      </tr>
                      );
                    })}
                  </tbody>
                </table>
                </>
              )}
            </div>
          );
        })}
      </div>
      </>
      )}

      <div className="plan-next-steps">
        <p className="muted">
          Accepting creates <strong>draft routes</strong>. Next: open each route, assign a driver, mark planned, then start.
        </p>
      </div>

      <div className="form-row">
        <button type="button" onClick={onAccept} disabled={accepting}>
          {accepting ? 'Creating routes…' : 'Accept → create draft routes'}
        </button>
        <button type="button" className="secondary" onClick={onDiscard} disabled={accepting}>
          Discard proposal
        </button>
      </div>
    </section>
  );
}

export function PlanRunStatusBadge({ status }: { status: RoutePlanRunDto['status'] }) {
  return <span className="badge badge-conv">{planRunStatusLabel(status)}</span>;
}
