import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type {
  FleetDto,
  FleetModule,
  GeocodingProvider,
  MyTenantProfileDto,
  PasswordPolicyDto,
  TenantApiKeyDto,
  CreateTenantApiKeyResponse,
  TenantGeocodingSettingsDto,
  TestTenantGeocodingResultDto,
  TenantDeliverySettingsDto,
  TenantPlanningRulesDto,
  TenantRole,
  TenantUserDto,
} from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { Loading } from '../../../components/Loading';
import { ALL_FLEET_MODULES, formatModules, hasModule } from '../../../utils/fleetModules';
import { formatPasswordPolicy } from '../../../utils/passwordPolicy';
import {
  isGeographicClusteringEnabled,
  setGeographicClusteringInMarkdown,
} from '../../../utils/planningRulesMarkdown';

export function TenantSettingsPage() {
  const { user, refreshUser } = useAuth();
  const [tenant, setTenant] = useState<FleetDto | null>(null);
  const [passwordPolicy, setPasswordPolicy] = useState<PasswordPolicyDto | null>(null);
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [loading, setLoading] = useState(true);
  const [savingOrg, setSavingOrg] = useState(false);
  const [savingAccount, setSavingAccount] = useState(false);
  const [error, setError] = useState('');
  const [orgSaved, setOrgSaved] = useState(false);
  const [accountSaved, setAccountSaved] = useState(false);

  useEffect(() => {
    setLoading(true);
    setError('');
    api
      .get<MyTenantProfileDto>('/api/tenants/me')
      .then((profile) => {
        const t = normalizeFleetDto(profile.tenant);
        setTenant(t);
        setPasswordPolicy(profile.passwordPolicy);
        setName(t.name);
        setSlug(t.slug);
        setContactName(t.contactName);
        setContactEmail(t.contactEmail);
        setContactPhone(t.contactPhone);
        setDisplayName(user?.displayName ?? '');
        setEmail(user?.email ?? t.adminEmail ?? '');
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load settings'))
      .finally(() => setLoading(false));
  }, [user?.displayName, user?.email]);

  const saveOrganization = async () => {
    setSavingOrg(true);
    setError('');
    setOrgSaved(false);
    try {
      const updated = normalizeFleetDto(
        await api.patch<FleetDto>('/api/tenants/me', {
          name,
          slug,
          contactName,
          contactEmail,
          contactPhone,
        }),
      );
      setTenant(updated);
      setSlug(updated.slug);
      setOrgSaved(true);
      await refreshUser();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save organization');
    } finally {
      setSavingOrg(false);
    }
  };

  const saveAccount = async () => {
    setSavingAccount(true);
    setError('');
    setAccountSaved(false);
    try {
      const body: { displayName?: string; email?: string; newPassword?: string } = {};
      const trimmedName = displayName.trim();
      const trimmedEmail = email.trim();
      if (trimmedName && trimmedName !== user?.displayName) body.displayName = trimmedName;
      if (trimmedEmail && trimmedEmail !== user?.email) body.email = trimmedEmail;
      if (newPassword.trim()) body.newPassword = newPassword.trim();
      if (!body.displayName && !body.email && !body.newPassword) {
        setError('Change your display name, email, or enter a new password.');
        return;
      }
      await api.patch('/api/tenants/me/admin', body);
      setNewPassword('');
      setAccountSaved(true);
      await refreshUser();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save account');
    } finally {
      setSavingAccount(false);
    }
  };

  if (loading) return <Loading />;
  if (!tenant) return <p className="error">{error || 'Settings unavailable'}</p>;

  const isAdmin = user?.isTenantAdmin ?? false;
  const fleetId = user?.fleetId ?? tenant.id;
  const usesGeocoding =
    hasModule(tenant.modules, 'Delivery') || hasModule(tenant.modules, 'RoutePlanning');
  const usesRoutePlanning = hasModule(tenant.modules, 'RoutePlanning');
  const usesDelivery = hasModule(tenant.modules, 'Delivery');

  return (
    <div>
      <div className="page-header">
        <h2>Settings</h2>
        {!isAdmin && <p className="muted">Your role: Operator — contact a tenant administrator to change organization or team settings.</p>}
      </div>
      {error && <p className="error">{error}</p>}

      <fieldset className="module-picker">
        <legend>Organization</legend>
        <p className="muted">
          {isAdmin
            ? 'Update your tenant name, slug, and contact details. Product modules are managed by the platform administrator.'
            : 'Organization details (read-only). Product modules are managed by the platform administrator.'}
        </p>
        <div className="form-row">
          <input placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} disabled={!isAdmin} />
          <input placeholder="Slug" value={slug} onChange={(e) => setSlug(e.target.value)} disabled={!isAdmin} />
        </div>
        <div className="form-row">
          <input placeholder="Contact name" value={contactName} onChange={(e) => setContactName(e.target.value)} disabled={!isAdmin} />
          <input type="email" placeholder="Contact email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} disabled={!isAdmin} />
          <input type="tel" placeholder="Contact phone" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} disabled={!isAdmin} />
        </div>
        <p className="muted">Enabled products: {formatModules(tenant.modules)}</p>
        {user?.modules?.length ? (
          <p className="muted">Your access: {formatModules(user.modules)}</p>
        ) : null}
        {isAdmin && (
          <div className="form-row">
            <button type="button" onClick={saveOrganization} disabled={savingOrg}>
              {savingOrg ? 'Saving…' : 'Save organization'}
            </button>
            {orgSaved && <span className="muted">Saved.</span>}
          </div>
        )}
      </fieldset>

      <fieldset className="module-picker">
        <legend>Your account</legend>
        <div className="form-row">
          <input
            placeholder="Display name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
          />
          <input
            type="email"
            placeholder="Sign-in email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <input
            type="password"
            placeholder="New password (leave blank to keep)"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
        </div>
        {passwordPolicy && (
          <p className="muted">
            Password: {formatPasswordPolicy(passwordPolicy)} (e.g. Demo123!).
          </p>
        )}
        <div className="form-row">
          <button type="button" onClick={saveAccount} disabled={savingAccount}>
            {savingAccount ? 'Saving…' : 'Save account'}
          </button>
          {accountSaved && <span className="muted">Saved.</span>}
        </div>
      </fieldset>

        {isAdmin && hasModule(tenant.modules, 'Delivery') && fleetId && (
        <fieldset className="module-picker">
          <legend>Delivery</legend>
          <p className="muted">Manage warehouse and hub addresses used for route planning.</p>
          <p><Link to={`/delivery/fleets/${fleetId}/depots`}>Depots</Link></p>
        </fieldset>
      )}

      {isAdmin && (
        <TeamSection
          tenantModules={tenant.modules}
          currentUserId={user?.userId}
          onSelfUpdated={refreshUser}
        />
      )}
      {isAdmin && usesGeocoding && <GeocodingSection />}
      {isAdmin && usesDelivery && <DeliverySettingsSection />}
      {isAdmin && usesRoutePlanning && <PlanningRulesSection />}
      {isAdmin && <ApiKeysSection hasDelivery={hasModule(tenant.modules, 'Delivery')} />}
    </div>
  );
}

