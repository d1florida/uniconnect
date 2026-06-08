using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniConnect.Delivery.Entities;
using UniConnect.GeneralFleet.Entities;
using UniConnect.Infrastructure.Identity;
using UniConnect.Insights.Entities;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoutePlanning.Entities;
using UniConnect.Tenant.Entities;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<VehicleLocation> VehicleLocations => Set<VehicleLocation>();
    public DbSet<RoboTaxiProfile> RoboTaxiProfiles => Set<RoboTaxiProfile>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<DeliveryAssignment> DeliveryAssignments => Set<DeliveryAssignment>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<DeliveryRouteStop> DeliveryRouteStops => Set<DeliveryRouteStop>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<FixedRouteTemplate> FixedRouteTemplates => Set<FixedRouteTemplate>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<TenantGeocodingSettings> TenantGeocodingSettings => Set<TenantGeocodingSettings>();
    public DbSet<TenantPlanningRules> TenantPlanningRules => Set<TenantPlanningRules>();
    public DbSet<TenantDeliverySettings> TenantDeliverySettings => Set<TenantDeliverySettings>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverWorkPattern> DriverWorkPatterns => Set<DriverWorkPattern>();
    public DbSet<DriverScheduleException> DriverScheduleExceptions => Set<DriverScheduleException>();
    public DbSet<DriverScheduleRequest> DriverScheduleRequests => Set<DriverScheduleRequest>();
    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();
    public DbSet<RoutePlanRun> RoutePlanRuns => Set<RoutePlanRun>();
    public DbSet<GeocodedAddress> GeocodedAddresses => Set<GeocodedAddress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantEntity>(e =>
        {
            e.ToTable("Tenants");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Modules).HasColumnName("Modules");
        });

        modelBuilder.Entity<Vehicle>(e =>
        {
            e.ToTable("Vehicles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Vin).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.VehicleNumber }).IsUnique();
            e.HasIndex(x => x.HomeDepotId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Depot>().WithMany().HasForeignKey(x => x.HomeDepotId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MaintenanceRecord>(e =>
        {
            e.ToTable("MaintenanceRecords");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Vehicle).WithMany(v => v.MaintenanceRecords).HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleLocation>(e =>
        {
            e.ToTable("VehicleLocations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.VehicleId, x.RecordedAt });
            e.HasOne(x => x.Vehicle).WithMany(v => v.Locations).HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoboTaxiProfile>(e =>
        {
            e.HasKey(x => x.VehicleId);
        });

        modelBuilder.Entity<DeliveryOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Assignment).WithOne(a => a.DeliveryOrder).HasForeignKey<DeliveryAssignment>(a => a.DeliveryOrderId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.FixedRouteTemplateId);
        });

        modelBuilder.Entity<DeliveryZone>(e =>
        {
            e.ToTable("DeliveryZones");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Name });
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FixedRouteTemplate>(e =>
        {
            e.ToTable("FixedRouteTemplates");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.DeliveryZoneId });
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DeliveryZone).WithMany().HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RouteDays)
                .HasConversion(
                    v => v.Select(d => (int)d).ToArray(),
                    v => v.Select(i => (DayOfWeek)i).OrderBy(d => d).ToList())
                .HasColumnType("integer[]")
                .Metadata.SetValueComparer(
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<DayOfWeek>>(
                        (left, right) =>
                            (left ?? new List<DayOfWeek>()).SequenceEqual(right ?? new List<DayOfWeek>()),
                        v => v.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
                        v => v.ToList()));
        });

        modelBuilder.Entity<Depot>(e =>
        {
            e.ToTable("Depots");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Name });
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DriverId);
        });

        modelBuilder.Entity<DeliveryRoute>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ScheduledDate });
            e.HasIndex(x => x.DriverId);
            e.HasIndex(x => x.DepotId);
            e.HasIndex(x => x.FixedRouteTemplateId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Depot>().WithMany().HasForeignKey(x => x.DepotId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeliveryRouteStop>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RouteId, x.Sequence }).IsUnique();
            e.HasIndex(x => x.DeliveryOrderId);
            e.HasOne(x => x.Route).WithMany(r => r.Stops).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<TenantApiKey>(e =>
        {
            e.ToTable("TenantApiKeys");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyPrefix).IsUnique();
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Name, x.Phone });
            e.HasIndex(x => x.DeliveryZoneId);
        });

        modelBuilder.Entity<Driver>(e =>
        {
            e.ToTable("Drivers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.TenantId, x.UserId })
                .IsUnique()
                .HasFilter("\"UserId\" IS NOT NULL");
        });

        modelBuilder.Entity<DriverWorkPattern>(e =>
        {
            e.ToTable("DriverWorkPatterns");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DriverId, x.DayOfWeek }).IsUnique();
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DriverScheduleException>(e =>
        {
            e.ToTable("DriverScheduleExceptions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DriverId, x.Date }).IsUnique();
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DriverScheduleRequest>(e =>
        {
            e.ToTable("DriverScheduleRequests");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.DriverId, x.FromDate, x.ToDate });
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OperationalEvent>(e =>
        {
            e.ToTable("OperationalEvents");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.OccurredAt });
            e.HasIndex(x => new { x.TenantId, x.Domain, x.OccurredAt });
            e.HasIndex(x => x.DriverId);
            e.HasIndex(x => x.VehicleId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.PlanRunId);
        });

        modelBuilder.Entity<RoutePlanRun>(e =>
        {
            e.ToTable("RoutePlanRuns");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.RequestedByUserId);
            e.HasIndex(x => x.DepotId);
            e.HasIndex(x => x.FixedRouteTemplateId);
        });

        modelBuilder.Entity<GeocodedAddress>(e =>
        {
            e.ToTable("GeocodedAddresses");
            e.HasKey(x => x.NormalizedAddress);
        });

        modelBuilder.Entity<TenantGeocodingSettings>(e =>
        {
            e.ToTable("TenantGeocodingSettings");
            e.HasKey(x => x.TenantId);
            e.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantGeocodingSettings>(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantPlanningRules>(e =>
        {
            e.ToTable("TenantPlanningRules");
            e.HasKey(x => x.TenantId);
            e.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantPlanningRules>(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantDeliverySettings>(e =>
        {
            e.ToTable("TenantDeliverySettings");
            e.HasKey(x => x.TenantId);
            e.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantDeliverySettings>(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
