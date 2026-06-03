import { assetCategoryLabel } from './assetLabels';
import type { AssetCategory } from '../api/types';

export function vehicleSelectLabel(v: {
  vehicleNumber: string;
  licensePlate: string;
  make?: string;
  model?: string;
  category?: AssetCategory | string | null;
}): string {
  const name = [v.make, v.model].filter(Boolean).join(' ').trim();
  const identity = name ? `${v.vehicleNumber} · ${v.licensePlate} — ${name}` : `${v.vehicleNumber} · ${v.licensePlate}`;
  return v.category ? `${assetCategoryLabel(v.category)} · ${identity}` : identity;
}

export function vehiclePrimaryLabel(v: { vehicleNumber: string; licensePlate?: string }): string {
  if (v.licensePlate && v.licensePlate !== v.vehicleNumber)
    return `${v.vehicleNumber} (${v.licensePlate})`;
  return v.vehicleNumber;
}
