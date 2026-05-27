using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniConnect.Delivery.Entities;
using UniConnect.GeneralFleet.Entities;
using UniConnect.Infrastructure.Identity;
using UniConnect.RoboTaxi.Entities;
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
    public DbSet<BusinessAccount> BusinessAccounts => Set<BusinessAccount>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<DeliveryAssignment> DeliveryAssignments => Set<DeliveryAssignment>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<DeliveryRouteStop> DeliveryRouteStops => Set<DeliveryRouteStop>();

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
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<BusinessAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AccountCode }).IsUnique();
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BusinessAccount).WithMany().HasForeignKey(x => x.BusinessAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Assignment).WithOne(a => a.DeliveryOrder).HasForeignKey<DeliveryAssignment>(a => a.DeliveryOrderId);
        });

        modelBuilder.Entity<DeliveryAssignment>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<DeliveryRoute>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ScheduledDate });
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryRouteStop>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RouteId, x.Sequence }).IsUnique();
            e.HasOne(x => x.Route).WithMany(r => r.Stops).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => x.TenantId);
        });
    }
}
