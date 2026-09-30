using FalimyCalc.Shared.Enums;

namespace FalimyCalc.Data.Entities;

public class PendingTransaction : SyncableEntity
{
    public string RawSmsBody { get; set; } = string.Empty;
    public decimal? DetectedAmount { get; set; }
    public string? DetectedMerchant { get; set; }
    public string? DetectedBank { get; set; }
    public string? DetectedAccount { get; set; }
    public TransactionType? DetectedType { get; set; }
    public DateTime? DetectedDate { get; set; }
    public PendingTransactionStatus Status { get; set; } = PendingTransactionStatus.Pending;

    /// <summary>
    /// Set when the user approves this pending transaction.
    /// Points to the Expense.GlobalId that was created from it.
    /// </summary>
    public Guid? ApprovedAsExpenseGlobalId { get; set; }
}
