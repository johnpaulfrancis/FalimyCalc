namespace FalimyCalc.Shared.Enums;

public enum PendingTransactionStatus
{
    Pending = 0,    // Detected from SMS, awaiting user review
    Approved = 1,   // User approved — converted to Expense
    Rejected = 2    // User rejected — discarded
}
