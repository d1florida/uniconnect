import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { CustomerDto, DeliveryZoneDto, FixedRouteTemplateDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';

const DEFAULT_OPEN_HOURS_START = '07:00';
const DEFAULT_OPEN_HOURS_END = '17:00';

const emptyForm = () => ({
  name: '',
  phone: '',
  externalRef: '',
  deliveryAddress: '',
  deliveryZoneId: '',
  deliveryWindowStart: DEFAULT_OPEN_HOURS_START,
  deliveryWindowEnd: DEFAULT_OPEN_HOURS_END,
  noDeliveryStart: '',
  noDeliveryEnd: '',
  notes: '',
});

function formatTimeRange(start?: string, end?: string) {
  if (!start || !end) return null;
  return `${start} – ${end}`;
}

function optionalTime(value: string) {
  const trimmed = value.trim();
  return trimmed || null;
}

export function DeliveryCustomersPage() {
  const { fleetId: fleetIdParam } = useParams<{ fleetId: string }>();
  const { user } = useAuth();
  const fleetId = fleetIdParam ?? user?.fleetId;
  const isAdmin = user?.isTenantAdmin ?? user?.isPlatformAdmin ?? false;

  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [zones, setZones] = useState<DeliveryZoneDto[]>([]);
  const [templates, setTemplates] = useState<FixedRouteTemplateDto[]>([]);
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
    return Promise.all([
      api.get<CustomerDto[]>(`/api/delivery/tenants/${fleetId}/customers${query}`),
      api.get<DeliveryZoneDto[]>(`/api/delivery/tenants/${fleetId}/delivery-zones`),
      api.get<FixedRouteTemplateDto[]>(`/api/delivery/tenants/${fleetId}/fixed-route-templates`),
    ]).then(([customerList, zoneList, templateList]) => {
      setCustomers(customerList);
      setZones(zoneList);
      setTemplates(templateList);
    });
  };

  useEffect(() => {
    if (!fleetId) {
      setLoading(false);
      return;
    }
    load(isAdmin)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load customers'))
      .finally(() => setLoading(false));
  }, [fleetId, isAdmin]);

  const createCustomer = async () => {
    if (!fleetId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.post<CustomerDto>(`/api/delivery/tenants/${fleetId}/customers`, {
        name: createForm.name.trim(),
        phone: createForm.phone.trim() || null,
        externalRef: createForm.externalRef.trim() || null,
        deliveryAddress: createForm.deliveryAddress.trim() || null,
        deliveryHours: null,
        deliveryWindowStart: createForm.deliveryWindowStart.trim() || null,
        deliveryWindowEnd: createForm.deliveryWindowEnd.trim() || null,
        noDeliveryStart: optionalTime(createForm.noDeliveryStart),
        noDeliveryEnd: optionalTime(createForm.noDeliveryEnd),
        deliveryZoneId: createForm.deliveryZoneId || null,
        notes: createForm.notes.trim() || null,
      });
      setShowForm(false);
      setCreateForm(emptyForm());
      setSavedMessage('Customer added.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add customer');
    } finally {
      setSaving(false);
    }
  };

  const startEdit = (customer: CustomerDto) => {
    setEditingId(customer.id);
    setEditForm({
      name: customer.name,
      phone: customer.phone ?? '',
      externalRef: customer.externalRef ?? '',
      deliveryAddress: customer.deliveryAddress ?? '',
      deliveryZoneId: customer.deliveryZoneId ?? '',
      deliveryWindowStart: customer.deliveryWindowStart ?? DEFAULT_OPEN_HOURS_START,
      deliveryWindowEnd: customer.deliveryWindowEnd ?? DEFAULT_OPEN_HOURS_END,
      noDeliveryStart: customer.noDeliveryStart ?? '',
      noDeliveryEnd: customer.noDeliveryEnd ?? '',
      notes: customer.notes ?? '',
    });
    setEditActive(customer.isActive);
    setError('');
  };

  const saveEdit = async () => {
    if (!fleetId || !editingId) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      await api.patch<CustomerDto>(`/api/delivery/tenants/${fleetId}/customers/${editingId}`, {
        name: editForm.name.trim(),
        phone: editForm.phone.trim() || null,
        externalRef: editForm.externalRef.trim() || null,
        deliveryAddress: editForm.deliveryAddress.trim() || null,
        deliveryHours: null,
        deliveryWindowStart: editForm.deliveryWindowStart.trim() || null,
        deliveryWindowEnd: editForm.deliveryWindowEnd.trim() || null,
        noDeliveryStart: optionalTime(editForm.noDeliveryStart),
        noDeliveryEnd: optionalTime(editForm.noDeliveryEnd),
        deliveryZoneId: editForm.deliveryZoneId || null,
        notes: editForm.notes.trim() || null,
        isActive: editActive,
      });
      setEditingId(null);
      setSavedMessage('Customer updated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update customer');
    } finally {
      setSaving(false);
    }
  };

  const deactivateCustomer = async (customerId: string) => {
    if (!fleetId || !window.confirm('Deactivate this customer?')) return;
    setSaving(true);
    setError('');
    setSavedMessage('');
    try {
      const customer = customers.find((c) => c.id === customerId);
      if (!customer) return;
      await api.patch<CustomerDto>(`/api/delivery/tenants/${fleetId}/customers/${customerId}`, {
        name: customer.name,
        phone: customer.phone ?? null,
        externalRef: customer.externalRef ?? null,
        deliveryAddress: customer.deliveryAddress ?? null,
        deliveryHours: null,
        deliveryWindowStart: customer.deliveryWindowStart ?? null,
        deliveryWindowEnd: customer.deliveryWindowEnd ?? null,
        noDeliveryStart: customer.noDeliveryStart ?? null,
        noDeliveryEnd: customer.noDeliveryEnd ?? null,
        deliveryZoneId: customer.deliveryZoneId ?? null,
        notes: customer.notes ?? null,
        isActive: false,
      });
      setSavedMessage('Customer deactivated.');
      await load(isAdmin);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to deactivate customer');
    } finally {
      setSaving(false);
    }
  };

  const templateForZone = (zoneId?: string) =>
    templates.find((t) => t.isActive && t.deliveryZoneId === zoneId);

  const zoneSelect = (
    value: string,
    onChange: (zoneId: string) => void,
  ) => (
    <label>
      Delivery zone
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">— None (daily planning) —</option>
        {zones.filter((z) => z.isActive).map((z) => (
          <option key={z.id} value={z.id}>{z.name}</option>
        ))}
      </select>
    </label>
  );

  if (loading) return <Loading />;
  if (!fleetId) return <p className="muted">No tenant selected.</p>;

  return (
    <div>
      <p className="breadcrumb">
          <Link to={`/delivery/fleets/${fleetId}`}>Delivery</Link> / Customers
          {' · '}
          <Link to={`/delivery/fleets/${fleetId}/fixed-routes`}>Fixed routes</Link>
        </p>
      <div className="page-header">
        <h1>Customers</h1>
        {isAdmin && (
          <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>
            + Add customer
          </button>
        )}
      </div>

      {error && <ErrorAlert message={error} />}
      {savedMessage && <p className="success">{savedMessage}</p>}

      {showForm && isAdmin && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row">
            <input placeholder="Name" value={createForm.name} onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })} />
            <input placeholder="Phone" value={createForm.phone} onChange={(e) => setCreateForm({ ...createForm, phone: e.target.value })} />
            <input placeholder="External ref (optional)" value={createForm.externalRef} onChange={(e) => setCreateForm({ ...createForm, externalRef: e.target.value })} />
          </div>
          <div className="form-row">
            <input placeholder="Delivery address" value={createForm.deliveryAddress} onChange={(e) => setCreateForm({ ...createForm, deliveryAddress: e.target.value })} style={{ flex: 1 }} />
          </div>
          <div className="form-row">
            {zoneSelect(createForm.deliveryZoneId, (deliveryZoneId) => setCreateForm({ ...createForm, deliveryZoneId }))}
            {templateForZone(createForm.deliveryZoneId) && (
              <span className="muted" style={{ alignSelf: 'end' }}>
                New orders will be held for {templateForZone(createForm.deliveryZoneId)?.name}.
              </span>
            )}
          </div>
          <div className="form-row" style={{ marginTop: '0.75rem' }}>
            <label>
              Open hours start
              <input
                type="time"
                value={createForm.deliveryWindowStart}
                onChange={(e) => setCreateForm({ ...createForm, deliveryWindowStart: e.target.value })}
              />
            </label>
            <label>
              Open hours end
              <input
                type="time"
                value={createForm.deliveryWindowEnd}
                onChange={(e) => setCreateForm({ ...createForm, deliveryWindowEnd: e.target.value })}
              />
            </label>
          </div>
          <p className="muted">Daily open hours for route planning.</p>
          <div className="form-row" style={{ marginTop: '0.75rem' }}>
            <label>
              No delivery start
              <input
                type="time"
                value={createForm.noDeliveryStart}
                onChange={(e) => setCreateForm({ ...createForm, noDeliveryStart: e.target.value })}
              />
            </label>
            <label>
              No delivery end
              <input
                type="time"
                value={createForm.noDeliveryEnd}
                onChange={(e) => setCreateForm({ ...createForm, noDeliveryEnd: e.target.value })}
              />
            </label>
          </div>
          <p className="muted">Optional daily block (e.g. lunch 12:00–13:00). Leave empty if not needed.</p>
          <div className="form-row">
            <input placeholder="Notes (optional)" value={createForm.notes} onChange={(e) => setCreateForm({ ...createForm, notes: e.target.value })} style={{ flex: 1 }} />
          </div>
          <div className="form-row">
            <button type="button" onClick={() => void createCustomer()} disabled={saving || !createForm.name.trim()}>
              {saving ? 'Saving…' : 'Add customer'}
            </button>
          </div>
        </div>
      )}

      <div className="cards" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
        {customers.map((c) => (
          <div key={c.id} className="card">
            {editingId === c.id ? (
              <>
                <div className="form-row">
                  <input value={editForm.name} onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} />
                  <input placeholder="Phone" value={editForm.phone} onChange={(e) => setEditForm({ ...editForm, phone: e.target.value })} />
                  <input placeholder="External ref" value={editForm.externalRef} onChange={(e) => setEditForm({ ...editForm, externalRef: e.target.value })} />
                </div>
                <div className="form-row">
                  <input placeholder="Delivery address" value={editForm.deliveryAddress} onChange={(e) => setEditForm({ ...editForm, deliveryAddress: e.target.value })} style={{ flex: 1 }} />
                </div>
                <div className="form-row">
                  {zoneSelect(editForm.deliveryZoneId, (deliveryZoneId) => setEditForm({ ...editForm, deliveryZoneId }))}
                </div>
                <div className="form-row" style={{ marginTop: '0.75rem' }}>
                  <label>
                    Open hours start
                    <input
                      type="time"
                      value={editForm.deliveryWindowStart}
                      onChange={(e) => setEditForm({ ...editForm, deliveryWindowStart: e.target.value })}
                    />
                  </label>
                  <label>
                    Open hours end
                    <input
                      type="time"
                      value={editForm.deliveryWindowEnd}
                      onChange={(e) => setEditForm({ ...editForm, deliveryWindowEnd: e.target.value })}
                    />
                  </label>
                </div>
                <div className="form-row" style={{ marginTop: '0.75rem' }}>
                  <label>
                    No delivery start
                    <input
                      type="time"
                      value={editForm.noDeliveryStart}
                      onChange={(e) => setEditForm({ ...editForm, noDeliveryStart: e.target.value })}
                    />
                  </label>
                  <label>
                    No delivery end
                    <input
                      type="time"
                      value={editForm.noDeliveryEnd}
                      onChange={(e) => setEditForm({ ...editForm, noDeliveryEnd: e.target.value })}
                    />
                  </label>
                </div>
                <div className="form-row">
                  <input placeholder="Notes" value={editForm.notes} onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })} style={{ flex: 1 }} />
                </div>
                <label className="checkbox-label">
                  <input type="checkbox" checked={editActive} onChange={(e) => setEditActive(e.target.checked)} />
                  Active
                </label>
                <div className="form-row">
                  <button type="button" onClick={() => void saveEdit()} disabled={saving}>Save</button>
                  <button type="button" className="secondary" onClick={() => setEditingId(null)}>Cancel</button>
                </div>
              </>
            ) : (
              <>
                <h3 style={{ marginTop: 0 }}>
                  {c.name}
                  {!c.isActive && <span className="muted"> (inactive)</span>}
                </h3>
                {c.deliveryZoneName && (
                  <p style={{ margin: '0.25rem 0' }}>
                    <span className="badge badge-held">{c.deliveryZoneName}</span>
                    {templateForZone(c.deliveryZoneId) && (
                      <span className="muted"> · {templateForZone(c.deliveryZoneId)?.name}</span>
                    )}
                  </p>
                )}
                {c.phone && <p className="muted" style={{ margin: '0.25rem 0' }}>{c.phone}</p>}
                {c.externalRef && (
                  <p className="muted" style={{ margin: '0.25rem 0' }}>
                    Ref: {c.externalRef}
                  </p>
                )}
                <p style={{ margin: '0.5rem 0' }}>
                  <strong>Delivery</strong>
                  <br />
                  {c.deliveryAddress ?? <span className="muted">No address set</span>}
                </p>
                <p style={{ margin: '0.5rem 0' }}>
                  <strong>Open hours</strong>
                  <br />
                  {formatTimeRange(c.deliveryWindowStart, c.deliveryWindowEnd) ?? (
                    <span className="muted">Not set</span>
                  )}
                </p>
                <p style={{ margin: '0.5rem 0' }}>
                  <strong>No deliveries</strong>
                  <br />
                  {formatTimeRange(c.noDeliveryStart, c.noDeliveryEnd) ?? (
                    <span className="muted">None</span>
                  )}
                </p>
                {c.deliveryLatitude != null && c.deliveryLongitude != null && (
                  <p className="muted" style={{ margin: '0.25rem 0', fontSize: '0.85rem' }}>
                    {c.deliveryLatitude.toFixed(4)}, {c.deliveryLongitude.toFixed(4)}
                  </p>
                )}
                {c.notes && <p className="muted" style={{ margin: '0.5rem 0' }}>{c.notes}</p>}
                {isAdmin && (
                  <div className="form-row" style={{ marginTop: '0.75rem' }}>
                    <button type="button" className="secondary" onClick={() => startEdit(c)}>Edit</button>
                    {c.isActive && (
                      <button type="button" className="secondary" onClick={() => void deactivateCustomer(c.id)}>Deactivate</button>
                    )}
                  </div>
                )}
              </>
            )}
          </div>
        ))}
      </div>
      {customers.length === 0 && (
        <p className="muted">No customers yet. Add delivery locations for repeat recipients.</p>
      )}
    </div>
  );
}
