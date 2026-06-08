import { useCallback, useEffect, useMemo, useState } from 'react';

import { Link, useParams } from 'react-router-dom';

import {

  Calendar,

  dateFnsLocalizer,

  type EventProps,

  type SlotInfo,

  type View,

} from 'react-big-calendar';

import {

  addDays,

  endOfMonth,

  endOfWeek,

  format,

  getDay,

  parse,

  startOfMonth,

  startOfWeek,

} from 'date-fns';

import { enUS } from 'date-fns/locale';

import 'react-big-calendar/lib/css/react-big-calendar.css';

import { api } from '../../../api/client';

import type {

  BulkUpsertDriverScheduleExceptionRequest,

  CreateDriverScheduleRequestRequest,

  DriverCalendarDayDto,

  DriverCalendarDto,

  DriverScheduleRequestDto,

  UpsertDriverScheduleExceptionRequest,

} from '../../../api/types';

import { useAuth } from '../../../auth/AuthContext';

import { ErrorAlert } from '../../../components/ErrorAlert';

import { Loading } from '../../../components/Loading';

import { hasModule } from '../../../utils/fleetModules';

import {

  buildDriverScheduleEvents,

  driverColor,

  type DriverScheduleEvent,

} from '../driverCalendarEvents';

import { formatWorkMinutes } from '../deliveryLabels';



const locales = { 'en-US': enUS };

const localizer = dateFnsLocalizer({

  format,

  parse,

  startOfWeek,

  getDay,

  locales,

});



function toDateOnly(d: Date): string {

  return format(d, 'yyyy-MM-dd');

}

function slotDateRange(slot: SlotInfo): { from: string; to: string } {

  const from = toDateOnly(slot.start);

  let end = slot.end;

  if (end > slot.start && end.getHours() === 0 && end.getMinutes() === 0 && end.getSeconds() === 0) {

    end = addDays(end, -1);

  }

  const to = toDateOnly(end);

  return { from, to: to < from ? from : to };

}



function calendarMonthPaddingRange(year: number, month: number): { from: string; to: string } {

  const first = new Date(year, month, 1);

  const last = new Date(year, month + 1, 0);

  const padStart = first.getDay();

  const padEnd = 6 - last.getDay();

  return {

    from: toDateOnly(addDays(first, -padStart)),

    to: toDateOnly(addDays(last, padEnd)),

  };

}



function loadRangeForView(date: Date, view: View): { from: string; to: string } {

  if (view === 'month') {

    return calendarMonthPaddingRange(date.getFullYear(), date.getMonth());

  }

  if (view === 'week' || view === 'work_week') {

    const start = startOfWeek(date);

    const end = endOfWeek(date);

    return { from: toDateOnly(start), to: toDateOnly(end) };

  }

  if (view === 'day') {

    const d = toDateOnly(date);

    return { from: d, to: d };

  }

  const start = startOfMonth(date);

  const end = endOfMonth(date);

  return { from: toDateOnly(start), to: toDateOnly(end) };

}



type EditCell = {

  driverId: string;

  driverName: string;

  day: DriverCalendarDayDto;

};



const emptyDay = (): DriverCalendarDayDto => ({

  date: '',

  isWorking: true,

  availableWorkMinutes: 0,

  source: 'pattern',

  hasException: false,

  fixedRoutes: [],

  assignedRoutes: [],

});



const emptyExceptionForm = (day: DriverCalendarDayDto) => ({

  isWorking: day.isWorking,

  shiftStartTime: day.shiftStartTime ?? '07:00',

  shiftEndTime: day.shiftEndTime ?? '17:00',

  lunchMinutes: '30',

  breakMinutes: '15',

  maxRouteMinutes: day.maxRouteMinutes != null ? String(day.maxRouteMinutes) : '',

  returnByTime: day.returnByTime ?? '',

  offBlockStartTime: day.offBlockStartTime?.slice(0, 5) ?? '',

  offBlockEndTime: day.offBlockEndTime?.slice(0, 5) ?? '',

  note: day.note ?? '',

});



function parseDateOnly(value: string): Date {

  const [y, m, d] = value.split('-').map(Number);

  return new Date(y, m - 1, d);

}



