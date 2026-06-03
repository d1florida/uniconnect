import { Link } from 'react-router-dom';
import type { DepotDto } from '../../../api/types';

interface DepotAddressFieldProps {
  label: string;
  placeholder: string;
  depots: DepotDto[];
  fleetId?: string;
  value: string;
  onChange: (address: string) => void;
}

export function DepotAddressField({
  label,
  placeholder,
  depots,
  fleetId,
  value,
  onChange,
}: DepotAddressFieldProps) {
  const matchingDepot = depots.find((d) => d.address === value);

  return (
    <label style={{ flex: 1, minWidth: '14rem' }}>
      {label}
      {depots.length > 0 ? (
        <select
          value={matchingDepot?.id ?? ''}
          onChange={(e) => {
            const depot = depots.find((d) => d.id === e.target.value);
            if (depot) onChange(depot.address);
          }}
          style={{ width: '100%', marginBottom: '0.35rem' }}
        >
          <option value="">Choose a depot…</option>
          {depots.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name} — {d.address}
            </option>
          ))}
        </select>
      ) : (
        fleetId && (
          <span className="muted" style={{ display: 'block', marginBottom: '0.35rem', fontSize: '0.875rem' }}>
            No depots configured.{' '}
            <Link to={`/delivery/fleets/${fleetId}/depots`}>Add a depot</Link>
          </span>
        )
      )}
      <input
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        style={{ width: '100%' }}
      />
    </label>
  );
}
