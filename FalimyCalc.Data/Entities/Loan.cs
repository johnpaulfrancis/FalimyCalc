namespace FalimyCalc.Data.Entities;

public class Loan : SyncableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Lender { get; set; }                      // e.g. "HDFC Bank", "SBI", "Bajaj Finserv"
    public string LoanType { get; set; } = "Personal Loan";   // "Home Loan", "Car Loan", "Personal Loan", "Education Loan", "Consumer Durable", "Gold Loan", "Credit Card EMI", "Other"

    public decimal PrincipalAmount { get; set; }             // Initial total borrowed amount
    public decimal InterestRate { get; set; }                // Annual interest rate percentage (e.g. 8.5)
    public int TenureMonths { get; set; }                    // Duration in months (e.g. 60)
    public decimal EmiAmount { get; set; }                   // Monthly EMI amount

    public decimal OutstandingBalance { get; set; }          // Remaining principal balance
    public decimal TotalPaidAmount { get; set; }             // Total money paid so far (principal + interest)
    public int EmisPaid { get; set; }                        // Count of EMIs paid so far

    public DateTime StartDate { get; set; }                  // Loan start date
    public DateTime NextDueDate { get; set; }                // Next EMI due date
    public string Status { get; set; } = "Active";           // "Active", "Closed", "Paused"

    public string? Colour { get; set; }                      // Hex color e.g. "#f43f5e"
    public string? Icon { get; set; }                        // Emoji e.g. "🏠", "🚗", "📱"
    public string? Note { get; set; }                        // Account number / reference

    // Profile Owner (null = Family-wide shared loan, or specific profile's loan)
    public Guid? MemberGlobalId { get; set; }

    // Linked Bank Account (where EMI is debited from)
    public Guid? AccountGlobalId { get; set; }
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    // Repayments & Part-Payments
    public ICollection<LoanPayment> Payments { get; set; } = [];
}
