import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type {
  DeliveryZoneDto,
  DepotDto,
  DriverDto,
  DeliveryVehicleDto,
  FixedRouteTemplateDto,
} from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import {
  DAY_OF_WEEK_OPTIONS,
  type DayOfWeekName,
  daysOfWeekToApiValues,
  toggleDayOfWeek,
} from '../deliveryLabels';
import { vehicleSelectLabel } from '../../../utils/vehicleLabels';

const emptyZoneForm = () => ({ name: '' });

const emptyTemplateForm = () => ({
  name: '',
  deliveryZoneId: '',
  daysOfWeek: ['Thursday'] as DayOfWeekName[],
  depotId: '',
  defaultVehicleId: '',
  defaultDriverId: '',
});

export function DeliveryFixedRoutesPage() {
  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();
  const { user } = useAuth();
  const fleetId = fleetIdParam ?? user?.fleetId;
  const isAdmin = user?.isTenantAdmin ?? user?.isPlatformAdmin ?? false;

  const [zones, setZones] = useState<DeliveryZoneDto[]>([]);
  const [templates, setTemplates] = useState<FixedRouteTemplateDto[]>([]);
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [vehicles, setVehicles] = useState<DeliveryVehicleDto[]>([]);
  const [drivers, setDrivers] = useState<DriverDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [savedMessage, setSavedMessage] = useState('');

  const [showZoneForm, setShowZoneForm] = useState(false);
  const [zoneForm, setZoneForm] = useState(emptyZoneForm());
  const [editingZoneId, setEditingZoneId] = useState<string | null>(null);
  const [editZoneForm, setEditZoneForm] = useState(emptyZoneForm());
  const [editZoneActive, setEditZoneActive] = useState(true);

  const [showTemplateForm, setShowTemplateForm] = useState(false);
  const [templateForm, setTemplateForm] = useState(emptyTemplateForm());
  const [editingTemplateId, setEditingTemplateId] = useState<string | null>(null);
  const [editTemplateForm, setEditTemplateForm] = useState(emptyTemplateForm());
  const [editTemplateActive, setEditTemplateActive] = useState(true);

  const load = (includeInactive = false) => {
    if (!fleetId) return Promise.resolve();
    const query = includeInactive ? '?includeInactive=true' : '';
    return Promise.all([
      api.get<DeliveryZoneDto[]>(`/api/delivery/tenants/${fleetId}/delivery-zones${query}`),
      api.get<FixedRouteTemplateDto[]>(`/api/delivery/tenants/${fleetId}/fixed-route-templates${query}`),
      api.get<DepotDto[]>(`/api/delivery/tenants/${fleetId}/depots`),
      api.get<DeliveryVehicleDto[]>(`/api/delivery/tenants/${fleetId}/vehicles`),
      api.get<DriverDto[]>(`/api/delivery/tenants/${fleetId}/drivers`),
    ]).then(([zoneList, templateList, depotList, vehicleList, driverList]) => {
      setZones(zoneList);
      setTemplates(templateList);
      setDepots(depotList);
      setVehicles(vehicleList);
      setDrivers(driverList);
    });
  };

  useEffect(() => {
    if (!fleetId) {
      setLoading(false);
      return;
    }
    load(isAdmin)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load fixed routes'))
      .finally(() => setLoading(false));
  }, [fleetId, isAdmin]);

  useEffect(() => {
    if (!showTemplateForm || depots.length === 0) return;
    setTemplateForm((prev) => {
      if (prev.depotId) return prev;
      const defaultDepot = depots.find((d) => d.isDefault) ?? depots[0];
      return defaultDepot ? { ...prev, depotId: defaultDepot.id } : prev;
    });
  }, [showTemplateForm, depots]);

  const createZone = async () => {
    if (!fleetId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.post(`/api/delivery/tenants/${fleetId}/delivery-zones`, {
        name: zoneForm.name.trim(),
      });
      setShowZoneForm(false);
      setZoneForm(emptyZoneForm());
      setSavedMessage('Delivery zone added.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add zone');
    } finally {
      setSaving(false);
    }
  };

  const saveZoneEdit = async () => {
    if (!fleetId || !editingZoneId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.patch(`/api/delivery/tenants/${fleetId}/delivery-zones/${editingZoneId}`, {
        name: editZoneForm.name.trim(),
        isActive: editZoneActive,
      });
      setEditingZoneId(null);
      setSavedMessage('Zone updated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update zone');
    } finally {
      setSaving(false);
    }
  };

  const deactivateZone = async (zoneId: string) => {
    if (!fleetId || !window.confirm('Deactivate this delivery zone?')) return;
    setError('');
    try {
      await api.delete(`/api/delivery/tenants/${fleetId}/delivery-zones/${zoneId}`);
      setSavedMessage('Zone deactivated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to deactivate zone');
    }
  };

  const createTemplate = async () => {
    if (!fleetId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      if (templateForm.daysOfWeek.length === 0) {
        setError('Select at least one route day.');
        return;
      }
      await api.post(`/api/delivery/tenants/${fleetId}/fixed-route-templates`, {
        name: templateForm.name.trim(),
        deliveryZoneId: templateForm.deliveryZoneId,
        daysOfWeek: daysOfWeekToApiValues(templateForm.daysOfWeek),
        depotId: templateForm.depotId,
        defaultVehicleId: templateForm.defaultVehicleId || null,
        defaultDriverId: templateForm.defaultDriverId || null,
      });
      setShowTemplateForm(false);
      setTemplateForm(emptyTemplateForm());
      setSavedMessage('Fixed route template added.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add template');
    } finally {
      setSaving(false);
    }
  };

  const startTemplateEdit = (template: FixedRouteTemplateDto) => {
    setEditingTemplateId(template.id);
    setEditTemplateForm({
      name: template.name,
      deliveryZoneId: template.deliveryZoneId,
      daysOfWeek: template.daysOfWeek.filter((day): day is DayOfWeekName =>
        DAY_OF_WEEK_OPTIONS.includes(day as DayOfWeekName)),
      depotId: template.depotId,
      defaultVehicleId: template.defaultVehicleId ?? '',
      defaultDriverId: template.defaultDriverId ?? '',
    });
    setEditTemplateActive(template.isActive);
    setError('');
  };

  const saveTemplateEdit = async () => {
    if (!fleetId || !editingTemplateId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      if (editTemplateForm.daysOfWeek.length === 0) {
        setError('Select at least one route day.');
        return;
      }
      await api.patch(`/api/delivery/tenants/${fleetId}/fixed-route-templates/${editingTemplateId}`, {
        name: editTemplateForm.name.trim(),
        daysOfWeek: daysOfWeekToApiValues(editTemplateForm.daysOfWeek),
        depotId: editTemplateForm.depotId,
        defaultVehicleId: editTemplateForm.defaultVehicleId || null,
        defaultDriverId: editTemplateForm.defaultDriverId || null,
        isActive: editTemplateActive,
      });
      setEditingTemplateId(null);
      setSavedMessage('Template updated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update template');
    } finally {
      setSaving(false);
    }
  };

  const deactivateTemplate = async (templateId: string) => {
    if (!fleetId || !window.confirm('Deactivate this fixed route template?')) return;
    setError('');
    try {
      await api.delete(`/api/delivery/tenants/${fleetId}/fixed-route-templates/${templateId}`);
      setSavedMessage('Template deactivated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to deactivate template');
    }
  };

  const renderTemplateFields = (
    form: ReturnType<typeof emptyTemplateForm>,
    setForm: (next: ReturnType<typeof emptyTemplateForm>) => void,
    zoneLocked = false,
  ) => (
    <>
      <div className="form-row">
        <input
          placeholder="Template name (e.g. Pasco — Tue & Thu)"
          value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })}
          style={{ flex: 1 }}
        />
      </div>
      <div className="form-row">
        <label>
          Zone
          <select
            value={form.deliveryZoneId}
            disabled={zoneLocked}
            onChange={(e) => setForm({ ...form, deliveryZoneId: e.target.value })}
          >
            <option value="">— Select zone —</option>
            {zones.filter((z) => z.isActive).map((z) => (
              <option key={z.id} value={z.id}>{z.name}</option>
            ))}
          </select>
        </label>
        <label>
          Depot
          <select value={form.depotId} onChange={(e) => setForm({ ...form, depotId: e.target.value })}>
            <option value="">— Select depot —</option>
            {depots.filter((d) => d.isActive).map((d) => (
              <option key={d.id} value={d.id}>{d.name}</option>
            ))}
          </select>
        </label>
      </div>
      <fieldset style={{ border: 'none', padding: 0, margin: '0.5rem 0' }}>
        <legend className="muted" style={{ padding: 0 }}>Route days</legend>
        <div className="form-row" style={{ flexWrap: 'wrap', gap: '0.75rem' }}>
          {DAY_OF_WEEK_OPTIONS.map((day) => (
            <label key={day} className="checkbox-label">
              <input
                type="checkbox"
                checked={form.daysOfWeek.includes(day)}
                onChange={() => setForm({ ...form, daysOfWeek: toggleDayOfWeek(form.daysOfWeek, day) })}
              />
              {day}
            </label>
          ))}
        </div>
      </fieldset>
      <div className="form-row">
        <label>
          Default vehicle (optional)
          <select
            value={form.defaultVehicleId}
            onChange={(e) => setForm({ ...form, defaultVehicleId: e.target.value })}
          >
            <option value="">— None —</option>
            {vehicles.map((v) => (
              <option key={v.id} value={v.id}>{vehicleSelectLabel(v)}</option>
            ))}
          </select>
        </label>
        <label>
          Default driver (optional)
          <select
            value={form.defaultDriverId}
            onChange={(e) => setForm({ ...form, defaultDriverId: e.target.value })}
          >
            <option value="">— None —</option>
            {drivers.filter((d) => d.isActive).map((d) => (
              <option key={d.id} value={d.id}>{d.displayName}</option>
            ))}
          </select>
        </label>
      </div>
    </>
  );

  if (!fleetId && !loading) {
    return (
      <div>
        <div className="page-header"><h2>Fixed routes</h2></div>
        <p className="error">Tenant context is missing.</p>
      </div>
    );
  }

  if (loading) return <Loading />;

  return (
    <div>
      <div className="page-header">
        <h2>Fixed routes</h2>
        <p className="muted">
          Geographic zones and recurring route templates. Orders for zoned customers are held until their route day.
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
        <p className="muted">Contact a tenant administrator to configure zones and templates.</p>
      )}

      <section className="card" style={{ marginBottom: '1.5rem' }}>
        <h3 style={{ marginTop: 0 }}>Delivery zones</h3>
        <p className="muted">Assign customers to a zone under Customers. v1 uses manual assignment only.</p>

        {isAdmin && (
          <div className="tabs">
            <button type="button" className={showZoneForm ? 'active' : ''} onClick={() => setShowZoneForm(!showZoneForm)}>
              + Add zone
            </button>
          </div>
        )}

        {showZoneForm && isAdmin && (
          <div style={{ marginBottom: '1rem' }}>
            <div className="form-row">
              <input
                placeholder="Zone name (e.g. Pasco County)"
                value={zoneForm.name}
                onChange={(e) => setZoneForm({ name: e.target.value })}
                style={{ flex: 1 }}
              />
              <button type="button" onClick={() => void createZone()} disabled={saving || !zoneForm.name.trim()}>
                {saving ? 'Saving…' : 'Add zone'}
              </button>
            </div>
          </div>
        )}

        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Customers</th>
              <th>Match</th>
              {isAdmin && <th></th>}
            </tr>
          </thead>
          <tbody>
            {zones.map((z) => (
              <tr key={z.id}>
                {editingZoneId === z.id ? (
                  <td colSpan={isAdmin ? 4 : 3}>
                    <div className="form-row">
                      <input value={editZoneForm.name} onChange={(e) => setEditZoneForm({ name: e.target.value })} />
                      <label className="checkbox-label">
                        <input type="checkbox" checked={editZoneActive} onChange={(e) => setEditZoneActive(e.target.checked)} />
                        Active
                      </label>
                      <button type="button" onClick={() => void saveZoneEdit()} disabled={saving}>Save</button>
                      <button type="button" className="secondary" onClick={() => setEditingZoneId(null)}>Cancel</button>
                    </div>
                  </td>
                ) : (
                  <>
                    <td>
                      {z.name}
                      {!z.isActive && <span className="muted"> (inactive)</span>}
                    </td>
                    <td>{z.customerCount}</td>
                    <td>{z.matchType}</td>
                    {isAdmin && (
                      <td>
                        <button type="button" className="secondary" onClick={() => {
                          setEditingZoneId(z.id);
                          setEditZoneForm({ name: z.name });
                          setEditZoneActive(z.isActive);
                        }}
                        >
                          Edit
                        </button>
                        {z.isActive && (
                          <>
                            {' '}
                            <button type="button" className="secondary" onClick={() => void deactivateZone(z.id)}>
                              Deactivate
                            </button>
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
        {zones.length === 0 && <p className="muted">No delivery zones yet.</p>}
      </section>

      <section className="card">
        <h3 style={{ marginTop: 0 }}>Fixed route templates</h3>
        <p className="muted">
          One template per zone defines the recurring route day, depot, and optional defaults for planning.
        </p>

        {isAdmin && (
          <div className="tabs">
            <button
              type="button"
              className={showTemplateForm ? 'active' : ''}
              onClick={() => setShowTemplateForm(!showTemplateForm)}
              disabled={zones.filter((z) => z.isActive).length === 0}
            >
              + Add template
            </button>
          </div>
        )}

        {showTemplateForm && isAdmin && (
          <div style={{ marginBottom: '1rem' }}>
            {renderTemplateFields(templateForm, setTemplateForm)}
            <div className="form-row">
              <button
                type="button"
                onClick={() => void createTemplate()}
                disabled={
                  saving
                  || !templateForm.name.trim()
                  || !templateForm.deliveryZoneId
                  || !templateForm.depotId
                  || templateForm.daysOfWeek.length === 0
                }
              >
                {saving ? 'Saving…' : 'Add template'}
              </button>
            </div>
          </div>
        )}

        <table>
          <thead>
            <tr>
              <th>Template</th>
              <th>Zone</th>
              <th>Runs</th>
              <th>Next run</th>
              <th>Held</th>
              <th>Due today</th>
              <th>Depot</th>
              {isAdmin && <th></th>}
            </tr>
          </thead>
          <tbody>
            {templates.map((t) => (
              <tr key={t.id}>
                {editingTemplateId === t.id ? (
                  <td colSpan={isAdmin ? 8 : 7}>
                    {renderTemplateFields(editTemplateForm, setEditTemplateForm, true)}
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={editTemplateActive}
                        onChange={(e) => setEditTemplateActive(e.target.checked)}
                      />
                      Active
                    </label>
                    <div className="form-row">
                      <button type="button" onClick={() => void saveTemplateEdit()} disabled={saving}>Save</button>
                      <button type="button" className="secondary" onClick={() => setEditingTemplateId(null)}>Cancel</button>
                    </div>
                  </td>
                ) : (
                  <>
                    <td>
                      {t.name}
                      {!t.isActive && <span className="muted"> (inactive)</span>}
                    </td>
                    <td>{t.deliveryZoneName}</td>
                    <td>{t.daysOfWeekLabel}</td>
                    <td>{t.nextRouteDate ?? '—'}</td>
                    <td>{t.heldOrderCount}</td>
                    <td>{t.dueOrderCount}</td>
                    <td>{t.depotName ?? '—'}</td>
                    {isAdmin && (
                      <td>
                        {fleetId && t.isActive && t.dueOrderCount > 0 && (
                          <>
                            <Link to={`/delivery/fleets/${fleetId}/plan?mode=fixed&template=${t.id}`}>
                              Plan
                            </Link>
                            {' · '}
                          </>
                        )}
                        <button type="button" className="secondary" onClick={() => startTemplateEdit(t)}>Edit</button>
                        {t.isActive && (
                          <>
                            {' '}
                            <button type="button" className="secondary" onClick={() => void deactivateTemplate(t.id)}>
                              Deactivate
                            </button>
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
        {templates.length === 0 && (
          <p className="muted">No fixed route templates yet. Add a zone first, then create a template.</p>
        )}
      </section>
    </div>
  );
}
