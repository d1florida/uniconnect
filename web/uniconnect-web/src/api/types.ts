export type FleetModule = 'General' | 'RoboTaxi' | 'Delivery';
export type VehicleStatus = 'Active' | 'InShop' | 'Retired';
export type ServiceType = 'OilChange' | 'TireRotation' | 'BrakeService' | 'Inspection' | 'Repair' | 'Other';
export type MaintenanceStatus = 'Scheduled' | 'Completed' | 'Cancelled';
export type OperationalState = 'Idle' | 'OnTrip' | 'Charging' | 'Maintenance' | 'Grounded';
export type GroundedReason = 'None' | 'SafetyReview' | 'SensorFault' | 'SoftwareRollback' | 'Weather';
export type DeliveryChannel = 'B2B' | 'B2C';
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
  licensePlate: string;
  currentMileage: number;
  status: VehicleStatus;
  latestLocation?: LocationDto;
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
  licensePlate: string;
  operationalState: OperationalState;
  latestLocation?: LocationDto;
}

export interface DeliveryTrackingDto {
  vehicleId: string;
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
  channel: DeliveryChannel;
  status: DeliveryOrderStatus;
  pickupAddress: string;
  deliveryAddress: string;
  recipientName: string;
  recipientPhone: string;
  businessAccountId?: string;
  businessAccountName?: string;
  parcelDescription: string;
  assignment?: {
    id: string;
    vehicleId: string;
    licensePlate: string;
    automationMode: AutomationMode;
    assignedAt: string;
  };
  createdAt: string;
}

export interface DeliveryDashboardDto {
  openOrders: number;
  inTransit: number;
  b2BOrders: number;
  b2COrders: number;
  autonomousActive: number;
  conventionalActive: number;
  activeRoutes: number;
  plannedRoutes: number;
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
  completedAt?: string;
}

export interface DeliveryRouteDto {
  id: string;
  tenantId: string;
  name: string;
  status: DeliveryRouteStatus;
  depotAddress: string;
  scheduledDate: string;
  vehicleId?: string;
  licensePlate?: string;
  automationMode?: AutomationMode;
  stopCount: number;
  completedStops: number;
  pendingStops: number;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
}

export interface DeliveryRouteDetailDto extends Omit<DeliveryRouteDto, 'stopCount' | 'completedStops' | 'pendingStops'> {
  stops: DeliveryRouteStopDto[];
}

export interface DeliveryVehicleDto {
  id: string;
  licensePlate: string;
  make: string;
  model: string;
  isAutonomous: boolean;
  operationalState?: OperationalState;
  latestLocation?: LocationDto;
}

export interface BusinessAccountDto {
  id: string;
  tenantId: string;
  companyName: string;
  accountCode: string;
  contactEmail: string;
}
