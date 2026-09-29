using FalimyCalc.Shared.Enums;

namespace FalimyCalc.Shared.DTOs;

/// <summary>
/// Represents an SMS-detected transaction awaiting user approval.
/// Synced to the server so it can also be reviewed from the web UI.
/// </summary>
public class PendingTransactionDto
{
    public Guid GlobalId { get; set; }
    public string RawSmsBody { get; set; } = string.Empty;
    public decimal? DetectedAmount { get; set; }
    public string? DetectedMerchant { get; set; }
    public string? DetectedBank { get; set; }
    public string? DetectedAccount { get; set; }
    public TransactionType? DetectedType { get; set; }
    public DateTime? DetectedDate { get; set; }
    public PendingTransactionStatus Status { get; set; }
    public Guid? ApprovedAsExpenseGlobalId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
}
