using FalimyCalc.Data.Entities;
using FalimyCalc.Shared.DTOs;
using FalimyCalc.Shared.Sync;
using Microsoft.EntityFrameworkCore;

namespace FalimyCalc.Data.Services;

public class SyncService
{
    private readonly AppDbContext _db;

    public SyncService(AppDbContext db)
    {
        _db = db;
    }

    // ── Pull: server → phone ───────────────────────────────────────────────
    // Returns all records with RowVersion > sinceRowVersion.
    // The phone sends its last known RowVersion; we return only what changed.

    public async Task<SyncPullResponse> PullAsync(long sinceRowVersion, string deviceId)
    {
        try
        {
            var expenses = await _db.Expenses
                .Where(e => e.RowVersion > sinceRowVersion)
                .ToListAsync();

            var categories = await _db.Categories
                .Where(c => c.RowVersion > sinceRowVersion)
                .ToListAsync();

            var pending = await _db.PendingTransactions
                .Where(p => p.RowVersion > sinceRowVersion)
                .ToListAsync();

            // Find the highest RowVersion across all returned records
            long newRowVersion = sinceRowVersion;
            if (expenses.Any()) newRowVersion = Math.Max(newRowVersion, expenses.Max(e => e.RowVersion));
            if (categories.Any()) newRowVersion = Math.Max(newRowVersion, categories.Max(c => c.RowVersion));
            if (pending.Any()) newRowVersion = Math.Max(newRowVersion, pending.Max(p => p.RowVersion));

            return new SyncPullResponse
            {
                Success = true,
                ServerRowVersion = newRowVersion,
                Expenses = expenses.Select(MapToDto).ToList(),
                Categories = categories.Select(MapToDto).ToList(),
                PendingTransactions = pending.Select(MapToDto).ToList()
            };
        }
        catch (Exception ex)
        {
            return new SyncPullResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ── Push: phone → server ───────────────────────────────────────────────
    // Applies Last-Write-Wins using ModifiedAt.
    // If the server record is newer, we keep it — phone change is silently ignored.
    // The phone will pull the server version on its next sync.

    public async Task<SyncPushResponse> PushAsync(SyncPushRequest request)
    {
        var acceptedExpenses = new List<Guid>();
        var acceptedCategories = new List<Guid>();
        var acceptedPending = new List<Guid>();

        try
        {
            // ── Categories first (expenses may reference them) ──────────────
            foreach (var dto in request.Categories)
            {
                var existing = await _db.Categories
                    .FirstOrDefaultAsync(c => c.GlobalId == dto.GlobalId);

                if (existing is null)
                {
                    // New record — insert it
                    _db.Categories.Add(MapFromDto(dto));
                    acceptedCategories.Add(dto.GlobalId);
                }
                else if (dto.ModifiedAt > existing.ModifiedAt)
                {
                    // Phone version is newer — update server record
                    existing.Name = dto.Name;
                    existing.Icon = dto.Icon;
                    existing.Colour = dto.Colour;
                    existing.IsDeleted = dto.IsDeleted;
                    existing.ModifiedAt = dto.ModifiedAt;
                    existing.DeviceId = dto.DeviceId;
                    acceptedCategories.Add(dto.GlobalId);
                }
                // else: server version is newer — ignore phone change
            }

            // Save categories before processing expenses so FK lookups work
            if (acceptedCategories.Any())
                await _db.SaveChangesAsync();

            // ── Expenses ────────────────────────────────────────────────────
            foreach (var dto in request.Expenses)
            {
                var existing = await _db.Expenses
                    .FirstOrDefaultAsync(e => e.GlobalId == dto.GlobalId);

                if (existing is null)
                {
                    var expense = MapFromDto(dto);
                    // Resolve CategoryId from CategoryGlobalId
                    expense.CategoryId = await ResolveCategoryIdAsync(dto.CategoryGlobalId);
                    _db.Expenses.Add(expense);
                    acceptedExpenses.Add(dto.GlobalId);
                }
                else if (dto.ModifiedAt > existing.ModifiedAt)
                {
                    existing.Amount = dto.Amount;
                    existing.Description = dto.Description;
                    existing.TransactionDate = dto.TransactionDate;
                    existing.CategoryGlobalId = dto.CategoryGlobalId;
                    existing.CategoryId = await ResolveCategoryIdAsync(dto.CategoryGlobalId);
                    existing.IsDeleted = dto.IsDeleted;
                    existing.ModifiedAt = dto.ModifiedAt;
                    existing.DeviceId = dto.DeviceId;
                    acceptedExpenses.Add(dto.GlobalId);
                }
            }

            // ── Pending Transactions ────────────────────────────────────────
            foreach (var dto in request.PendingTransactions)
            {
                var existing = await _db.PendingTransactions
                    .FirstOrDefaultAsync(p => p.GlobalId == dto.GlobalId);

                if (existing is null)
                {
                    _db.PendingTransactions.Add(MapFromDto(dto));
                    acceptedPending.Add(dto.GlobalId);
                }
                else if (dto.ModifiedAt > existing.ModifiedAt)
                {
                    existing.Status = dto.Status;
                    existing.DetectedAmount = dto.DetectedAmount;
                    existing.DetectedMerchant = dto.DetectedMerchant;
                    existing.DetectedBank = dto.DetectedBank;
                    existing.DetectedAccount = dto.DetectedAccount;
                    existing.DetectedType = dto.DetectedType;
                    existing.DetectedDate = dto.DetectedDate;
                    existing.ApprovedAsExpenseGlobalId = dto.ApprovedAsExpenseGlobalId;
                    existing.IsDeleted = dto.IsDeleted;
                    existing.ModifiedAt = dto.ModifiedAt;
                    existing.DeviceId = dto.DeviceId;
                    acceptedPending.Add(dto.GlobalId);
                }
            }

            await _db.SaveChangesAsync();

            // Return the current max RowVersion so the phone can start its
            // pull from here immediately after a successful push
            long newRowVersion = GetCurrentMaxRowVersion();

            return new SyncPushResponse
            {
                Success = true,
                NewServerRowVersion = newRowVersion,
                AcceptedExpenseIds = acceptedExpenses,
                AcceptedCategoryIds = acceptedCategories,
                AcceptedPendingTransactionIds = acceptedPending
            };
        }
        catch (Exception ex)
        {
            return new SyncPushResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<int?> ResolveCategoryIdAsync(Guid? categoryGlobalId)
    {
        if (!categoryGlobalId.HasValue) return null;
        var cat = await _db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == categoryGlobalId.Value);
        return cat?.Id;
    }

    private long GetCurrentMaxRowVersion()
    {
        long max = 0;
        if (_db.Expenses.Any()) max = Math.Max(max, _db.Expenses.Max(e => e.RowVersion));
        if (_db.Categories.Any()) max = Math.Max(max, _db.Categories.Max(c => c.RowVersion));
        if (_db.PendingTransactions.Any()) max = Math.Max(max, _db.PendingTransactions.Max(p => p.RowVersion));
        return max;
    }

    // ── Mappers: Entity → DTO ───────────────────────────────────────────────

    private static ExpenseDto MapToDto(Expense e) => new()
    {
        GlobalId = e.GlobalId,
        Amount = e.Amount,
        Description = e.Description,
        TransactionDate = e.TransactionDate,
        CategoryGlobalId = e.CategoryGlobalId,
        DeviceId = e.DeviceId,
        CreatedAt = e.CreatedAt,
        ModifiedAt = e.ModifiedAt,
        IsDeleted = e.IsDeleted
    };

    private static CategoryDto MapToDto(Category c) => new()
    {
        GlobalId = c.GlobalId,
        Name = c.Name,
        Icon = c.Icon,
        Colour = c.Colour,
        DeviceId = c.DeviceId,
        CreatedAt = c.CreatedAt,
        ModifiedAt = c.ModifiedAt,
        IsDeleted = c.IsDeleted
    };

    private static FalimyCalc.Shared.DTOs.PendingTransactionDto MapToDto(PendingTransaction p) => new()
    {
        GlobalId = p.GlobalId,
        RawSmsBody = p.RawSmsBody,
        DetectedAmount = p.DetectedAmount,
        DetectedMerchant = p.DetectedMerchant,
        DetectedBank = p.DetectedBank,
        DetectedAccount = p.DetectedAccount,
        DetectedType = p.DetectedType,
        DetectedDate = p.DetectedDate,
        Status = p.Status,
        ApprovedAsExpenseGlobalId = p.ApprovedAsExpenseGlobalId,
        DeviceId = p.DeviceId,
        CreatedAt = p.CreatedAt,
        ModifiedAt = p.ModifiedAt,
        IsDeleted = p.IsDeleted
    };

    // ── Mappers: DTO → Entity ───────────────────────────────────────────────

    private static Expense MapFromDto(ExpenseDto dto) => new()
    {
        GlobalId = dto.GlobalId,
        Amount = dto.Amount,
        Description = dto.Description,
        TransactionDate = dto.TransactionDate,
        CategoryGlobalId = dto.CategoryGlobalId,
        DeviceId = dto.DeviceId,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        IsDeleted = dto.IsDeleted
    };

    private static Category MapFromDto(CategoryDto dto) => new()
    {
        GlobalId = dto.GlobalId,
        Name = dto.Name,
        Icon = dto.Icon,
        Colour = dto.Colour,
        DeviceId = dto.DeviceId,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        IsDeleted = dto.IsDeleted
    };

    private static PendingTransaction MapFromDto(FalimyCalc.Shared.DTOs.PendingTransactionDto dto) => new()
    {
        GlobalId = dto.GlobalId,
        RawSmsBody = dto.RawSmsBody,
        DetectedAmount = dto.DetectedAmount,
        DetectedMerchant = dto.DetectedMerchant,
        DetectedBank = dto.DetectedBank,
        DetectedAccount = dto.DetectedAccount,
        DetectedType = dto.DetectedType,
        DetectedDate = dto.DetectedDate,
        Status = dto.Status,
        ApprovedAsExpenseGlobalId = dto.ApprovedAsExpenseGlobalId,
        DeviceId = dto.DeviceId,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        IsDeleted = dto.IsDeleted
    };
}
