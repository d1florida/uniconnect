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
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Driver> Drivers => Set<Driver>();
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
        });

        modelBuilder.Entity<Driver>(e =>
        {
            e.ToTable("Drivers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => x.UserId);
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
        });

        modelBuilder.Entity<GeocodedAddress>(e =>
        {
            e.ToTable("GeocodedAddresses");
            e.HasKey(x => x.NormalizedAddress);
        });
    }
}
