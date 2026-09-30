using FalimyCalc.Data.Services;
using FalimyCalc.Shared.Sync;
using Microsoft.AspNetCore.Mvc;

namespace FalimyCalc.Web.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly SyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(SyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Phone → Server: push local changes to the server.
    /// The server applies Last-Write-Wins and returns which records were accepted
    /// plus the current server RowVersion so the phone can immediately pull.
    /// </summary>
    [HttpPost("push")]
    public async Task<ActionResult<SyncPushResponse>> Push([FromBody] SyncPushRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new SyncPushResponse
            {
                Success = false,
                ErrorMessage = "DeviceId is required."
            });

        _logger.LogInformation(
            "Sync push from device {DeviceId}: {Expenses} expenses, {Categories} categories, {Pending} pending",
            request.DeviceId,
            request.Expenses.Count,
            request.Categories.Count,
            request.PendingTransactions.Count);

        var response = await _syncService.PushAsync(request);

        if (!response.Success)
        {
            _logger.LogWarning("Sync push failed for device {DeviceId}: {Error}",
                request.DeviceId, response.ErrorMessage);
            return StatusCode(500, response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Server → Phone: pull all records changed since the phone's last sync.
    /// The phone sends its last known RowVersion; the server returns only newer records.
    /// Pass sinceRowVersion=0 to get everything (first sync).
    /// </summary>
    [HttpGet("pull")]
    public async Task<ActionResult<SyncPullResponse>> Pull(
        [FromQuery] long sinceRowVersion = 0,
        [FromQuery] string deviceId = "")
    {
        _logger.LogInformation(
            "Sync pull from device {DeviceId} since RowVersion {RowVersion}",
            deviceId, sinceRowVersion);

        var response = await _syncService.PullAsync(sinceRowVersion, deviceId);

        if (!response.Success)
            return StatusCode(500, response);

        _logger.LogInformation(
            "Pull response: {Expenses} expenses, {Categories} categories, {Pending} pending, new RowVersion={RowVersion}",
            response.Expenses.Count,
            response.Categories.Count,
            response.PendingTransactions.Count,
            response.ServerRowVersion);

        return Ok(response);
    }

    /// <summary>
    /// Simple health check so the phone can detect whether the server is reachable
    /// before attempting a full sync.
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { alive = true, serverTime = DateTime.UtcNow });
    }
}
