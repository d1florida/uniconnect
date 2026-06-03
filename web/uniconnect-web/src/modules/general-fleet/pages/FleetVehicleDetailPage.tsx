import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../../../api/client';
import type { DepotDto, MaintenanceRecordDto, MaintenanceStatus, ServiceType, UpdateVehicleRequest, VehicleDto, VehicleLocationDto, VehicleStatus } from '../../../api/types';
import { FleetMap } from '../../../components/FleetMap';
import { ErrorAlert } from '../../../components/ErrorAlert';
import { Loading } from '../../../components/Loading';
import { useAuth } from '../../../auth/AuthContext';
import { hasModule } from '../../../utils/fleetModules';
import { ASSET_CATEGORIES, assetCategoryLabel } from '../../../utils/assetLabels';
import { vehiclePrimaryLabel } from '../../../utils/vehicleLabels';

function vehicleToForm(vehicle: VehicleDto): UpdateVehicleRequest {
  return {
    vin: vehicle.vin,
    make: vehicle.make,
    model: vehicle.model,
    year: vehicle.year,
    category: vehicle.category,
    vehicleNumber: vehicle.vehicleNumber,
    licensePlate: vehicle.licensePlate,
    currentMileage: vehicle.currentMileage,
    status: vehicle.status,
  };
}

