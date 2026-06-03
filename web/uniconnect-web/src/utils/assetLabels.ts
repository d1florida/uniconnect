export type AssetCategory =
  | 'Tractor'
  | 'StraightTruck'
  | 'LightVehicle'
  | 'Trailer'
  | 'OffRoadEquipment'
  | 'SpecializedEquipment';

export const ASSET_CATEGORIES: AssetCategory[] = [
  'Tractor',
  'StraightTruck',
  'LightVehicle',
  'Trailer',
  'OffRoadEquipment',
  'SpecializedEquipment',
];

const CATEGORY_LABELS: Record<AssetCategory, string> = {
  Tractor: 'Tractor',
  StraightTruck: 'Straight truck',
  LightVehicle: 'Light vehicle',
  Trailer: 'Trailer',
  OffRoadEquipment: 'Off-road equipment',
  SpecializedEquipment: 'Specialized equipment',
};

export function assetCategoryLabel(category: AssetCategory | string | undefined | null): string {
  if (!category) return CATEGORY_LABELS.LightVehicle;
  return CATEGORY_LABELS[category as AssetCategory] ?? category;
}
