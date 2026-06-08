import { useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

import iconUrl from 'leaflet/dist/images/marker-icon.png';
import iconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png';
import shadowUrl from 'leaflet/dist/images/marker-shadow.png';

const DefaultIcon = L.icon({
  iconUrl,
  iconRetinaUrl,
  shadowUrl,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
});
L.Marker.prototype.options.icon = DefaultIcon;

export interface MapMarker {
  id: string;
  label: string;
  lat: number;
  lng: number;
  detail?: string;
  color?: string;
}

interface FleetMapProps {
  markers: MapMarker[];
  center?: [number, number];
  zoom?: number;
  /** When false, keeps the explicit center instead of fitting to markers. Defaults to true unless center is set. */
  fitToMarkers?: boolean;
}

function FitMapToMarkers({ markers, zoom }: { markers: MapMarker[]; zoom: number }) {
  const map = useMap();
  const positionsKey = markers.map((m) => `${m.lat},${m.lng}`).join('|');

  useEffect(() => {
    if (markers.length === 0) return;

    if (markers.length === 1) {
      map.setView([markers[0].lat, markers[0].lng], zoom, { animate: false });
      return;
    }

    const bounds = L.latLngBounds(markers.map((m) => [m.lat, m.lng] as L.LatLngTuple));
    map.fitBounds(bounds, { padding: [48, 48], maxZoom: zoom });
  }, [map, markers, positionsKey, zoom]);

  return null;
}

export function FleetMap({ markers, center, zoom = 12, fitToMarkers }: FleetMapProps) {
  const shouldFitToMarkers = fitToMarkers ?? center === undefined;
  const defaultCenter: [number, number] = center ?? (
    markers.length > 0 ? [markers[0].lat, markers[0].lng] : [39.8283, -98.5795]
  );

  return (
    <div className="map-container">
      <MapContainer center={defaultCenter} zoom={zoom} style={{ height: '100%', width: '100%' }}>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        {shouldFitToMarkers && <FitMapToMarkers markers={markers} zoom={zoom} />}
        {markers.map((m) => (
          <Marker key={m.id} position={[m.lat, m.lng]}>
            <Popup>
              <strong>{m.label}</strong>
              {m.detail && <div>{m.detail}</div>}
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}