function ScheduleEvent({ event }: EventProps<DriverScheduleEvent>) {

  return (

    <span className="rbc-event-inner-custom" title={event.title}>

      {event.title}

    </span>

  );

}



export function DeliveryDriverCalendarPage() {

  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();

  const { user } = useAuth();

  const fleetId = fleetIdParam ?? user?.fleetId;

  const isAdmin = user?.isTenantAdmin ?? user?.isPlatformAdmin ?? false;

  const isDriver = user?.isDriver ?? false;



  const [calendarDate, setCalendarDate] = useState(() => new Date());

  const [calendarView, setCalendarView] = useState<View>('week');

  const [driverFilter, setDriverFilter] = useState<string>(() => user?.driverId ?? 'all');

  const [calendar, setCalendar] = useState<DriverCalendarDto | null>(null);

  const [pendingRequests, setPendingRequests] = useState<DriverScheduleRequestDto[]>([]);

  const [loading, setLoading] = useState(true);

  const [saving, setSaving] = useState(false);

  const [error, setError] = useState('');

  const [editCell, setEditCell] = useState<EditCell | null>(null);

  const [exceptionForm, setExceptionForm] = useState(emptyExceptionForm(emptyDay()));

  const [rangeModalOpen, setRangeModalOpen] = useState(false);

  const [rangeForm, setRangeForm] = useState({

    from: '',

    to: '',

    note: '',

    requestType: 'Pto',

    asRequest: false,

  });



  const range = useMemo(

    () => loadRangeForView(calendarDate, calendarView),

    [calendarDate, calendarView],

  );



  const loadRequests = useCallback(() => {

    if (!fleetId) return Promise.resolve();

    return api

      .get<DriverScheduleRequestDto[]>(

        `/api/delivery/tenants/${fleetId}/schedule-requests?status=Pending`,

      )

      .then(setPendingRequests)

      .catch(() => setPendingRequests([]));

  }, [fleetId]);



  const load = useCallback(() => {

    if (!fleetId) return Promise.resolve();

    return Promise.all([

      api.get<DriverCalendarDto>(

        `/api/delivery/tenants/${fleetId}/drivers/calendar?from=${range.from}&to=${range.to}`,

      ),

      loadRequests(),

    ]).then(([cal]) => setCalendar(cal));

  }, [fleetId, range.from, range.to, loadRequests]);



  useEffect(() => {

    if (isDriver && user?.driverId) {

      setDriverFilter(user.driverId);

    }

  }, [isDriver, user?.driverId]);



  useEffect(() => {

    if (!fleetId) {

      setLoading(false);

      return;

    }

    setLoading(true);

    setError('');

    load()

      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load calendar'))

      .finally(() => setLoading(false));

  }, [fleetId, load]);



  const rows = calendar?.drivers ?? [];

  const filteredRows =

    driverFilter === 'all' ? rows : rows.filter((r) => r.driverId === driverFilter);



  const events = useMemo(

    () => buildDriverScheduleEvents(filteredRows),

    [filteredRows],

  );



  const findDay = (driverId: string, date: string): DriverCalendarDayDto | null => {

    const row = rows.find((r) => r.driverId === driverId);

    return row?.days.find((d) => d.date === date) ?? null;

  };



  const openEdit = (driverId: string, driverName: string, day: DriverCalendarDayDto) => {

    if (!isAdmin) return;

    setEditCell({ driverId, driverName, day });

    setExceptionForm(emptyExceptionForm(day));

  };



  const openRangeModal = (driverId: string, from: string, to?: string) => {

    const row = rows.find((r) => r.driverId === driverId);

    setRangeForm({

      from,

      to: to ?? from,

      note: '',

      requestType: 'Pto',

      asRequest: !isAdmin,

    });

    if (!isAdmin && row) {

      setDriverFilter(driverId);

    }

    setRangeModalOpen(true);

  };



  const closeEdit = () => {

    setEditCell(null);

    setError('');

  };



  const saveException = async () => {

    if (!fleetId || !editCell) return;

    setSaving(true);

    setError('');

    try {

      const body: UpsertDriverScheduleExceptionRequest = {

        date: editCell.day.date,

        isWorking: exceptionForm.isWorking,

        note: exceptionForm.note.trim() || undefined,

      };

      if (exceptionForm.isWorking) {

        body.shiftStartTime = exceptionForm.shiftStartTime;

        body.shiftEndTime = exceptionForm.shiftEndTime;

        body.lunchMinutes = Number(exceptionForm.lunchMinutes) || 0;

        body.breakMinutes = Number(exceptionForm.breakMinutes) || 0;

        if (exceptionForm.maxRouteMinutes) body.maxRouteMinutes = Number(exceptionForm.maxRouteMinutes);

        if (exceptionForm.returnByTime) body.returnByTime = exceptionForm.returnByTime;

        if (exceptionForm.offBlockStartTime && exceptionForm.offBlockEndTime) {
          body.offBlockStartTime = exceptionForm.offBlockStartTime;
          body.offBlockEndTime = exceptionForm.offBlockEndTime;
        }

      }

      await api.put(

        `/api/delivery/tenants/${fleetId}/drivers/${editCell.driverId}/schedule-exceptions`,

        body,

      );

      closeEdit();

      await load();

    } catch (e) {

      setError(e instanceof Error ? e.message : 'Failed to save exception');

    } finally {

      setSaving(false);

    }

  };



  const saveRange = async () => {

    if (!fleetId || driverFilter === 'all') return;

    if (!rangeForm.from || !rangeForm.to) {

      setError('Start and end dates are required.');

      return;

    }

    setSaving(true);

    setError('');

    try {

      if (rangeForm.asRequest) {

        const body: CreateDriverScheduleRequestRequest = {

          driverId: driverFilter,

          fromDate: rangeForm.from,

          toDate: rangeForm.to,

          requestType: rangeForm.requestType,

          note: rangeForm.note.trim() || undefined,

        };

        await api.post(`/api/delivery/tenants/${fleetId}/schedule-requests`, body);

      } else {

        const body: BulkUpsertDriverScheduleExceptionRequest = {

          from: rangeForm.from,

          to: rangeForm.to,

          isWorking: false,

          note: rangeForm.note.trim() || undefined,

        };

        await api.put(

          `/api/delivery/tenants/${fleetId}/drivers/${driverFilter}/schedule-exceptions/bulk`,

          body,

        );

      }

      setRangeModalOpen(false);

      await load();

    } catch (e) {

      setError(e instanceof Error ? e.message : 'Failed to save');

    } finally {

      setSaving(false);

    }

  };



  const revertException = async () => {

    if (!fleetId || !editCell?.day.hasException) return;

    setSaving(true);

    setError('');

    try {

      await api.delete(

        `/api/delivery/tenants/${fleetId}/drivers/${editCell.driverId}/schedule-exceptions/${editCell.day.date}`,

      );

      closeEdit();

      await load();

    } catch (e) {

      setError(e instanceof Error ? e.message : 'Failed to revert exception');

    } finally {

      setSaving(false);

    }

  };



  const reviewRequest = async (requestId: string, action: 'approve' | 'deny') => {

    if (!fleetId) return;

    setSaving(true);

    setError('');

    try {

      await api.post(

        `/api/delivery/tenants/${fleetId}/schedule-requests/${requestId}/${action}`,

        {},

      );

      await load();

    } catch (e) {

      setError(e instanceof Error ? e.message : `Failed to ${action} request`);

    } finally {

      setSaving(false);

    }

  };



  const onSelectSlot = (slot: SlotInfo) => {

    if (driverFilter === 'all') {

      setError('Select a driver from the dropdown, then click or drag days on the calendar.');

      return;

    }

    const row = rows.find((r) => r.driverId === driverFilter);

    if (!row) return;

    const { from, to } = slotDateRange(slot);

    if (from !== to || !isAdmin) {

      openRangeModal(driverFilter, from, to);

      return;

    }

    const day = findDay(driverFilter, from);

    if (day) openEdit(driverFilter, row.displayName, day);

    else openRangeModal(driverFilter, from, to);

  };



  const onSelectEvent = (event: DriverScheduleEvent) => {

    if (event.resource.eventKind === 'route' && event.resource.routeId) {

      window.open(`/delivery/routes/${event.resource.routeId}`, '_blank', 'noopener');

      return;

    }

    if (!isAdmin) {

      openRangeModal(event.resource.driverId, event.resource.day.date);

      return;

    }

    openEdit(event.resource.driverId, event.resource.driverName, event.resource.day);

  };



  if (!hasModule(user?.modules, 'Delivery') && !user?.isPlatformAdmin) {

    return <p className="error">Delivery module is not enabled for your account.</p>;

  }



  if (!fleetId && !loading) {

    return (

      <div>

        <div className="page-header"><h2>Driver calendar</h2></div>

        <p className="error">Tenant context is missing.</p>

      </div>

    );

  }



  return (

    <div>

      <div className="page-header">

        <h2>Driver calendar</h2>

        <p>

          Shifts, routes, and time off from weekly patterns plus date exceptions.
          Admins can mark mid-shift unavailable blocks (e.g. 10:00–12:00) while the driver remains on shift.

          {isAdmin
            ? ' Select a driver, then use Add PTO, drag across days, or click a shift to edit.'
            : ' Select a driver, then use Request PTO or click a day to submit time off for approval.'}

          {fleetId && (

            <>

              {' '}

              · <Link to={`/delivery/fleets/${fleetId}/drivers`}>Weekly patterns</Link>

              {' · '}

              <Link to={`/delivery/fleets/${fleetId}/plan`}>Plan routes</Link>

            </>

          )}

        </p>

      </div>



      <ErrorAlert message={error} />



      {isAdmin && pendingRequests.length > 0 && (

        <div className="card" style={{ marginBottom: '1rem' }}>

          <h3 style={{ marginTop: 0 }}>Pending PTO requests ({pendingRequests.length})</h3>

          <ul className="plan-order-preview">

            {pendingRequests.map((req) => (

              <li key={req.id} style={{ marginBottom: '0.5rem' }}>

                <strong>{req.driverName}</strong>

                {' · '}

                {req.fromDate === req.toDate ? req.fromDate : `${req.fromDate} → ${req.toDate}`}

                {' · '}

                {req.requestType}

                {req.note && <span className="muted"> — {req.note}</span>}

                <span className="muted"> (by {req.requestedByName})</span>

                {' '}

                <button type="button" onClick={() => void reviewRequest(req.id, 'approve')} disabled={saving}>

                  Approve

                </button>

                {' '}

                <button type="button" className="secondary" onClick={() => void reviewRequest(req.id, 'deny')} disabled={saving}>

                  Deny

                </button>

              </li>

            ))}

          </ul>

        </div>

      )}



      <div className="calendar-toolbar form-row">

        {!isDriver ? (

        <select

          value={driverFilter}

          onChange={(e) => setDriverFilter(e.target.value)}

          aria-label="Filter by driver"

        >

          <option value="all">All drivers</option>

          {rows.map((r) => (

            <option key={r.driverId} value={r.driverId}>

              {r.displayName}{r.isActive ? '' : ' (inactive)'}

            </option>

          ))}

        </select>

        ) : (

          <span className="muted" style={{ alignSelf: 'center' }}>{user?.displayName} — my schedule</span>

        )}

        <button

          type="button"

          className={!isDriver && driverFilter === 'all' ? 'secondary' : undefined}

          disabled={!isDriver && driverFilter === 'all'}

          title={!isDriver && driverFilter === 'all' ? 'Select a driver first' : undefined}

          onClick={() => {

            if (driverFilter === 'all') return;

            const today = toDateOnly(new Date());

            openRangeModal(driverFilter, today);

          }}

        >

          {isAdmin ? 'Add PTO (multi-day)' : 'Request PTO'}

        </button>

      </div>

      {driverFilter === 'all' && rows.length > 0 && (

        <p className="plan-hint" style={{ marginTop: '-0.5rem', marginBottom: '1rem' }}>

          Choose a driver above to add multi-day PTO or drag across days on the calendar.

        </p>

      )}



      {driverFilter === 'all' && rows.length > 0 && (

        <div className="calendar-driver-legend">

          {rows.filter((r) => r.isActive).map((r) => (

            <span key={r.driverId} className="calendar-driver-legend-item">

              <span className="calendar-driver-swatch" style={{ background: driverColor(r.driverId) }} />

              {r.displayName}

            </span>

          ))}

          <span className="calendar-driver-legend-item muted">· Dashed gray = mid-shift unavailable · Route blocks open route detail</span>

        </div>

      )}



      <div className="driver-schedule-calendar card">

        {loading ? (

          <Loading />

        ) : rows.length === 0 ? (

          <p className="muted" style={{ padding: '1rem' }}>No drivers yet.</p>

        ) : (

          <Calendar

            localizer={localizer}

            events={events}

            startAccessor="start"

            endAccessor="end"

            date={calendarDate}

            view={calendarView}

            onNavigate={setCalendarDate}

            onView={setCalendarView}

            views={['month', 'week', 'day']}

            defaultView="week"

            popup

            selectable={driverFilter !== 'all'}

            onSelectSlot={onSelectSlot}

            style={{ height: calendarView === 'month' ? 720 : 640 }}

            components={{ event: ScheduleEvent }}

            onSelectEvent={onSelectEvent}

            eventPropGetter={(event) => {

              const { day, driverId, eventKind } = event.resource;

              if (eventKind === 'route') {

                return {

                  style: {

                    backgroundColor: 'rgba(99, 102, 241, 0.85)',

                    borderColor: '#6366f1',

                    color: '#fff',

                    fontSize: '0.75rem',

                  },

                };

              }

              if (eventKind === 'offblock') {

                return {

                  style: {

                    backgroundColor: 'rgba(75, 85, 99, 0.8)',

                    borderColor: '#4b5563',

                    borderStyle: 'dashed',

                    color: '#fff',

                    fontSize: '0.72rem',

                  },

                };

              }

              const base = driverColor(driverId);

              if (!day.isWorking) {

                return {

                  style: {

                    backgroundColor: 'rgba(107, 114, 128, 0.45)',

                    borderColor: 'var(--border)',

                    color: 'var(--muted)',

                  },

                };

              }

              return {

                style: {

                  backgroundColor: day.hasException ? '#b45309' : base,

                  borderColor: day.hasException ? 'var(--warning)' : base,

                  color: '#fff',

                },

              };

            }}

          />

        )}

      </div>



      {rangeModalOpen && driverFilter !== 'all' && (

        <div className="card calendar-edit-panel">

          <h3 style={{ marginTop: 0 }}>

            {rangeForm.asRequest ? 'Request time off' : 'Add PTO (multi-day)'}

          </h3>

          <div className="form-row">

            <label>

              From

              <input

                type="date"

                value={rangeForm.from}

                onChange={(e) => setRangeForm({ ...rangeForm, from: e.target.value })}

              />

            </label>

            <label>

              To

              <input

                type="date"

                value={rangeForm.to}

                onChange={(e) => setRangeForm({ ...rangeForm, to: e.target.value })}

              />

            </label>

            {rangeForm.asRequest && (

              <label>

                Type

                <select

                  value={rangeForm.requestType}

                  onChange={(e) => setRangeForm({ ...rangeForm, requestType: e.target.value })}

                >

                  <option value="Pto">PTO</option>

                  <option value="Sick">Sick</option>

                  <option value="Training">Training</option>

                  <option value="Other">Other</option>

                </select>

              </label>

            )}

          </div>

          <div className="form-row">

            <label style={{ flex: 1 }}>

              Note

              <input

                value={rangeForm.note}

                onChange={(e) => setRangeForm({ ...rangeForm, note: e.target.value })}

                placeholder="Optional reason"

              />

            </label>

          </div>

          <div className="form-row">

            <button type="button" onClick={() => void saveRange()} disabled={saving}>

              {saving ? 'Saving…' : rangeForm.asRequest ? 'Submit request' : 'Save PTO'}

            </button>

            <button type="button" className="secondary" onClick={() => setRangeModalOpen(false)} disabled={saving}>

              Cancel

            </button>

          </div>

        </div>

      )}



      {editCell && (

        <div className="card calendar-edit-panel">

          <h3 style={{ marginTop: 0 }}>

            {editCell.driverName} — {parseDateOnly(editCell.day.date).toLocaleDateString()}

          </h3>

          <p className="muted">

            {editCell.day.hasException

              ? 'Editing a date exception (overrides weekly pattern).'

              : `Currently from ${editCell.day.source}. Save to create an exception for this date.`}

          </p>

          {(editCell.day.assignedRoutes?.length ?? 0) > 0 && (

            <p className="muted">

              Assigned routes:{' '}

              {editCell.day.assignedRoutes.map((r) => (

                <Link key={r.routeId} to={`/delivery/routes/${r.routeId}`}>

                  {r.name}

                </Link>

              ))}

            </p>

          )}

          <div className="form-row">

            <label>

              <input

                type="checkbox"

                checked={exceptionForm.isWorking}

                onChange={(e) => setExceptionForm({ ...exceptionForm, isWorking: e.target.checked })}

              />{' '}

              Working this day

            </label>

          </div>

          {exceptionForm.isWorking && (

            <div className="form-row">

              <label>

                Shift start

                <input

                  type="time"

                  value={exceptionForm.shiftStartTime}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, shiftStartTime: e.target.value })}

                />

              </label>

              <label>

                Shift end

                <input

                  type="time"

                  value={exceptionForm.shiftEndTime}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, shiftEndTime: e.target.value })}

                />

              </label>

              <label>

                Lunch (min)

                <input

                  type="number"

                  min={0}

                  value={exceptionForm.lunchMinutes}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, lunchMinutes: e.target.value })}

                />

              </label>

              <label>

                Breaks (min)

                <input

                  type="number"

                  min={0}

                  value={exceptionForm.breakMinutes}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, breakMinutes: e.target.value })}

                />

              </label>

              <label>

                Max route (min)

                <input

                  type="number"

                  min={1}

                  placeholder="optional"

                  value={exceptionForm.maxRouteMinutes}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, maxRouteMinutes: e.target.value })}

                />

              </label>

              <label>

                Return by

                <input

                  type="time"

                  value={exceptionForm.returnByTime}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, returnByTime: e.target.value })}

                />

              </label>

              <label>

                Unavailable from

                <input

                  type="time"

                  value={exceptionForm.offBlockStartTime}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, offBlockStartTime: e.target.value })}

                  title="Mid-shift block start (optional; set both start and end)"

                />

              </label>

              <label>

                Unavailable until

                <input

                  type="time"

                  value={exceptionForm.offBlockEndTime}

                  onChange={(e) => setExceptionForm({ ...exceptionForm, offBlockEndTime: e.target.value })}

                  title="Mid-shift block end (optional; set both start and end)"

                />

              </label>

            </div>

          )}

          {exceptionForm.isWorking && (

            <p className="plan-hint" style={{ marginTop: '-0.25rem' }}>

              Use unavailable from/until for partial time off during a working day (e.g. doctor appointment 10:00–12:00).

              Leave both empty for a full working shift.

            </p>

          )}

          <div className="form-row">

            <label style={{ flex: 1 }}>

              Note (PTO, training, etc.)

              <input

                value={exceptionForm.note}

                onChange={(e) => setExceptionForm({ ...exceptionForm, note: e.target.value })}

                placeholder="Optional"

              />

            </label>

          </div>

          {editCell.day.isWorking && (

            <p className="muted">

              Available: {formatWorkMinutes(editCell.day.availableWorkMinutes)}

              {editCell.day.maxRouteMinutes != null && (

                <> · cap {formatWorkMinutes(editCell.day.maxRouteMinutes)}</>

              )}

            </p>

          )}

          <div className="form-row">

            <button type="button" onClick={() => void saveException()} disabled={saving}>

              {saving ? 'Saving…' : 'Save exception'}

            </button>

            {editCell.day.hasException && (

              <button type="button" className="secondary" onClick={() => void revertException()} disabled={saving}>

                Revert to weekly pattern

              </button>

            )}

            <button type="button" className="secondary" onClick={closeEdit} disabled={saving}>

              Cancel

            </button>

          </div>

        </div>

      )}



      {!isAdmin && (

        <p className="muted" style={{ marginTop: '1rem' }}>

          Select a driver, then click a day or use Request PTO to submit time off for admin approval.

        </p>

      )}

    </div>

  );

}


