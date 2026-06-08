import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type {
  AcceptPlanResultDto,
  DeliveryOrderDto,
  DeliveryVehicleDto,
  DepotDto,
  FixedRouteTemplateDto,
  PlanReadinessDto,
  RoutePlanRunDto,
} from '../../../api/types';
import { vehicleStatusLabel } from '../deliveryLabels';
import { vehicleSelectLabel } from '../../../utils/vehicleLabels';
import { useAuth } from '../../../auth/AuthContext';
import { Loading } from '../../../components/Loading';
import { hasModule } from '../../../utils/fleetModules';
import { PlanProposalReview, PlanRunStatusBadge } from '../components/PlanProposalReview';

type PlanMode = 'daily' | 'fixed';

export function DeliveryPlanPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const initialMode: PlanMode = searchParams.get('mode') === 'fixed' ? 'fixed' : 'daily';
  const initialTemplateId = searchParams.get('template') ?? '';

  const [planMode, setPlanMode] = useState<PlanMode>(initialMode);
  const [fixedRouteTemplateId, setFixedRouteTemplateId] = useState(initialTemplateId);
  const [templates, setTemplates] = useState<FixedRouteTemplateDto[]>([]);
  const [depotId, setDepotId] = useState('');
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [scheduledDate, setScheduledDate] = useState(new Date().toISOString().slice(0, 10));
  const [planRuns, setPlanRuns] = useState<RoutePlanRunDto[]>([]);
  const [readyOrders, setReadyOrders] = useState<DeliveryOrderDto[]>([]);
  const [allOrders, setAllOrders] = useState<DeliveryOrderDto[]>([]);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [readiness, setReadiness] = useState<PlanReadinessDto | null>(null);
  const [reviewPlan, setReviewPlan] = useState<RoutePlanRunDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [planning, setPlanning] = useState(false);
  const [accepting, setAccepting] = useState(false);
  const [error, setError] = useState('');
  const [acceptSuccess, setAcceptSuccess] = useState<AcceptPlanResultDto | null>(null);
  const [reviewLoadingId, setReviewLoadingId] = useState<string | null>(null);
  const reviewSectionRef = useRef<HTMLFieldSetElement>(null);
  const reviewPlanIdRef = useRef<string | null>(null);
  reviewPlanIdRef.current = reviewPlan?.id ?? null;

  const tenantId = fleetId ?? user?.fleetId;

  const fallbackReadiness = (
    orders: DeliveryOrderDto[],
    vehicleList: DeliveryVehicleDto[],
  ): PlanReadinessDto => {
    const ready = orders.filter((o) => o.status === 'Created');
    const active = vehicleList.filter((v) => (v.status ?? 'Active') === 'Active').length;
    return {
      readyOrderCount: ready.length,
      plannableOrderCount: ready.length,
      vehicleCount: vehicleList.length,
      activeVehicleCount: active,
      driverCount: 0,
      activeDriverCount: 0,
      canPlan: ready.length > 0 && active > 0,
      notes: active === 0
        ? ['No active vehicles assigned to this depot.']
        : ready.length === 0
          ? ['No orders in Ready status.']
          : ['Geocoding status unavailable until the API is restarted with the latest build.'],
    };
  };

  const selectedTemplate = templates.find((t) => t.id === fixedRouteTemplateId) ?? null;

  const loadDepotScoped = (
    selectedDepotId: string,
    ready: DeliveryOrderDto[],
    mode: PlanMode = planMode,
    templateId = fixedRouteTemplateId,
    routeDate = scheduledDate,
  ) => {
    if (!tenantId || !selectedDepotId) {
      setVehicles([]);
      setReadiness(fallbackReadiness(ready, []));
      return Promise.resolve();
    }
    const params = new URLSearchParams();
    params.set('depotId', selectedDepotId);
    params.set('scheduledDate', routeDate);
    if (mode === 'fixed' && templateId) {
      params.set('fixedRouteTemplateId', templateId);
    }
    const query = `?${params.toString()}`;
    return Promise.all([
      api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${tenantId}/vehicles${query}`),
      api.get<PlanReadinessDto>(`/api/route-planning/tenants/${tenantId}/readiness${query}`).catch(() => null),
    ]).then(([vehicleList, readinessDto]) => {
      setVehicles(vehicleList);
      setReadiness(readinessDto ?? fallbackReadiness(ready, vehicleList));
    });
  };

  const filterReadyOrders = (
    orders: DeliveryOrderDto[],
    mode: PlanMode,
    templateId: string,
    routeDate: string,
  ) => {
    if (mode === 'fixed' && templateId) {
      return orders.filter(
        (o) =>
          o.status === 'Created'
          && o.fixedRouteTemplateId === templateId
          && (!o.heldUntil || o.heldUntil <= routeDate),
      );
    }
    return orders.filter((o) => o.status === 'Created' && !o.fixedRouteTemplateId);
  };

  const loadInputs = async (overrides?: {
    mode?: PlanMode;
    templateId?: string;
    routeDate?: string;
  }) => {
    if (!tenantId) return;
    const [orders, depotList, templateList] = await Promise.all([
      api.get<DeliveryOrderDto[]>(`/api/delivery/tenants/${tenantId}/orders`),
      api.get<DepotDto[]>(`/api/delivery/tenants/${tenantId}/depots`),
      api.get<FixedRouteTemplateDto[]>(`/api/delivery/tenants/${tenantId}/fixed-route-templates`),
    ]);
    setAllOrders(orders);
    setDepots(depotList);
    setTemplates(templateList);

    const mode = overrides?.mode ?? planMode;
    let templateId = overrides?.templateId ?? fixedRouteTemplateId;
    let routeDate = overrides?.routeDate ?? scheduledDate;

    const activeTemplate =
      templateList.find((t) => t.id === templateId && t.isActive)
      ?? (mode === 'fixed' ? templateList.find((t) => t.isActive) ?? null : null);

    if (mode === 'fixed' && activeTemplate) {
      templateId = activeTemplate.id;
      setFixedRouteTemplateId(activeTemplate.id);
      routeDate = activeTemplate.nextRouteDate ?? routeDate;
      setScheduledDate(routeDate);
      setDepotId(activeTemplate.depotId);
    }

    const ready = filterReadyOrders(orders, mode, templateId, routeDate);
    setReadyOrders(ready);

    let effectiveDepotId = depotId;
    if (mode === 'fixed' && activeTemplate) {
      effectiveDepotId = activeTemplate.depotId;
    } else if (!effectiveDepotId && depotList.length > 0) {
      effectiveDepotId = (depotList.find((d) => d.isDefault) ?? depotList[0]).id;
      setDepotId(effectiveDepotId);
    }
    if (effectiveDepotId) {
      await loadDepotScoped(effectiveDepotId, ready, mode, templateId, routeDate);
    }
  };

  const loadRuns = () => {
    if (!tenantId) return Promise.resolve();
    return api.get<RoutePlanRunDto[]>(`/api/route-planning/tenants/${tenantId}/plan-runs`).then(setPlanRuns);
  };

  useEffect(() => {
    if (!tenantId) {
      setLoading(false);
      return;
    }
    Promise.all([loadRuns(), loadInputs()])
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [tenantId]);

  const scrollToReview = () => {
    requestAnimationFrame(() => {
      window.setTimeout(() => {
        reviewSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 200);
    });
  };

  useEffect(() => {
    if (reviewPlan?.status === 'Completed') scrollToReview();
  }, [reviewPlan?.id]);

  const reloadReviewData = () => {
    const planId = reviewPlanIdRef.current;
    if (!planId) return;
    void api.get<RoutePlanRunDto>(`/api/route-planning/plan-runs/${planId}`).then(setReviewPlan);
  };

  useEffect(() => {
    if (!tenantId || !reviewPlanIdRef.current) return;
    reloadReviewData();
  }, [tenantId, location.key]);

  useEffect(() => {
    if (!tenantId) return;

    const onVisible = () => {
      if (document.visibilityState === 'visible') reloadReviewData();
    };
    window.addEventListener('focus', reloadReviewData);
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      window.removeEventListener('focus', reloadReviewData);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [tenantId]);

  const switchPlanMode = (mode: PlanMode) => {
    setPlanMode(mode);
    setAcceptSuccess(null);
    setReviewPlan(null);
    const template = mode === 'fixed' ? templates.find((t) => t.isActive) : null;
    void loadInputs({
      mode,
      templateId: template?.id,
      routeDate: template?.nextRouteDate,
    }).catch((e) => setError(e instanceof Error ? e.message : 'Failed to reload'));
  };

  const onTemplateChange = (templateId: string) => {
    setFixedRouteTemplateId(templateId);
    const template = templates.find((t) => t.id === templateId);
    if (template) {
      setDepotId(template.depotId);
      if (template.nextRouteDate) setScheduledDate(template.nextRouteDate);
      const ready = filterReadyOrders(allOrders, 'fixed', templateId, template.nextRouteDate ?? scheduledDate);
      setReadyOrders(ready);
      void loadDepotScoped(
        template.depotId,
        ready,
        'fixed',
        templateId,
        template.nextRouteDate ?? scheduledDate,
      ).catch((err) => setError(err instanceof Error ? err.message : 'Failed to load template data'));
    }
  };

  const runPlan = async () => {
    if (!tenantId) return;
    setPlanning(true);
    setError('');
    setAcceptSuccess(null);
    try {
      const result = await api.post<RoutePlanRunDto>(`/api/route-planning/tenants/${tenantId}/plan`, {
        scheduledDate,
        depotId: depotId || null,
        depotAddress: null,
        orderIds: null,
        vehicleIds: null,
        maxStopsPerRoute: 25,
        fixedRouteTemplateId: planMode === 'fixed' && fixedRouteTemplateId ? fixedRouteTemplateId : null,
      });
      setReviewPlan(result);
      await loadRuns();
      await loadInputs();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Planning failed');
    } finally {
      setPlanning(false);
    }
  };

  const acceptPlan = async (planRunId: string) => {
    setAccepting(true);
    setError('');
    try {
      const result = await api.post<AcceptPlanResultDto>(
        `/api/route-planning/plan-runs/${planRunId}/accept`,
        { proposalVehicleIds: null },
      );
      setAcceptSuccess(result);
      setReviewPlan(null);
      await loadRuns();
      await loadInputs();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Accept failed');
    } finally {
      setAccepting(false);
    }
  };

  const discardPlan = async (planRunId: string) => {
    setError('');
    try {
      await api.post(`/api/route-planning/plan-runs/${planRunId}/discard`, { reason: 'Discarded from UI' });
      await loadRuns();
      setReviewPlan(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Discard failed');
    }
  };

  const openPlanForReview = async (planRunId: string) => {
    setReviewLoadingId(planRunId);
    setError('');
    setAcceptSuccess(null);
    try {
      const run = await api.get<RoutePlanRunDto>(`/api/route-planning/plan-runs/${planRunId}`);
      if (run.status !== 'Completed') {
        setError(`This plan is ${run.status.toLowerCase()} and can no longer be reviewed.`);
        setReviewPlan(null);
        return;
      }
      setReviewPlan(run);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load plan');
    } finally {
      setReviewLoadingId(null);
    }
  };

  if (!hasModule(user?.modules, 'RoutePlanning')) {
    return <p className="error">Route planning module is not enabled for your account.</p>;
  }
  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Plan routes</h2>
        <p className="muted">
          Turn <strong>ready orders</strong> into <strong>draft routes</strong> for review before dispatch.
          {' · '}
          <Link to="/delivery">← Delivery</Link>
          {tenantId && (
            <>
              {' · '}
              <Link to={`/delivery/fleets/${tenantId}/fixed-routes`}>Fixed routes</Link>
            </>
          )}
        </p>
      </div>

      <div className="tabs" style={{ marginBottom: '1rem' }}>
        <button type="button" className={planMode === 'daily' ? 'active' : ''} onClick={() => switchPlanMode('daily')}>
          Daily plan
        </button>
        <button
          type="button"
          className={planMode === 'fixed' ? 'active' : ''}
          onClick={() => switchPlanMode('fixed')}
          disabled={templates.filter((t) => t.isActive).length === 0}
        >
          Fixed route plan
        </button>
      </div>
      {error && <p className="error">{error}</p>}
      {(planning || reviewLoadingId) && (
        <p className="plan-hint" role="status">
          {planning ? 'Generating route plan…' : 'Loading plan for review…'}
        </p>
      )}

      {acceptSuccess && (
        <section className="success-banner">
          <h3>Plan accepted</h3>
          <p>
            Created {acceptSuccess.createdRoutes.length} draft route
            {acceptSuccess.createdRoutes.length === 1 ? '' : 's'} for {acceptSuccess.planRun.scheduledDate}.
          </p>
          <ul className="accept-route-links">
            {acceptSuccess.createdRoutes.map((r) => (
              <li key={r.id}>
                <Link to={`/delivery/routes/${r.id}`}>{r.name}</Link>
              </li>
            ))}
          </ul>
          <p className="muted">
            Next: assign a driver on each route, mark planned, then start.
            {tenantId && (
              <>
                {' '}
                <Link to={`/delivery/fleets/${tenantId}/routes`}>View all routes</Link>
              </>
            )}
          </p>
        </section>
      )}

      <fieldset className="module-picker">
        <legend>1. What will be planned</legend>
        {readiness && (
          <ul className="plan-readiness-list">
            <li className={readiness.readyOrderCount > 0 ? 'plan-readiness-ok' : 'plan-readiness-warn'}>
              <strong>{readiness.readyOrderCount}</strong> order{readiness.readyOrderCount === 1 ? '' : 's'} in{' '}
              <strong>Ready</strong> status
            </li>
            <li className={readiness.plannableOrderCount > 0 ? 'plan-readiness-ok' : 'plan-readiness-warn'}>
              <strong>{readiness.plannableOrderCount}</strong> order{readiness.plannableOrderCount === 1 ? '' : 's'}{' '}
              with geocoded pickup &amp; delivery addresses
            </li>
            <li className={readiness.activeVehicleCount > 0 ? 'plan-readiness-ok' : 'plan-readiness-warn'}>
              <strong>{readiness.activeVehicleCount}</strong> active vehicle{readiness.activeVehicleCount === 1 ? '' : 's'}{' '}
              at the selected depot
              {readiness.vehicleCount > readiness.activeVehicleCount && (
                <span className="muted">
                  {' '}
                  ({readiness.vehicleCount - readiness.activeVehicleCount} inactive at this depot)
                </span>
              )}
            </li>
            {planMode === 'daily' && readiness.activeDriverCount > 0 && (
              <li className={(readiness.workingDriverCount ?? 0) > 0 ? 'plan-readiness-ok' : 'plan-readiness-warn'}>
                <strong>{readiness.workingDriverCount ?? 0}</strong> of{' '}
                <strong>{readiness.activeDriverCount}</strong> active driver
                {(readiness.activeDriverCount ?? 0) === 1 ? '' : 's'} scheduled for{' '}
                <strong>{readiness.scheduledDate ?? scheduledDate}</strong>
                {(readiness.driversOffCount ?? 0) > 0 && (
                  <span className="muted">
                    {' '}
                    ({readiness.driversOffCount} off)
                  </span>
                )}
                {tenantId && (
                  <span className="muted">
                    {' '}
                    ·{' '}
                    <Link to={`/delivery/fleets/${tenantId}/driver-calendar`}>
                      Driver calendar
                    </Link>
                  </span>
                )}
              </li>
            )}
          </ul>
        )}
        {readiness?.notes.map((note) => (
          <p key={note} className="plan-hint">{note}</p>
        ))}
        {tenantId && (
          <p className="muted">
            Vehicles are assigned to a home depot under{' '}
            <Link to={`/delivery/fleets/${tenantId}/vehicles`}>Delivery → Assets</Link>. Planning uses{' '}
            <strong>Active</strong> vehicles at the depot selected below.
          </p>
        )}
        {readyOrders.length === 0 ? (
          <p className="plan-hint">
            {planMode === 'fixed'
              ? <>No held orders are due for this fixed route on the selected date.</>
              : <>No daily-ready orders in <strong>Ready</strong> status.</>}
            {tenantId && (
              <>
                {' '}
                <Link to={`/delivery/fleets/${tenantId}/orders`}>View orders</Link>
                {planMode === 'fixed' && (
                  <>
                    {' '}
                    or <Link to={`/delivery/fleets/${tenantId}/fixed-routes`}>configure fixed routes</Link>
                  </>
                )}
                .
              </>
            )}
          </p>
        ) : (
          <details className="plan-input-details">
            <summary>Preview ready orders ({readyOrders.length})</summary>
            <ul className="plan-order-preview">
              {readyOrders.map((o) => (
                <li key={o.id}>
                  <Link to={`/delivery/orders/${o.id}`}>{o.recipientName}</Link>
                  <span className="muted">
                    {' '}
                    · {o.parcelDescription || '—'} · {o.pickupAddress} → {o.deliveryAddress}
                  </span>
                </li>
              ))}
            </ul>
          </details>
        )}
        {vehicles.length > 0 && (
          <details className="plan-input-details">
            <summary>Preview vehicles at this depot ({vehicles.length})</summary>
            <ul className="plan-order-preview">
              {vehicles.map((v) => (
                <li key={v.id}>
                  {vehicleSelectLabel(v)}
                  {v.homeDepotName && <span className="muted"> · {v.homeDepotName}</span>}
                  {' · '}
                  <span className={(v.status ?? 'Active') === 'Active' ? 'badge badge-conv' : 'badge badge-muted'}>
                    {vehicleStatusLabel(v.status ?? 'Active')}
                  </span>
                  {(v.status ?? 'Active') === 'Active' ? (
                    <span className="muted"> · included in planning</span>
                  ) : (
                    <span className="muted"> · excluded until Active</span>
                  )}
                  {v.isAutonomous && <span className="muted"> · Autonomous</span>}
                </li>
              ))}
            </ul>
          </details>
        )}
      </fieldset>

      <fieldset className="module-picker">
        <legend>2. Generate plan</legend>
        <div className="form-row">
          {planMode === 'fixed' && (
            <label>
              Fixed route
              <select
                value={fixedRouteTemplateId}
                onChange={(e) => onTemplateChange(e.target.value)}
                style={{ minWidth: '14rem' }}
              >
                <option value="">— Select template —</option>
                {templates.filter((t) => t.isActive).map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name} ({t.heldOrderCount} held)
                  </option>
                ))}
              </select>
            </label>
          )}
          <label>
            Route date
            <input
              type="date"
              value={scheduledDate}
              onChange={(e) => {
                const nextDate = e.target.value;
                setScheduledDate(nextDate);
                const ready = filterReadyOrders(allOrders, planMode, fixedRouteTemplateId, nextDate);
                setReadyOrders(ready);
                if (depotId) {
                  void loadDepotScoped(depotId, ready, planMode, fixedRouteTemplateId, nextDate).catch((err) =>
                    setError(err instanceof Error ? err.message : 'Failed to reload readiness'),
                  );
                }
              }}
            />
          </label>
          <label>
            Depot
            {depots.length > 0 ? (
              <select
                value={depotId}
                disabled={planMode === 'fixed'}
                onChange={(e) => {
                  const id = e.target.value;
                  setDepotId(id);
                  void loadDepotScoped(id, readyOrders, planMode, fixedRouteTemplateId, scheduledDate).catch((err) =>
                    setError(err instanceof Error ? err.message : 'Failed to load depot data'),
                  );
                }}
                style={{ width: '100%' }}
              >
                {depots.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name} — {d.address}
                  </option>
                ))}
              </select>
            ) : (
              <span className="muted">
                {' '}
                No depots configured.{' '}
                {tenantId && <Link to={`/delivery/fleets/${tenantId}/depots`}>Add a depot</Link>}
              </span>
            )}
          </label>
          <button
            type="button"
            onClick={() => void runPlan()}
            disabled={
              planning
              || !depotId
              || !readiness?.canPlan
              || (planMode === 'fixed' && !fixedRouteTemplateId)
            }
          >
            {planning
              ? 'Planning…'
              : planMode === 'fixed' && selectedTemplate
                ? `Optimize ${selectedTemplate.name}`
                : 'Generate plan'}
          </button>
        </div>
        <p className="muted">
          {planMode === 'fixed'
            ? 'Uses held orders for the selected template on its route day. Depot and defaults come from the template.'
            : 'Uses geocoded addresses and drive-time estimates. Held fixed-route orders are excluded.'}
        </p>
      </fieldset>

      {reviewPlan && reviewPlan.status === 'Completed' && (
        <fieldset ref={reviewSectionRef} className="module-picker plan-review-panel">
          <legend>3. Review &amp; accept</legend>
          <PlanProposalReview
            plan={reviewPlan}
            orders={allOrders}
            tenantId={tenantId}
            accepting={accepting}
            onAccept={() => void acceptPlan(reviewPlan.id)}
            onDiscard={() => void discardPlan(reviewPlan.id)}
          />
        </fieldset>
      )}

      <section className="card">
        <h3>Recent plan runs</h3>
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>By</th>
              <th>Status</th>
              <th>Orders</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {planRuns.map((r) => (
              <tr key={r.id} className={reviewPlan?.id === r.id ? 'plan-run-selected' : undefined}>
                <td>{r.scheduledDate}</td>
                <td>{r.requestedByName}</td>
                <td><PlanRunStatusBadge status={r.status} /></td>
                <td>
                  {r.ordersPlanned}/{r.ordersRequested} planned
                  {r.ordersUnassigned > 0 && <span className="muted"> · {r.ordersUnassigned} unassigned</span>}
                </td>
                <td>
                  {r.status === 'Completed' && (
                    <>
                      <button
                        type="button"
                        onClick={() => void openPlanForReview(r.id)}
                        disabled={reviewLoadingId === r.id}
                      >
                        {reviewLoadingId === r.id ? 'Loading…' : 'Review'}
                      </button>{' '}
                      <button type="button" onClick={() => void acceptPlan(r.id)} disabled={accepting}>
                        Accept
                      </button>{' '}
                      <button type="button" className="secondary" onClick={() => void discardPlan(r.id)}>
                        Discard
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {planRuns.length === 0 && <p className="muted">No plan runs yet.</p>}
      </section>
    </div>
  );
}
