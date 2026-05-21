using Microsoft.AspNetCore.Identity;

namespace UniConnect.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid? FleetId { get; set; }
}
