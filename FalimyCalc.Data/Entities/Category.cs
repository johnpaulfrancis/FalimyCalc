namespace FalimyCalc.Data.Entities;

public class Category : SyncableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Colour { get; set; }
    public decimal? MonthlyBudget { get; set; }

    // Navigation
    public ICollection<Expense> Expenses { get; set; } = [];
}
