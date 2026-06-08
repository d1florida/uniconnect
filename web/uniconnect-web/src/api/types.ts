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

export type TenantRole = 'Admin' | 'Operator' | 'Driver';

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
  linkedDriverId?: string;
  linkedDriverName?: string;
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

export interface AddressCheckResultDto {
  inputAddress: string;
  suggestedAddress: string;
  standardizedAddress?: string;
  latitude?: number;
  longitude?: number;
  geocodeSource?: string;
  resolved: boolean;
  message: string;
}

export interface CheckAddressesResultDto {
  pickup: AddressCheckResultDto;
  delivery: AddressCheckResultDto;
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
  pickupFormattedAddress?: string;
  deliveryFormattedAddress?: string;
  externalRef?: string;
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
  fixedRouteTemplateId?: string;
  fixedRouteTemplateName?: string;
  heldUntil?: string;
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
  ordersHeldForFixedRoutes: number;
  draftRoutes: number;
  routesNeedingDrivers: number;
}

export interface DeliveryZoneDto {
  id: string;
  tenantId: string;
  name: string;
  matchType: string;
  isActive: boolean;
  customerCount: number;
  createdAt: string;
}

export interface FixedRouteTemplateDto {
  id: string;
  tenantId: string;
  name: string;
  deliveryZoneId: string;
  deliveryZoneName: string;
  daysOfWeek: string[];
  daysOfWeekLabel: string;
  depotId: string;
  depotName?: string;
  defaultVehicleId?: string;
  defaultDriverId?: string;
  isActive: boolean;
  heldOrderCount: number;
  dueOrderCount: number;
  nextRouteDate?: string;
  createdAt: string;
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

export type GeocodingProvider = 'UsCensus' | 'OpenStreetMap' | 'GoogleMaps';

export interface TenantGeocodingSettingsDto {
  provider: GeocodingProvider;
  allowPostalFallback: boolean;
  nominatimUserAgent: string;
  nominatimBaseUrl?: string | null;
  googleApiKeyConfigured: boolean;
  googleApiKeyHint?: string | null;
  googleApiKeyDecryptFailed: boolean;
  isConfigured: boolean;
  updatedAt?: string | null;
}

export interface UpdateTenantGeocodingSettingsRequest {
  provider: GeocodingProvider;
  allowPostalFallback: boolean;
  nominatimUserAgent?: string | null;
  nominatimBaseUrl?: string | null;
  /** Omit to keep existing key; empty string clears; non-empty sets a new key. */
  googleApiKey?: string | null;
}

export interface TestTenantGeocodingRequest {
  address: string;
}

export interface TestTenantGeocodingResultDto {
  address: string;
  success: boolean;
  latitude?: number | null;
  longitude?: number | null;
  standardizedAddress?: string | null;
  source?: string | null;
  message: string;
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
  deliveryAddress?: string;
  deliveryLatitude?: number;
  deliveryLongitude?: number;
  deliveryHours?: string;
  deliveryWindowStart?: string;
  deliveryWindowEnd?: string;
  noDeliveryStart?: string;
  noDeliveryEnd?: string;
  deliveryZoneId?: string;
  deliveryZoneName?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
}

export interface DriverDto {
  id: string;
  displayName: string;
  userId?: string;
  linkedUserName?: string;
  isActive: boolean;
  shiftStartTime: string;
  shiftEndTime: string;
  lunchMinutes: number;
  breakMinutes: number;
  availableWorkMinutes: number;
  maxRouteMinutes?: number;
  returnByTime?: string;
  createdAt: string;
}

export interface DriverWorkPatternDayDto {
  dayOfWeek: number;
  isWorkingDay: boolean;
  shiftStartTime: string;
  shiftEndTime: string;
  lunchMinutes: number;
  breakMinutes: number;
  availableWorkMinutes: number;
  maxRouteMinutes?: number;
  returnByTime?: string;
}

export interface DriverCalendarFixedRouteDto {
  templateId: string;
  name: string;
}

export interface DriverCalendarAssignedRouteDto {
  routeId: string;
  name: string;
  status: string;
  stopCount: number;
}

export interface DriverCalendarDayDto {
  date: string;
  isWorking: boolean;
  shiftStartTime?: string;
  shiftEndTime?: string;
  availableWorkMinutes: number;
  maxRouteMinutes?: number;
  returnByTime?: string;
  source: 'pattern' | 'exception' | 'profile' | string;
  hasException: boolean;
  scheduleExceptionId?: string;
  note?: string;
  offBlockStartTime?: string;
  offBlockEndTime?: string;
  fixedRoutes: DriverCalendarFixedRouteDto[];
  assignedRoutes: DriverCalendarAssignedRouteDto[];
}

export interface DriverCalendarRowDto {
  driverId: string;
  displayName: string;
  isActive: boolean;
  days: DriverCalendarDayDto[];
}

export interface DriverCalendarDto {
  from: string;
  to: string;
  drivers: DriverCalendarRowDto[];
}

export interface UpsertDriverScheduleExceptionRequest {
  date: string;
  isWorking: boolean;
  shiftStartTime?: string;
  shiftEndTime?: string;
  lunchMinutes?: number;
  breakMinutes?: number;
  maxRouteMinutes?: number;
  returnByTime?: string;
  offBlockStartTime?: string;
  offBlockEndTime?: string;
  note?: string;
}

export interface BulkUpsertDriverScheduleExceptionRequest {
  from: string;
  to: string;
  isWorking: boolean;
  note?: string;
}

export interface CreateDriverScheduleRequestRequest {
  driverId: string;
  fromDate: string;
  toDate: string;
  requestType: string;
  note?: string;
}

export interface DriverScheduleRequestDto {
  id: string;
  driverId: string;
  driverName: string;
  fromDate: string;
  toDate: string;
  requestType: string;
  status: string;
  note?: string;
  requestedByName: string;
  createdAt: string;
  reviewedByName?: string;
  reviewedAt?: string;
  reviewNote?: string;
}

export interface TenantDeliverySettingsDto {
  allowMultipleRoutesPerDriverPerDay: boolean;
  timeZoneId: string;
  updatedAt?: string;
}

export interface TenantPlanningRulesDto {
  markdown: string;
  compiledPolicyJson?: string;
  compiledAt?: string;
  compileWarnings: string[];
  updatedAt: string;
}

export interface CompileTenantPlanningRulesResultDto {
  compiledPolicyJson?: string;
  warnings: string[];
  usedAi: boolean;
}

export interface PlanRunExplanationDto {
  planRunId: string;
  explanation: string;
  usedAi: boolean;
}

export type RoutePlanRunStatus = 'Requested' | 'Completed' | 'Accepted' | 'Discarded' | 'Failed';

export interface PlanReadinessDto {
  readyOrderCount: number;
  plannableOrderCount: number;
  vehicleCount: number;
  activeVehicleCount: number;
  driverCount: number;
  activeDriverCount: number;
  canPlan: boolean;
  notes: string[];
  windowedOrderCount?: number;
  heldOrderCount?: number;
  fixedRouteTemplateId?: string;
  fixedRouteDueOrderCount?: number;
  workingDriverCount?: number;
  driversOffCount?: number;
  scheduledDate?: string;
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
  deliveryOpenStart?: string;
  deliveryOpenEnd?: string;
  noDeliveryStart?: string;
  noDeliveryEnd?: string;
  estimatedArrival?: string;
  windowWarnings?: string[];
}

export interface PlannedRouteProposalDto {
  vehicleId?: string;
  vehicleLabel?: string;
  estimatedMinutes: number;
  stops: PlannedStopDto[];
  driverId?: string;
  driverLabel?: string;
  shiftAvailableMinutes?: number;
  shiftWindow?: string;
  estimatedRouteStart?: string;
  warnings?: string[];
  windowViolationCount?: number;
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
