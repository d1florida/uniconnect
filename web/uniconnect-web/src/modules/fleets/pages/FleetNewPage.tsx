import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../../api/client';
import { normalizeFleetDto } from '../../../api/normalize';
import type { FleetDto, FleetModule } from '../../../api/types';
import { ALL_FLEET_MODULES } from '../../../utils/fleetModules';

export function FleetNewPage() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [adminEmail, setAdminEmail] = useState('');
  const [adminPassword, setAdminPassword] = useState('');
  const [modules, setModules] = useState<FleetModule[]>(['General']);
  const [error, setError] = useState('');

  const toggleModule = (module: FleetModule) => {
    setModules((prev) => {
      if (prev.includes(module)) {
        let next = prev.filter((m) => m !== module);
        if (module === 'Delivery') next = next.filter((m) => m !== 'RoutePlanning' && m !== 'Insights');
        return next;
      }
      let next = [...prev, module];
      const meta = ALL_FLEET_MODULES.find((m) => m.value === module);
      if (meta?.requiresDelivery && !next.includes('Delivery')) next.push('Delivery');
      return next;
    });
  };

  const create = async () => {
    if (modules.length === 0) {
      setError('Select at least one product module.');
      return;
    }
    if (!adminEmail.trim()) {
      setError('Tenant administrator email is required.');
      return;
    }
    if (!adminPassword) {
      setError('Tenant administrator password is required.');
      return;
    }
    try {
      const fleet = normalizeFleetDto(await api.post<FleetDto>('/api/tenants', {
        name,
        slug,
        modules,
        contactName,
        contactEmail,
        contactPhone,
        adminEmail: adminEmail.trim(),
        adminPassword: adminPassword.trim(),
      }));
      if (!fleet?.id) {
        setError('Create succeeded but the API returned an invalid response. Restart the API (.\\dev.ps1) and try again.');
        return;
      }
      navigate(`/tenants/${fleet.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  };

  return (
    <div>
      <div className="page-header"><h2>New tenant</h2></div>
      {error && <p className="error">{error}</p>}
      <div className="form-row">
        <input placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} />
        <input placeholder="Slug" value={slug} onChange={(e) => setSlug(e.target.value)} />
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
            required
          />
          <input
            type="password"
            placeholder="Administrator password"
            value={adminPassword}
            onChange={(e) => setAdminPassword(e.target.value)}
            required
          />
        </div>
        <p className="muted">Password: at least 8 characters with one digit (e.g. Demo123!).</p>
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
      <div className="form-row">
        <button type="button" onClick={create}>Create</button>
      </div>
    </div>
  );
}
