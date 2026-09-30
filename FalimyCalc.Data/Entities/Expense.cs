namespace FalimyCalc.Data.Entities;

public class Expense : SyncableEntity
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }

    // Foreign key — nullable because a category may not exist on the server yet
    // if it was created on the phone and not yet synced
    public int? CategoryId { get; set; }

    // GlobalId-based FK for cross-device reference (used during sync)
    public Guid? CategoryGlobalId { get; set; }

    // Navigation
    public Category? Category { get; set; }
}
