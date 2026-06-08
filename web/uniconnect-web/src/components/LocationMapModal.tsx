import { useEffect } from 'react';
import { FleetMap, type MapMarker } from './FleetMap';

export interface LocationMapTarget {
  label: string;
  lat: number;
  lng: number;
  detail?: string;
}

interface LocationMapModalProps {
  target: LocationMapTarget | null;
  onClose: () => void;
}

export function LocationMapModal({ target, onClose }: LocationMapModalProps) {
  useEffect(() => {
    if (!target) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [target, onClose]);

  if (!target) return null;

  const markers: MapMarker[] = [
    {
      id: 'location',
      label: target.label,
      lat: target.lat,
      lng: target.lng,
      detail: target.detail,
    },
  ];

  return (
    <div
      className="map-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="map-modal-title"
      onClick={onClose}
    >
      <div className="map-modal" onClick={(e) => e.stopPropagation()}>
        <div className="map-modal-header">
          <h3 id="map-modal-title">{target.label}</h3>
          <button type="button" className="map-modal-close" onClick={onClose}>
            Close
          </button>
        </div>
        {target.detail && <p className="map-modal-detail muted">{target.detail}</p>}
        <p className="map-modal-coords muted">
          {target.lat.toFixed(5)}, {target.lng.toFixed(5)}
        </p>
        <FleetMap markers={markers} center={[target.lat, target.lng]} zoom={15} fitToMarkers={false} />
      </div>
    </div>
  );
}
