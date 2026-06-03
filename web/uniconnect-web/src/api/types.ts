export type FleetModule = 'General' | 'RoboTaxi' | 'Delivery' | 'RoutePlanning' | 'Insights';
export type AssetCategory =
  | 'Tractor'
  | 'StraightTruck'
  | 'LightVehicle'
  | 'Trailer'
  | 'OffRoadEquipment'
  | 'SpecializedEquipment';
export type VehicleStatus = 'Active' | 'InShop' | 'Retired';
export type ServiceType = 'OilChange' | 'TireRotation' | 'BrakeService' | 'Inspection' | 'Repair' | 'Other';
export type MaintenanceStatus = 'Scheduled' | 'Completed' | 'Cancelled';
export type OperationalState = 'Idle' | 'OnTrip' | 'Charging' | 'Maintenance' | 'Grounded';
export type GroundedReason = 'None' | 'SafetyReview' | 'SensorFault' | 'SoftwareRollback' | 'Weather';
export type DeliveryOrderStatus = 'Created' | 'Assigned' | 'PickedUp' | 'InTransit' | 'Delivered' | 'Failed' | 'Cancelled';
export type AutomationMode = 'Conventional' | 'Autonomous';
export type DeliveryRouteStatus = 'Draft' | 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
export type DeliveryStopType = 'Depot' | 'Dropoff' | 'Pickup';
export type DeliveryStopStatus = 'Pending' | 'Completed' | 'Skipped';

export interface LocationDto {
  latitude: number;
  longitude: number;
  recordedAt: string;
  speedKph?: number;
}

export interface FleetDto {
  id: string;
  name: string;
  slug: string;
  modules: FleetModule[];
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  adminEmail?: string;
  createdAt: string;
}

export interface PasswordPolicyDto {
  minLength: number;
  requireDigit: boolean;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireNonAlphanumeric: boolean;
}

export type TenantRole = 'Admin' | 'Operator';

export interface MyTenantProfileDto {
  tenant: FleetDto;
  passwordPolicy: PasswordPolicyDto;
  tenantRole: TenantRole;
  isTenantAdmin: boolean;
  moduleAccess: FleetModule[];
}

export interface TenantUserDto {
  id: string;
  email: string;
  displayName: string;
  role: TenantRole;
  moduleAccess: FleetModule[];
  isActive: boolean;
}

export interface FleetDashboardDto {
  totalTenants: number;
  generalTenants: number;
  roboTaxiTenants: number;
  deliveryTenants: number;
}

export interface VehicleDto {
  id: string;
  tenantId: string;
  vin: string;
  make: string;
  model: string;
  year: number;
  category: AssetCategory;
  vehicleNumber: string;
  licensePlate: string;
  currentMileage: number;
  status: VehicleStatus;
  latestLocation?: LocationDto;
  homeDepotId?: string;
  homeDepotName?: string;
}

export interface UpdateVehicleRequest {
  vin: string;
  make: string;
  model: string;
  year: number;
  category: AssetCategory;
  vehicleNumber: string;
  licensePlate: string;
  currentMileage: number;
  status: VehicleStatus;
}

export interface MaintenanceRecordDto {
  id: string;
  vehicleId: string;
  serviceType: ServiceType;
  performedOn: string;
  mileageAtService: number;
  cost: number;
  notes: string;
  status: MaintenanceStatus;
}

export interface FleetVehicleTrackingDto {
  vehicleId: string;
  category: AssetCategory;
  vehicleNumber: string;
  licensePlate: string;
  status: VehicleStatus;
  latestLocation?: LocationDto;
}

export interface VehicleLocationDto {
  id: string;
  latitude: number;
  longitude: number;
  recordedAt: string;
  speedKph?: number;
  source: string;
}

export interface RoboTaxiTrackingDto {
  vehicleId: string;
  category: AssetCategory;
  vehicleNumber: string;
  licensePlate: string;
  operationalState: OperationalState;
  latestLocation?: LocationDto;
}

