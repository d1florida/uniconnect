import { BrowserRouter, Navigate, Route, Routes, useParams } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { PlatformAdminRoute } from './auth/PlatformAdminRoute';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { FleetDashboardPage } from './modules/general-fleet/pages/FleetDashboardPage';
import { FleetVehiclesPage } from './modules/general-fleet/pages/FleetVehiclesPage';
import { FleetTrackingPage } from './modules/general-fleet/pages/FleetTrackingPage';
import { FleetVehicleDetailPage } from './modules/general-fleet/pages/FleetVehicleDetailPage';
import { FleetsDashboardPage } from './modules/fleets/pages/FleetsDashboardPage';
import { FleetNewPage } from './modules/fleets/pages/FleetNewPage';
import { FleetDetailPage } from './modules/fleets/pages/FleetDetailPage';
import { RoboTaxiDashboardPage } from './modules/robo-taxi/pages/RoboTaxiDashboardPage';
import { RoboTaxiFleetPage } from './modules/robo-taxi/pages/RoboTaxiFleetPage';
import { RoboTaxiVehicleDetailPage } from './modules/robo-taxi/pages/RoboTaxiVehicleDetailPage';
import { DeliveryDashboardPage } from './modules/delivery/pages/DeliveryDashboardPage';
import { DeliveryOrdersPage } from './modules/delivery/pages/DeliveryOrdersPage';
import { DeliveryOrderDetailPage } from './modules/delivery/pages/DeliveryOrderDetailPage';
import { DeliveryFleetMapPage } from './modules/delivery/pages/DeliveryFleetMapPage';
import { DeliveryRoutesPage } from './modules/delivery/pages/DeliveryRoutesPage';
import { DeliveryRouteDetailPage } from './modules/delivery/pages/DeliveryRouteDetailPage';
import { DeliveryPlanPage } from './modules/delivery/pages/DeliveryPlanPage';
import { DeliveryDriversPage } from './modules/delivery/pages/DeliveryDriversPage';
import { DeliveryDepotsPage } from './modules/delivery/pages/DeliveryDepotsPage';
import { DeliveryVehiclesPage } from './modules/delivery/pages/DeliveryVehiclesPage';
import { InsightsDashboardPage } from './modules/insights/pages/InsightsDashboardPage';
import { InsightsSubjectPage } from './modules/insights/pages/InsightsSubjectPage';
import { TenantOperatorRoute } from './auth/TenantOperatorRoute';
import { TenantSettingsPage } from './modules/settings/pages/TenantSettingsPage';
import './App.css';

function RedirectLegacyTenantRoute() {
  const { fleetId } = useParams();
  return <Navigate to={fleetId ? `/tenants/${fleetId}` : '/tenants'} replace />;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route
            path="tenants"
            element={
              <PlatformAdminRoute>
                <FleetsDashboardPage />
              </PlatformAdminRoute>
            }
          />
          <Route
            path="tenants/new"
            element={
              <PlatformAdminRoute>
                <FleetNewPage />
              </PlatformAdminRoute>
            }
          />
          <Route
            path="tenants/:fleetId"
            element={
              <PlatformAdminRoute>
                <FleetDetailPage />
              </PlatformAdminRoute>
            }
          />
          <Route path="fleets/new" element={<Navigate to="/tenants/new" replace />} />
          <Route path="fleets/:fleetId" element={<RedirectLegacyTenantRoute />} />
          <Route path="fleets" element={<Navigate to="/tenants" replace />} />

          <Route
            path="settings"
            element={
              <TenantOperatorRoute>
                <TenantSettingsPage />
              </TenantOperatorRoute>
            }
          />

          <Route index element={<FleetDashboardPage />} />
          <Route path="fleet/fleets/:fleetId/vehicles" element={<FleetVehiclesPage />} />
          <Route path="fleet/fleets/:fleetId/tracking" element={<FleetTrackingPage />} />
          <Route path="fleet/vehicles/:vehicleId" element={<FleetVehicleDetailPage />} />

          <Route path="robo-taxis" element={<RoboTaxiDashboardPage />} />
          <Route path="robo-taxis/fleets/:fleetId" element={<RoboTaxiFleetPage />} />
          <Route path="robo-taxis/vehicles/:vehicleId" element={<RoboTaxiVehicleDetailPage />} />

          <Route path="delivery" element={<DeliveryDashboardPage />} />
          <Route path="delivery/fleets/:fleetId/orders" element={<DeliveryOrdersPage />} />
          <Route path="delivery/fleets/:fleetId/map" element={<DeliveryFleetMapPage />} />
          <Route path="delivery/fleets/:fleetId/routes" element={<DeliveryRoutesPage />} />
          <Route path="delivery/fleets/:fleetId/drivers" element={<DeliveryDriversPage />} />
          <Route path="delivery/fleets/:fleetId/depots" element={<DeliveryDepotsPage />} />
          <Route path="delivery/fleets/:fleetId/vehicles" element={<DeliveryVehiclesPage />} />
          <Route path="delivery/routes/:routeId" element={<DeliveryRouteDetailPage />} />
          <Route path="delivery/orders/:orderId" element={<DeliveryOrderDetailPage />} />
          <Route path="delivery/fleets/:fleetId/plan" element={<DeliveryPlanPage />} />

          <Route path="insights" element={<InsightsDashboardPage />} />
          <Route path="insights/drivers/:driverId" element={<InsightsSubjectPage kind="drivers" />} />
          <Route path="insights/customers/:customerId" element={<InsightsSubjectPage kind="customers" />} />
          <Route path="insights/planners/:plannerId" element={<InsightsSubjectPage kind="planners" />} />
          <Route path="insights/vehicles/:vehicleId" element={<InsightsSubjectPage kind="vehicles" />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
