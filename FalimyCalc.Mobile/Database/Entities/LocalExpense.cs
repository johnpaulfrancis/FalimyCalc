using FalimyCalc.Shared.Enums;
using SQLite;

namespace FalimyCalc.Mobile.Database.Entities;

/// <summary>
/// Local SQLite representation of an expense on the phone.
/// Mirrors the server Expense entity but adds SyncStatus for offline tracking.
/// </summary>
[Table("Expenses")]
public class LocalExpense
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public Guid GlobalId { get; set; }

    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Guid? CategoryGlobalId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Tracks whether this record needs to be pushed to the server.
    /// Synced = in sync, PendingCreate/Update/Delete = needs push.
    /// </summary>
    [Indexed]
    public SyncStatus SyncStatus { get; set; } = SyncStatus.PendingCreate;
}
