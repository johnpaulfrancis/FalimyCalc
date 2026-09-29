namespace FalimyCalc.Shared.Sync;

/// <summary>
/// Server's response after receiving a push from the mobile device.
/// Tells the phone which records were accepted, and the new server RowVersion
/// so the phone knows where to start its next pull from.
/// </summary>
public class SyncPushResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long NewServerRowVersion { get; set; }
    public List<Guid> AcceptedExpenseIds { get; set; } = [];
    public List<Guid> AcceptedCategoryIds { get; set; } = [];
    public List<Guid> AcceptedPendingTransactionIds { get; set; } = [];
}
