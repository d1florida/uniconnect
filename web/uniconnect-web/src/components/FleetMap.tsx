import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
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
}

export function FleetMap({ markers, center, zoom = 12 }: FleetMapProps) {
  const defaultCenter: [number, number] = center ?? (
    markers.length > 0 ? [markers[0].lat, markers[0].lng] : [37.7749, -122.4194]
  );

  return (
    <div className="map-container">
      <MapContainer center={defaultCenter} zoom={zoom} style={{ height: '100%', width: '100%' }}>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
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
