import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DriverDto, DriverWorkPatternDayDto, TenantUserDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { hasModule } from '../../../utils/fleetModules';
import { formatWorkMinutes } from '../deliveryLabels';

const DEFAULT_SHIFT_START = '07:00';
const DEFAULT_SHIFT_END = '17:00';
const DEFAULT_LUNCH_MINUTES = 30;
const DEFAULT_BREAK_MINUTES = 15;

const DAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

const emptyForm = () => ({
  displayName: '',
  userId: '',
  shiftStartTime: DEFAULT_SHIFT_START,
  shiftEndTime: DEFAULT_SHIFT_END,
  lunchMinutes: String(DEFAULT_LUNCH_MINUTES),
  breakMinutes: String(DEFAULT_BREAK_MINUTES),
  maxRouteMinutes: '',
  returnByTime: '',
});

export function DeliveryDriversPage() {
  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();
  const { user } = useAuth();
  const fleetId = fleetIdParam ?? user?.fleetId;
  const isAdmin = user?.isTenantAdmin ?? user?.isPlatformAdmin ?? false;

  const [drivers, setDrivers] = useState<DriverDto[]>([]);
  const [teamUsers, setTeamUsers] = useState<TenantUserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [savedMessage, setSavedMessage] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [createForm, setCreateForm] = useState(emptyForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState(emptyForm());
  const [editActive, setEditActive] = useState(true);
  const [scheduleDriverId, setScheduleDriverId] = useState<string | null>(null);
  const [workPattern, setWorkPattern] = useState<DriverWorkPatternDayDto[]>([]);
  const [scheduleLoading, setScheduleLoading] = useState(false);

  const load = () => {
    if (!fleetId) return Promise.resolve();
    return api.get<DriverDto[]>(`/api/delivery/tenants/${fleetId}/drivers`).then(setDrivers);
  };

  useEffect(() => {
    if (!fleetId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError('');
    const requests: Promise<void>[] = [load().then(() => undefined)];
    if (isAdmin) {
      requests.push(
        api.get<TenantUserDto[]>('/api/tenants/me/users').then(setTeamUsers).catch(() => setTeamUsers([])),
      );
    }
    Promise.all(requests)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load drivers'))
      .finally(() => setLoading(false));
  }, [fleetId, isAdmin]);

  const createDriver = async () => {
    if (!fleetId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.post<DriverDto>(`/api/delivery/tenants/${fleetId}/drivers`, {
        displayName: createForm.displayName.trim(),
        userId: createForm.userId || null,
        shiftStartTime: createForm.shiftStartTime,
        shiftEndTime: createForm.shiftEndTime,
        lunchMinutes: Number(createForm.lunchMinutes) || 0,
        breakMinutes: Number(createForm.breakMinutes) || 0,
        maxRouteMinutes: createForm.maxRouteMinutes ? Number(createForm.maxRouteMinutes) : null,
        returnByTime: createForm.returnByTime.trim() || null,
      });
      setShowForm(false);
      setCreateForm(emptyForm());
      setSavedMessage('Driver added.');
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add driver');
    } finally {
      setSaving(false);
    }
  };

  const startEdit = (driver: DriverDto) => {
    setEditingId(driver.id);
    setEditForm({
      displayName: driver.displayName,
      userId: driver.userId ?? '',
      shiftStartTime: driver.shiftStartTime,
      shiftEndTime: driver.shiftEndTime,
      lunchMinutes: String(driver.lunchMinutes),
      breakMinutes: String(driver.breakMinutes),
      maxRouteMinutes: driver.maxRouteMinutes != null ? String(driver.maxRouteMinutes) : '',
      returnByTime: driver.returnByTime ?? '',
    });
    setEditActive(driver.isActive);
    setError('');
  };

  const cancelEdit = () => {
    setEditingId(null);
    setEditForm(emptyForm());
  };

  const openSchedule = async (driverId: string) => {
    if (!fleetId) return;
    setScheduleDriverId(driverId);
    setScheduleLoading(true);
    setError('');
    try {
      const pattern = await api.get<DriverWorkPatternDayDto[]>(
        `/api/delivery/tenants/${fleetId}/drivers/${driverId}/work-pattern`,
      );
      setWorkPattern(pattern.sort((a, b) => a.dayOfWeek - b.dayOfWeek));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load weekly schedule');
      setScheduleDriverId(null);
    } finally {
      setScheduleLoading(false);
    }
  };

  const closeSchedule = () => {
    setScheduleDriverId(null);
    setWorkPattern([]);
  };

  const updatePatternDay = (dayOfWeek: number, patch: Partial<DriverWorkPatternDayDto>) => {
    setWorkPattern((prev) =>
      prev.map((day) => (day.dayOfWeek === dayOfWeek ? { ...day, ...patch } : day)),
    );
  };

  const saveSchedule = async () => {
    if (!fleetId || !scheduleDriverId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.put<DriverWorkPatternDayDto[]>(
        `/api/delivery/tenants/${fleetId}/drivers/${scheduleDriverId}/work-pattern`,
        workPattern.map((day) => ({
          dayOfWeek: day.dayOfWeek,
          isWorkingDay: day.isWorkingDay,
          shiftStartTime: day.shiftStartTime,
          shiftEndTime: day.shiftEndTime,
          lunchMinutes: day.lunchMinutes,
          breakMinutes: day.breakMinutes,
          maxRouteMinutes: day.maxRouteMinutes ?? null,
          returnByTime: day.returnByTime ?? null,
        })),
      );
      setSavedMessage('Weekly schedule saved.');
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save weekly schedule');
    } finally {
      setSaving(false);
    }
  };

  const saveEdit = async (driverId: string) => {
    if (!fleetId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.patch<DriverDto>(`/api/delivery/tenants/${fleetId}/drivers/${driverId}`, {
        displayName: editForm.displayName.trim(),
        userId: editForm.userId || null,
        isActive: editActive,
        shiftStartTime: editForm.shiftStartTime,
        shiftEndTime: editForm.shiftEndTime,
        lunchMinutes: Number(editForm.lunchMinutes) || 0,
        breakMinutes: Number(editForm.breakMinutes) || 0,
        maxRouteMinutes: editForm.maxRouteMinutes ? Number(editForm.maxRouteMinutes) : null,
        returnByTime: editForm.returnByTime.trim() || null,
      });
      setEditingId(null);
      setSavedMessage('Driver updated.');
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update driver');
    } finally {
      setSaving(false);
    }
  };

  const deleteDriver = async (driverId: string, displayName: string) => {
    if (!fleetId) return;
    if (!window.confirm(`Delete driver "${displayName}"? This cannot be undone.`)) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.delete(`/api/delivery/tenants/${fleetId}/drivers/${driverId}`);
      setEditingId(null);
      setSavedMessage('Driver deleted.');
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete driver');
    } finally {
      setSaving(false);
    }
  };

  if (!hasModule(user?.modules, 'Delivery') && !user?.isPlatformAdmin) {
    return <p className="error">Delivery module is not enabled for your account.</p>;
  }

  if (!fleetId && !loading) {
    return (
      <div>
        <div className="page-header"><h2>Drivers</h2></div>
        <p className="error">Tenant context is missing. Use the Drivers link under Delivery in the sidebar.</p>
      </div>
    );
  }

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Drivers</h2>
        <p>
          Delivery drivers for assignments, route planning, and insights. Weekly schedules resolve on read for each plan date.
          {fleetId && (
            <>
              {' '}
              · <Link to={`/delivery/fleets/${fleetId}/orders`}>Orders</Link>
              {' · '}
              <Link to={`/delivery/fleets/${fleetId}/routes`}>Routes</Link>
              {' · '}
              <Link to={`/delivery/fleets/${fleetId}/driver-calendar`}>Calendar</Link>
            </>
          )}
        </p>
      </div>

      <ErrorAlert message={error} />
      {savedMessage && <p className="muted">{savedMessage}</p>}

      {!isAdmin && (
        <p className="muted">Only tenant admins can add or edit drivers. Contact your administrator to make changes.</p>
      )}

      {isAdmin && (
        <div className="form-row">
          <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>
            + Add driver
          </button>
        </div>
      )}

      {isAdmin && showForm && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row">
            <input
              placeholder="Display name"
              value={createForm.displayName}
              onChange={(e) => setCreateForm({ ...createForm, displayName: e.target.value })}
            />
            <select
              value={createForm.userId}
              onChange={(e) => setCreateForm({ ...createForm, userId: e.target.value })}
            >
              <option value="">No linked user</option>
              {teamUsers.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.displayName} ({u.email})
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => void createDriver()}
              disabled={saving || !createForm.displayName.trim()}
            >
              {saving ? 'Saving…' : 'Add driver'}
            </button>
          </div>
          <div className="form-row" style={{ marginTop: '0.75rem' }}>
            <label>
              Shift start
              <input
                type="time"
                value={createForm.shiftStartTime}
                onChange={(e) => setCreateForm({ ...createForm, shiftStartTime: e.target.value })}
              />
            </label>
            <label>
              Shift end
              <input
                type="time"
                value={createForm.shiftEndTime}
                onChange={(e) => setCreateForm({ ...createForm, shiftEndTime: e.target.value })}
              />
            </label>
            <label>
              Lunch (min)
              <input
                type="number"
                min={0}
                value={createForm.lunchMinutes}
                onChange={(e) => setCreateForm({ ...createForm, lunchMinutes: e.target.value })}
              />
            </label>
            <label>
              Breaks (min)
              <input
                type="number"
                min={0}
                value={createForm.breakMinutes}
                onChange={(e) => setCreateForm({ ...createForm, breakMinutes: e.target.value })}
              />
            </label>
            <label>
              Max route (min)
              <input
                type="number"
                min={1}
                placeholder="optional"
                value={createForm.maxRouteMinutes}
                onChange={(e) => setCreateForm({ ...createForm, maxRouteMinutes: e.target.value })}
              />
            </label>
            <label>
              Return by
              <input
                type="time"
                value={createForm.returnByTime}
                onChange={(e) => setCreateForm({ ...createForm, returnByTime: e.target.value })}
              />
            </label>
          </div>
          <p className="muted">Default Mon–Fri schedule is created automatically. Use Weekly schedule after adding to customize part-time days.</p>
          <p className="muted">Optionally link a tenant user account for sign-in identity. The driver name can differ from the user display name.</p>
        </div>
      )}

      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Shift</th>
            <th>Lunch / breaks</th>
            <th>Available</th>
            <th>Linked user</th>
            <th>Status</th>
            <th>Added</th>
            {isAdmin && <th></th>}
          </tr>
        </thead>
        <tbody>
          {drivers.map((d) =>
            editingId === d.id ? (
              <tr key={d.id}>
                <td>
                  <input
                    value={editForm.displayName}
                    onChange={(e) => setEditForm({ ...editForm, displayName: e.target.value })}
                  />
                </td>
                <td>
                  <input
                    type="time"
                    value={editForm.shiftStartTime}
                    onChange={(e) => setEditForm({ ...editForm, shiftStartTime: e.target.value })}
                  />
                  {' – '}
                  <input
                    type="time"
                    value={editForm.shiftEndTime}
                    onChange={(e) => setEditForm({ ...editForm, shiftEndTime: e.target.value })}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    min={0}
                    style={{ width: '4rem' }}
                    value={editForm.lunchMinutes}
                    onChange={(e) => setEditForm({ ...editForm, lunchMinutes: e.target.value })}
                  />
                  {' / '}
                  <input
                    type="number"
                    min={0}
                    style={{ width: '4rem' }}
                    value={editForm.breakMinutes}
                    onChange={(e) => setEditForm({ ...editForm, breakMinutes: e.target.value })}
                  />
                  {' min'}
                </td>
                <td>
                  <input
                    type="number"
                    min={1}
                    placeholder="max min"
                    style={{ width: '5rem' }}
                    value={editForm.maxRouteMinutes}
                    onChange={(e) => setEditForm({ ...editForm, maxRouteMinutes: e.target.value })}
                  />
                  <input
                    type="time"
                    value={editForm.returnByTime}
                    onChange={(e) => setEditForm({ ...editForm, returnByTime: e.target.value })}
                  />
                </td>
                <td>
                  <select value={editForm.userId} onChange={(e) => setEditForm({ ...editForm, userId: e.target.value })}>
                    <option value="">No linked user</option>
                    {teamUsers.map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.displayName} ({u.email})
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <label className="checkbox-label">
                    <input type="checkbox" checked={editActive} onChange={(e) => setEditActive(e.target.checked)} />
                    Active
                  </label>
                </td>
                <td>{new Date(d.createdAt).toLocaleDateString()}</td>
                <td>
                  <button type="button" onClick={() => void saveEdit(d.id)} disabled={saving || !editForm.displayName.trim()}>
                    Save
                  </button>{' '}
                  <button type="button" className="secondary" onClick={cancelEdit}>
                    Cancel
                  </button>{' '}
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => void deleteDriver(d.id, d.displayName)}
                    disabled={saving}
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ) : (
              <tr key={d.id}>
                <td>{d.displayName}</td>
                <td>{d.shiftStartTime}–{d.shiftEndTime}</td>
                <td>{d.lunchMinutes} / {d.breakMinutes} min</td>
                <td>
                  {formatWorkMinutes(d.availableWorkMinutes)}
                  {d.maxRouteMinutes != null && <> · cap {formatWorkMinutes(d.maxRouteMinutes)}</>}
                  {d.returnByTime && <> · back {d.returnByTime}</>}
                </td>
                <td>{d.linkedUserName ?? '—'}</td>
                <td>
                  <span className={`badge ${d.isActive ? 'badge-conv' : 'badge-muted'}`}>
                    {d.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>{new Date(d.createdAt).toLocaleDateString()}</td>
                {isAdmin && (
                  <td>
                    <button type="button" className="secondary" onClick={() => startEdit(d)}>
                      Edit
                    </button>{' '}
                    <button type="button" className="secondary" onClick={() => void openSchedule(d.id)}>
                      Schedule
                    </button>
                  </td>
                )}
              </tr>
            ),
          )}
        </tbody>
      </table>

      {drivers.length === 0 && !showForm && (
        <p className="muted">No drivers yet.{isAdmin ? ' Add your first driver above.' : ''}</p>
      )}

      {scheduleDriverId && (
        <div className="card" style={{ marginTop: '1rem' }}>
          <h3 style={{ marginTop: 0 }}>
            Weekly schedule — {drivers.find((d) => d.id === scheduleDriverId)?.displayName ?? 'Driver'}
          </h3>
          <p className="muted">
            Uncheck a day for time off. Route planning uses this pattern for the plan date (resolve on read).
          </p>
          {scheduleLoading ? (
            <Loading />
          ) : (
            <>
              <table>
                <thead>
                  <tr>
                    <th>Day</th>
                    <th>Working</th>
                    <th>Shift</th>
                    <th>Lunch</th>
                    <th>Breaks</th>
                    <th>Max route</th>
                    <th>Return by</th>
                  </tr>
                </thead>
                <tbody>
                  {workPattern.map((day) => (
                    <tr key={day.dayOfWeek}>
                      <td>{DAY_LABELS[day.dayOfWeek]}</td>
                      <td>
                        <input
                          type="checkbox"
                          checked={day.isWorkingDay}
                          onChange={(e) => updatePatternDay(day.dayOfWeek, { isWorkingDay: e.target.checked })}
                        />
                      </td>
                      <td>
                        <input
                          type="time"
                          disabled={!day.isWorkingDay}
                          value={day.shiftStartTime}
                          onChange={(e) => updatePatternDay(day.dayOfWeek, { shiftStartTime: e.target.value })}
                        />
                        {' – '}
                        <input
                          type="time"
                          disabled={!day.isWorkingDay}
                          value={day.shiftEndTime}
                          onChange={(e) => updatePatternDay(day.dayOfWeek, { shiftEndTime: e.target.value })}
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          min={0}
                          disabled={!day.isWorkingDay}
                          style={{ width: '4rem' }}
                          value={day.lunchMinutes}
                          onChange={(e) =>
                            updatePatternDay(day.dayOfWeek, { lunchMinutes: Number(e.target.value) || 0 })
                          }
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          min={0}
                          disabled={!day.isWorkingDay}
                          style={{ width: '4rem' }}
                          value={day.breakMinutes}
                          onChange={(e) =>
                            updatePatternDay(day.dayOfWeek, { breakMinutes: Number(e.target.value) || 0 })
                          }
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          min={1}
                          disabled={!day.isWorkingDay}
                          placeholder="—"
                          style={{ width: '5rem' }}
                          value={day.maxRouteMinutes ?? ''}
                          onChange={(e) =>
                            updatePatternDay(day.dayOfWeek, {
                              maxRouteMinutes: e.target.value ? Number(e.target.value) : undefined,
                            })
                          }
                        />
                      </td>
                      <td>
                        <input
                          type="time"
                          disabled={!day.isWorkingDay}
                          value={day.returnByTime ?? ''}
                          onChange={(e) =>
                            updatePatternDay(day.dayOfWeek, { returnByTime: e.target.value || undefined })
                          }
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="form-row" style={{ marginTop: '0.75rem' }}>
                <button type="button" onClick={() => void saveSchedule()} disabled={saving || workPattern.length !== 7}>
                  {saving ? 'Saving…' : 'Save weekly schedule'}
                </button>
                <button type="button" className="secondary" onClick={closeSchedule}>
                  Close
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}
