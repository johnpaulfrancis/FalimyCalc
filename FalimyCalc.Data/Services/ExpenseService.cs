using FalimyCalc.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FalimyCalc.Data.Services;

public class ExpenseService
{
    private readonly AppDbContext _db;

    public ExpenseService(AppDbContext db)
    {
        _db = db;
    }

    // ── Categories ─────────────────────────────────────────────────────────

    public async Task<List<Category>> GetCategoriesAsync()
    {
        return await _db.Categories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryAsync(Guid globalId)
    {
        return await _db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == globalId && !c.IsDeleted);
    }

    public async Task<Category> CreateCategoryAsync(Category category)
    {
        category.GlobalId = Guid.NewGuid();
        category.CreatedAt = DateTime.UtcNow;
        category.ModifiedAt = DateTime.UtcNow;
        category.DeviceId = "web";

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<Category?> UpdateCategoryAsync(Category updated)
    {
        var existing = await _db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == updated.GlobalId);

        if (existing is null) return null;

        existing.Name = updated.Name;
        existing.Icon = updated.Icon;
        existing.Colour = updated.Colour;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.DeviceId = "web";

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteCategoryAsync(Guid globalId)
    {
        var existing = await _db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == globalId);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Expenses ───────────────────────────────────────────────────────────

    public async Task<(List<Expense> Items, int TotalCount)> GetExpensesAsync(
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        Guid? categoryGlobalId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var query = _db.Expenses
            .Include(e => e.Category)
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(e => e.Description.Contains(searchTerm));

        if (categoryGlobalId.HasValue)
            query = query.Where(e => e.CategoryGlobalId == categoryGlobalId.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.TransactionDate <= toDate.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Expense?> GetExpenseAsync(Guid globalId)
    {
        return await _db.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.GlobalId == globalId && !e.IsDeleted);
    }

    public async Task<Expense> CreateExpenseAsync(Expense expense)
    {
        expense.GlobalId = Guid.NewGuid();
        expense.CreatedAt = DateTime.UtcNow;
        expense.ModifiedAt = DateTime.UtcNow;
        expense.DeviceId = "web";

        // Resolve CategoryId from CategoryGlobalId
        if (expense.CategoryGlobalId.HasValue)
        {
            var cat = await _db.Categories
                .FirstOrDefaultAsync(c => c.GlobalId == expense.CategoryGlobalId.Value);
            expense.CategoryId = cat?.Id;
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return expense;
    }

    public async Task<Expense?> UpdateExpenseAsync(Expense updated)
    {
        var existing = await _db.Expenses
            .FirstOrDefaultAsync(e => e.GlobalId == updated.GlobalId);

        if (existing is null) return null;

        existing.Amount = updated.Amount;
        existing.Description = updated.Description;
        existing.TransactionDate = updated.TransactionDate;
        existing.CategoryGlobalId = updated.CategoryGlobalId;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.DeviceId = "web";

        // Resolve CategoryId from CategoryGlobalId
        if (updated.CategoryGlobalId.HasValue)
        {
            var cat = await _db.Categories
                .FirstOrDefaultAsync(c => c.GlobalId == updated.CategoryGlobalId.Value);
            existing.CategoryId = cat?.Id;
        }
        else
        {
            existing.CategoryId = null;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteExpenseAsync(Guid globalId)
    {
        var existing = await _db.Expenses
            .FirstOrDefaultAsync(e => e.GlobalId == globalId);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Summary ────────────────────────────────────────────────────────────

    public async Task<decimal> GetMonthlyTotalAsync(int year, int month)
    {
        return await _db.Expenses
            .Where(e => !e.IsDeleted
                     && e.TransactionDate.Year == year
                     && e.TransactionDate.Month == month)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;
    }

    public async Task<Dictionary<string, decimal>> GetMonthlyTotalsByCategoryAsync(int year, int month)
    {
        return await _db.Expenses
            .Include(e => e.Category)
            .Where(e => !e.IsDeleted
                     && e.TransactionDate.Year == year
                     && e.TransactionDate.Month == month)
            .GroupBy(e => e.Category != null ? e.Category.Name : "Uncategorised")
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Category, x => x.Total);
    }
}