export interface DeliveryTrackingDto {
  vehicleId: string;
  category: AssetCategory;
  vehicleNumber: string;
  licensePlate: string;
  isAutonomous: boolean;
  operationalState?: OperationalState;
  latestLocation?: LocationDto;
  activeOrderId?: string;
  activeOrderStatus?: DeliveryOrderStatus;
  deliveryAddress?: string;
}

export interface RoboTaxiProfileDto {
  vehicleId: string;
  autonomyLevel: string;
  operationalState: OperationalState;
  softwareVersion: string;
  batteryPercent?: number;
  passengerCapacity: number;
  lastDisengagementAt?: string;
  groundedReason: GroundedReason;
}

export interface RoboTaxiVehicleDto extends VehicleDto {
  profile: RoboTaxiProfileDto;
}

export interface RoboTaxiDashboardDto {
  totalVehicles: number;
  idle: number;
  onTrip: number;
  charging: number;
  maintenance: number;
  grounded: number;
  staleLocation: number;
}

export interface DeliveryOrderDto {
  id: string;
  tenantId: string;
  status: DeliveryOrderStatus;
  pickupAddress: string;
  deliveryAddress: string;
  recipientName: string;
  recipientPhone: string;
  parcelDescription: string;
  pickupLatitude?: number;
  pickupLongitude?: number;
  deliveryLatitude?: number;
  deliveryLongitude?: number;
  pickupGeocodeSource?: string;
  deliveryGeocodeSource?: string;
  assignment?: {
    id: string;
    vehicleId: string;
    vehicleNumber: string;
    licensePlate: string;
    automationMode: AutomationMode;
    driverId?: string;
    driverName?: string;
    assignedAt: string;
  };
  createdAt: string;
}

export interface DepotDto {
  id: string;
  tenantId: string;
  name: string;
  address: string;
  latitude?: number;
  longitude?: number;
  isDefault: boolean;
  hours?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
}

export interface DeliveryDashboardDto {
  openOrders: number;
  inTransit: number;
  totalOrders: number;
  autonomousActive: number;
  conventionalActive: number;
  activeRoutes: number;
  plannedRoutes: number;
  ordersReadyToPlan: number;
  draftRoutes: number;
  routesNeedingDrivers: number;
}

export interface DeliveryRouteStopDto {
  id: string;
  sequence: number;
  stopType: DeliveryStopType;
  status: DeliveryStopStatus;
  address: string;
  recipientName?: string;
  recipientPhone?: string;
  parcelDescription?: string;
  notes?: string;
  deliveryOrderId?: string;
  completedAt?: string;
  latitude?: number;
  longitude?: number;
}

export interface DeliveryRouteDto {
  id: string;
  tenantId: string;
  name: string;
  status: DeliveryRouteStatus;
  depotAddress: string;
  scheduledDate: string;
  vehicleId?: string;
  vehicleNumber?: string;
  licensePlate?: string;
  driverId?: string;
  driverName?: string;
  automationMode?: AutomationMode;
  stopCount: number;
  completedStops: number;
  pendingStops: number;
  routePlanRunId?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
}

export interface DeliveryRouteDetailDto extends Omit<DeliveryRouteDto, 'stopCount' | 'completedStops' | 'pendingStops'> {
  stops: DeliveryRouteStopDto[];
  estimatedDriveMinutes?: number;
  estimatedMinutesToNextStop?: number;
  estimatedNextStopArrivalAt?: string;
}

export interface DeliveryVehicleDto {
  id: string;
  category: AssetCategory;
  vehicleNumber: string;
  licensePlate: string;
  make: string;
  model: string;
  isAutonomous: boolean;
  status?: VehicleStatus;
  operationalState?: OperationalState;
  latestLocation?: LocationDto;
  homeDepotId?: string;
  homeDepotName?: string;
}

export interface TenantApiKeyDto {
  id: string;
  tenantId: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  lastUsedAt?: string;
  revokedAt?: string;
  isActive: boolean;
}

