namespace FalimyCalc.Shared.DTOs;

/// <summary>
/// Represents an expense record transferred between mobile and server.
/// This is the sync payload — not a UI model.
/// </summary>
public class ExpenseDto
{
    public Guid GlobalId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Guid? CategoryGlobalId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
}
