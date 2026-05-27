import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { BusinessAccountDto } from '../../../api/types';

export function DeliveryBusinessAccountsPage() {
  const { fleetId } = useParams<{ fleetId: string }>();
  const [accounts, setAccounts] = useState<BusinessAccountDto[]>([]);
  const [form, setForm] = useState({ companyName: '', accountCode: '', contactEmail: '' });

  const load = () => api.get<BusinessAccountDto[]>(`/api/delivery/tenants/${fleetId}/business-accounts`).then(setAccounts);

  useEffect(() => {
    if (fleetId) load();
  }, [fleetId]);

  const create = async () => {
    await api.post(`/api/delivery/tenants/${fleetId}/business-accounts`, form);
    setForm({ companyName: '', accountCode: '', contactEmail: '' });
    await load();
  };

  return (
    <div>
      <div className="page-header"><h2>B2B accounts</h2></div>
      <div className="form-row">
        <input placeholder="Company" value={form.companyName} onChange={(e) => setForm({ ...form, companyName: e.target.value })} />
        <input placeholder="Account code" value={form.accountCode} onChange={(e) => setForm({ ...form, accountCode: e.target.value })} />
        <input placeholder="Email" value={form.contactEmail} onChange={(e) => setForm({ ...form, contactEmail: e.target.value })} />
        <button onClick={create}>Add account</button>
      </div>
      <table>
        <thead><tr><th>Company</th><th>Code</th><th>Email</th></tr></thead>
        <tbody>
          {accounts.map((a) => (
            <tr key={a.id}><td>{a.companyName}</td><td>{a.accountCode}</td><td>{a.contactEmail}</td></tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
