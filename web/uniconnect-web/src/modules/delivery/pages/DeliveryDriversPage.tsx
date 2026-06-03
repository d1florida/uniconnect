import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DriverDto, TenantUserDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { hasModule } from '../../../utils/fleetModules';

const emptyForm = () => ({ displayName: '', userId: '' });

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
    setEditForm({ displayName: driver.displayName, userId: driver.userId ?? '' });
    setEditActive(driver.isActive);
    setError('');
  };

  const cancelEdit = () => {
    setEditingId(null);
    setEditForm(emptyForm());
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
          Delivery drivers for assignments and insights
          {fleetId && (
            <>
              {' '}
              · <Link to={`/delivery/fleets/${fleetId}/orders`}>Orders</Link>
              {' · '}
              <Link to={`/delivery/fleets/${fleetId}/routes`}>Routes</Link>
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
          <p className="muted">Optionally link a tenant user account for sign-in identity. The driver name can differ from the user display name.</p>
        </div>
      )}

      <table>
        <thead>
          <tr>
            <th>Name</th>
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
    </div>
  );
}
