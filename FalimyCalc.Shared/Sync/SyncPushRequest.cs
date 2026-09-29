using FalimyCalc.Shared.DTOs;

namespace FalimyCalc.Shared.Sync;

/// <summary>
/// Payload sent FROM the mobile device TO the server during a sync push.
/// Contains only records that have changed since the last sync.
/// </summary>
public class SyncPushRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime PushedAt { get; set; } = DateTime.UtcNow;
    public List<ExpenseDto> Expenses { get; set; } = [];
    public List<CategoryDto> Categories { get; set; } = [];
    public List<PendingTransactionDto> PendingTransactions { get; set; } = [];
}
