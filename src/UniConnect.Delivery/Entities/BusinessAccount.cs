using UniConnect.Domain.Entities;

namespace UniConnect.Delivery.Entities;

public class BusinessAccount
{
    public Guid Id { get; set; }
    public Guid FleetId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    public Fleet Fleet { get; set; } = null!;
}
