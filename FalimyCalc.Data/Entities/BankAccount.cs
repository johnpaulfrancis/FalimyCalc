namespace FalimyCalc.Data.Entities;

public class BankAccount : SyncableEntity
{
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Initial opening balance when the account was added. Can be positive, zero, or negative (e.g. credit cards, overdrafts).
    /// </summary>
    public decimal InitialBalance { get; set; }

    public string? AccountNumberSuffix { get; set; } // e.g. last 4 digits
    public string? AccountType { get; set; }         // e.g. "Savings", "Current", "Credit Card", "Cash Wallet"
    public string? BankName { get; set; }            // e.g. "HDFC Bank", "SBI", "ICICI"
    public string? Colour { get; set; }              // e.g. "#6366f1"
    public string? Icon { get; set; }                // e.g. "🏦", "💳", "💵", "📱"

    /// <summary>
    /// Which family member/profile owns this bank account.
    /// </summary>
    public Guid MemberGlobalId { get; set; }

    // Navigation
    public ICollection<Expense> Expenses { get; set; } = [];
}
