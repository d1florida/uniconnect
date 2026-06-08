import type { FleetDto, FleetModule } from '../api/types';

export const ALL_FLEET_MODULES: { value: FleetModule; label: string; requiresDelivery?: boolean }[] = [
  { value: 'General', label: 'General Fleet' },
  { value: 'RoboTaxi', label: 'Robo-Taxi' },
  { value: 'Delivery', label: 'Delivery' },
  { value: 'RoutePlanning', label: 'Route planning', requiresDelivery: true },
  { value: 'Insights', label: 'Insights', requiresDelivery: true },
];

export function hasModule(modules: FleetModule[] | undefined, module: FleetModule): boolean {
  return modules?.includes(module) ?? false;
}

export function formatModules(modules: FleetModule[] | undefined): string {
  if (!modules?.length) return '—';
  return modules
    .map((m) => ALL_FLEET_MODULES.find((x) => x.value === m)?.label ?? m)
    .join(', ');
}

export function formatContact(fleet: Pick<FleetDto, 'contactName' | 'contactEmail' | 'contactPhone'>): string {
  const parts = [fleet.contactName, fleet.contactEmail, fleet.contactPhone].filter(Boolean);
  return parts.length ? parts.join(' · ') : '—';
}
