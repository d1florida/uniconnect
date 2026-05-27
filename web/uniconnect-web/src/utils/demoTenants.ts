export const DEMO_TENANT_IDS = new Set([
  '11111111-1111-1111-1111-111111111101',
  '22222222-2222-2222-2222-222222222201',
  '33333333-3333-3333-3333-333333333301',
]);

export function isDemoTenant(id: string) {
  return DEMO_TENANT_IDS.has(id);
}
