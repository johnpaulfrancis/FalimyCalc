namespace FalimyCalc.Data.Entities;

public class RecurringTransaction : SyncableEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = "Expense"; // "Expense" or "Income"
    
    /// <summary>
    /// Frequency: "Daily", "Weekly", "Bi-Weekly", "Monthly", "Quarterly", "Semi-Annually", "Yearly"
    /// </summary>
    public string Frequency { get; set; } = "Monthly";
    
    public DateTime NextDueDate { get; set; } = DateTime.Today;
    public DateTime? LastPaidDate { get; set; }

    public int? CategoryId { get; set; }
    public Guid? CategoryGlobalId { get; set; }
    public Category? Category { get; set; }

    public int? BankAccountId { get; set; }
    public Guid? AccountGlobalId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public Guid? MemberGlobalId { get; set; }

    public string? Icon { get; set; }   // e.g. "🍿", "📦", "📶", "🏠", "🛡️", "🎵"
    public string? Colour { get; set; } // e.g. "#e50914", "#00a8e1", "#1db954"
    public string Status { get; set; } = "Active"; // "Active", "Paused", "Cancelled"
    
    public bool AutoLog { get; set; } = false;
    public string? Note { get; set; }
    public int? BillingCycleDay { get; set; } // e.g. 5 for 5th of every month
}