export interface CreateTenantApiKeyResponse {
  key: TenantApiKeyDto;
  secret: string;
}

export interface InsightsKpiDto {
  eventCount: number;
  ordersDelivered: number;
  ordersFailed: number;
  stopsCompleted: number;
  routesCompleted: number;
  onTimeRate?: number;
  avgDelayMinutes?: number;
}

export interface OperationalEventDto {
  id: string;
  occurredAt: string;
  domain: string;
  eventType: string;
  orderId?: string;
  routeId?: string;
  stopId?: string;
  vehicleId?: string;
  driverId?: string;
  customerId?: string;
  userId?: string;
  planRunId?: string;
  driverLabel?: string;
  vehicleLabel?: string;
  customerLabel?: string;
  plannerLabel?: string;
  narrative?: string;
  metricsJson: string;
  contextJson: string;
}

export interface SubjectSummaryDto {
  subjectType: string;
  subjectId: string;
  label: string;
  from: string;
  to: string;
  kpis: InsightsKpiDto;
  notableEvents: OperationalEventDto[];
}

export interface PlannerSummaryDto {
  userId: string;
  displayName: string;
  email?: string;
  from: string;
  to: string;
  plansRequested: number;
  plansAccepted: number;
  plansDiscarded: number;
  ordersPlanned: number;
  acceptRate: number;
  notableEvents: OperationalEventDto[];
}

export interface PlannerDigestDto {
  activePlanners: number;
  plansRequested: number;
  plansAccepted: number;
  acceptRate: number;
}

export interface TenantDigestDto {
  tenantId: string;
  from: string;
  to: string;
  delivery: InsightsKpiDto;
  planning?: PlannerDigestDto;
}

export interface ReportSubjectDto {
  type: string;
  id: string;
  label: string;
  role?: string;
}

export interface AnalyticsReportBundleDto {
  schemaVersion: string;
  reportType: string;
  tenantId: string;
  from: string;
  to: string;
  subject: ReportSubjectDto;
  executiveSummary: string;
  kpis: Record<string, unknown>;
  notableEvents: OperationalEventDto[];
  comparison?: Record<string, unknown>;
}

export interface CustomerDto {
  id: string;
  name: string;
  phone?: string;
  externalRef?: string;
}

export interface DriverDto {
  id: string;
  displayName: string;
  userId?: string;
  linkedUserName?: string;
  isActive: boolean;
  createdAt: string;
}

export type RoutePlanRunStatus = 'Requested' | 'Completed' | 'Accepted' | 'Discarded' | 'Failed';

export interface PlanReadinessDto {
  readyOrderCount: number;
  plannableOrderCount: number;
  vehicleCount: number;
  activeVehicleCount: number;
  canPlan: boolean;
  notes: string[];
}

export interface PlannedStopDto {
  orderId?: string;
  stopType: string;
  sequence: number;
  address: string;
  recipientName?: string;
  parcelDescription?: string;
  latitude?: number;
  longitude?: number;
}

export interface PlannedRouteProposalDto {
  vehicleId?: string;
  vehicleLabel?: string;
  estimatedMinutes: number;
  stops: PlannedStopDto[];
}

export interface AcceptPlanResultDto {
  planRun: RoutePlanRunDto;
  createdRoutes: { id: string; name: string }[];
}

export interface OptimizeSequenceResultDto {
  routeId: string;
  optimizedStopIds: string[];
  estimatedMinutesBefore: number;
  estimatedMinutesAfter: number;
}

export interface RoutePlanRunDto {
  id: string;
  tenantId: string;
  requestedByUserId: string;
  requestedByName: string;
  status: RoutePlanRunStatus;
  scheduledDate: string;
  depotAddress: string;
  depotLatitude?: number;
  depotLongitude?: number;
  ordersRequested: number;
  ordersPlanned: number;
  ordersUnassigned: number;
  proposalCount: number;
  computeDurationMs?: number;
  proposals: PlannedRouteProposalDto[];
  createdAt: string;
  completedAt?: string;
  resolvedAt?: string;
}
