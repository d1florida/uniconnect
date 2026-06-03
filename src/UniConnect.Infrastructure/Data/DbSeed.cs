using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniConnect.Delivery.Entities;
using UniConnect.Infrastructure.Identity;
using UniConnect.Delivery.Enums;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;
using UniConnect.GeneralFleet.Entities;
using UniConnect.GeneralFleet.Enums;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoboTaxi.Enums;
using UniConnect.Tenant.Entities;
using UniConnect.Insights.Entities;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.Infrastructure.Services;
using UniConnect.Insights.Enums;

namespace UniConnect.Infrastructure.Data;

public static class DbSeed
{
    public static readonly Guid GeneralTenantId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid AvTenantId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid DeliveryTenantId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid DemoDepotId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0002");
    private const string DemoDepotAddress = "2500 Distribution Way, San Francisco, CA";
    // When changing DemoDepotAddress, add the previous value here so existing dev DBs pick up the new seed address.
    private static readonly string[] LegacyDemoDepotAddresses =
    [
        "2500 Distribution Way, San Francisco, CA",
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.Tenants.AnyAsync())
            await SeedTenantsAsync(db);

        if (!await db.DeliveryRoutes.AnyAsync())
            await SeedDeliveryRoutesAsync(db);

        await SeedUsersAsync(scope.ServiceProvider);
        await SeedDemoApiKeyAsync(db);
        await SeedInsightsDataAsync(db);
        await EnsureDemoDepotAsync(db);
        await EnsureVehicleCategoriesAsync(db);
        await BackfillVehicleHomeDepotsAsync(db);

        var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();

        foreach (var order in await db.DeliveryOrders.ToListAsync())
        {
            var pickup = await geocoding.GeocodeAsync(order.PickupAddress);
            if (pickup.HasValue)
            {
                order.PickupLatitude = pickup.Value.Latitude;
                order.PickupLongitude = pickup.Value.Longitude;
            }

            var delivery = await geocoding.GeocodeAsync(order.DeliveryAddress);
            if (delivery.HasValue)
            {
                order.DeliveryLatitude = delivery.Value.Latitude;
                order.DeliveryLongitude = delivery.Value.Longitude;
            }
        }

        foreach (var depot in await db.Depots.ToListAsync())
        {
            var point = await geocoding.GeocodeAsync(depot.Address);
            if (point.HasValue)
            {
                depot.Latitude = point.Value.Latitude;
                depot.Longitude = point.Value.Longitude;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoApiKeyAsync(AppDbContext db)
    {
        const string demoSecret = "uc_live_DemoDeliveryPartner0123456";
        if (await db.TenantApiKeys.AnyAsync(k => k.TenantId == DeliveryTenantId))
            return;

        db.TenantApiKeys.Add(new TenantApiKey
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),
            TenantId = DeliveryTenantId,
            Name = "Demo partner integration",
            KeyPrefix = demoSecret[..16],
            KeyHash = TenantApiKeyService.HashSecret(demoSecret),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedTenantsAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        var generalTenant = new TenantEntity
        {
            Id = GeneralTenantId,
            Name = "Demo General Fleet",
            Slug = "demo-general",
            Modules = ProductModule.General,
            ContactName = "Alex Morgan",
            ContactEmail = "fleet@demo.local",
            ContactPhone = "+1 555-0101",
            CreatedAt = now
        };

        var avTenant = new TenantEntity
        {
            Id = AvTenantId,
            Name = "Demo AV Fleet",
            Slug = "demo-av",
            Modules = ProductModule.RoboTaxi,
            ContactName = "Jordan Lee",
            ContactEmail = "av@demo.local",
            ContactPhone = "+1 555-0102",
            CreatedAt = now
        };

        var deliveryTenant = new TenantEntity
        {
            Id = DeliveryTenantId,
            Name = "Demo Delivery Fleet",
            Slug = "demo-delivery",
            Modules = ProductModule.Delivery | ProductModule.RoutePlanning | ProductModule.Insights,
            ContactName = "Sam Rivera",
            ContactEmail = "delivery@demo.local",
            ContactPhone = "+1 555-0103",
            CreatedAt = now
        };

        var truck1 = new Vehicle
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101"),
            TenantId = GeneralTenantId,
            Vin = "1HGBH41JXMN109186",
            Make = "Ford",
            Model = "Transit",
            Year = 2022,
            Category = AssetCategory.LightVehicle,
            VehicleNumber = "101",
            LicensePlate = "GEN-001",
            CurrentMileage = 45000,
            Status = VehicleStatus.Active
        };

        var truck2 = new Vehicle
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102"),
            TenantId = GeneralTenantId,
            Vin = "2HGBH41JXMN109187",
            Make = "Mercedes",
            Model = "Sprinter",
            Year = 2023,
            Category = AssetCategory.LightVehicle,
            VehicleNumber = "102",
            LicensePlate = "GEN-002",
            CurrentMileage = 22000,
            Status = VehicleStatus.Active
        };

        var av1 = new Vehicle
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101"),
            TenantId = AvTenantId,
            Vin = "AVROBO00000000001",
            Make = "Waymo",
            Model = "Jaguar I-PACE",
            Year = 2024,
            Category = AssetCategory.SpecializedEquipment,
            VehicleNumber = "201",
            LicensePlate = "AV-001",
            CurrentMileage = 12000,
            Status = VehicleStatus.Active
        };

