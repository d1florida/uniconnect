using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniConnect.Infrastructure.Data;

namespace UniConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return Ok(new
        {
            status = canConnect ? "healthy" : "degraded",
            database = canConnect ? "connected" : "unavailable",
            timestamp = DateTime.UtcNow
        });
    }
}
