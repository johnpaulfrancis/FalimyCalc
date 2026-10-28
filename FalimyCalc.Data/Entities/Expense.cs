namespace FalimyCalc.Data.Entities;

public class Expense : SyncableEntity
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }

    public string? Note { get; set; }
    public string? Account { get; set; } // Display name of account (e.g. "HDFC Salary")
    public string Currency { get; set; } = "INR";
    public string? Subcategory { get; set; }
    public string? Tags { get; set; }

    // Foreign key — nullable because a category may not exist on the server yet
    // if it was created on the phone and not yet synced
    public int? CategoryId { get; set; }

    // GlobalId-based FK for cross-device reference (used during sync)
    public Guid? CategoryGlobalId { get; set; }

    // Which family member added this expense (null = unassigned / legacy)
    public Guid? MemberGlobalId { get; set; }

    // Bank Account association
    public Guid? AccountGlobalId { get; set; }
    public int? BankAccountId { get; set; }

    // Navigation
    public Category? Category { get; set; }
    public BankAccount? BankAccount { get; set; }
}
