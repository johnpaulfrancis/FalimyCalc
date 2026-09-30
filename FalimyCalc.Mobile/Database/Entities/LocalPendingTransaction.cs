using FalimyCalc.Shared.Enums;
using SQLite;

namespace FalimyCalc.Mobile.Database.Entities;

[Table("PendingTransactions")]
public class LocalPendingTransaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public Guid GlobalId { get; set; }

    public string RawSmsBody { get; set; } = string.Empty;
    public decimal? DetectedAmount { get; set; }
    public string? DetectedMerchant { get; set; }
    public string? DetectedBank { get; set; }
    public string? DetectedAccount { get; set; }
    public TransactionType? DetectedType { get; set; }
    public DateTime? DetectedDate { get; set; }

    [Indexed]
    public PendingTransactionStatus Status { get; set; } = PendingTransactionStatus.Pending;

    public Guid? ApprovedAsExpenseGlobalId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }

    [Indexed]
    public SyncStatus SyncStatus { get; set; } = SyncStatus.PendingCreate;
}
