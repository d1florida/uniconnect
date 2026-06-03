import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type { FleetDto, FleetModule, MyTenantProfileDto, PasswordPolicyDto, TenantApiKeyDto, CreateTenantApiKeyResponse, TenantRole, TenantUserDto } from '../../../api/types';
import { useAuth } from '../../../auth/AuthContext';
import { Loading } from '../../../components/Loading';
import { ALL_FLEET_MODULES, formatModules, hasModule } from '../../../utils/fleetModules';
import { formatPasswordPolicy } from '../../../utils/passwordPolicy';

export function TenantSettingsPage() {
  const { user, refreshUser } = useAuth();
  const [tenant, setTenant] = useState<FleetDto | null>(null);
  const [passwordPolicy, setPasswordPolicy] = useState<PasswordPolicyDto | null>(null);
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
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
        setEmail(user?.email ?? t.adminEmail ?? '');
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load settings'))
      .finally(() => setLoading(false));
  }, [user?.email]);

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
      const body: { email?: string; newPassword?: string } = {};
      const trimmedEmail = email.trim();
      if (trimmedEmail && trimmedEmail !== user?.email) body.email = trimmedEmail;
      if (newPassword.trim()) body.newPassword = newPassword.trim();
      if (!body.email && !body.newPassword) {
        setError('Change your email or enter a new password.');
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

      {isAdmin && <TeamSection tenantModules={tenant.modules} currentUserId={user?.userId} />}
      {isAdmin && <ApiKeysSection hasDelivery={hasModule(tenant.modules, 'Delivery')} />}
    </div>
  );
}

function TeamSection({ tenantModules, currentUserId }: { tenantModules: FleetModule[]; currentUserId?: string }) {
  const [users, setUsers] = useState<TenantUserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    email: '',
    displayName: '',
    password: '',
    role: 'Operator' as TenantRole,
    modules: [] as FleetModule[],
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

  const updateUser = async (userId: string, patch: { role?: TenantRole; moduleAccess?: FleetModule[]; isActive?: boolean }) => {
    setError('');
    try {
      await api.patch(`/api/tenants/me/users/${userId}`, patch);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update user');
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
            <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as TenantRole })}>
              <option value="Operator">Operator</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
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
          <div className="form-row">
            <button type="button" onClick={() => void createUser()} disabled={!form.email.trim() || !form.displayName.trim() || !form.password || form.modules.length === 0}>
              Add user
            </button>
          </div>
        </div>
      )}

      {loading ? (
        <p className="muted">Loading team…</p>
      ) : (
        <table>
          <thead>
            <tr><th>Name</th><th>Email</th><th>Role</th><th>Products</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.displayName}</td>
                <td>{u.email}</td>
                <td>
                  <select
                    value={u.role}
                    disabled={u.id === currentUserId}
                    onChange={(e) => void updateUser(u.id, { role: e.target.value as TenantRole })}
                  >
                    <option value="Operator">Operator</option>
                    <option value="Admin">Admin</option>
                  </select>
                </td>
                <td>
                  {availableModules.map((m) => (
                    <label key={m.value} className="checkbox-label" style={{ display: 'inline-flex', marginRight: '0.75rem' }}>
                      <input
                        type="checkbox"
                        checked={u.moduleAccess.includes(m.value)}
                        onChange={() => {
                          const next = u.moduleAccess.includes(m.value)
                            ? u.moduleAccess.filter((x) => x !== m.value)
                            : [...u.moduleAccess, m.value];
                          if (next.length === 0) {
                            setError('Users must have at least one product.');
                            return;
                          }
                          void updateUser(u.id, { moduleAccess: next });
                        }}
                      />
                      {m.label}
                    </label>
                  ))}
                </td>
                <td>{u.isActive ? 'Active' : 'Disabled'}</td>
                <td>
                  {u.id !== currentUserId && (
                    <>
                      <button type="button" className="secondary" onClick={() => void updateUser(u.id, { isActive: !u.isActive })}>
                        {u.isActive ? 'Disable' : 'Enable'}
                      </button>
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
