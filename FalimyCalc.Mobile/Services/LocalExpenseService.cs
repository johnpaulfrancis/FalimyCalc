using FalimyCalc.Mobile.Database;
using FalimyCalc.Mobile.Database.Entities;
using FalimyCalc.Shared.Enums;
using SQLite;

namespace FalimyCalc.Mobile.Services;

/// <summary>
/// Local CRUD service for the mobile app.
/// All operations work against SQLite — no network required.
/// SyncStatus is set automatically so the sync client knows what to push.
/// </summary>
public class LocalExpenseService
{
    private readonly LocalDatabase _localDb;
    private string? _deviceId;

    public LocalExpenseService(LocalDatabase localDb)
    {
        _localDb = localDb;
    }

    private async Task<SQLiteAsyncConnection> DbAsync() =>
        await _localDb.GetConnectionAsync();

    // ── Device ID ──────────────────────────────────────────────────────────

    public async Task<string> GetDeviceIdAsync()
    {
        if (_deviceId is not null) return _deviceId;

        var db = await DbAsync();
        var meta = await db.FindAsync<SyncMetadata>(SyncMetadata.DeviceIdKey);

        if (meta is null)
        {
            _deviceId = Guid.NewGuid().ToString("N");
            await db.InsertAsync(new SyncMetadata
            {
                Key = SyncMetadata.DeviceIdKey,
                Value = _deviceId
            });
        }
        else
        {
            _deviceId = meta.Value;
        }

        return _deviceId;
    }

    // ── Categories ─────────────────────────────────────────────────────────

    public async Task<List<LocalCategory>> GetCategoriesAsync()
    {
        var db = await DbAsync();
        return await db.Table<LocalCategory>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<LocalCategory> CreateCategoryAsync(LocalCategory category)
    {
        var db = await DbAsync();
        category.GlobalId = Guid.NewGuid();
        category.CreatedAt = DateTime.UtcNow;
        category.ModifiedAt = DateTime.UtcNow;
        category.DeviceId = await GetDeviceIdAsync();
        category.SyncStatus = SyncStatus.PendingCreate;
        await db.InsertAsync(category);
        return category;
    }

    public async Task UpdateCategoryAsync(LocalCategory category)
    {
        var db = await DbAsync();
        category.ModifiedAt = DateTime.UtcNow;
        category.SyncStatus = SyncStatus.PendingUpdate;
        await db.UpdateAsync(category);
    }

    public async Task DeleteCategoryAsync(LocalCategory category)
    {
        var db = await DbAsync();
        category.IsDeleted = true;
        category.ModifiedAt = DateTime.UtcNow;
        category.SyncStatus = SyncStatus.PendingDelete;
        await db.UpdateAsync(category);
    }

    // ── Expenses ───────────────────────────────────────────────────────────

    public async Task<List<LocalExpense>> GetExpensesAsync(
        string? searchTerm = null,
        Guid? categoryGlobalId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var db = await DbAsync();
        var query = db.Table<LocalExpense>().Where(e => !e.IsDeleted);

        // sqlite-net-pcl supports basic Where chaining
        if (categoryGlobalId.HasValue)
            query = query.Where(e => e.CategoryGlobalId == categoryGlobalId.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.TransactionDate <= toDate.Value);

        var results = await query
            .OrderByDescending(e => e.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Apply search in-memory (sqlite-net-pcl has limited LIKE support)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            results = results
                .Where(e => e.Description.ToLowerInvariant().Contains(term))
                .ToList();
        }

        return results;
    }

    public async Task<LocalExpense?> GetExpenseAsync(Guid globalId)
    {
        var db = await DbAsync();
        return await db.Table<LocalExpense>()
            .FirstOrDefaultAsync(e => e.GlobalId == globalId && !e.IsDeleted);
    }

    public async Task<LocalExpense> CreateExpenseAsync(LocalExpense expense)
    {
        var db = await DbAsync();
        expense.GlobalId = Guid.NewGuid();
        expense.CreatedAt = DateTime.UtcNow;
        expense.ModifiedAt = DateTime.UtcNow;
        expense.DeviceId = await GetDeviceIdAsync();
        expense.SyncStatus = SyncStatus.PendingCreate;
        await db.InsertAsync(expense);
        return expense;
    }

    public async Task UpdateExpenseAsync(LocalExpense expense)
    {
        var db = await DbAsync();
        expense.ModifiedAt = DateTime.UtcNow;

        // Don't downgrade PendingCreate to PendingUpdate —
        // if it was never synced, still treat it as a new record for the server
        if (expense.SyncStatus != SyncStatus.PendingCreate)
            expense.SyncStatus = SyncStatus.PendingUpdate;

        await db.UpdateAsync(expense);
    }

    public async Task DeleteExpenseAsync(LocalExpense expense)
    {
        var db = await DbAsync();
        expense.IsDeleted = true;
        expense.ModifiedAt = DateTime.UtcNow;

        // If never synced, safe to hard-delete locally — server never knew about it
        if (expense.SyncStatus == SyncStatus.PendingCreate)
            await db.DeleteAsync(expense);
        else
        {
            expense.SyncStatus = SyncStatus.PendingDelete;
            await db.UpdateAsync(expense);
        }
    }

    // ── Monthly summary ────────────────────────────────────────────────────

    public async Task<decimal> GetMonthlyTotalAsync(int year, int month)
    {
        var db = await DbAsync();
        var expenses = await db.Table<LocalExpense>()
            .Where(e => !e.IsDeleted)
            .ToListAsync();

        return expenses
            .Where(e => e.TransactionDate.Year == year && e.TransactionDate.Month == month)
            .Sum(e => e.Amount);
    }

    public async Task<List<LocalExpense>> GetRecentExpensesAsync(int count = 5)
    {
        var db = await DbAsync();
        return await db.Table<LocalExpense>()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.TransactionDate)
            .Take(count)
            .ToListAsync();
    }

