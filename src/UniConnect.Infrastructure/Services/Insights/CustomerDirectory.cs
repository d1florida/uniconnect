using Microsoft.EntityFrameworkCore;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Interfaces;
using UniConnect.Infrastructure.Data;

namespace UniConnect.Infrastructure.Services.Insights;

public class CustomerDirectory(AppDbContext db) : ICustomerDirectory
{
    public async Task<Customer> GetOrCreateAsync(Guid tenantId, string name, string? phone, CancellationToken ct = default)
    {
        var trimmedName = name.Trim();
        var trimmedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Customer name is required.");

        var existing = await db.Customers
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId && c.Name == trimmedName && c.Phone == trimmedPhone,
                ct);

        if (existing is not null)
            return existing;

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = trimmedName,
            Phone = trimmedPhone,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer;
    }
}
