using System.Net.Http.Json;
using FalimyCalc.Mobile.Database.Entities;
using FalimyCalc.Mobile.Services;
using FalimyCalc.Shared.DTOs;
using FalimyCalc.Shared.Enums;
using FalimyCalc.Shared.Sync;

namespace FalimyCalc.Mobile.Services;

public enum SyncState
{
    Idle,
    Syncing,
    Success,
    Failed,
    ServerUnreachable
}

public class SyncResult
{
    public SyncState State { get; set; }
    public string? Message { get; set; }
    public int PushedCount { get; set; }
    public int PulledCount { get; set; }
    public DateTime? LastSyncTime { get; set; }
}

/// <summary>
/// Handles bidirectional sync between the phone's SQLite database
/// and the server's REST API.
///
/// Sync sequence:
///   1. Ping server — abort silently if unreachable
///   2. Push — send all locally pending records to server
///   3. Pull — fetch all server records newer than LastSyncedRowVersion
///   4. Apply pulled records to local SQLite
///   5. Mark pushed records as Synced
///   6. Save new LastSyncedRowVersion
/// </summary>
public class SyncClient
{
    private readonly HttpClient _httpClient;
    private readonly LocalExpenseService _localService;

    // Fired when sync state changes so the UI can update
    public event Action<SyncResult>? SyncStateChanged;

    public SyncClient(HttpClient httpClient, LocalExpenseService localService)
    {
        _httpClient = httpClient;
        _localService = localService;
    }

    private string _serverUrl = string.Empty;

    /// <summary>
    /// Stores the server URL. Safe to call at any time — does not touch HttpClient properties.
    /// </summary>
    public void ConfigureServerUrl(string serverUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
    }

    public string? CurrentServerUrl => _serverUrl;

    // ── Public entry point ─────────────────────────────────────────────────

    public async Task<SyncResult> SyncAsync()
    {
        var result = new SyncResult { State = SyncState.Syncing };
        SyncStateChanged?.Invoke(result);

        try
        {
            // 1. Check server is reachable
            if (!await PingAsync())
            {
                result.State = SyncState.ServerUnreachable;
                result.Message = "Server is not reachable. Changes are saved locally.";
                SyncStateChanged?.Invoke(result);
                return result;
            }

            var deviceId = await _localService.GetDeviceIdAsync();

            // 2. Push local changes to server
            var pushResult = await PushAsync(deviceId);
            if (!pushResult.Success)
            {
                result.State = SyncState.Failed;
                result.Message = $"Push failed: {pushResult.ErrorMessage}";
                SyncStateChanged?.Invoke(result);
                return result;
            }

            result.PushedCount = pushResult.AcceptedExpenseIds.Count
                                + pushResult.AcceptedCategoryIds.Count
                                + pushResult.AcceptedPendingTransactionIds.Count;

            // 3. Pull server changes
            var lastRowVersion = await _localService.GetLastSyncedRowVersionAsync();
            var pullResult = await PullAsync(lastRowVersion, deviceId);

            if (!pullResult.Success)
            {
                result.State = SyncState.Failed;
                result.Message = $"Pull failed: {pullResult.ErrorMessage}";
                SyncStateChanged?.Invoke(result);
                return result;
            }

            // 4. Apply pulled records locally
            await ApplyPullAsync(pullResult, deviceId);
            result.PulledCount = pullResult.Expenses.Count
                                + pullResult.Categories.Count
                                + pullResult.PendingTransactions.Count;

            // 5. Mark pushed records as Synced locally
            await _localService.MarkAsSyncedAsync(
                pushResult.AcceptedExpenseIds,
                pushResult.AcceptedCategoryIds,
                pushResult.AcceptedPendingTransactionIds);

            // 6. Save the new RowVersion for next pull
            await _localService.SetLastSyncedRowVersionAsync(pullResult.ServerRowVersion);

            result.State = SyncState.Success;
            result.LastSyncTime = DateTime.Now;
            result.Message = $"↑ {result.PushedCount} pushed · ↓ {result.PulledCount} pulled";
            SyncStateChanged?.Invoke(result);
            return result;
        }
        catch (Exception ex)
        {
            result.State = SyncState.Failed;
            result.Message = ex.Message;
            SyncStateChanged?.Invoke(result);
            return result;
        }
    }

