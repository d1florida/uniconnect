using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniConnect.Delivery;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Enums;
using UniConnect.Infrastructure.Identity;
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
using UniConnect.Infrastructure.Services.Insights;
using UniConnect.Infrastructure.Services.RoutePlanning;
using UniConnect.Insights.Enums;
using System.Text.Json;

namespace UniConnect.Infrastructure.Data;

public static class DbSeed
{
    public static readonly Guid GeneralTenantId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid AvTenantId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid DeliveryTenantId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid DemoDepotId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0002");
    private static readonly Guid PascoZoneId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01");
    private static readonly Guid PascoTemplateId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02");
    private static readonly Guid PascoCustomerNorthId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03");
    private static readonly Guid PascoCustomerCentralId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee04");
    private static readonly Guid PascoOrderNorthId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffeeee0001");
    private static readonly Guid PascoOrderCentralId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffeeee0002");
    private const string DemoDepotAddress = "12022 67th Ln, Largo, FL 33773";
    private const string DemoDepotName = "Tampa Bay Distribution Center";
    // When changing DemoDepotAddress, add the previous value here so existing dev DBs pick up the new seed address.
    private static readonly string[] LegacyDemoDepotAddresses =
    [
        "2500 Distribution Way, San Francisco, CA",
        "12022 67th ln largo fl 33773",
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
        await LinkDemoDriverUsersAsync(scope.ServiceProvider, db);
        await BackfillCustomerDeliveryLocationsAsync(db);
        await EnsureDemoDepotAsync(db);
        await EnsureDemoDeliveryOrdersAsync(db);
        await EnsureFixedRoutesAsync(db);
        await EnsureVehicleCategoriesAsync(db);
        await BackfillVehicleHomeDepotsAsync(db);

        var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();
        await BackfillVehicleLocationsToHomeDepotsAsync(db, geocoding);

        foreach (var order in await db.DeliveryOrders.ToListAsync())
        {
            var pickup = await geocoding.GeocodeAsync(order.PickupAddress, tenantId: order.TenantId);
            if (pickup.HasValue)
            {
                order.PickupLatitude = pickup.Value.Latitude;
                order.PickupLongitude = pickup.Value.Longitude;
                order.PickupFormattedAddress = pickup.Value.FormattedAddress;
            }

            var delivery = await geocoding.GeocodeAsync(order.DeliveryAddress, tenantId: order.TenantId);
            if (delivery.HasValue)
            {
                order.DeliveryLatitude = delivery.Value.Latitude;
                order.DeliveryLongitude = delivery.Value.Longitude;
                order.DeliveryFormattedAddress = delivery.Value.FormattedAddress;
            }
        }

        foreach (var depot in await db.Depots.ToListAsync())
        {
            var point = await geocoding.GeocodeAsync(depot.Address, tenantId: depot.TenantId);
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
        AddLocations(db, deliveryVan.Id, 27.8862m, -82.7335m, now);
        AddLocations(db, deliveryAv.Id, 27.8840m, -82.7310m, now);

        var demoOrders = BuildDemoDeliveryOrders(DemoDepotAddress, now, deliveryVan.Id, deliveryAv.Id);
        db.DeliveryOrders.AddRange(demoOrders.Orders);
        if (demoOrders.Assignments.Count > 0)
            db.DeliveryAssignments.AddRange(demoOrders.Assignments);

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
            Name = "Morning Tampa Bay drops",
            Status = DeliveryRouteStatus.InProgress,
            DepotAddress = DemoDepotAddress,
            ScheduledDate = DateOnly.FromDateTime(now),
            VehicleId = van.Id,
            AutomationMode = AutomationMode.Conventional,
            CreatedAt = now.AddHours(-4),
            StartedAt = now.AddHours(-2)
        };

        var stopAddresses = new[]
        {
            ("400 N Tampa St, Tampa, FL 33602", "Alice Chen", "+1-727-555-1001", "Box — office supplies"),
            ("600 Cleveland St, Clearwater, FL 33755", "Bob Martinez", "+1-727-555-1002", "Envelope"),
            ("234 Beach Dr NE, St. Petersburg, FL 33701", "Cafe Norte", "+1-727-555-1003", "Catering trays"),
            ("102 N Kentucky Ave, Lakeland, FL 33801", "Dana Lee", "+1-863-555-1004", "Small parcel"),
            ("3300 W Kennedy Blvd, Tampa, FL 33609", "Evan Park", "+1-813-555-1005", "Documents"),
            ("200 Central Ave, St. Petersburg, FL 33701", "Ferry Gifts", "+1-727-555-1006", "Retail restock"),
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
            Name = "Afternoon Clearwater — draft",
            Status = DeliveryRouteStatus.Draft,
            DepotAddress = DemoDepotAddress,
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
                    Address = DemoDepotAddress
                },
                new DeliveryRouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
                    Sequence = 1,
                    StopType = DeliveryStopType.Dropoff,
                    Status = DeliveryStopStatus.Pending,
                    Address = "1800 Gulf to Bay Blvd, Clearwater, FL 33765",
                    RecipientName = "Clearwater Office",
                    ParcelDescription = "Supply restock"
                },
                new DeliveryRouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
                    Sequence = 2,
                    StopType = DeliveryStopType.Dropoff,
                    Status = DeliveryStopStatus.Pending,
                    Address = "2510 McMullen Booth Rd, Clearwater, FL 33761",
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
        await EnsureTenantUser("alex.driver@demo.local", "Alex Driver", DeliveryTenantId, ProductModule.Delivery, TenantRole.Driver);
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
            customer ??= await db.Customers.FirstOrDefaultAsync(c =>
                c.TenantId == order.TenantId && c.Name == order.RecipientName);
            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    Name = order.RecipientName,
                    Phone = string.IsNullOrWhiteSpace(order.RecipientPhone) ? null : order.RecipientPhone,
                    DeliveryAddress = order.DeliveryAddress,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Customers.Add(customer);
            }
            else if (string.IsNullOrWhiteSpace(customer.DeliveryAddress) && !string.IsNullOrWhiteSpace(order.DeliveryAddress))
            {
                customer.DeliveryAddress = order.DeliveryAddress;
            }

            order.CustomerId = customer.Id;
        }

        if (!await db.Drivers.AnyAsync(d => d.TenantId == DeliveryTenantId))
        {
            db.Drivers.AddRange(
                new Driver
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0001"),
                    TenantId = DeliveryTenantId,
                    DisplayName = "Alex Driver",
                    ShiftStartTime = new TimeOnly(7, 0),
                    ShiftEndTime = new TimeOnly(17, 0),
                    LunchMinutes = 30,
                    BreakMinutes = 15,
                    MaxRouteMinutes = 240,
                    ReturnByTime = new TimeOnly(12, 0),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Driver
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0003"),
                    TenantId = DeliveryTenantId,
                    DisplayName = "Jordan Driver",
                    ShiftStartTime = new TimeOnly(8, 0),
                    ShiftEndTime = new TimeOnly(18, 0),
                    LunchMinutes = 30,
                    BreakMinutes = 30,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
        }
        else
        {
            foreach (var driver in await db.Drivers.Where(d => d.TenantId == DeliveryTenantId).ToListAsync())
            {
                if (driver.ShiftStartTime == default)
                    driver.ShiftStartTime = new TimeOnly(7, 0);
                if (driver.ShiftEndTime == default)
                    driver.ShiftEndTime = new TimeOnly(17, 0);
                if (driver.DisplayName == "Alex Driver")
                {
                    driver.MaxRouteMinutes = 240;
                    driver.ReturnByTime = new TimeOnly(12, 0);
                }
            }
        }

        await db.SaveChangesAsync();
        await SeedDriverWorkPatternsAsync(db);
        await SeedDriverScheduleExceptionsAsync(db);
        await SeedPlanningRulesAsync(db);
        await SeedDemoOperationalEventsAsync(db);
    }

    private static async Task LinkDemoDriverUsersAsync(IServiceProvider sp, AppDbContext db)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var alexUser = await userManager.FindByEmailAsync("alex.driver@demo.local");
        if (alexUser is null)
            return;

        var alexDriverId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0001");
        var alex = await db.Drivers.FirstOrDefaultAsync(d => d.Id == alexDriverId);
        if (alex is not null && alex.UserId != alexUser.Id)
        {
            alex.UserId = alexUser.Id;
            await db.SaveChangesAsync();
        }
        else if (alex is null)
            await DriverUserProvisioning.EnsureDriverForUserAsync(db, DeliveryTenantId, alexUser);
    }

    private static async Task SeedDriverWorkPatternsAsync(AppDbContext db)
    {
        var driversWithoutPatterns = await db.Drivers
            .Where(d => !db.DriverWorkPatterns.Any(p => p.DriverId == d.Id))
            .ToListAsync();

        foreach (var driver in driversWithoutPatterns)
            db.DriverWorkPatterns.AddRange(DriverWorkPatternHelper.CreateDefaultPatterns(driver));

        if (driversWithoutPatterns.Count > 0)
            await db.SaveChangesAsync();
    }

    private static async Task SeedDriverScheduleExceptionsAsync(AppDbContext db)
    {
        var alexId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0001");
        if (!await db.Drivers.AnyAsync(d => d.Id == alexId))
            return;

        var ptoDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);
        if (!await db.DriverScheduleExceptions.AnyAsync(e => e.DriverId == alexId && e.Date == ptoDate))
        {
            db.DriverScheduleExceptions.Add(new DriverScheduleException
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeee0001"),
                DriverId = alexId,
                Date = ptoDate,
                IsWorking = false,
                ShiftStartTime = new TimeOnly(7, 0),
                ShiftEndTime = new TimeOnly(17, 0),
                LunchMinutes = 30,
                BreakMinutes = 15,
                Note = "PTO (demo)",
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
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
                Name = DemoDepotName,
                Address = DemoDepotAddress,
                IsDefault = true,
                Hours = "Mon–Fri 6am–6pm",
                Notes = "Primary Tampa Bay distribution center",
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

        depot.Name = DemoDepotName;
        depot.Hours = "Mon–Fri 6am–6pm";
        depot.Notes = "Primary Tampa Bay distribution center";
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

    private static async Task BackfillCustomerDeliveryLocationsAsync(AppDbContext db)
    {
        var customers = await db.Customers
            .Where(c => c.DeliveryAddress != null && (c.DeliveryLatitude == null || c.DeliveryLongitude == null))
            .ToListAsync();
        if (customers.Count == 0)
            return;

        var ordersByCustomer = await db.DeliveryOrders
            .Where(o => o.CustomerId != null)
            .GroupBy(o => o.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Address = g.OrderByDescending(o => o.CreatedAt).Select(o => o.DeliveryAddress).First() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Address);

        var changed = false;
        foreach (var customer in customers)
        {
            if (string.IsNullOrWhiteSpace(customer.DeliveryAddress)
                && ordersByCustomer.TryGetValue(customer.Id, out var address)
                && !string.IsNullOrWhiteSpace(address))
            {
                customer.DeliveryAddress = address;
                changed = true;
            }
        }

        if (changed)
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

    private static async Task BackfillVehicleLocationsToHomeDepotsAsync(AppDbContext db, IGeocodingService geocoding)
    {
        var vehicles = await db.Vehicles.Where(v => v.HomeDepotId != null).ToListAsync();
        if (vehicles.Count == 0)
            return;

        var depotIds = vehicles.Select(v => v.HomeDepotId!.Value).Distinct().ToList();
        var depots = await db.Depots.Where(d => depotIds.Contains(d.Id)).ToListAsync();
        var changed = false;

        foreach (var depot in depots)
        {
            if (depot.Latitude is not null && depot.Longitude is not null)
                continue;

            var geocoded = await geocoding.GeocodeAsync(depot.Address, tenantId: depot.TenantId);
            if (!geocoded.HasValue)
                continue;

            depot.Latitude = geocoded.Value.Latitude;
            depot.Longitude = geocoded.Value.Longitude;
            changed = true;
        }

        var activeStatuses = new[]
        {
            DeliveryOrderStatus.Assigned,
            DeliveryOrderStatus.PickedUp,
            DeliveryOrderStatus.InTransit
        };
        var activeVehicleIds = (await db.DeliveryAssignments
            .Join(db.DeliveryOrders, a => a.DeliveryOrderId, o => o.Id, (a, o) => new { a.VehicleId, o.Status })
            .Where(x => activeStatuses.Contains(x.Status))
            .Select(x => x.VehicleId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        var now = DateTime.UtcNow;
        foreach (var vehicle in vehicles)
        {
            if (activeVehicleIds.Contains(vehicle.Id))
                continue;

            var depot = depots.FirstOrDefault(d => d.Id == vehicle.HomeDepotId);
            if (depot?.Latitude is null || depot.Longitude is null)
                continue;

            db.VehicleLocations.Add(new VehicleLocation
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicle.Id,
                Latitude = depot.Latitude.Value,
                Longitude = depot.Longitude.Value,
                RecordedAt = now,
                SpeedKph = 0,
                Source = LocationSource.Manual
            });
            changed = true;
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

    private static Guid DemoOrderId(int index) =>
        Guid.Parse($"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee{index:D2}");

    private static bool IsDemoOrderId(Guid id)
    {
        var text = id.ToString();
        return text.StartsWith("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee", StringComparison.OrdinalIgnoreCase)
            && text.Length == 36;
    }

    private sealed record DemoOrderSpec(
        int Index,
        DeliveryOrderStatus Status,
        string DeliveryAddress,
        string RecipientName,
        string RecipientPhone,
        string ParcelDescription,
        int CreatedHoursAgo,
        bool AssignToVan,
        bool AssignToAv);

    private static readonly DemoOrderSpec[] DemoDeliveryOrderSpecs =
    [
        new(1, DeliveryOrderStatus.InTransit, "400 N Tampa St, Tampa, FL 33602", "Maria Gonzalez", "+1-813-555-0101", "Small package", 48, true, false),
        new(2, DeliveryOrderStatus.Created, "1 Dali Blvd, St. Petersburg, FL 33701", "James Whitaker", "+1-727-555-0102", "Documents", 47, false, false),
        new(3, DeliveryOrderStatus.Assigned, "600 Cleveland St, Clearwater, FL 33755", "Clearwater Clinic", "+1-727-555-0103", "Medical supplies", 46, false, true),
        new(4, DeliveryOrderStatus.Created, "102 N Kentucky Ave, Lakeland, FL 33801", "Lakeland Books", "+1-863-555-0104", "Book shipment", 45, false, false),
        new(5, DeliveryOrderStatus.Created, "1800 Gulf to Bay Blvd, Clearwater, FL 33765", "Bayview Dental", "+1-727-555-0105", "Lab samples", 44, false, false),
        new(6, DeliveryOrderStatus.Created, "234 Beach Dr NE, St. Petersburg, FL 33701", "Beachside Cafe", "+1-727-555-0106", "Catering trays", 43, false, false),
        new(7, DeliveryOrderStatus.Created, "3300 W Kennedy Blvd, Tampa, FL 33609", "Westshore Office", "+1-813-555-0107", "Office supplies", 42, false, false),
        new(8, DeliveryOrderStatus.Created, "500 E Kennedy Blvd, Tampa, FL 33602", "County Records", "+1-813-555-0108", "Legal filings", 41, false, false),
        new(9, DeliveryOrderStatus.Created, "1100 Cleveland St, Clearwater, FL 33755", "Downtown Clearwater", "+1-727-555-0109", "Retail restock", 40, false, false),
        new(10, DeliveryOrderStatus.Created, "401 E Jackson St, Tampa, FL 33602", "Jackson Tower", "+1-813-555-0110", "Electronics", 39, false, false),
        new(11, DeliveryOrderStatus.Created, "200 Central Ave, St. Petersburg, FL 33701", "Central Arts", "+1-727-555-0111", "Framed prints", 38, false, false),
        new(12, DeliveryOrderStatus.Created, "901 Florida Ave S, Lakeland, FL 33803", "Florida Southern", "+1-863-555-0112", "Campus mail", 37, false, false),
        new(13, DeliveryOrderStatus.Created, "2500 W Waters Ave, Tampa, FL 33614", "Waters Plaza", "+1-813-555-0113", "Apparel", 36, false, false),
        new(14, DeliveryOrderStatus.Created, "7901 4th St N, St. Petersburg, FL 33702", "Fourth Street Market", "+1-727-555-0114", "Produce crate", 35, false, false),
        new(15, DeliveryOrderStatus.Created, "2510 McMullen Booth Rd, Clearwater, FL 33761", "McMullen Commerce", "+1-727-555-0115", "Parts kit", 34, false, false),
        new(16, DeliveryOrderStatus.Created, "211 S Florida Ave, Lakeland, FL 33801", "Florida Ave Gifts", "+1-863-555-0116", "Gift boxes", 33, false, false),
        new(17, DeliveryOrderStatus.Created, "1900 Ulmerton Rd, Clearwater, FL 33762", "Ulmerton Industrial", "+1-727-555-0117", "Tooling case", 32, false, false),
        new(18, DeliveryOrderStatus.Created, "4200 George J Bean Pkwy, Tampa, FL 33607", "Airport Cargo", "+1-813-555-0118", "Air freight handoff", 31, false, false),
        new(19, DeliveryOrderStatus.Created, "1633 1st Ave S, St. Petersburg, FL 33712", "Gulfport Bistro", "+1-727-555-0119", "Dry goods", 30, false, false),
        new(20, DeliveryOrderStatus.Created, "2727 W Lake Ave, Tampa, FL 33611", "South Tampa Home", "+1-813-555-0120", "Furniture parts", 29, false, false),
        new(21, DeliveryOrderStatus.Created, "13575 Icot Blvd, Clearwater, FL 33760", "Icot Center", "+1-727-555-0121", "Pharmacy bag", 28, false, false),
        new(22, DeliveryOrderStatus.Created, "1000 Broadway, Dunedin, FL 34698", "Dunedin Brewery", "+1-727-555-0122", "Beverage supplies", 27, false, false),
        new(23, DeliveryOrderStatus.Created, "455 E Waycroft Way, Largo, FL 33771", "Largo Medical", "+1-727-555-0123", "Hospital linens", 26, false, false),
        new(24, DeliveryOrderStatus.Created, "5150 US Hwy 19 N, Pinellas Park, FL 33781", "Pinellas Park Hub", "+1-727-555-0124", "Mixed freight", 25, false, false),
        new(25, DeliveryOrderStatus.Created, "4100 George Rd, Tampa, FL 33634", "Carrollwood Office", "+1-813-555-0125", "Printer toner", 24, false, false),
    ];

    private static (List<DeliveryOrder> Orders, List<DeliveryAssignment> Assignments) BuildDemoDeliveryOrders(
        string pickupAddress,
        DateTime now,
        Guid conventionalVanId,
        Guid autonomousVanId)
    {
        var orders = new List<DeliveryOrder>();
        var assignments = new List<DeliveryAssignment>();

        foreach (var spec in DemoDeliveryOrderSpecs)
        {
            var order = new DeliveryOrder
            {
                Id = DemoOrderId(spec.Index),
                TenantId = DeliveryTenantId,
                Status = spec.Status,
                PickupAddress = pickupAddress,
                DeliveryAddress = spec.DeliveryAddress,
                RecipientName = spec.RecipientName,
                RecipientPhone = spec.RecipientPhone,
                ParcelDescription = spec.ParcelDescription,
                CreatedAt = now.AddHours(-spec.CreatedHoursAgo),
            };
            orders.Add(order);

            if (spec.AssignToVan)
            {
                assignments.Add(new DeliveryAssignment
                {
                    Id = Guid.NewGuid(),
                    DeliveryOrderId = order.Id,
                    VehicleId = conventionalVanId,
                    AutomationMode = AutomationMode.Conventional,
                    AssignedAt = now.AddHours(-Math.Max(1, spec.CreatedHoursAgo / 2)),
                });
            }
            else if (spec.AssignToAv)
            {
                assignments.Add(new DeliveryAssignment
                {
                    Id = Guid.NewGuid(),
                    DeliveryOrderId = order.Id,
                    VehicleId = autonomousVanId,
                    AutomationMode = AutomationMode.Autonomous,
                    AssignedAt = now.AddHours(-Math.Max(1, spec.CreatedHoursAgo / 2)),
                });
            }
        }

        return (orders, assignments);
    }

    private static async Task EnsureDemoDeliveryOrdersAsync(AppDbContext db)
    {
        var depot = await db.Depots.AsNoTracking()
            .Where(d => d.TenantId == DeliveryTenantId && d.IsActive)
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.CreatedAt)
            .FirstOrDefaultAsync();
        var pickupAddress = depot?.Address ?? DemoDepotAddress;

        var markerId = DemoOrderId(25);
        if (await db.DeliveryOrders.AnyAsync(o => o.Id == markerId))
        {
            var seedOrders = await db.DeliveryOrders
                .Where(o => o.TenantId == DeliveryTenantId)
                .ToListAsync();
            var changed = false;
            foreach (var order in seedOrders.Where(o => IsDemoOrderId(o.Id)))
            {
                if (!string.Equals(order.PickupAddress, pickupAddress, StringComparison.OrdinalIgnoreCase))
                {
                    order.PickupAddress = pickupAddress;
                    order.PickupLatitude = null;
                    order.PickupLongitude = null;
                    order.PickupFormattedAddress = null;
                    changed = true;
                }
            }

            if (changed)
                await db.SaveChangesAsync();
            return;
        }

        var van = await db.Vehicles.FirstOrDefaultAsync(v =>
            v.TenantId == DeliveryTenantId && v.LicensePlate == "DEL-001");
        var av = await db.Vehicles.FirstOrDefaultAsync(v =>
            v.TenantId == DeliveryTenantId && v.LicensePlate == "DEL-AV1");
        if (van is null || av is null)
            return;

        var existingSeedIds = await db.DeliveryOrders
            .Where(o => o.TenantId == DeliveryTenantId)
            .Select(o => o.Id)
            .ToListAsync();
        var toRemove = existingSeedIds.Where(IsDemoOrderId).ToList();
        if (toRemove.Count > 0)
        {
            var existingAssignments = await db.DeliveryAssignments
                .Where(a => toRemove.Contains(a.DeliveryOrderId))
                .ToListAsync();
            db.DeliveryAssignments.RemoveRange(existingAssignments);
            var existingOrders = await db.DeliveryOrders.Where(o => toRemove.Contains(o.Id)).ToListAsync();
            db.DeliveryOrders.RemoveRange(existingOrders);
            await db.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var demoOrders = BuildDemoDeliveryOrders(pickupAddress, now, van.Id, av.Id);
        db.DeliveryOrders.AddRange(demoOrders.Orders);
        if (demoOrders.Assignments.Count > 0)
            db.DeliveryAssignments.AddRange(demoOrders.Assignments);
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

    private static async Task EnsureFixedRoutesAsync(AppDbContext db)
    {
        if (!await db.DeliveryZones.AnyAsync(z => z.Id == PascoZoneId))
        {
            db.DeliveryZones.Add(new DeliveryZone
            {
                Id = PascoZoneId,
                TenantId = DeliveryTenantId,
                Name = "Pasco County",
                MatchType = DeliveryZoneMatchType.Manual,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var alexDriverId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0001");
        var defaultVanId = await db.Vehicles.AsNoTracking()
            .Where(v => v.TenantId == DeliveryTenantId && v.LicensePlate == "DEL-001")
            .Select(v => v.Id)
            .FirstOrDefaultAsync();

        if (!await db.FixedRouteTemplates.AnyAsync(t => t.Id == PascoTemplateId))
        {
            db.FixedRouteTemplates.Add(new FixedRouteTemplate
            {
                Id = PascoTemplateId,
                TenantId = DeliveryTenantId,
                Name = "Pasco — Tue & Thu",
                DeliveryZoneId = PascoZoneId,
                RouteDays = [DayOfWeek.Tuesday, DayOfWeek.Thursday],
                DepotId = DemoDepotId,
                DefaultVehicleId = defaultVanId == Guid.Empty ? null : defaultVanId,
                DefaultDriverId = alexDriverId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            var pascoTemplate = await db.FixedRouteTemplates.FirstAsync(t => t.Id == PascoTemplateId);
            pascoTemplate.Name = "Pasco — Tue & Thu";
            pascoTemplate.RouteDays = [DayOfWeek.Tuesday, DayOfWeek.Thursday];
        }

        if (!await db.Customers.AnyAsync(c => c.Id == PascoCustomerNorthId))
        {
            db.Customers.Add(new Customer
            {
                Id = PascoCustomerNorthId,
                TenantId = DeliveryTenantId,
                Name = "Pasco North Office",
                Phone = "+1-727-555-0301",
                DeliveryAddress = "8731 Old County Rd 54, New Port Richey, FL 34653",
                DeliveryZoneId = PascoZoneId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await db.Customers.AnyAsync(c => c.Id == PascoCustomerCentralId))
        {
            db.Customers.Add(new Customer
            {
                Id = PascoCustomerCentralId,
                TenantId = DeliveryTenantId,
                Name = "Pasco Central Supply",
                Phone = "+1-352-555-0302",
                DeliveryAddress = "14100 7th St, Dade City, FL 33525",
                DeliveryZoneId = PascoZoneId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        if (!await db.DeliveryOrders.AnyAsync(o => o.Id == PascoOrderNorthId))
        {
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                Id = PascoOrderNorthId,
                TenantId = DeliveryTenantId,
                Status = DeliveryOrderStatus.Created,
                PickupAddress = DemoDepotAddress,
                DeliveryAddress = "8731 Old County Rd 54, New Port Richey, FL 34653",
                RecipientName = "Pasco North Office",
                RecipientPhone = "+1-727-555-0301",
                ParcelDescription = "Office supplies",
                CustomerId = PascoCustomerNorthId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await db.DeliveryOrders.AnyAsync(o => o.Id == PascoOrderCentralId))
        {
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                Id = PascoOrderCentralId,
                TenantId = DeliveryTenantId,
                Status = DeliveryOrderStatus.Created,
                PickupAddress = DemoDepotAddress,
                DeliveryAddress = "14100 7th St, Dade City, FL 33525",
                RecipientName = "Pasco Central Supply",
                RecipientPhone = "+1-352-555-0302",
                ParcelDescription = "Warehouse restock",
                CustomerId = PascoCustomerCentralId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        await RefreshDeliveryOrderHoldsAsync(db);
    }

    private static async Task RefreshDeliveryOrderHoldsAsync(AppDbContext db)
    {
        var templates = await db.FixedRouteTemplates.AsNoTracking()
            .Where(t => t.TenantId == DeliveryTenantId && t.IsActive)
            .ToDictionaryAsync(t => t.DeliveryZoneId);

        var orders = await db.DeliveryOrders
            .Where(o => o.TenantId == DeliveryTenantId && o.Status == DeliveryOrderStatus.Created)
            .ToListAsync();

        if (orders.Count == 0)
            return;

        var customerIds = orders.Where(o => o.CustomerId.HasValue).Select(o => o.CustomerId!.Value).Distinct().ToList();
        var customers = customerIds.Count == 0
            ? new Dictionary<Guid, Customer>()
            : await db.Customers.Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var order in orders)
        {
            if (!order.CustomerId.HasValue || !customers.TryGetValue(order.CustomerId.Value, out var customer)
                || !customer.DeliveryZoneId.HasValue
                || !templates.TryGetValue(customer.DeliveryZoneId.Value, out var template))
            {
                order.FixedRouteTemplateId = null;
                order.HeldUntil = null;
                continue;
            }

            order.FixedRouteTemplateId = template.Id;
            order.HeldUntil = FixedRouteScheduleHelper.ComputeNextRouteDate(template.RouteDays, today);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedPlanningRulesAsync(AppDbContext db)
    {
        const string markdown = """
            # Demo Delivery — planning rules

            ## Fleet
            - Balance routes across active trucks by estimated drive minutes.
            - Cluster stops by geography when multiple trucks run (reduces cross-region miles).
            - Maximum 25 stops per route.
            - Enforce driver route caps — overflow orders stay unassigned.

            ## Fixed routes
            - Held orders wait for their fixed route day.
            - Same-day add-on orders may be included when the planner selects them.
            """;

        var compiled = PlanningPolicyMarkdownParser.Parse(markdown);
        var compiledJson = JsonSerializer.Serialize(compiled.Policy, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var existing = await db.TenantPlanningRules.FirstOrDefaultAsync(r => r.TenantId == DeliveryTenantId);
        if (existing is null)
        {
            db.TenantPlanningRules.Add(new TenantPlanningRules
            {
                TenantId = DeliveryTenantId,
                Markdown = markdown,
                CompiledPolicyJson = compiledJson,
                CompileWarningsJson = JsonSerializer.Serialize(compiled.Warnings),
                CompiledAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (!existing.Markdown.Contains("Enforce driver", StringComparison.OrdinalIgnoreCase) ||
                 !existing.Markdown.Contains("Cluster stops", StringComparison.OrdinalIgnoreCase) ||
                 existing.Markdown.Contains("## Drivers", StringComparison.OrdinalIgnoreCase))
        {
            existing.Markdown = markdown;
            existing.CompiledPolicyJson = compiledJson;
            existing.CompileWarningsJson = JsonSerializer.Serialize(compiled.Warnings);
            existing.CompiledAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}