    // ── Pending Transactions ───────────────────────────────────────────────

    public async Task<List<LocalPendingTransaction>> GetPendingTransactionsAsync()
    {
        var db = await DbAsync();
        return await db.Table<LocalPendingTransaction>()
            .Where(p => !p.IsDeleted && p.Status == PendingTransactionStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<LocalPendingTransaction> CreatePendingTransactionAsync(
        LocalPendingTransaction pending)
    {
        var db = await DbAsync();
        pending.GlobalId = Guid.NewGuid();
        pending.CreatedAt = DateTime.UtcNow;
        pending.ModifiedAt = DateTime.UtcNow;
        pending.DeviceId = await GetDeviceIdAsync();
        pending.SyncStatus = SyncStatus.PendingCreate;
        await db.InsertAsync(pending);
        return pending;
    }

    public async Task ApprovePendingTransactionAsync(
        LocalPendingTransaction pending,
        LocalExpense createdExpense)
    {
        var db = await DbAsync();

        // Create the expense
        await CreateExpenseAsync(createdExpense);

        // Mark pending as approved
        pending.Status = PendingTransactionStatus.Approved;
        pending.ApprovedAsExpenseGlobalId = createdExpense.GlobalId;
        pending.ModifiedAt = DateTime.UtcNow;
        pending.SyncStatus = SyncStatus.PendingUpdate;
        await db.UpdateAsync(pending);
    }

    public async Task RejectPendingTransactionAsync(LocalPendingTransaction pending)
    {
        var db = await DbAsync();
        pending.Status = PendingTransactionStatus.Rejected;
        pending.ModifiedAt = DateTime.UtcNow;
        pending.SyncStatus = SyncStatus.PendingUpdate;
        await db.UpdateAsync(pending);
    }

    // ── Sync helpers ───────────────────────────────────────────────────────

    /// <summary>Returns all records that need to be pushed to the server.</summary>
    public async Task<(List<LocalExpense> Expenses,
                       List<LocalCategory> Categories,
                       List<LocalPendingTransaction> PendingTransactions)>
        GetPendingSyncItemsAsync()
    {
        var db = await DbAsync();

        var expenses = await db.Table<LocalExpense>()
            .Where(e => e.SyncStatus != SyncStatus.Synced)
            .ToListAsync();

        var categories = await db.Table<LocalCategory>()
            .Where(c => c.SyncStatus != SyncStatus.Synced)
            .ToListAsync();

        var pending = await db.Table<LocalPendingTransaction>()
            .Where(p => p.SyncStatus != SyncStatus.Synced)
            .ToListAsync();

        return (expenses, categories, pending);
    }

    /// <summary>
    /// Marks the given records as Synced after a successful push.
    /// </summary>
    public async Task MarkAsSyncedAsync(
        IEnumerable<Guid> expenseIds,
        IEnumerable<Guid> categoryIds,
        IEnumerable<Guid> pendingIds)
    {
        var db = await DbAsync();

        foreach (var id in expenseIds)
        {
            var e = await db.Table<LocalExpense>()
                .FirstOrDefaultAsync(x => x.GlobalId == id);
            if (e is not null)
            {
                // Hard-delete locally if it was pending delete
                if (e.SyncStatus == SyncStatus.PendingDelete)
                    await db.DeleteAsync(e);
                else
                {
                    e.SyncStatus = SyncStatus.Synced;
                    await db.UpdateAsync(e);
                }
            }
        }

        foreach (var id in categoryIds)
        {
            var c = await db.Table<LocalCategory>()
                .FirstOrDefaultAsync(x => x.GlobalId == id);
            if (c is not null)
            {
                if (c.SyncStatus == SyncStatus.PendingDelete)
                    await db.DeleteAsync(c);
                else
                {
                    c.SyncStatus = SyncStatus.Synced;
                    await db.UpdateAsync(c);
                }
            }
        }

        foreach (var id in pendingIds)
        {
            var p = await db.Table<LocalPendingTransaction>()
                .FirstOrDefaultAsync(x => x.GlobalId == id);
            if (p is not null)
            {
                p.SyncStatus = SyncStatus.Synced;
                await db.UpdateAsync(p);
            }
        }
    }

    /// <summary>Gets the last RowVersion successfully pulled from the server.</summary>
    public async Task<long> GetLastSyncedRowVersionAsync()
    {
        var db = await DbAsync();
        var meta = await db.FindAsync<SyncMetadata>(SyncMetadata.LastSyncedRowVersionKey);
        return meta is null ? 0 : long.Parse(meta.Value);
    }

    /// <summary>Stores the RowVersion after a successful pull.</summary>
    public async Task SetLastSyncedRowVersionAsync(long rowVersion)
    {
        var db = await DbAsync();
        var existing = await db.FindAsync<SyncMetadata>(SyncMetadata.LastSyncedRowVersionKey);
        if (existing is null)
            await db.InsertAsync(new SyncMetadata
            {
                Key = SyncMetadata.LastSyncedRowVersionKey,
                Value = rowVersion.ToString()
            });
        else
        {
            existing.Value = rowVersion.ToString();
            await db.UpdateAsync(existing);
        }
    }
}
