import { useEffect, useMemo, useState } from 'react';

import { Link } from 'react-router-dom';

import type { DepotDto } from '../../../api/types';

import { isDepotPickupAddress } from '../deliveryLabels';



interface DepotAddressFieldProps {

  label: string;

  placeholder: string;

  depots: DepotDto[];

  fleetId?: string;

  value: string;

  onChange: (address: string) => void;

  onDepotSelect?: (depot: DepotDto | null) => void;

  pickupLatitude?: number;

  pickupLongitude?: number;

  /** When false, only the free-text field is shown (e.g. customer delivery addresses). */

  showDepotPicker?: boolean;

  multiline?: boolean;

}



export function DepotAddressField({

  label,

  placeholder,

  depots,

  fleetId,

  value,

  onChange,

  onDepotSelect,

  pickupLatitude,

  pickupLongitude,

  showDepotPicker = true,

  multiline = false,

}: DepotAddressFieldProps) {

  const pickupCoords = useMemo(

    () => ({ latitude: pickupLatitude, longitude: pickupLongitude }),

    [pickupLatitude, pickupLongitude],

  );



  const matchingDepot = useMemo(

    () => depots.find((d) =>

      isDepotPickupAddress(value, d.address, pickupCoords, {

        latitude: d.latitude,

        longitude: d.longitude,

      })),

    [depots, value, pickupCoords],

  );



  // Holds the user's depot pick until the parent re-renders with the new address.

  const [pendingDepotId, setPendingDepotId] = useState<string | null>(null);



  useEffect(() => {

    if (pendingDepotId && matchingDepot?.id === pendingDepotId) {

      setPendingDepotId(null);

    }

  }, [matchingDepot?.id, pendingDepotId]);



  const selectedDepotId = matchingDepot?.id ?? pendingDepotId ?? '';



  const pickerVisible = showDepotPicker && depots.length > 0;

  const addressDiffersFromSelectedDepot =

    matchingDepot != null

    && !isDepotPickupAddress(value, matchingDepot.address);



  const fieldStyle = { width: '100%' as const };



  const applyDepot = (depot: DepotDto) => {

    setPendingDepotId(depot.id);

    onChange(depot.address);

    onDepotSelect?.(depot);

  };



  const handleDepotSelect = (depotId: string) => {

    if (!depotId) {

      setPendingDepotId(null);

      onDepotSelect?.(null);

      return;

    }

    const depot = depots.find((d) => d.id === depotId);

    if (depot) applyDepot(depot);

  };



  return (

    <div className="depot-address-field">

      <span className="depot-address-field-label">{label}</span>

      {pickerVisible ? (

        <>

          <select

            aria-label={`${label} depot`}

            value={selectedDepotId}

            onChange={(e) => handleDepotSelect(e.target.value)}

            style={{ width: '100%', marginBottom: '0.35rem' }}

          >

            <option value="">Choose a depot…</option>

            {depots.map((d) => (

              <option key={d.id} value={d.id}>

                {d.name} — {d.address}

              </option>

            ))}

          </select>

          {addressDiffersFromSelectedDepot && matchingDepot && (

            <button

              type="button"

              className="secondary"

              style={{ marginBottom: '0.35rem', fontSize: '0.875rem' }}

              onClick={() => applyDepot(matchingDepot)}

            >

              Use depot address ({matchingDepot.name})

            </button>

          )}

        </>

      ) : showDepotPicker && fleetId ? (

        <span className="muted" style={{ display: 'block', marginBottom: '0.35rem', fontSize: '0.875rem' }}>

          No depots configured.{' '}

          <Link to={`/delivery/fleets/${fleetId}/depots`}>Add a depot</Link>

        </span>

      ) : null}

      {multiline ? (

        <textarea

          aria-label={label}

          rows={2}

          placeholder={placeholder}

          value={value}

          onChange={(e) => onChange(e.target.value)}

          style={fieldStyle}

        />

      ) : (

        <input

          aria-label={label}

          placeholder={placeholder}

          value={value}

          onChange={(e) => onChange(e.target.value)}

          style={fieldStyle}

        />

      )}

    </div>

  );

}

