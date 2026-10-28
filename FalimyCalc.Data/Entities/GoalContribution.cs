namespace FalimyCalc.Data.Entities;

public class GoalContribution : SyncableEntity
{
    public Guid GoalGlobalId { get; set; }
    public int? FinancialGoalId { get; set; }
    public FinancialGoal? FinancialGoal { get; set; }

    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string Type { get; set; } = "Deposit"; // "Deposit" (adds to goal) or "Withdrawal" (withdraws from goal)
    public string? Note { get; set; }

    // From / To Bank Account
    public Guid? AccountGlobalId { get; set; }
    public string? AccountName { get; set; }
}
