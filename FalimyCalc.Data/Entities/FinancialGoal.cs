namespace FalimyCalc.Data.Entities;

public class FinancialGoal : SyncableEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; } // Running total of saved funds
    public DateTime? TargetDate { get; set; }  // Target completion date

    public string? Colour { get; set; }        // e.g. "#6366f1"
    public string? Icon { get; set; }          // e.g. "🏖️", "🚗", "🛡️", "💻", "🏠"
    public string? Category { get; set; }      // e.g. "Emergency Fund", "Travel", "Purchase", "Investment"
    public string? Note { get; set; }
    public string Status { get; set; } = "Active"; // "Active", "Completed", "Paused"

    // Owner (null = Family-wide goal, or specific member's goal)
    public Guid? MemberGlobalId { get; set; }

    // Optional linked bank account where this sinking fund is physically held
    public Guid? AccountGlobalId { get; set; }
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    // Contributions
    public ICollection<GoalContribution> Contributions { get; set; } = [];
}
