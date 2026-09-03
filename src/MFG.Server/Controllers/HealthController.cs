using Microsoft.AspNetCore.Mvc;
using MFG.Data;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        bool dbHealthy;
        try
        {
            dbHealthy = await _db.Database.CanConnectAsync(ct);
        }
        catch
        {
            dbHealthy = false;
        }

        var status = dbHealthy ? "healthy" : "degraded";
        var statusCode = dbHealthy ? 200 : 503;

        return StatusCode(statusCode, new
        {
            status,
            timestamp = DateTime.UtcNow,
            database = dbHealthy ? "connected" : "disconnected"
        });
    }
}
