import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type { FleetDto, FleetModule } from '../../../api/types';
import { ALL_FLEET_MODULES, formatModules } from '../../../utils/fleetModules';
import { isDemoTenant } from '../../../utils/demoTenants';

const PRODUCT_LINKS: Record<FleetModule, { label: string; to: (id: string) => string }> = {
  General: { label: 'General Fleet', to: (id) => `/fleet/fleets/${id}/vehicles` },
  RoboTaxi: { label: 'Robo-Taxi', to: (id) => `/robo-taxis/fleets/${id}` },
  Delivery: { label: 'Delivery', to: (id) => `/delivery/fleets/${id}/orders` },
};

export function FleetDetailPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const navigate = useNavigate();
  const [fleet, setFleet] = useState<FleetDto | null>(null);
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [adminEmail, setAdminEmail] = useState('');
  const [adminPassword, setAdminPassword] = useState('');
  const [modules, setModules] = useState<FleetModule[]>([]);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (!fleetId) return;
    setError('');
    setFleet(null);
    api.get<FleetDto>(`/api/tenants/${fleetId}`)
      .then((f) => {
        const fleet = normalizeFleetDto(f);
        setFleet(fleet);
        setName(fleet.name);
        setSlug(fleet.slug);
        setContactName(fleet.contactName);
        setContactEmail(fleet.contactEmail);
        setContactPhone(fleet.contactPhone);
        setAdminEmail(fleet.adminEmail ?? '');
        setAdminPassword('');
        setModules(fleet.modules);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed'));
  }, [fleetId]);

  const toggleModule = (module: FleetModule) => {
    setModules((prev) =>
      prev.includes(module) ? prev.filter((m) => m !== module) : [...prev, module],
    );
  };

  const save = async () => {
    if (!fleetId) return;
    if (modules.length === 0) {
      setError('Select at least one product module.');
      return;
    }
    try {
      const body: Record<string, unknown> = {
        name,
        slug,
        modules,
        contactName,
        contactEmail,
        contactPhone,
        adminEmail: adminEmail.trim(),
      };
      if (adminPassword.trim()) body.adminPassword = adminPassword.trim();

      const updated = normalizeFleetDto(await api.patch<FleetDto>(`/api/tenants/${fleetId}`, body));
      setFleet(updated);
      setContactName(updated.contactName);
      setContactEmail(updated.contactEmail);
      setContactPhone(updated.contactPhone);
      setAdminEmail(updated.adminEmail ?? '');
      setAdminPassword('');
      setModules(updated.modules);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  };

  const remove = async () => {
    if (!fleetId || !fleet) return;
    if (!window.confirm(`Delete tenant "${fleet.name}"? All vehicles, orders, and admin users for this tenant will be permanently removed.`)) return;
    try {
      setError('');
      await api.delete(`/api/tenants/${fleetId}`);
      navigate('/tenants');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete');
    }
  };

  if (!fleet) {
    return (
      <div>
        <div className="page-header"><h2>Tenant</h2><Link to="/tenants">← All tenants</Link></div>
        {error ? <p className="error">{error}</p> : <p className="loading">Loading…</p>}
      </div>
    );
  }

  return (
    <div>
      <div className="page-header">
        <h2>{fleet.name}</h2>
        <Link to="/tenants">← All tenants</Link>
      </div>
      {error && <p className="error">{error}</p>}
      {saved && <p className="muted">Saved.</p>}
      <p className="muted">Created {new Date(fleet.createdAt).toLocaleDateString()}</p>
      <div className="form-row">
        <input placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} />
        <input placeholder="Slug" value={slug} onChange={(e) => setSlug(e.target.value)} />
        <button type="button" onClick={save}>Save</button>
      </div>
      <fieldset className="module-picker">
        <legend>Contact</legend>
        <div className="form-row">
          <input placeholder="Contact name" value={contactName} onChange={(e) => setContactName(e.target.value)} />
          <input type="email" placeholder="Contact email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} />
          <input type="tel" placeholder="Contact phone" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} />
        </div>
      </fieldset>
      <fieldset className="module-picker">
        <legend>Tenant administrator</legend>
        <div className="form-row">
          <input
            type="email"
            placeholder="Administrator email"
            value={adminEmail}
            onChange={(e) => setAdminEmail(e.target.value)}
          />
          <input
            type="password"
            placeholder="New password (leave blank to keep)"
            value={adminPassword}
            onChange={(e) => setAdminPassword(e.target.value)}
          />
        </div>
        <p className="muted">Password: at least 8 characters with one digit (e.g. Demo123!). Leave blank to keep the current password.</p>
      </fieldset>
      <fieldset className="module-picker">
        <legend>Product modules</legend>
        {ALL_FLEET_MODULES.map(({ value, label }) => (
          <label key={value} className="checkbox-label">
            <input
              type="checkbox"
              checked={modules.includes(value)}
              onChange={() => toggleModule(value)}
            />
            {label}
          </label>
        ))}
      </fieldset>
      <p className="muted">Enabled: {formatModules(fleet.modules)}</p>
      <ul>
        {fleet.modules.map((m) => (
          <li key={m}>
            <Link to={PRODUCT_LINKS[m].to(fleet.id)}>Open {PRODUCT_LINKS[m].label}</Link>
          </li>
        ))}
      </ul>
      {!isDemoTenant(fleet.id) && (
        <div className="form-row" style={{ marginTop: '1.5rem' }}>
          <button type="button" className="secondary" onClick={remove}>Delete tenant</button>
        </div>
      )}
    </div>
  );
}