export function FleetVehicleDetailPage() {
  const { vehicleId } = useParams<{ vehicleId: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [vehicle, setVehicle] = useState<VehicleDto | null>(null);
  const [depots, setDepots] = useState<DepotDto[]>([]);
  const [savingDepot, setSavingDepot] = useState(false);
  const [maintenance, setMaintenance] = useState<MaintenanceRecordDto[]>([]);
  const [locations, setLocations] = useState<VehicleLocationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [editForm, setEditForm] = useState<UpdateVehicleRequest | null>(null);
  const [lat, setLat] = useState('37.775');
  const [lng, setLng] = useState('-122.418');
  const [maintForm, setMaintForm] = useState({
    serviceType: 'OilChange' as ServiceType,
    performedOn: new Date().toISOString().slice(0, 10),
    mileageAtService: 0,
    cost: 0,
    notes: '',
    status: 'Completed' as MaintenanceStatus,
  });

  const load = useCallback(async () => {
    if (!vehicleId) return;
    setLoading(true);
    setError('');
    try {
      const [v, m, locs] = await Promise.all([
        api.get<VehicleDto>(`/api/fleet/vehicles/${vehicleId}`),
        api.get<MaintenanceRecordDto[]>(`/api/fleet/vehicles/${vehicleId}/maintenance`),
        api.get<VehicleLocationDto[]>(`/api/fleet/vehicles/${vehicleId}/locations`),
      ]);
      setVehicle(v);
      setEditForm(vehicleToForm(v));
      setMaintenance(m);
      setLocations(locs);
      setMaintForm((f) => ({ ...f, mileageAtService: v.currentMileage }));
      if (hasModule(user?.modules, 'Delivery')) {
        const depotList = await api.get<DepotDto[]>(`/api/delivery/tenants/${v.tenantId}/depots`);
        setDepots(depotList);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load');
    } finally {
      setLoading(false);
    }
  }, [vehicleId, user?.modules]);

  const assignHomeDepot = async (homeDepotId: string) => {
    if (!vehicle) return;
    setSavingDepot(true);
    setError('');
    try {
      await api.patch(`/api/delivery/tenants/${vehicle.tenantId}/vehicles/${vehicle.id}/home-depot`, {
        homeDepotId: homeDepotId || null,
      });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update home depot');
    } finally {
      setSavingDepot(false);
    }
  };

  useEffect(() => { load(); }, [load]);

  const saveVehicle = async () => {
    if (!vehicleId || !editForm) return;
    if (!editForm.vin.trim() || !editForm.make.trim() || !editForm.model.trim() || !editForm.vehicleNumber.trim() || !editForm.licensePlate.trim()) {
      setError('VIN, make, model, vehicle ID, and license plate are required.');
      return;
    }
    setSaving(true);
    setError('');
    try {
      await api.put(`/api/fleet/vehicles/${vehicleId}`, {
        ...editForm,
        vin: editForm.vin.trim(),
        make: editForm.make.trim(),
        model: editForm.model.trim(),
        vehicleNumber: editForm.vehicleNumber.trim(),
        licensePlate: editForm.licensePlate.trim(),
      });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save vehicle');
    } finally {
      setSaving(false);
    }
  };

  const resetEditForm = () => {
    if (vehicle) setEditForm(vehicleToForm(vehicle));
  };

  const recordLocation = async () => {
    await api.post(`/api/fleet/vehicles/${vehicleId}/locations`, {
      latitude: parseFloat(lat),
      longitude: parseFloat(lng),
    });
    await load();
  };

  const addMaintenance = async () => {
    await api.post(`/api/fleet/vehicles/${vehicleId}/maintenance`, {
      ...maintForm,
      performedOn: maintForm.performedOn,
    });
    await load();
  };

  const remove = async () => {
    if (!vehicleId || !vehicle) return;
    if (!window.confirm(`Delete vehicle ${vehiclePrimaryLabel(vehicle)}? This cannot be undone.`)) return;
    try {
      setError('');
      await api.delete(`/api/fleet/vehicles/${vehicleId}`);
      navigate(`/fleet/fleets/${vehicle.tenantId}/vehicles`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete');
    }
  };

  if (loading) return <Loading />;
  const editDirty = vehicle && editForm
    ? JSON.stringify(editForm) !== JSON.stringify(vehicleToForm(vehicle))
    : false;
  const markers = vehicle?.latestLocation
    ? [{ id: vehicle.id, label: vehiclePrimaryLabel(vehicle), lat: vehicle.latestLocation.latitude, lng: vehicle.latestLocation.longitude }]
    : [];

  return (
    <div>
      <div className="page-header">
        <h2>{vehicle ? vehiclePrimaryLabel(vehicle) : 'Vehicle'}</h2>
        <p>
          {vehicle && (
            <>
              {assetCategoryLabel(vehicle.category)}
              {' · '}
              ID {vehicle.vehicleNumber}
              {' · '}
              {vehicle.year} {vehicle.make} {vehicle.model}
            </>
          )}
          {vehicle && (
            <>
              {' · '}
              {hasModule(user?.modules, 'General') ? (
                <Link to={`/fleet/fleets/${vehicle.tenantId}/vehicles`}>Back to fleet</Link>
              ) : hasModule(user?.modules, 'Delivery') ? (
                <Link to={`/delivery/fleets/${vehicle.tenantId}/vehicles`}>Back to vehicles</Link>
              ) : null}
            </>
          )}
        </p>
        {vehicle && (
          <button type="button" className="secondary" onClick={remove}>Delete vehicle</button>
        )}
      </div>
      <ErrorAlert message={error} />
      {vehicle && editForm && (
        <div className="card" style={{ marginBottom: '1rem' }}>
          <h3>Edit vehicle</h3>
          <div className="form-row">
            <label>
              Asset type
              <select
                value={editForm.category}
                onChange={(e) => setEditForm({ ...editForm, category: e.target.value as UpdateVehicleRequest['category'] })}
              >
                {ASSET_CATEGORIES.map((c) => (
                  <option key={c} value={c}>{assetCategoryLabel(c)}</option>
                ))}
              </select>
            </label>
            <label>
              VIN
              <input value={editForm.vin} onChange={(e) => setEditForm({ ...editForm, vin: e.target.value })} />
            </label>
            <label>
              Make
              <input value={editForm.make} onChange={(e) => setEditForm({ ...editForm, make: e.target.value })} />
            </label>
            <label>
              Model
              <input value={editForm.model} onChange={(e) => setEditForm({ ...editForm, model: e.target.value })} />
            </label>
            <label>
              Year
              <input
                type="number"
                value={editForm.year}
                onChange={(e) => setEditForm({ ...editForm, year: +e.target.value })}
              />
            </label>
            <label>
              Vehicle ID
              <input
                value={editForm.vehicleNumber}
                onChange={(e) => setEditForm({ ...editForm, vehicleNumber: e.target.value })}
                placeholder="e.g. 101"
              />
            </label>
            <label>
              License plate
              <input
                value={editForm.licensePlate}
                onChange={(e) => setEditForm({ ...editForm, licensePlate: e.target.value })}
              />
            </label>
            <label>
              Mileage
              <input
                type="number"
                value={editForm.currentMileage}
                onChange={(e) => setEditForm({ ...editForm, currentMileage: +e.target.value })}
              />
            </label>
            <label>
              Status
              <select
                value={editForm.status}
                onChange={(e) => setEditForm({ ...editForm, status: e.target.value as VehicleStatus })}
              >
                {(['Active', 'InShop', 'Retired'] as VehicleStatus[]).map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </label>
          </div>
          <div className="form-row">
            <button
              type="button"
              onClick={() => void saveVehicle()}
              disabled={saving || !editDirty}
            >
              {saving ? 'Saving…' : 'Save changes'}
            </button>
            <button type="button" className="secondary" onClick={resetEditForm} disabled={saving || !editDirty}>
              Reset
            </button>
          </div>
        </div>
      )}
      {hasModule(user?.modules, 'Delivery') && vehicle && (
        <div className="form-row" style={{ marginBottom: '1rem' }}>
          <label>
            Home depot (delivery)
            <select
              value={vehicle.homeDepotId ?? ''}
              disabled={savingDepot || depots.length === 0}
              onChange={(e) => void assignHomeDepot(e.target.value)}
            >
              <option value="">— Unassigned —</option>
              {depots.map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </label>
          {depots.length === 0 && (
            <span className="muted">
              {' '}
              <Link to={`/delivery/fleets/${vehicle.tenantId}/depots`}>Add a depot</Link>
            </span>
          )}
        </div>
      )}
      <FleetMap markers={markers} />
      <div className="form-row" style={{ marginTop: '1rem' }}>
        <input value={lat} onChange={(e) => setLat(e.target.value)} placeholder="Lat" />
        <input value={lng} onChange={(e) => setLng(e.target.value)} placeholder="Lng" />
        <button type="button" onClick={recordLocation}>Record location</button>
      </div>

      <h3>Log maintenance</h3>
      <div className="form-row">
        <select value={maintForm.serviceType} onChange={(e) => setMaintForm({ ...maintForm, serviceType: e.target.value as ServiceType })}>
          {(['OilChange', 'TireRotation', 'BrakeService', 'Inspection', 'Repair', 'Other'] as ServiceType[]).map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <input type="date" value={maintForm.performedOn} onChange={(e) => setMaintForm({ ...maintForm, performedOn: e.target.value })} />
        <input type="number" placeholder="Mileage" value={maintForm.mileageAtService} onChange={(e) => setMaintForm({ ...maintForm, mileageAtService: +e.target.value })} />
        <input type="number" placeholder="Cost" value={maintForm.cost} onChange={(e) => setMaintForm({ ...maintForm, cost: +e.target.value })} />
        <select value={maintForm.status} onChange={(e) => setMaintForm({ ...maintForm, status: e.target.value as MaintenanceStatus })}>
          <option value="Scheduled">Scheduled</option>
          <option value="Completed">Completed</option>
          <option value="Cancelled">Cancelled</option>
        </select>
        <input placeholder="Notes" value={maintForm.notes} onChange={(e) => setMaintForm({ ...maintForm, notes: e.target.value })} />
        <button type="button" onClick={addMaintenance}>Add record</button>
      </div>

      <h3>Maintenance history</h3>
      <table>
        <thead><tr><th>Service</th><th>Date</th><th>Status</th><th>Cost</th><th>Notes</th></tr></thead>
        <tbody>
          {maintenance.map((m) => (
            <tr key={m.id}>
              <td>{m.serviceType}</td>
              <td>{m.performedOn}</td>
              <td>{m.status}</td>
              <td>${m.cost}</td>
              <td>{m.notes}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <h3>Location history</h3>
      <table>
        <thead><tr><th>Time</th><th>Lat</th><th>Lng</th><th>Speed</th></tr></thead>
        <tbody>
          {locations.map((l) => (
            <tr key={l.id}>
              <td>{new Date(l.recordedAt).toLocaleString()}</td>
              <td>{l.latitude}</td>
              <td>{l.longitude}</td>
              <td>{l.speedKph ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
