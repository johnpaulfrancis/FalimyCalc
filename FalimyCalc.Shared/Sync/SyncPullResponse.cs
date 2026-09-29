using FalimyCalc.Shared.DTOs;

namespace FalimyCalc.Shared.Sync;

/// <summary>
/// Server's response to a pull request from the mobile device.
/// Contains only records that changed AFTER the phone's LastSyncedRowVersion.
/// </summary>
public class SyncPullResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The phone should store this and use it as the starting point
    /// for its next pull request.
    /// </summary>
    public long ServerRowVersion { get; set; }

    public List<ExpenseDto> Expenses { get; set; } = [];
    public List<CategoryDto> Categories { get; set; } = [];
    public List<PendingTransactionDto> PendingTransactions { get; set; } = [];
}