    // ── Ping ───────────────────────────────────────────────────────────────

    public async Task<bool> PingAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_serverUrl)) return false;
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/sync/ping");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Push ───────────────────────────────────────────────────────────────

    private async Task<SyncPushResponse> PushAsync(string deviceId)
    {
        var (pendingExpenses, pendingCategories, pendingTransactions) =
            await _localService.GetPendingSyncItemsAsync();

        // Nothing to push
        if (!pendingExpenses.Any() && !pendingCategories.Any() && !pendingTransactions.Any())
            return new SyncPushResponse { Success = true };

        var request = new SyncPushRequest
        {
            DeviceId = deviceId,
            PushedAt = DateTime.UtcNow,
            Expenses = pendingExpenses.Select(MapToDto).ToList(),
            Categories = pendingCategories.Select(MapToDto).ToList(),
            PendingTransactions = pendingTransactions.Select(MapToDto).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync($"{_serverUrl}/api/sync/push", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SyncPushResponse>()
               ?? new SyncPushResponse { Success = false, ErrorMessage = "Empty response" };
    }

    // ── Pull ───────────────────────────────────────────────────────────────

    private async Task<SyncPullResponse> PullAsync(long sinceRowVersion, string deviceId)
    {
        var url = $"{_serverUrl}/api/sync/pull?sinceRowVersion={sinceRowVersion}&deviceId={deviceId}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SyncPullResponse>()
               ?? new SyncPullResponse { Success = false, ErrorMessage = "Empty response" };
    }

    // ── Apply pulled records to local SQLite ───────────────────────────────

    private async Task ApplyPullAsync(SyncPullResponse pull, string myDeviceId)
    {
        var db = await _localService.GetDbAsync();

        // Categories first — expenses may reference them
        foreach (var dto in pull.Categories)
        {
            // Skip records that originated from this device (we already have them)
            var existing = await db.Table<LocalCategory>()
                .FirstOrDefaultAsync(c => c.GlobalId == dto.GlobalId);

            if (existing is null)
            {
                db.InsertAsync(MapFromDto<LocalCategory>(dto, dto.Name,
                    c => { c.Name = dto.Name; c.Icon = dto.Icon; c.Colour = dto.Colour; }))
                    .GetAwaiter().GetResult();
            }
            else if (dto.ModifiedAt > existing.ModifiedAt || existing.SyncStatus == SyncStatus.Synced)
            {
                existing.Name = dto.Name;
                existing.Icon = dto.Icon;
                existing.Colour = dto.Colour;
                existing.IsDeleted = dto.IsDeleted;
                existing.ModifiedAt = dto.ModifiedAt;
                existing.DeviceId = dto.DeviceId;
                existing.SyncStatus = SyncStatus.Synced;
                await db.UpdateAsync(existing);
            }
        }

        foreach (var dto in pull.Expenses)
        {
            var existing = await db.Table<LocalExpense>()
                .FirstOrDefaultAsync(e => e.GlobalId == dto.GlobalId);

            if (existing is null)
            {
                await db.InsertAsync(new LocalExpense
                {
                    GlobalId = dto.GlobalId,
                    Amount = dto.Amount,
                    Description = dto.Description,
                    TransactionDate = dto.TransactionDate,
                    CategoryGlobalId = dto.CategoryGlobalId,
                    DeviceId = dto.DeviceId,
                    CreatedAt = dto.CreatedAt,
                    ModifiedAt = dto.ModifiedAt,
                    IsDeleted = dto.IsDeleted,
                    SyncStatus = SyncStatus.Synced
                });
            }
            else if (dto.ModifiedAt > existing.ModifiedAt || existing.SyncStatus == SyncStatus.Synced)
            {
                existing.Amount = dto.Amount;
                existing.Description = dto.Description;
                existing.TransactionDate = dto.TransactionDate;
                existing.CategoryGlobalId = dto.CategoryGlobalId;
                existing.IsDeleted = dto.IsDeleted;
                existing.ModifiedAt = dto.ModifiedAt;
                existing.DeviceId = dto.DeviceId;
                existing.SyncStatus = SyncStatus.Synced;
                await db.UpdateAsync(existing);
            }
        }

        foreach (var dto in pull.PendingTransactions)
        {
            var existing = await db.Table<LocalPendingTransaction>()
                .FirstOrDefaultAsync(p => p.GlobalId == dto.GlobalId);

            if (existing is null)
            {
                await db.InsertAsync(new LocalPendingTransaction
                {
                    GlobalId = dto.GlobalId,
                    RawSmsBody = dto.RawSmsBody,
                    DetectedAmount = dto.DetectedAmount,
                    DetectedMerchant = dto.DetectedMerchant,
                    DetectedBank = dto.DetectedBank,
                    DetectedAccount = dto.DetectedAccount,
                    DetectedType = dto.DetectedType,
                    DetectedDate = dto.DetectedDate,
                    Status = dto.Status,
                    ApprovedAsExpenseGlobalId = dto.ApprovedAsExpenseGlobalId,
                    DeviceId = dto.DeviceId,
                    CreatedAt = dto.CreatedAt,
                    ModifiedAt = dto.ModifiedAt,
                    IsDeleted = dto.IsDeleted,
                    SyncStatus = SyncStatus.Synced
                });
            }
            else if (dto.ModifiedAt > existing.ModifiedAt || existing.SyncStatus == SyncStatus.Synced)
            {
                existing.Status = dto.Status;
                existing.ApprovedAsExpenseGlobalId = dto.ApprovedAsExpenseGlobalId;
                existing.IsDeleted = dto.IsDeleted;
                existing.ModifiedAt = dto.ModifiedAt;
                existing.SyncStatus = SyncStatus.Synced;
                await db.UpdateAsync(existing);
            }
        }
    }

    // ── Mappers ────────────────────────────────────────────────────────────

    private static ExpenseDto MapToDto(LocalExpense e) => new()
    {
        GlobalId = e.GlobalId,
        Amount = e.Amount,
        Description = e.Description,
        TransactionDate = e.TransactionDate,
        CategoryGlobalId = e.CategoryGlobalId,
        DeviceId = e.DeviceId,
        CreatedAt = e.CreatedAt,
        ModifiedAt = e.ModifiedAt,
        IsDeleted = e.IsDeleted
    };

    private static CategoryDto MapToDto(LocalCategory c) => new()
    {
        GlobalId = c.GlobalId,
        Name = c.Name,
        Icon = c.Icon,
        Colour = c.Colour,
        DeviceId = c.DeviceId,
        CreatedAt = c.CreatedAt,
        ModifiedAt = c.ModifiedAt,
        IsDeleted = c.IsDeleted
    };

    private static FalimyCalc.Shared.DTOs.PendingTransactionDto MapToDto(LocalPendingTransaction p) => new()
    {
        GlobalId = p.GlobalId,
        RawSmsBody = p.RawSmsBody,
        DetectedAmount = p.DetectedAmount,
        DetectedMerchant = p.DetectedMerchant,
        DetectedBank = p.DetectedBank,
        DetectedAccount = p.DetectedAccount,
        DetectedType = p.DetectedType,
        DetectedDate = p.DetectedDate,
        Status = p.Status,
        ApprovedAsExpenseGlobalId = p.ApprovedAsExpenseGlobalId,
        DeviceId = p.DeviceId,
        CreatedAt = p.CreatedAt,
        ModifiedAt = p.ModifiedAt,
        IsDeleted = p.IsDeleted
    };

    private static T MapFromDto<T>(object dto, string name, Action<T> configure)
        where T : LocalCategory, new()
    {
        throw new NotImplementedException(); // unused helper — kept for pattern
    }
}