        var av2 = new Vehicle
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb102"),
            TenantId = AvTenantId,
            Vin = "AVROBO00000000002",
            Make = "Cruise",
            Model = "Origin",
            Year = 2024,
            Category = AssetCategory.SpecializedEquipment,
            VehicleNumber = "202",
            LicensePlate = "AV-002",
            CurrentMileage = 8500,
            Status = VehicleStatus.Active
        };

        var deliveryVan = new Vehicle
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc01"),
            TenantId = DeliveryTenantId,
            Vin = "DELCONV0000000001",
            Make = "Ford",
            Model = "E-Transit",
            Year = 2023,
            Category = AssetCategory.LightVehicle,
            VehicleNumber = "1",
            LicensePlate = "DEL-001",
            CurrentMileage = 31000,
            Status = VehicleStatus.Active
        };

        var deliveryAv = new Vehicle
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc02"),
            TenantId = DeliveryTenantId,
            Vin = "DELAV000000000001",
            Make = "Nuro",
            Model = "R3",
            Year = 2024,
            Category = AssetCategory.SpecializedEquipment,
            VehicleNumber = "2",
            LicensePlate = "DEL-AV1",
            CurrentMileage = 5000,
            Status = VehicleStatus.Active
        };

        db.Tenants.AddRange(generalTenant, avTenant, deliveryTenant);
        db.Vehicles.AddRange(truck1, truck2, av1, av2, deliveryVan, deliveryAv);

        db.RoboTaxiProfiles.AddRange(
            new RoboTaxiProfile
            {
                VehicleId = av1.Id,
                AutonomyLevel = AutonomyLevel.L4,
                OperationalState = OperationalState.OnTrip,
                SoftwareVersion = "2.4.1",
                BatteryPercent = 72,
                PassengerCapacity = 4,
                GroundedReason = GroundedReason.None
            },
            new RoboTaxiProfile
            {
                VehicleId = av2.Id,
                AutonomyLevel = AutonomyLevel.L4,
                OperationalState = OperationalState.Grounded,
                SoftwareVersion = "2.3.9",
                BatteryPercent = 45,
                PassengerCapacity = 4,
                GroundedReason = GroundedReason.SafetyReview,
                LastDisengagementAt = now.AddHours(-3)
            },
            new RoboTaxiProfile
            {
                VehicleId = deliveryAv.Id,
                AutonomyLevel = AutonomyLevel.L4,
                OperationalState = OperationalState.Idle,
                SoftwareVersion = "1.8.0",
                BatteryPercent = 90,
                PassengerCapacity = 0,
                GroundedReason = GroundedReason.None
            });

        db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = truck1.Id,
            ServiceType = ServiceType.OilChange,
            PerformedOn = DateOnly.FromDateTime(now.AddDays(-30)),
            MileageAtService = 44000,
            Cost = 89.99m,
            Notes = "Routine oil change",
            Status = MaintenanceStatus.Completed
        });

        db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = av2.Id,
            ServiceType = ServiceType.Inspection,
            PerformedOn = DateOnly.FromDateTime(now),
            MileageAtService = 8500,
            Cost = 0m,
            Notes = "Scheduled safety inspection",
            Status = MaintenanceStatus.Scheduled
        });

        AddLocations(db, truck1.Id, 37.7749m, -122.4194m, now);
        AddLocations(db, truck2.Id, 37.7849m, -122.4094m, now);
        AddLocations(db, av1.Id, 37.7799m, -122.4144m, now);
        AddLocations(db, av2.Id, 37.7699m, -122.4294m, now);
        AddLocations(db, deliveryVan.Id, 37.7649m, -122.4344m, now);
        AddLocations(db, deliveryAv.Id, 37.7599m, -122.4394m, now);

        var order1 = new DeliveryOrder
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01"),
            TenantId = DeliveryTenantId,
            Status = DeliveryOrderStatus.InTransit,
            PickupAddress = "100 Market St, San Francisco",
            DeliveryAddress = "500 Howard St, San Francisco",
            RecipientName = "Jane Consumer",
            RecipientPhone = "+1-555-0101",
            ParcelDescription = "Small package",
            CreatedAt = now.AddHours(-2)
        };

        var order2 = new DeliveryOrder
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02"),
            TenantId = DeliveryTenantId,
            Status = DeliveryOrderStatus.Created,
            PickupAddress = "200 Mission St, San Francisco",
            DeliveryAddress = "800 Folsom St, San Francisco",
            RecipientName = "John Smith",
            RecipientPhone = "+1-555-0102",
            ParcelDescription = "Documents",
            CreatedAt = now.AddHours(-1)
        };

        var order3 = new DeliveryOrder
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03"),
            TenantId = DeliveryTenantId,
            Status = DeliveryOrderStatus.Assigned,
            PickupAddress = "Acme Warehouse, Oakland",
            DeliveryAddress = "Retail Hub, San Jose",
            RecipientName = "Receiving Desk",
            RecipientPhone = "+1-555-0200",
            ParcelDescription = "Pallet - office supplies",
            CreatedAt = now.AddHours(-4)
        };

        db.DeliveryOrders.AddRange(order1, order2, order3);

        db.DeliveryAssignments.AddRange(
            new DeliveryAssignment
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = order1.Id,
                VehicleId = deliveryVan.Id,
                AutomationMode = AutomationMode.Conventional,
                AssignedAt = now.AddHours(-1)
            },
            new DeliveryAssignment
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = order3.Id,
                VehicleId = deliveryAv.Id,
                AutomationMode = AutomationMode.Autonomous,
                AssignedAt = now.AddHours(-3)
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDeliveryRoutesAsync(AppDbContext db)
    {
        var van = await db.Vehicles.FirstOrDefaultAsync(v => v.TenantId == DeliveryTenantId && v.LicensePlate == "DEL-001");
        if (van is null) return;

        var now = DateTime.UtcNow;
        var routeId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1");
        var route = new DeliveryRoute
        {
            Id = routeId,
            TenantId = DeliveryTenantId,
            Name = "Morning SF drops — May 20",
            Status = DeliveryRouteStatus.InProgress,
            DepotAddress = "2500 Distribution Way, San Francisco, CA",
            ScheduledDate = DateOnly.FromDateTime(now),
            VehicleId = van.Id,
            AutomationMode = AutomationMode.Conventional,
            CreatedAt = now.AddHours(-4),
            StartedAt = now.AddHours(-2)
        };

        var stopAddresses = new[]
        {
            ("1200 Market St, San Francisco", "Alice Chen", "+1-555-1001", "Box — office supplies"),
            ("450 Sutter St, San Francisco", "Bob Martinez", "+1-555-1002", "Envelope"),
            ("88 Kearny St, San Francisco", "Cafe Norte", "+1-555-1003", "Catering trays"),
            ("200 Pine St, San Francisco", "Dana Lee", "+1-555-1004", "Small parcel"),
            ("555 California St, San Francisco", "Evan Park", "+1-555-1005", "Documents"),
            ("1 Ferry Building, San Francisco", "Ferry Gifts", "+1-555-1006", "Retail restock"),
            ("900 North Point St, San Francisco", "Gina Walsh", "+1-555-1007", "Pharmacy bag"),
            ("3000 Fillmore St, San Francisco", "Hayes Deli", "+1-555-1008", "Produce crate"),
            ("1400 Valencia St, San Francisco", "Ian Brooks", "+1-555-1009", "Apparel"),
            ("500 Brannan St, San Francisco", "Jules Kim", "+1-555-1010", "Electronics"),
            ("1100 Mission St, San Francisco", "Kara Singh", "+1-555-1011", "Parts kit"),
            ("2200 Lombard St, San Francisco", "Luna Pet Care", "+1-555-1012", "Pet food"),
        };

        var stops = new List<DeliveryRouteStop>
        {
            new()
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffff0100"),
                RouteId = routeId,
                Sequence = 0,
                StopType = DeliveryStopType.Depot,
                Status = DeliveryStopStatus.Completed,
                Address = route.DepotAddress,
                CompletedAt = now.AddHours(-2)
            }
        };

        var seq = 1;
        foreach (var (address, name, phone, parcel) in stopAddresses)
        {
            var completed = seq <= 3;
            stops.Add(new DeliveryRouteStop
            {
                Id = Guid.NewGuid(),
                RouteId = routeId,
                Sequence = seq,
                StopType = DeliveryStopType.Dropoff,
                Status = completed ? DeliveryStopStatus.Completed : DeliveryStopStatus.Pending,
                Address = address,
                RecipientName = name,
                RecipientPhone = phone,
                ParcelDescription = parcel,
                CompletedAt = completed ? now.AddHours(-2).AddMinutes(seq * 12) : null
            });
            seq++;
        }

        route.Stops = stops;

        var draftRoute = new DeliveryRoute
        {
            Id = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
            TenantId = DeliveryTenantId,
            Name = "Afternoon pickups — draft",
            Status = DeliveryRouteStatus.Draft,
            DepotAddress = "2500 Distribution Way, San Francisco, CA",
            ScheduledDate = DateOnly.FromDateTime(now),
            CreatedAt = now.AddHours(-1),
            Stops =
            [
                new DeliveryRouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
                    Sequence = 0,
                    StopType = DeliveryStopType.Depot,
                    Status = DeliveryStopStatus.Pending,
                    Address = "2500 Distribution Way, San Francisco, CA"
                },
                new DeliveryRouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
                    Sequence = 1,
                    StopType = DeliveryStopType.Pickup,
                    Status = DeliveryStopStatus.Pending,
                    Address = "Acme Warehouse, Oakland",
                    RecipientName = "Receiving",
                    ParcelDescription = "Return pallets"
                },
                new DeliveryRouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
                    Sequence = 2,
                    StopType = DeliveryStopType.Dropoff,
                    Status = DeliveryStopStatus.Pending,
                    Address = "Retail Hub, San Jose",
                    RecipientName = "Dock B",
                    ParcelDescription = "Consolidated shipment"
                }
            ]
        };

        db.DeliveryRoutes.AddRange(route, draftRoute);
        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = sp.GetRequiredService<AppDbContext>();

        if (!await roleManager.RoleExistsAsync("PlatformAdmin"))
            await roleManager.CreateAsync(new IdentityRole<Guid>("PlatformAdmin"));

        async Task EnsureTenantUser(string email, string name, Guid tenantId, ProductModule moduleAccess, TenantRole role = TenantRole.Admin)
        {
            var existing = await userManager.FindByEmailAsync(email);
            var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId);
            var moduleFlags = ProductModuleHelper.EffectiveModules(moduleAccess, tenant.Modules);

            if (existing is not null)
            {
                existing.TenantRole = role;
                existing.ModuleAccess = moduleFlags;
                existing.IsActive = true;
                await userManager.UpdateAsync(existing);
                return;
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = name,
                TenantId = tenantId,
                TenantRole = role,
                ModuleAccess = moduleFlags,
                IsActive = true,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "Demo123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        async Task EnsurePlatformAdmin(string email, string name)
        {
            if (await userManager.FindByEmailAsync(email) != null) return;
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = name,
                TenantId = null,
                TenantRole = TenantRole.Operator,
                ModuleAccess = ProductModule.None,
                IsActive = true,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "Demo123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(user, "PlatformAdmin");
        }

        await EnsureTenantUser("fleet@demo.local", "General Fleet Operator", GeneralTenantId, ProductModule.General);
        await EnsureTenantUser("av@demo.local", "AV Fleet Operator", AvTenantId, ProductModule.RoboTaxi);
        await EnsureTenantUser("delivery@demo.local", "Delivery Dispatcher", DeliveryTenantId, ProductModule.Delivery | ProductModule.RoutePlanning | ProductModule.Insights);
        await EnsureTenantUser("delivery.ops@demo.local", "Delivery Operator", DeliveryTenantId, ProductModule.Delivery, TenantRole.Operator);
        await EnsurePlatformAdmin("admin@demo.local", "Platform Admin");
    }

    private static async Task SeedInsightsDataAsync(AppDbContext db)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == DeliveryTenantId);
        if (tenant is not null)
            tenant.Modules = ProductModule.Delivery | ProductModule.RoutePlanning | ProductModule.Insights;

        var orders = await db.DeliveryOrders.Where(o => o.TenantId == DeliveryTenantId).ToListAsync();
        foreach (var order in orders)
        {
            if (order.CustomerId.HasValue) continue;
            var customer = await db.Customers.FirstOrDefaultAsync(c =>
                c.TenantId == order.TenantId && c.Name == order.RecipientName && c.Phone == order.RecipientPhone);
            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    Name = order.RecipientName,
                    Phone = string.IsNullOrWhiteSpace(order.RecipientPhone) ? null : order.RecipientPhone,
                    CreatedAt = DateTime.UtcNow
                };
                db.Customers.Add(customer);
            }

            order.CustomerId = customer.Id;
        }

        if (!await db.Drivers.AnyAsync(d => d.TenantId == DeliveryTenantId))
        {
            db.Drivers.Add(new Driver
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0001"),
                TenantId = DeliveryTenantId,
                DisplayName = "Alex Driver",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        await SeedDemoOperationalEventsAsync(db);
    }

    private static async Task SeedDemoOperationalEventsAsync(AppDbContext db)
    {
        if (await db.OperationalEvents.AnyAsync(e => e.TenantId == DeliveryTenantId))
            return;

        var now = DateTime.UtcNow;
        var van = await db.Vehicles.FirstOrDefaultAsync(v => v.TenantId == DeliveryTenantId && v.LicensePlate == "DEL-001");
        var av = await db.Vehicles.FirstOrDefaultAsync(v => v.TenantId == DeliveryTenantId && v.LicensePlate == "DEL-AV1");
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.TenantId == DeliveryTenantId);
        var dispatcher = await db.Users.FirstOrDefaultAsync(u => u.Email == "delivery@demo.local");
        var orders = await db.DeliveryOrders.Where(o => o.TenantId == DeliveryTenantId).ToListAsync();
        var route = await db.DeliveryRoutes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1"));
        if (van is null || driver is null || orders.Count == 0)
            return;

        var planRunId = Guid.Parse("11111111-1111-1111-1111-111111111188");
        var events = new List<OperationalEvent>();

        void Add(
            DateTime at,
            string domain,
            string eventType,
            string narrative,
            Guid? orderId = null,
            Guid? routeId = null,
            Guid? stopId = null,
            Guid? vehicleId = null,
            Guid? driverId = null,
            Guid? customerId = null,
            Guid? userId = null,
            Guid? planRun = null,
            string? vehicleLabel = null,
            string? driverLabel = null,
            string? customerLabel = null,
            string? plannerLabel = null,
            string metricsJson = "{}")
        {
            events.Add(new OperationalEvent
            {
                Id = Guid.NewGuid(),
                TenantId = DeliveryTenantId,
                OccurredAt = at,
                Domain = domain,
                EventType = eventType,
                OrderId = orderId,
                RouteId = routeId,
                StopId = stopId,
                VehicleId = vehicleId,
                DriverId = driverId,
                CustomerId = customerId,
                UserId = userId,
                PlanRunId = planRun,
                VehicleLabel = vehicleLabel,
                DriverLabel = driverLabel,
                CustomerLabel = customerLabel,
                PlannerLabel = plannerLabel,
                Narrative = narrative,
                MetricsJson = metricsJson,
            });
        }

        foreach (var order in orders)
        {
            Add(
                now.AddDays(-22).AddHours(Math.Abs(order.RecipientName.GetHashCode()) % 5),
                InsightsDomains.Delivery,
                DeliveryEventTypes.OrderCreated,
                $"Order created for {order.RecipientName} delivering to {order.DeliveryAddress}.",
                orderId: order.Id,
                customerId: order.CustomerId,
                customerLabel: order.RecipientName);
        }

        var order1 = orders.FirstOrDefault(o => o.Id == Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01"));
        var order2 = orders.FirstOrDefault(o => o.Id == Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02"));
        var order3 = orders.FirstOrDefault(o => o.Id == Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03"));
        if (order1 is not null)
        {
            Add(
                now.AddDays(-14),
                InsightsDomains.Delivery,
                DeliveryEventTypes.OrderAssigned,
                $"Order assigned to {van.LicensePlate}.",
                orderId: order1.Id,
                vehicleId: van.Id,
                customerId: order1.CustomerId,
                vehicleLabel: van.LicensePlate,
                customerLabel: order1.RecipientName,
                metricsJson: """{"automationMode":"Conventional"}""");
        }

        if (order3 is not null && av is not null)
        {
            Add(
                now.AddDays(-12),
                InsightsDomains.Delivery,
                DeliveryEventTypes.OrderAssigned,
                $"Order assigned to autonomous vehicle {av.LicensePlate}.",
                orderId: order3.Id,
                vehicleId: av.Id,
                customerId: order3.CustomerId,
                vehicleLabel: av.LicensePlate,
                customerLabel: order3.RecipientName,
                metricsJson: """{"automationMode":"Autonomous"}""");
        }

        if (route is not null)
        {
            var stopCount = route.Stops.Count(s => s.StopType != DeliveryStopType.Depot);
            Add(
                now.AddDays(-7),
                InsightsDomains.Delivery,
                DeliveryEventTypes.RouteCreated,
                $"Route {route.Name} created with {stopCount} stop(s).",
                routeId: route.Id,
                metricsJson: $$"""{"stopCount":{{stopCount}}}""");

            Add(
                now.AddDays(-7).AddMinutes(15),
                InsightsDomains.Delivery,
                DeliveryEventTypes.RouteAssigned,
                $"Route {route.Name} assigned to {driver.DisplayName} on {van.LicensePlate}.",
                routeId: route.Id,
                vehicleId: van.Id,
                driverId: driver.Id,
                vehicleLabel: van.LicensePlate,
                driverLabel: driver.DisplayName,
                metricsJson: """{"automationMode":"Conventional"}""");

            Add(
                now.AddDays(-6),
                InsightsDomains.Delivery,
                DeliveryEventTypes.RouteStarted,
                $"Route {route.Name} started with {driver.DisplayName}.",
                routeId: route.Id,
                vehicleId: van.Id,
                driverId: driver.Id,
                vehicleLabel: van.LicensePlate,
                driverLabel: driver.DisplayName);

            foreach (var stop in route.Stops.Where(s => s.Status == DeliveryStopStatus.Completed && s.StopType != DeliveryStopType.Depot))
            {
                Add(
                    stop.CompletedAt ?? now.AddDays(-6),
                    InsightsDomains.Delivery,
                    DeliveryEventTypes.StopCompleted,
                    $"Stop #{stop.Sequence} ({stop.StopType}) marked Completed.",
                    routeId: route.Id,
                    stopId: stop.Id,
                    vehicleId: van.Id,
                    driverId: driver.Id,
                    customerId: stop.RecipientName is not null
                        ? orders.FirstOrDefault(o => o.RecipientName == stop.RecipientName)?.CustomerId
                        : null,
                    vehicleLabel: van.LicensePlate,
                    driverLabel: driver.DisplayName,
                    customerLabel: stop.RecipientName);
            }
        }

        if (dispatcher is not null)
        {
            Add(
                now.AddDays(-10),
                InsightsDomains.RoutePlanning,
                RoutePlanningEventTypes.PlanRequested,
                "Route plan requested for upcoming deliveries.",
                userId: dispatcher.Id,
                planRun: planRunId,
                plannerLabel: dispatcher.DisplayName);

            Add(
                now.AddDays(-10).AddMinutes(2),
                InsightsDomains.RoutePlanning,
                RoutePlanningEventTypes.PlanCompleted,
                "Route plan completed with 2 draft routes.",
                userId: dispatcher.Id,
                planRun: planRunId,
                plannerLabel: dispatcher.DisplayName,
                metricsJson: """{"ordersPlanned":3,"ordersUnassigned":0,"proposalCount":2,"computeDurationMs":842}""");

            Add(
                now.AddDays(-9),
                InsightsDomains.RoutePlanning,
                RoutePlanningEventTypes.PlanAccepted,
                "Accepted plan with 1 draft route(s).",
                userId: dispatcher.Id,
                planRun: planRunId,
                plannerLabel: dispatcher.DisplayName,
                metricsJson: """{"routesCreated":1}""");

            if (route is not null)
            {
                Add(
                    now.AddDays(-5),
                    InsightsDomains.RoutePlanning,
                    RoutePlanningEventTypes.SequenceOptimized,
                    $"Optimized stop sequence on route {route.Name}.",
                    routeId: route.Id,
                    userId: dispatcher.Id,
                    plannerLabel: dispatcher.DisplayName,
                    metricsJson: """{"estimatedMinutesBefore":94,"estimatedMinutesAfter":78}""");
            }
        }

        if (order1 is not null)
        {
            Add(
                now.AddDays(-3),
                InsightsDomains.Delivery,
                DeliveryEventTypes.OrderDelivered,
                $"Order delivered to {order1.RecipientName} at {order1.DeliveryAddress}.",
                orderId: order1.Id,
                vehicleId: van.Id,
                driverId: driver.Id,
                customerId: order1.CustomerId,
                vehicleLabel: van.LicensePlate,
                driverLabel: driver.DisplayName,
                customerLabel: order1.RecipientName);
        }

        if (order2 is not null)
        {
            Add(
                now.AddDays(-1),
                InsightsDomains.Delivery,
                DeliveryEventTypes.OrderFailed,
                "Delivery attempt failed — recipient unavailable.",
                orderId: order2.Id,
                vehicleId: van.Id,
                driverId: driver.Id,
                customerId: order2.CustomerId,
                vehicleLabel: van.LicensePlate,
                driverLabel: driver.DisplayName,
                customerLabel: order2.RecipientName);
        }

        db.OperationalEvents.AddRange(events);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoDepotAsync(AppDbContext db)
    {
        var depot = await db.Depots.FirstOrDefaultAsync(d => d.Id == DemoDepotId);
        if (depot is null)
        {
            db.Depots.Add(new Depot
            {
                Id = DemoDepotId,
                TenantId = DeliveryTenantId,
                Name = "SF Main Warehouse",
                Address = DemoDepotAddress,
                IsDefault = true,
                Hours = "Mon–Fri 6am–6pm",
                Notes = "Primary distribution center",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return;
        }

        var isSeedManaged = string.Equals(depot.Address, DemoDepotAddress, StringComparison.OrdinalIgnoreCase)
            || LegacyDemoDepotAddresses.Any(legacy =>
                string.Equals(depot.Address, legacy, StringComparison.OrdinalIgnoreCase));

        if (!isSeedManaged)
            return;

        depot.Name = "SF Main Warehouse";
        depot.Hours = "Mon–Fri 6am–6pm";
        depot.Notes = "Primary distribution center";
        depot.IsDefault = true;
        depot.IsActive = true;

        if (!string.Equals(depot.Address, DemoDepotAddress, StringComparison.OrdinalIgnoreCase))
        {
            depot.Address = DemoDepotAddress;
            depot.Latitude = null;
            depot.Longitude = null;
        }

        await db.SaveChangesAsync();
    }

    private static async Task BackfillVehicleHomeDepotsAsync(AppDbContext db)
    {
        var defaultDepots = await db.Depots
            .Where(d => d.IsActive && d.IsDefault)
            .ToListAsync();

        var changed = false;
        foreach (var depot in defaultDepots)
        {
            var vehicles = await db.Vehicles
                .Where(v => v.TenantId == depot.TenantId && v.HomeDepotId == null)
                .ToListAsync();
            foreach (var vehicle in vehicles)
            {
                vehicle.HomeDepotId = depot.Id;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static async Task EnsureVehicleCategoriesAsync(AppDbContext db)
    {
        var avPlatePrefixes = new[] { "AV-", "DEL-AV" };
        var vehicles = await db.Vehicles.ToListAsync();
        var changed = false;

        foreach (var vehicle in vehicles)
        {
            var expected = avPlatePrefixes.Any(p => vehicle.LicensePlate.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                ? AssetCategory.SpecializedEquipment
                : (AssetCategory?)null;

            if (expected.HasValue && vehicle.Category != expected.Value)
            {
                vehicle.Category = expected.Value;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static void AddLocations(AppDbContext db, Guid vehicleId, decimal lat, decimal lng, DateTime now)
    {
        for (var i = 0; i < 3; i++)
        {
            db.VehicleLocations.Add(new VehicleLocation
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                Latitude = lat + (i * 0.001m),
                Longitude = lng + (i * 0.001m),
                RecordedAt = now.AddMinutes(-30 + i * 10),
                SpeedKph = 25 + i * 5,
                Source = LocationSource.GpsDevice
            });
        }
    }
}