type TeamUserForm = {
  displayName: string;
  email: string;
  newPassword: string;
  role: TenantRole;
  modules: FleetModule[];
  isActive: boolean;
};

function TeamSection({
  tenantModules,
  currentUserId,
  onSelfUpdated,
}: {
  tenantModules: FleetModule[];
  currentUserId?: string;
  onSelfUpdated: () => Promise<void>;
}) {
  const [users, setUsers] = useState<TenantUserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingUserId, setEditingUserId] = useState<string | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);
  const [form, setForm] = useState({
    email: '',
    displayName: '',
    password: '',
    role: 'Operator' as TenantRole,
    modules: [] as FleetModule[],
  });
  const [editForm, setEditForm] = useState<TeamUserForm>({
    displayName: '',
    email: '',
    newPassword: '',
    role: 'Operator',
    modules: [],
    isActive: true,
  });

  const availableModules = ALL_FLEET_MODULES.filter((m) => tenantModules.includes(m.value));

  const load = () => api.get<TenantUserDto[]>('/api/tenants/me/users').then(setUsers);

  useEffect(() => {
    load()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load team'))
      .finally(() => setLoading(false));
  }, []);

  const toggleModule = (module: FleetModule) => {
    setForm((f) => ({
      ...f,
      modules: f.modules.includes(module) ? f.modules.filter((m) => m !== module) : [...f.modules, module],
    }));
  };

  const createUser = async () => {
    setError('');
    try {
      await api.post('/api/tenants/me/users', {
        email: form.email.trim(),
        displayName: form.displayName.trim(),
        password: form.password,
        role: form.role,
        moduleAccess: form.modules,
      });
      setShowForm(false);
      setForm({ email: '', displayName: '', password: '', role: 'Operator', modules: [] });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to add user');
    }
  };

  const startEdit = (u: TenantUserDto) => {
    setEditingUserId(u.id);
    setEditForm({
      displayName: u.displayName,
      email: u.email,
      newPassword: '',
      role: u.role,
      modules: [...u.moduleAccess],
      isActive: u.isActive,
    });
    setError('');
  };

  const cancelEdit = () => {
    setEditingUserId(null);
    setEditForm({
      displayName: '',
      email: '',
      newPassword: '',
      role: 'Operator',
      modules: [],
      isActive: true,
    });
  };

  const toggleEditModule = (module: FleetModule) => {
    setEditForm((f) => ({
      ...f,
      modules: f.modules.includes(module) ? f.modules.filter((m) => m !== module) : [...f.modules, module],
    }));
  };

  const saveEdit = async () => {
    if (!editingUserId) return;
    const original = users.find((u) => u.id === editingUserId);
    if (!original) return;

    const trimmedName = editForm.displayName.trim();
    const trimmedEmail = editForm.email.trim();
    if (!trimmedName) {
      setError('Display name is required.');
      return;
    }
    if (!trimmedEmail) {
      setError('Email is required.');
      return;
    }
    if (editForm.role !== 'Driver' && editForm.isActive && editForm.modules.length === 0) {
      setError('Active users must have at least one product.');
      return;
    }

    const patch: {
      displayName?: string;
      email?: string;
      newPassword?: string;
      role?: TenantRole;
      moduleAccess?: FleetModule[];
      isActive?: boolean;
    } = {};

    if (trimmedName !== original.displayName) patch.displayName = trimmedName;
    if (trimmedEmail !== original.email) patch.email = trimmedEmail;
    if (editForm.newPassword.trim()) patch.newPassword = editForm.newPassword.trim();
    if (editForm.role !== original.role && editingUserId !== currentUserId) patch.role = editForm.role;
    if (editForm.isActive !== original.isActive && editingUserId !== currentUserId) patch.isActive = editForm.isActive;
    if (editForm.role !== 'Driver') {
      const sameModules =
        editForm.modules.length === original.moduleAccess.length
        && editForm.modules.every((m) => original.moduleAccess.includes(m));
      if (!sameModules) patch.moduleAccess = editForm.modules;
    }

    if (Object.keys(patch).length === 0) {
      setError('No changes to save.');
      return;
    }

    setSavingEdit(true);
    setError('');
    const wasSelf = editingUserId === currentUserId;
    try {
      await api.patch(`/api/tenants/me/users/${editingUserId}`, patch);
      cancelEdit();
      await load();
      if (wasSelf) await onSelfUpdated();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update user');
    } finally {
      setSavingEdit(false);
    }
  };

  const removeUser = async (userId: string) => {
    if (!window.confirm('Remove this user? They will no longer be able to sign in.')) return;
    setError('');
    try {
      await api.delete(`/api/tenants/me/users/${userId}`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to remove user');
    }
  };

  return (
    <fieldset className="module-picker">
      <legend>Team</legend>
      <p className="muted">Manage who can sign in to this tenant and which products they can access.</p>
      {error && <p className="error">{error}</p>}
      <div className="tabs">
        <button type="button" className={showForm ? 'active' : ''} onClick={() => setShowForm(!showForm)}>+ Add user</button>
      </div>

      {showForm && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <div className="form-row">
            <input placeholder="Email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            <input placeholder="Display name" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
            <input type="password" placeholder="Initial password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
            <select
              value={form.role}
              onChange={(e) => {
                const role = e.target.value as TenantRole;
                setForm({
                  ...form,
                  role,
                  modules: role === 'Driver' && availableModules.some((m) => m.value === 'Delivery')
                    ? ['Delivery']
                    : form.modules,
                });
              }}
            >
              <option value="Operator">Operator</option>
              <option value="Driver">Driver</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
          {form.role === 'Driver' ? (
            <p className="muted">Driver accounts get Delivery access and an linked driver profile for routes and calendar.</p>
          ) : (
          <fieldset className="module-picker">
            <legend>Product access</legend>
            {availableModules.map((m) => (
              <label key={m.value} className="checkbox-label">
                <input
                  type="checkbox"
                  checked={form.modules.includes(m.value)}
                  onChange={() => toggleModule(m.value)}
                />
                {m.label}
              </label>
            ))}
          </fieldset>
          )}
          <div className="form-row">
            <button
              type="button"
              onClick={() => void createUser()}
              disabled={
                !form.email.trim()
                || !form.displayName.trim()
                || !form.password
                || (form.role !== 'Driver' && form.modules.length === 0)
              }
            >
              Add user
            </button>
          </div>
        </div>
      )}

      {editingUserId && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <h3 style={{ marginTop: 0 }}>Edit user</h3>
          <div className="form-row">
            <input
              placeholder="Display name"
              value={editForm.displayName}
              onChange={(e) => setEditForm({ ...editForm, displayName: e.target.value })}
            />
            <input
              type="email"
              placeholder="Email"
              value={editForm.email}
              onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
            />
            <input
              type="password"
              placeholder="New password (optional)"
              value={editForm.newPassword}
              onChange={(e) => setEditForm({ ...editForm, newPassword: e.target.value })}
            />
            <select
              value={editForm.role}
              disabled={editingUserId === currentUserId}
              onChange={(e) => {
                const role = e.target.value as TenantRole;
                setEditForm({
                  ...editForm,
                  role,
                  modules: role === 'Driver' && availableModules.some((m) => m.value === 'Delivery')
                    ? ['Delivery']
                    : editForm.modules,
                });
              }}
            >
              <option value="Operator">Operator</option>
              <option value="Driver">Driver</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
          {editForm.role === 'Driver' ? (
            <p className="muted">Driver accounts get Delivery access and a linked driver profile.</p>
          ) : (
            <fieldset className="module-picker">
              <legend>Product access</legend>
              {availableModules.map((m) => (
                <label key={m.value} className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={editForm.modules.includes(m.value)}
                    onChange={() => toggleEditModule(m.value)}
                  />
                  {m.label}
                </label>
              ))}
            </fieldset>
          )}
          {editingUserId !== currentUserId && (
            <label className="checkbox-label" style={{ display: 'block', marginTop: '0.5rem' }}>
              <input
                type="checkbox"
                checked={editForm.isActive}
                onChange={(e) => setEditForm({ ...editForm, isActive: e.target.checked })}
              />
              Active (can sign in)
            </label>
          )}
          {editingUserId === currentUserId && (
            <p className="muted">You cannot change your own role or disable your account here. Use &quot;Your account&quot; above for personal sign-in details.</p>
          )}
          <div className="form-row">
            <button
              type="button"
              onClick={() => void saveEdit()}
              disabled={savingEdit || !editForm.displayName.trim() || !editForm.email.trim()}
            >
              {savingEdit ? 'Saving…' : 'Save changes'}
            </button>
            <button type="button" className="secondary" onClick={cancelEdit} disabled={savingEdit}>
              Cancel
            </button>
          </div>
        </div>
      )}

      {loading ? (
        <p className="muted">Loading team…</p>
      ) : (
        <table>
          <thead>
            <tr><th>Name</th><th>Email</th><th>Role</th><th>Driver profile</th><th>Products</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.displayName}</td>
                <td>{u.email}</td>
                <td>{u.role}</td>
                <td className="muted">{u.linkedDriverName ?? '—'}</td>
                <td>{u.role === 'Driver' ? 'Delivery' : formatModules(u.moduleAccess)}</td>
                <td>{u.isActive ? 'Active' : 'Disabled'}</td>
                <td>
                  <button type="button" className="secondary" onClick={() => startEdit(u)}>
                    Edit
                  </button>
                  {u.id !== currentUserId && (
                    <>
                      {' '}
                      <button type="button" className="secondary" onClick={() => void removeUser(u.id)}>Remove</button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </fieldset>
  );
}

const GEOCODING_PROVIDER_OPTIONS: { value: GeocodingProvider; label: string; hint: string }[] = [
  {
    value: 'UsCensus',
    label: 'US Census Bureau',
    hint: 'Free US street geocoding. Best for US delivery addresses; no API key.',
  },
  {
    value: 'OpenStreetMap',
    label: 'OpenStreetMap (Nominatim)',
    hint: 'Uses OpenStreetMap data. Set a contact User-Agent; optional self-hosted server URL.',
  },
  {
    value: 'GoogleMaps',
    label: 'Google Maps',
    hint: 'Uses the Google Geocoding API. Create a server API key and enable the Geocoding API (not only Maps JavaScript) in Google Cloud Console.',
  },
];

function GeocodingSection() {
  const [settings, setSettings] = useState<TenantGeocodingSettingsDto | null>(null);
  const [provider, setProvider] = useState<GeocodingProvider>('OpenStreetMap');
  const [allowPostalFallback, setAllowPostalFallback] = useState(true);
  const [nominatimUserAgent, setNominatimUserAgent] = useState('');
  const [nominatimBaseUrl, setNominatimBaseUrl] = useState('');
  const [googleApiKey, setGoogleApiKey] = useState('');
  const [clearGoogleKey, setClearGoogleKey] = useState(false);
  const [testAddress, setTestAddress] = useState('1600 Amphitheatre Parkway, Mountain View, CA 94043');
  const [testResult, setTestResult] = useState<TestTenantGeocodingResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api
      .get<TenantGeocodingSettingsDto>('/api/tenants/me/geocoding')
      .then((s) => {
        setSettings(s);
        setProvider(s.provider);
        setAllowPostalFallback(s.allowPostalFallback);
        setNominatimUserAgent(s.nominatimUserAgent);
        setNominatimBaseUrl(s.nominatimBaseUrl ?? '');
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load geocoding settings'))
      .finally(() => setLoading(false));
  }, []);

  const save = async () => {
    if (provider === 'GoogleMaps' && !settings?.googleApiKeyConfigured && !googleApiKey.trim()) {
      setError('Enter your Google Geocoding API key before saving Google Maps as the provider.');
      return;
    }
    setSaving(true);
    setError('');
    setSaved(false);
    setTestResult(null);
    try {
      const body: {
        provider: GeocodingProvider;
        allowPostalFallback: boolean;
        nominatimUserAgent: string | null;
        nominatimBaseUrl: string | null;
        googleApiKey?: string | null;
      } = {
        provider,
        allowPostalFallback,
        nominatimUserAgent: nominatimUserAgent.trim() || null,
        nominatimBaseUrl: nominatimBaseUrl.trim() || null,
      };
      if (clearGoogleKey) body.googleApiKey = '';
      else if (googleApiKey.trim()) body.googleApiKey = googleApiKey.trim();

      const updated = await api.put<TenantGeocodingSettingsDto>('/api/tenants/me/geocoding', body);
      setSettings(updated);
      setProvider(updated.provider);
      setAllowPostalFallback(updated.allowPostalFallback);
      setNominatimUserAgent(updated.nominatimUserAgent);
      setNominatimBaseUrl(updated.nominatimBaseUrl ?? '');
      setGoogleApiKey('');
      setClearGoogleKey(false);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save geocoding settings');
    } finally {
      setSaving(false);
    }
  };

  const runTest = async () => {
    const trimmed = testAddress.trim();
    if (!trimmed) {
      setError('Enter a sample address to test.');
      return;
    }
    setTesting(true);
    setError('');
    setTestResult(null);
    try {
      setTestResult(await api.post<TestTenantGeocodingResultDto>('/api/tenants/me/geocoding/test', { address: trimmed }));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Geocoding test failed');
    } finally {
      setTesting(false);
    }
  };

  const selectedHint = GEOCODING_PROVIDER_OPTIONS.find((o) => o.value === provider)?.hint;

  return (
    <fieldset className="module-picker">
      <legend>Geocoding</legend>
      <p className="muted">
        Choose how delivery addresses, depots, and route planning resolve coordinates for your tenant.
        Until you save here, the platform default geocoder applies (US Census + OpenStreetMap for US addresses).
      </p>
      {error && <p className="error">{error}</p>}
      {loading ? (
        <p className="muted">Loading geocoding settings…</p>
      ) : (
        <>
          <div className="form-row" style={{ flexDirection: 'column', alignItems: 'stretch', gap: '0.5rem' }}>
            <label className="muted" htmlFor="geocoding-provider">
              Provider
            </label>
            <select
              id="geocoding-provider"
              value={provider}
              onChange={(e) => setProvider(e.target.value as GeocodingProvider)}
            >
              {GEOCODING_PROVIDER_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
            {selectedHint && <p className="muted">{selectedHint}</p>}
          </div>

          <label className="checkbox-label" style={{ display: 'block', marginTop: '0.75rem' }}>
            <input
              type="checkbox"
              checked={allowPostalFallback}
              onChange={(e) => setAllowPostalFallback(e.target.checked)}
            />
            Allow postal-code fallback when street lookup fails
          </label>

          {provider === 'OpenStreetMap' && (
            <>
              <div className="form-row" style={{ marginTop: '0.75rem' }}>
                <input
                  placeholder="Nominatim User-Agent (required for public OSM)"
                  value={nominatimUserAgent}
                  onChange={(e) => setNominatimUserAgent(e.target.value)}
                  aria-label="Nominatim User-Agent"
                />
              </div>
              <div className="form-row">
                <input
                  placeholder="Nominatim base URL (optional, e.g. self-hosted)"
                  value={nominatimBaseUrl}
                  onChange={(e) => setNominatimBaseUrl(e.target.value)}
                  aria-label="Nominatim base URL"
                />
              </div>
            </>
          )}

          {provider === 'GoogleMaps' && (
            <div style={{ marginTop: '0.75rem' }}>
              {settings?.googleApiKeyDecryptFailed && (
                <p className="error">
                  A Google API key is stored but could not be read (this can happen after an API restart).
                  Re-enter your key below and save.
                </p>
              )}
              {settings?.googleApiKeyConfigured && !settings.googleApiKeyDecryptFailed && (
                <p className="muted">
                  API key on file{settings.googleApiKeyHint ? ` (${settings.googleApiKeyHint})` : ''}. Enter a new key below to replace it.
                </p>
              )}
              <div className="form-row">
                <input
                  type="password"
                  placeholder={settings?.googleApiKeyConfigured ? 'New Google API key (optional)' : 'Google Geocoding API key'}
                  value={googleApiKey}
                  onChange={(e) => {
                    setGoogleApiKey(e.target.value);
                    setClearGoogleKey(false);
                  }}
                  aria-label="Google Geocoding API key"
                  autoComplete="off"
                />
              </div>
              {settings?.googleApiKeyConfigured && (
                <label className="checkbox-label" style={{ display: 'block', marginTop: '0.5rem' }}>
                  <input
                    type="checkbox"
                    checked={clearGoogleKey}
                    onChange={(e) => {
                      setClearGoogleKey(e.target.checked);
                      if (e.target.checked) setGoogleApiKey('');
                    }}
                  />
                  Remove stored API key
                </label>
              )}
              <p className="muted">
                In Google Cloud Console: APIs &amp; Services → Library → enable <strong>Geocoding API</strong>. Use a server key (IP restriction), not a browser-restricted key.
              </p>
            </div>
          )}

          <div style={{ marginTop: '1rem' }}>
            <p className="muted">Test the saved provider (save first if you changed settings).</p>
            <div className="form-row">
              <input
                placeholder="Sample address"
                value={testAddress}
                onChange={(e) => setTestAddress(e.target.value)}
                aria-label="Test address"
              />
              <button type="button" className="secondary" onClick={() => void runTest()} disabled={testing}>
                {testing ? 'Testing…' : 'Test geocoding'}
              </button>
            </div>
            {testResult && (
              <p className={testResult.success ? 'muted' : 'error'} style={{ marginTop: '0.5rem' }}>
                {testResult.message}
                {testResult.success && testResult.standardizedAddress && (
                  <>
                    {' '}
                    — {testResult.standardizedAddress}
                    {testResult.source ? ` (${testResult.source})` : ''}
                  </>
                )}
              </p>
            )}
          </div>

          {settings && !settings.isConfigured && (
            <p className="muted" style={{ marginTop: '0.5rem' }}>
              Using platform defaults until you save a provider choice.
            </p>
          )}
          {settings?.updatedAt && (
            <p className="muted" style={{ marginTop: '0.5rem' }}>
              Last saved {new Date(settings.updatedAt).toLocaleString()}.
            </p>
          )}

          <div className="form-row" style={{ marginTop: '0.75rem' }}>
            <button type="button" onClick={() => void save()} disabled={saving}>
              {saving ? 'Saving…' : 'Save geocoding'}
            </button>
            {saved && <span className="muted">Saved.</span>}
          </div>
        </>
      )}
    </fieldset>
  );
}

const TIME_ZONE_OPTIONS = [
  { id: 'America/New_York', label: 'Eastern (America/New_York)' },
  { id: 'America/Chicago', label: 'Central (America/Chicago)' },
  { id: 'America/Denver', label: 'Mountain (America/Denver)' },
  { id: 'America/Phoenix', label: 'Arizona (America/Phoenix)' },
  { id: 'America/Los_Angeles', label: 'Pacific (America/Los_Angeles)' },
];

function DeliverySettingsSection() {
  const [settings, setSettings] = useState<TenantDeliverySettingsDto | null>(null);
  const [allowMultipleRoutes, setAllowMultipleRoutes] = useState(false);
  const [timeZoneId, setTimeZoneId] = useState('America/New_York');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api
      .get<TenantDeliverySettingsDto>('/api/tenants/me/delivery-settings')
      .then((s) => {
        setSettings(s);
        setAllowMultipleRoutes(s.allowMultipleRoutesPerDriverPerDay);
        setTimeZoneId(s.timeZoneId);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load delivery settings'))
      .finally(() => setLoading(false));
  }, []);

  const save = async () => {
    setSaving(true);
    setError('');
    setSaved(false);
    try {
      const updated = await api.put<TenantDeliverySettingsDto>('/api/tenants/me/delivery-settings', {
        allowMultipleRoutesPerDriverPerDay: allowMultipleRoutes,
        timeZoneId,
      });
      setSettings(updated);
      setAllowMultipleRoutes(updated.allowMultipleRoutesPerDriverPerDay);
      setTimeZoneId(updated.timeZoneId);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save delivery settings');
    } finally {
      setSaving(false);
    }
  };

  return (
    <fieldset className="module-picker">
      <legend>Delivery operations</legend>
      <p className="muted">
        Driver weekly schedules and route caps are set under Delivery → Drivers. These tenant-wide options affect route
        planning.
      </p>
      {error && <p className="error">{error}</p>}
      {loading ? (
        <p className="muted">Loading delivery settings…</p>
      ) : (
        <>
          <label className="checkbox-label" style={{ display: 'block', marginBottom: '0.75rem' }}>
            <input
              type="checkbox"
              checked={allowMultipleRoutes}
              onChange={(e) => {
                setAllowMultipleRoutes(e.target.checked);
                setSaved(false);
              }}
            />
            Allow multiple routes per driver per day
          </label>
          <p className="muted" style={{ marginTop: '-0.35rem', marginBottom: '0.75rem' }}>
            When enabled, the planner may assign more than one route to the same driver on a plan date (within their
            available shift minutes).
          </p>
          <label>
            Operations timezone
            <select
              value={timeZoneId}
              onChange={(e) => {
                setTimeZoneId(e.target.value);
                setSaved(false);
              }}
              style={{ display: 'block', marginTop: '0.25rem', minWidth: '20rem' }}
            >
              {TIME_ZONE_OPTIONS.map((tz) => (
                <option key={tz.id} value={tz.id}>
                  {tz.label}
                </option>
              ))}
            </select>
          </label>
          {settings?.updatedAt && (
            <p className="muted" style={{ marginTop: '0.5rem' }}>
              Last saved {new Date(settings.updatedAt).toLocaleString()}.
            </p>
          )}
          <div className="form-row" style={{ marginTop: '0.75rem' }}>
            <button type="button" onClick={() => void save()} disabled={saving}>
              {saving ? 'Saving…' : 'Save delivery settings'}
            </button>
            {saved && <span className="muted">Saved.</span>}
          </div>
        </>
      )}
    </fieldset>
  );
}

function PlanningRulesSection() {
  const [rules, setRules] = useState<TenantPlanningRulesDto | null>(null);
  const [markdown, setMarkdown] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [compiling, setCompiling] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  const [showPolicy, setShowPolicy] = useState(false);

  useEffect(() => {
    api
      .get<TenantPlanningRulesDto>('/api/tenants/me/planning-rules')
      .then((r) => {
        setRules(r);
        setMarkdown(r.markdown);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load planning rules'))
      .finally(() => setLoading(false));
  }, []);

  const save = async () => {
    setSaving(true);
    setError('');
    setSaved(false);
    try {
      const updated = await api.put<TenantPlanningRulesDto>('/api/tenants/me/planning-rules', { markdown });
      setRules(updated);
      setMarkdown(updated.markdown);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save planning rules');
    } finally {
      setSaving(false);
    }
  };

  const compile = async () => {
    setCompiling(true);
    setError('');
    try {
      const result = await api.post<{ compiledPolicyJson?: string; warnings: string[]; usedAi: boolean }>(
        '/api/tenants/me/planning-rules/compile',
        {},
      );
      const refreshed = await api.get<TenantPlanningRulesDto>('/api/tenants/me/planning-rules');
      setRules(refreshed);
      setSaved(true);
      if (result.warnings.length > 0) {
        setError(result.warnings.join(' '));
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to compile planning rules');
    } finally {
      setCompiling(false);
    }
  };

  return (
    <fieldset className="module-picker">
      <legend>Route planning rules</legend>
      <p className="muted">
        Write fleet-wide planning policy in plain English (truck balancing, clustering, caps enforcement). Driver
        shifts and weekly schedules live under Delivery → Drivers — the <code>## Drivers</code> section is ignored if
        present. Optional AI refines the compile step when <code>PlanningRules:OpenAiApiKey</code> is set on the API
        host.
      </p>
      {error && <p className="error">{error}</p>}
      {loading ? (
        <p className="muted">Loading planning rules…</p>
      ) : (
        <>
          <label className="checkbox-label" style={{ display: 'block', marginBottom: '0.75rem' }}>
            <input
              type="checkbox"
              checked={isGeographicClusteringEnabled(markdown, rules?.compiledPolicyJson)}
              onChange={(e) => {
                const next = setGeographicClusteringInMarkdown(markdown, e.target.checked);
                setMarkdown(next);
                setSaved(false);
              }}
            />
            Cluster stops by geography when multiple trucks run
          </label>
          <p className="muted" style={{ marginTop: '-0.35rem', marginBottom: '0.75rem' }}>
            Groups nearby deliveries on the same route to reduce cross-region miles. When off, routes are split by
            estimated drive time or stop count only.
          </p>
          <textarea
            rows={14}
            style={{ width: '100%', fontFamily: 'monospace', fontSize: '0.9rem' }}
            value={markdown}
            onChange={(e) => setMarkdown(e.target.value)}
            spellCheck={false}
          />
          {rules?.compileWarnings?.length ? (
            <ul className="muted">
              {rules.compileWarnings.map((w) => (
                <li key={w}>{w}</li>
              ))}
            </ul>
          ) : null}
          {rules?.compiledAt && (
            <p className="muted">
              Last compiled {new Date(rules.compiledAt).toLocaleString()}
              {rules.compiledPolicyJson && (
                <>
                  {' · '}
                  <button type="button" className="link-button" onClick={() => setShowPolicy((v) => !v)}>
                    {showPolicy ? 'Hide' : 'Show'} compiled policy
                  </button>
                </>
              )}
            </p>
          )}
          {showPolicy && rules?.compiledPolicyJson && (
            <pre className="muted" style={{ whiteSpace: 'pre-wrap', fontSize: '0.8rem' }}>
              {JSON.stringify(JSON.parse(rules.compiledPolicyJson), null, 2)}
            </pre>
          )}
          <div className="form-row">
            <button type="button" onClick={() => void save()} disabled={saving}>
              {saving ? 'Saving…' : 'Save & compile'}
            </button>
            <button type="button" onClick={() => void compile()} disabled={compiling}>
              {compiling ? 'Compiling…' : 'Re-compile'}
            </button>
            {saved && <span className="muted">Saved.</span>}
          </div>
        </>
      )}
    </fieldset>
  );
}

function ApiKeysSection({ hasDelivery }: { hasDelivery: boolean }) {
  const [keys, setKeys] = useState<TenantApiKeyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [keyName, setKeyName] = useState('');
  const [creating, setCreating] = useState(false);
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
  const revealRef = useRef<HTMLDivElement>(null);

  const loadKeys = () =>
    api.get<TenantApiKeyDto[]>('/api/tenants/me/api-keys').then(setKeys);

  useEffect(() => {
    loadKeys()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load API keys'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (revealedSecret) {
      revealRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, [revealedSecret]);

  const createKey = async () => {
    const trimmed = keyName.trim();
    if (!trimmed) {
      setError('Enter a key label before generating.');
      return;
    }
    setCreating(true);
    setError('');
    try {
      const res = await api.post<CreateTenantApiKeyResponse>('/api/tenants/me/api-keys', {
        name: trimmed,
      });
      if (!res.secret) {
        setError('Key was created but the secret was missing from the response. Revoke and create a new key.');
        await loadKeys().catch(() => undefined);
        return;
      }
      setRevealedSecret(res.secret);
      setKeyName('');
      try {
        await loadKeys();
      } catch {
        setError('Key created — copy the secret below. Refresh the page to see it in the list.');
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create API key');
    } finally {
      setCreating(false);
    }
  };

  const revokeKey = async (keyId: string) => {
    if (!window.confirm('Revoke this API key? Partner integrations using it will stop working.')) return;
    setError('');
    try {
      await api.delete(`/api/tenants/me/api-keys/${keyId}`);
      await loadKeys();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to revoke API key');
    }
  };

  return (
    <fieldset className="module-picker">
      <legend>API keys</legend>
      <p className="muted">
        Generate keys for partner integrations. Partners send <code>X-Api-Key</code> on delivery partner endpoints.
      </p>
      {hasDelivery && (
        <p className="muted">
          Partner routes: <code>POST /api/delivery/partner/orders</code>,{' '}
          <code>GET /api/delivery/partner/orders</code>,{' '}
          <code>GET /api/delivery/partner/orders/:id</code>
        </p>
      )}
      {!hasDelivery && (
        <p className="muted">Delivery module is required for partner order APIs.</p>
      )}
      {error && <p className="error">{error}</p>}

      <div className="form-row">
        <input
          placeholder="Key label (e.g. ERP integration)"
          value={keyName}
          onChange={(e) => {
            setKeyName(e.target.value);
            if (error) setError('');
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              void createKey();
            }
          }}
          aria-label="API key label"
        />
        <button type="button" onClick={() => void createKey()} disabled={creating || !keyName.trim()}>
          {creating ? 'Creating…' : 'Generate key'}
        </button>
      </div>
      {!keyName.trim() && !creating && (
        <p className="muted">Enter a label, then click Generate key.</p>
      )}

      {revealedSecret && (
        <div ref={revealRef} className="api-key-reveal card">
          <h3>New API key — copy now</h3>
          <p className="muted">This secret is shown once. Store it securely.</p>
          <code className="api-key-secret">{revealedSecret}</code>
          <div className="form-row">
            <button type="button" onClick={() => navigator.clipboard.writeText(revealedSecret)}>
              Copy to clipboard
            </button>
            <button type="button" className="secondary" onClick={() => setRevealedSecret(null)}>
              Dismiss
            </button>
          </div>
        </div>
      )}

      {loading ? (
        <p className="muted">Loading keys…</p>
      ) : (
        <table>
          <thead>
            <tr><th>Name</th><th>Prefix</th><th>Created</th><th>Last used</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {keys.map((k) => (
              <tr key={k.id}>
                <td>{k.name}</td>
                <td><code>{k.keyPrefix}…</code></td>
                <td>{new Date(k.createdAt).toLocaleDateString()}</td>
                <td>{k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleString() : '—'}</td>
                <td>{k.isActive ? 'Active' : 'Revoked'}</td>
                <td>
                  {k.isActive && (
                    <button type="button" className="secondary" onClick={() => revokeKey(k.id)}>
                      Revoke
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {keys.length === 0 && (
              <tr><td colSpan={6} className="muted">No API keys yet.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </fieldset>
  );
}
