import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DeliveryOrderDto, RoutePlanRunDto } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { planRunStatusLabel, formatDriveMinutes } from '../deliveryLabels';
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
        {livePlan.computeDurationMs != null && <> · computed in {livePlan.computeDurationMs}ms</>}
      </p>

      {livePlan.ordersUnassigned > 0 && (
        <p className="plan-hint">
          Unassigned orders stay <strong>Ready</strong> — add more vehicles or run another plan.
        </p>
      )}

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

          return (
            <div key={index} className="plan-route-card">
              <button type="button" className="plan-route-header" onClick={() => toggleRoute(index)}>
                <span className="plan-route-title">
                  Route {index + 1}: {proposal.vehicleLabel ?? 'No vehicle assigned'}
                </span>
                <span className="muted">
                  {orderCount} order{orderCount === 1 ? '' : 's'} · {deliveryStops.length} stops
                  {formatDriveMinutes(proposal.estimatedMinutes)
                    ? ` · ${formatDriveMinutes(proposal.estimatedMinutes)} est. drive`
                    : ' · drive time unavailable'}
                </span>
                <span className="plan-route-chevron">{isOpen ? '▾' : '▸'}</span>
              </button>

              {isOpen && (
                <table className="plan-stops-table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Type</th>
                      <th>Address</th>
                      <th>Parcel</th>
                      <th>Order</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deliveryStops.map((stop) => {
                      const recipient = displayPlannedStopRecipient(stop, liveOrders);
                      const parcel = displayPlannedStopParcel(stop, liveOrders);
                      return (
                      <tr key={`${stop.sequence}-${stop.address}`}>
                        <td>{stop.sequence}</td>
                        <td>{stop.stopType}</td>
                        <td>
                          {displayPlannedStopAddress(stop, liveOrders)}
                          {recipient && (
                            <span className="muted"> · {recipient}</span>
                          )}
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
