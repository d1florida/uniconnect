import type { FleetDto, FleetModule } from './types';

type LegacyFleet = FleetDto & { fleetType?: FleetModule };

type RawUserProfile = {
  userId: string;
  email: string;
  displayName: string;
  tenantId?: string;
  tenantName?: string;
  fleetId?: string;
  fleetName?: string;
  modules?: FleetModule[];
  fleetType?: FleetModule | null;
  tenantRole?: import('./types').TenantRole;
  isTenantAdmin?: boolean;
  isPlatformAdmin: boolean;
};

function modulesFromLegacy(raw: { modules?: FleetModule[]; fleetType?: FleetModule | null }): FleetModule[] {
  if (Array.isArray(raw.modules)) return raw.modules;
  if (raw.fleetType) return [raw.fleetType];
  return [];
}

export function normalizeFleetDto(raw: LegacyFleet): FleetDto {
  return {
    id: raw.id,
    name: raw.name ?? '',
    slug: raw.slug ?? '',
    modules: modulesFromLegacy(raw),
    contactName: raw.contactName ?? '',
    contactEmail: raw.contactEmail ?? '',
    contactPhone: raw.contactPhone ?? '',
    adminEmail: raw.adminEmail ?? undefined,
    createdAt: raw.createdAt,
  };
}

export function normalizeUserProfile(raw: RawUserProfile) {
  return {
    userId: raw.userId,
    email: raw.email,
    displayName: raw.displayName,
    fleetId: raw.tenantId ?? raw.fleetId,
    fleetName: raw.tenantName ?? raw.fleetName,
    modules: modulesFromLegacy(raw),
    tenantRole: raw.tenantRole,
    isTenantAdmin: raw.isTenantAdmin ?? raw.tenantRole === 'Admin',
    isPlatformAdmin: raw.isPlatformAdmin,
  };
}
