namespace FalimyCalc.Data.Entities;

public class LoanPayment : SyncableEntity
{
    public Guid LoanGlobalId { get; set; }
    public int? LoanId { get; set; }
    public Loan? Loan { get; set; }

    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }

    public decimal PrincipalComponent { get; set; }   // Part reducing outstanding principal
    public decimal InterestComponent { get; set; }    // Part going toward interest

    public string PaymentType { get; set; } = "EMI";   // "EMI", "Prepayment", "Down Payment", "Foreclosure"

    public Guid? AccountGlobalId { get; set; }
    public string? AccountName { get; set; }
    public string? Note { get; set; }
}
