import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DepotDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

const emptyForm = () => ({
  name: '',
  address: '',
  hours: '',
  notes: '',
  isDefault: false,
});

export function DeliveryDepotsPage() {
  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();
  const { user } = useAuth();
  const fleetId = fleetIdParam ?? user?.fleetId;
  const isAdmin = user?.isTenantAdmin ?? user?.isPlatformAdmin ?? false;

  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [savedMessage, setSavedMessage] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [createForm, setCreateForm] = useState(emptyForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState(emptyForm());
  const [editActive, setEditActive] = useState(true);

  const load = (includeInactive = false) => {
    if (!fleetId) return Promise.resolve();
    const query = includeInactive ? '?includeInactive=true' : '';
    return api.get<DepotDto[]>(`/api/delivery/tenants/${fleetId}/depots${query}`).then(setDepots);
  };

  useEffect(() => {
    if (!fleetId) {
      setLoading(false);
      return;
    }
    load(isAdmin)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load depots'))
      .finally(() => setLoading(false));
  }, [fleetId, isAdmin]);

  const createDepot = async () => {
    if (!fleetId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.post<DepotDto>(`/api/delivery/tenants/${fleetId}/depots`, {
        name: createForm.name.trim(),
        address: createForm.address.trim(),
        hours: createForm.hours.trim() || null,
        notes: createForm.notes.trim() || null,
        isDefault: createForm.isDefault,
      });
      setShowForm(false);
      setCreateForm(emptyForm());
      setSavedMessage('Depot added.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add depot');
    } finally {
      setSaving(false);
    }
  };

  const startEdit = (depot: DepotDto) => {
    setEditingId(depot.id);
    setEditForm({
      name: depot.name,
      address: depot.address,
      hours: depot.hours ?? '',
      notes: depot.notes ?? '',
      isDefault: depot.isDefault,
    });
    setEditActive(depot.isActive);
    setError('');
  };

  const saveEdit = async () => {
    if (!fleetId || !editingId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.patch<DepotDto>(`/api/delivery/tenants/${fleetId}/depots/${editingId}`, {
        name: editForm.name.trim(),
        address: editForm.address.trim(),
        hours: editForm.hours.trim() || null,
        notes: editForm.notes.trim() || null,
        isDefault: editForm.isDefault,
        isActive: editActive,
      });
      setEditingId(null);
      setSavedMessage('Depot updated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update depot');
    } finally {
      setSaving(false);
    }
  };

  const deactivateDepot = async (depotId: string) => {
    if (!fleetId) return;
    if (!window.confirm('Deactivate this depot? It will no longer appear in plan and route forms.')) return;
    setError('');
    try {
      await api.delete(`/api/delivery/tenants/${fleetId}/depots/${depotId}`);
      setSavedMessage('Depot deactivated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to deactivate depot');
    }
  };

  if (!fleetId && !loading) {
    return (
      <div>
        <div className="page-header"><h2>Depots</h2></div>
        <p className="error">Tenant context is missing. Use the Depots link under Delivery in the sidebar.</p>
      </div>
    );
  }

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Depots</h2>
        <p className="muted">
          Saved warehouse and hub addresses used for route planning.
          {' · '}
          <Link to="/delivery">Delivery</Link>
          {fleetId && (
            <>
              {' · '}
              <Link to={`/delivery/fleets/${fleetId}/plan`}>Plan routes</Link>
            </>
          )}
        </p>
      </div>
      <ErrorAlert message={error} />
      {savedMessage && <p className="muted">{savedMessage}</p>}

      {!isAdmin && (
        <p className="muted">Contact a tenant administrator to add or edit depots.</p>
      )}

      {isAdmin && (
        <div className="tabs">
          <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>
            + Add depot
          </button>
        </div>
      )}

      {showForm && isAdmin && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row">
            <input placeholder="Name (e.g. SF Main Warehouse)" value={createForm.name} onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })} />
            <input placeholder="Address" value={createForm.address} onChange={(e) => setCreateForm({ ...createForm, address: e.target.value })} style={{ flex: 1 }} />
          </div>
          <div className="form-row">
            <input placeholder="Hours (optional)" value={createForm.hours} onChange={(e) => setCreateForm({ ...createForm, hours: e.target.value })} />
            <input placeholder="Notes (optional)" value={createForm.notes} onChange={(e) => setCreateForm({ ...createForm, notes: e.target.value })} style={{ flex: 1 }} />
          </div>
          <label className="checkbox-label">
            <input type="checkbox" checked={createForm.isDefault} onChange={(e) => setCreateForm({ ...createForm, isDefault: e.target.checked })} />
            Default depot for planning
          </label>
          <div className="form-row">
            <button type="button" onClick={() => void createDepot()} disabled={saving || !createForm.name.trim() || !createForm.address.trim()}>
              {saving ? 'Saving…' : 'Add depot'}
            </button>
          </div>
        </div>
      )}

      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Address</th>
            <th>Hours</th>
            <th>Default</th>
            <th>Coordinates</th>
            {isAdmin && <th></th>}
          </tr>
        </thead>
        <tbody>
          {depots.map((d) => (
            <tr key={d.id}>
              {editingId === d.id ? (
                <>
                  <td colSpan={isAdmin ? 6 : 5}>
                    <div className="form-row">
                      <input value={editForm.name} onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} />
                      <input value={editForm.address} onChange={(e) => setEditForm({ ...editForm, address: e.target.value })} style={{ flex: 1 }} />
                    </div>
                    <div className="form-row">
                      <input placeholder="Hours" value={editForm.hours} onChange={(e) => setEditForm({ ...editForm, hours: e.target.value })} />
                      <input placeholder="Notes" value={editForm.notes} onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })} style={{ flex: 1 }} />
                    </div>
                    <label className="checkbox-label">
                      <input type="checkbox" checked={editForm.isDefault} onChange={(e) => setEditForm({ ...editForm, isDefault: e.target.checked })} />
                      Default depot
                    </label>
                    {' '}
                    <label className="checkbox-label">
                      <input type="checkbox" checked={editActive} onChange={(e) => setEditActive(e.target.checked)} />
                      Active
                    </label>
                    <div className="form-row">
                      <button type="button" onClick={() => void saveEdit()} disabled={saving}>Save</button>
                      <button type="button" className="secondary" onClick={() => setEditingId(null)}>Cancel</button>
                    </div>
                  </td>
                </>
              ) : (
                <>
                  <td>
                    {d.name}
                    {!d.isActive && <span className="muted"> (inactive)</span>}
                  </td>
                  <td>{d.address}</td>
                  <td>{d.hours ?? '—'}</td>
                  <td>{d.isDefault ? <span className="badge badge-av">Default</span> : '—'}</td>
                  <td>
                    {d.latitude != null && d.longitude != null ? (
                      <span title="Geocoded for route planning">
                        {d.latitude.toFixed(4)}, {d.longitude.toFixed(4)}
                      </span>
                    ) : (
                      <span className="muted">Pending geocode</span>
                    )}
                  </td>
                  {isAdmin && (
                    <td>
                      <button type="button" className="secondary" onClick={() => startEdit(d)}>Edit</button>
                      {d.isActive && (
                        <>
                          {' '}
                          <button type="button" className="secondary" onClick={() => void deactivateDepot(d.id)}>Deactivate</button>
                        </>
                      )}
                    </td>
                  )}
                </>
              )}
            </tr>
          ))}
        </tbody>
      </table>
      {depots.length === 0 && <p className="muted">No depots yet. Add your main warehouse to speed up daily planning.</p>}
    </div>
  );
}
