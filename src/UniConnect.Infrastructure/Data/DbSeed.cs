using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniConnect.Delivery.Entities;
using UniConnect.Infrastructure.Identity;
using UniConnect.Delivery.Enums;
using UniConnect.Domain.Entities;
using UniConnect.Domain.Enums;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoboTaxi.Enums;

namespace UniConnect.Infrastructure.Data;

public static class DbSeed
{
    public static readonly Guid GeneralFleetId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid AvFleetId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid DeliveryFleetId = Guid.Parse("33333333-3333-3333-3333-333333333301");

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.Fleets.AnyAsync())
            await SeedFleetsAsync(db);

        if (!await db.DeliveryRoutes.AnyAsync())
            await SeedDeliveryRoutesAsync(db);

        await SeedUsersAsync(scope.ServiceProvider);
    }

    private static async Task SeedFleetsAsync(AppDbContext db)
    {

        var now = DateTime.UtcNow;

        var generalFleet = new Fleet
        {
            Id = GeneralFleetId,
            Name = "Demo General Fleet",
            Slug = "demo-general",
            FleetType = FleetType.General,
            CreatedAt = now
        };

        var avFleet = new Fleet
        {
            Id = AvFleetId,
            Name = "Demo AV Fleet",
            Slug = "demo-av",
            FleetType = FleetType.RoboTaxi,
            CreatedAt = now
        };

        var deliveryFleet = new Fleet
        {
            Id = DeliveryFleetId,
            Name = "Demo Delivery Fleet",
            Slug = "demo-delivery",
            FleetType = FleetType.Delivery,
            CreatedAt = now
        };

        var truck1 = new Vehicle
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101"),
            FleetId = GeneralFleetId,
            Vin = "1HGBH41JXMN109186",
            Make = "Ford",
            Model = "Transit",
            Year = 2022,
            LicensePlate = "GEN-001",
            CurrentMileage = 45000,
            Status = VehicleStatus.Active
        };

        var truck2 = new Vehicle
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102"),
            FleetId = GeneralFleetId,
            Vin = "2HGBH41JXMN109187",
            Make = "Mercedes",
            Model = "Sprinter",
            Year = 2023,
            LicensePlate = "GEN-002",
            CurrentMileage = 22000,
            Status = VehicleStatus.Active
        };

        var av1 = new Vehicle
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101"),
            FleetId = AvFleetId,
            Vin = "AVROBO00000000001",
            Make = "Waymo",
            Model = "Jaguar I-PACE",
            Year = 2024,
            LicensePlate = "AV-001",
            CurrentMileage = 12000,
            Status = VehicleStatus.Active
        };

        var av2 = new Vehicle
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb102"),
            FleetId = AvFleetId,
            Vin = "AVROBO00000000002",
            Make = "Cruise",
            Model = "Origin",
            Year = 2024,
            LicensePlate = "AV-002",
            CurrentMileage = 8500,
            Status = VehicleStatus.Active
        };

        var deliveryVan = new Vehicle
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc01"),
            FleetId = DeliveryFleetId,
            Vin = "DELCONV0000000001",
            Make = "Ford",
            Model = "E-Transit",
            Year = 2023,
            LicensePlate = "DEL-001",
            CurrentMileage = 31000,
            Status = VehicleStatus.Active
        };

        var deliveryAv = new Vehicle
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc02"),
            FleetId = DeliveryFleetId,
            Vin = "DELAV000000000001",
            Make = "Nuro",
            Model = "R3",
            Year = 2024,
            LicensePlate = "DEL-AV1",
            CurrentMileage = 5000,
            Status = VehicleStatus.Active
        };

        db.Fleets.AddRange(generalFleet, avFleet, deliveryFleet);
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

        var businessAccount = new BusinessAccount
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01"),
            FleetId = DeliveryFleetId,
            CompanyName = "Acme Wholesale",
            AccountCode = "ACME-001",
            ContactEmail = "logistics@acme.example"
        };
        db.BusinessAccounts.Add(businessAccount);

        var b2cOrder1 = new DeliveryOrder
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01"),
            FleetId = DeliveryFleetId,
            Channel = DeliveryChannel.B2C,
            Status = DeliveryOrderStatus.InTransit,
            PickupAddress = "100 Market St, San Francisco",
            DeliveryAddress = "500 Howard St, San Francisco",
            RecipientName = "Jane Consumer",
            RecipientPhone = "+1-555-0101",
            ParcelDescription = "Small package",
            CreatedAt = now.AddHours(-2)
        };

        var b2cOrder2 = new DeliveryOrder
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02"),
            FleetId = DeliveryFleetId,
            Channel = DeliveryChannel.B2C,
            Status = DeliveryOrderStatus.Created,
            PickupAddress = "200 Mission St, San Francisco",
            DeliveryAddress = "800 Folsom St, San Francisco",
            RecipientName = "John Smith",
            RecipientPhone = "+1-555-0102",
            ParcelDescription = "Documents",
            CreatedAt = now.AddHours(-1)
        };

        var b2bOrder = new DeliveryOrder
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03"),
            FleetId = DeliveryFleetId,
            Channel = DeliveryChannel.B2B,
            Status = DeliveryOrderStatus.Assigned,
            PickupAddress = "Acme Warehouse, Oakland",
            DeliveryAddress = "Retail Hub, San Jose",
            RecipientName = "Receiving Desk",
            RecipientPhone = "+1-555-0200",
            BusinessAccountId = businessAccount.Id,
            ParcelDescription = "Pallet - office supplies",
            CreatedAt = now.AddHours(-4)
        };

        db.DeliveryOrders.AddRange(b2cOrder1, b2cOrder2, b2bOrder);

        db.DeliveryAssignments.AddRange(
            new DeliveryAssignment
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = b2cOrder1.Id,
                VehicleId = deliveryVan.Id,
                AutomationMode = AutomationMode.Conventional,
                AssignedAt = now.AddHours(-1)
            },
            new DeliveryAssignment
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = b2bOrder.Id,
                VehicleId = deliveryAv.Id,
                AutomationMode = AutomationMode.Autonomous,
                AssignedAt = now.AddHours(-3)
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDeliveryRoutesAsync(AppDbContext db)
    {
        var van = await db.Vehicles.FirstOrDefaultAsync(v => v.FleetId == DeliveryFleetId && v.LicensePlate == "DEL-001");
        if (van is null) return;

        var now = DateTime.UtcNow;
        var routeId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1");
        var route = new DeliveryRoute
        {
            Id = routeId,
            FleetId = DeliveryFleetId,
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
            FleetId = DeliveryFleetId,
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
                    ParcelDescription = "Consolidated B2B"
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

        if (!await roleManager.RoleExistsAsync("PlatformAdmin"))
            await roleManager.CreateAsync(new IdentityRole<Guid>("PlatformAdmin"));

        async Task EnsureUser(string email, string name, Guid? fleetId, string? role = null)
        {
            if (await userManager.FindByEmailAsync(email) != null) return;
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = name,
                FleetId = fleetId,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "Demo123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            if (role != null)
                await userManager.AddToRoleAsync(user, role);
        }

        await EnsureUser("fleet@demo.local", "General Fleet Operator", GeneralFleetId);
        await EnsureUser("av@demo.local", "AV Fleet Operator", AvFleetId);
        await EnsureUser("delivery@demo.local", "Delivery Dispatcher", DeliveryFleetId);
        await EnsureUser("admin@demo.local", "Platform Admin", null, "PlatformAdmin");
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
