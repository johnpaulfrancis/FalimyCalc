using FalimyCalc.Data.Entities;
using FalimyCalc.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FalimyCalc.Data.Services;

public class ExpenseService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ExpenseService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // ── Categories ─────────────────────────────────────────────────────────

    public async Task<List<Category>> GetCategoriesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Categories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == globalId && !c.IsDeleted);
    }

    public async Task<Category> CreateCategoryAsync(Category category)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        category.GlobalId = Guid.NewGuid();
        category.CreatedAt = DateTime.UtcNow;
        category.ModifiedAt = DateTime.UtcNow;
        category.DeviceId = "web";

        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task<Category?> UpdateCategoryAsync(Category updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == updated.GlobalId);

        if (existing is null) return null;

        existing.Name = updated.Name;
        existing.Icon = updated.Icon;
        existing.Colour = updated.Colour;
        existing.MonthlyBudget = updated.MonthlyBudget;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.DeviceId = "web";

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteCategoryAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Categories
            .FirstOrDefaultAsync(c => c.GlobalId == globalId);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    // ── Expenses ───────────────────────────────────────────────────────────

    public async Task<(List<Expense> Items, int TotalCount, decimal TotalAmount)> GetExpensesAsync(
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        Guid? categoryGlobalId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? memberGlobalId = null,
        string? sortBy = "date_desc",   // "date_desc", "date_asc", "amount_desc", "amount_asc", "desc_asc"
        Guid? accountGlobalId = null,
        string? transactionType = null) // "all", "expense", "income", "transfer"
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Expenses
            .Include(e => e.Category)
            .Include(e => e.BankAccount)
            .Where(e => !e.IsDeleted);

        if (memberGlobalId.HasValue)
            query = query.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        if (accountGlobalId.HasValue)
            query = query.Where(e => e.AccountGlobalId == accountGlobalId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(e => e.Description.Contains(searchTerm) || (e.Note != null && e.Note.Contains(searchTerm)) || (e.Tags != null && e.Tags.Contains(searchTerm)) || (e.Account != null && e.Account.Contains(searchTerm)));

        if (categoryGlobalId.HasValue)
            query = query.Where(e => e.CategoryGlobalId == categoryGlobalId.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.TransactionDate <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(transactionType) && transactionType != "all")
        {
            if (transactionType == "income")
            {
                query = query.Where(e => e.Amount < 0 ||
                    (e.Tags != null && (e.Tags.Contains("income") || e.Tags.Contains("salary"))) ||
                    (e.Subcategory != null && (e.Subcategory.Contains("income") || e.Subcategory.Contains("salary"))) ||
                    (e.Category != null && (e.Category.Name.Contains("income") || e.Category.Name.Contains("salary"))));
            }
            else if (transactionType == "transfer")
            {
                query = query.Where(e => (e.Tags != null && e.Tags.Contains("transfer")) ||
                                         (e.Subcategory != null && e.Subcategory == "Transfer"));
            }
            else if (transactionType == "expense")
            {
                query = query.Where(e => e.Amount >= 0 &&
                    (e.Tags == null || (!e.Tags.Contains("income") && !e.Tags.Contains("salary") && !e.Tags.Contains("transfer"))) &&
                    (e.Subcategory == null || (!e.Subcategory.Contains("income") && !e.Subcategory.Contains("salary") && e.Subcategory != "Transfer")) &&
                    (e.Category == null || (!e.Category.Name.Contains("income") && !e.Category.Name.Contains("salary"))));
            }
        }

        var total = await query.CountAsync();
        var totalAmount = await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;

        query = sortBy switch
        {
            "date_asc" => query.OrderBy(e => e.TransactionDate),
            "amount_desc" => query.OrderByDescending(e => e.Amount),
            "amount_asc" => query.OrderBy(e => e.Amount),
            "desc_asc" => query.OrderBy(e => e.Description),
            _ => query.OrderByDescending(e => e.TransactionDate)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total, totalAmount);
    }

    public async Task<List<Expense>> GetAllExpensesAsync(
        string? searchTerm = null,
        Guid? categoryGlobalId = null,
        Guid? accountGlobalId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Expenses
            .Include(e => e.Category)
            .Include(e => e.BankAccount)
            .Where(e => !e.IsDeleted);

        if (memberGlobalId.HasValue)
            query = query.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(e => e.Description.Contains(searchTerm));

        if (categoryGlobalId.HasValue)
            query = query.Where(e => e.CategoryGlobalId == categoryGlobalId.Value);

        if (accountGlobalId.HasValue)
            query = query.Where(e => e.AccountGlobalId == accountGlobalId.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.TransactionDate <= toDate.Value);

        return await query
            .OrderByDescending(e => e.TransactionDate)
            .ToListAsync();
    }

    public async Task<Expense?> GetExpenseAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Expenses
            .Include(e => e.Category)
            .Include(e => e.BankAccount)
            .FirstOrDefaultAsync(e => e.GlobalId == globalId && !e.IsDeleted);
    }

    public async Task<Expense> CreateExpenseAsync(Expense expense, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        expense.GlobalId = Guid.NewGuid();
        expense.CreatedAt = DateTime.UtcNow;
        expense.ModifiedAt = DateTime.UtcNow;
        expense.DeviceId = "web";

        // Stamp which member added this expense
        if (memberGlobalId.HasValue)
            expense.MemberGlobalId = memberGlobalId.Value;

        // Resolve CategoryId from CategoryGlobalId
        if (expense.CategoryGlobalId.HasValue)
        {
            var cat = await db.Categories
                .FirstOrDefaultAsync(c => c.GlobalId == expense.CategoryGlobalId.Value);
            expense.CategoryId = cat?.Id;
        }

        // Resolve BankAccountId from AccountGlobalId
        if (expense.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts
                .FirstOrDefaultAsync(a => a.GlobalId == expense.AccountGlobalId.Value && !a.IsDeleted);
            if (acc != null)
            {
                expense.BankAccountId = acc.Id;
                expense.Account = acc.AccountName;
            }
        }
        else if (!string.IsNullOrWhiteSpace(expense.Account) && expense.MemberGlobalId.HasValue)
        {
            // Fallback match by name for this member
            var acc = await db.BankAccounts
                .FirstOrDefaultAsync(a => a.MemberGlobalId == expense.MemberGlobalId.Value && a.AccountName == expense.Account.Trim() && !a.IsDeleted);
            if (acc != null)
            {
                expense.AccountGlobalId = acc.GlobalId;
                expense.BankAccountId = acc.Id;
            }
        }

        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return expense;
    }

    public async Task<Expense?> UpdateExpenseAsync(Expense updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Expenses
            .FirstOrDefaultAsync(e => e.GlobalId == updated.GlobalId);

        if (existing is null) return null;

        existing.Amount = updated.Amount;
        existing.Description = updated.Description;
        existing.TransactionDate = updated.TransactionDate;
        existing.CategoryGlobalId = updated.CategoryGlobalId;
        existing.Note = updated.Note;
        existing.Account = updated.Account;
        existing.AccountGlobalId = updated.AccountGlobalId;
        existing.Currency = string.IsNullOrWhiteSpace(updated.Currency) ? "INR" : updated.Currency;
        existing.Subcategory = updated.Subcategory;
        existing.Tags = updated.Tags;
        if (updated.MemberGlobalId.HasValue)
            existing.MemberGlobalId = updated.MemberGlobalId;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.DeviceId = "web";

        // Resolve CategoryId from CategoryGlobalId
        if (updated.CategoryGlobalId.HasValue)
        {
            var cat = await db.Categories
                .FirstOrDefaultAsync(c => c.GlobalId == updated.CategoryGlobalId.Value);
            existing.CategoryId = cat?.Id;
        }
        else
        {
            existing.CategoryId = null;
        }

        // Resolve BankAccountId from AccountGlobalId
        if (updated.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts
                .FirstOrDefaultAsync(a => a.GlobalId == updated.AccountGlobalId.Value && !a.IsDeleted);
            if (acc != null)
            {
                existing.BankAccountId = acc.Id;
                existing.Account = acc.AccountName;
            }
            else
            {
                existing.BankAccountId = null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(updated.Account) && existing.MemberGlobalId.HasValue)
        {
            var acc = await db.BankAccounts
                .FirstOrDefaultAsync(a => a.MemberGlobalId == existing.MemberGlobalId.Value && a.AccountName == updated.Account.Trim() && !a.IsDeleted);
            if (acc != null)
            {
                existing.AccountGlobalId = acc.GlobalId;
                existing.BankAccountId = acc.Id;
            }
            else
            {
                existing.BankAccountId = null;
            }
        }
        else
        {
            existing.BankAccountId = null;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteExpenseAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Expenses
            .FirstOrDefaultAsync(e => e.GlobalId == globalId);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;

        // If part of a transfer pair (has ref:xxxx tag), delete the linked peer transaction too
        if (!string.IsNullOrEmpty(existing.Tags) && existing.Tags.Contains("ref:"))
        {
            var parts = existing.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var refTag = parts.FirstOrDefault(p => p.StartsWith("ref:"));
            if (!string.IsNullOrEmpty(refTag))
            {
                var peer = await db.Expenses.FirstOrDefaultAsync(e => e.GlobalId != globalId && !e.IsDeleted && e.Tags != null && e.Tags.Contains(refTag));
                if (peer != null)
                {
                    peer.IsDeleted = true;
                    peer.ModifiedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();
        return true;
    }

    // ── Transaction Classification Helpers ────────────────────────────────────

    public static bool IsIncomeTransaction(Expense e)
    {
        if (e.Amount < 0) return true;
        var tags = e.Tags ?? "";
        if (tags.Contains("transfer-in", StringComparison.OrdinalIgnoreCase)) return false;
        var sub = e.Subcategory ?? "";
        var cat = e.Category?.Name ?? "";
        return tags.Contains("income", StringComparison.OrdinalIgnoreCase) ||
               tags.Contains("salary", StringComparison.OrdinalIgnoreCase) ||
               sub.Contains("income", StringComparison.OrdinalIgnoreCase) ||
               sub.Contains("salary", StringComparison.OrdinalIgnoreCase) ||
               cat.Contains("income", StringComparison.OrdinalIgnoreCase) ||
               cat.Contains("salary", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTransferTransaction(Expense e)
    {
        var tags = e.Tags ?? "";
        var sub = e.Subcategory ?? "";
        return tags.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sub, "Transfer", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTransferOutTransaction(Expense e)
    {
        var tags = e.Tags ?? "";
        return tags.Contains("transfer-out", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTransferInTransaction(Expense e)
    {
        var tags = e.Tags ?? "";
        return tags.Contains("transfer-in", StringComparison.OrdinalIgnoreCase);
    }

    // ── Bank Accounts & Transfers ────────────────────────────────────────────

    public record BankAccountWithBalance(
        BankAccount Account,
        FamilyMember? Owner,
        decimal InitialBalance,
        decimal TotalSpent,
        decimal CurrentBalance,
        int TransactionCount,
        decimal TotalReceived = 0m
    );

    public record TransferRequest(
        decimal Amount,
        Guid SourceAccountGlobalId,
        Guid DestinationAccountGlobalId,
        DateTime TransactionDate,
        string? Description = null,
        string? Note = null,
        Guid? MemberGlobalId = null
    );

    public async Task<(Expense Outflow, Expense Inflow)> CreateTransferAsync(TransferRequest request)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Transfer amount must be greater than zero.");

        if (request.SourceAccountGlobalId == request.DestinationAccountGlobalId)
            throw new ArgumentException("Source and Destination accounts must be different.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var sourceAcc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == request.SourceAccountGlobalId && !a.IsDeleted);
        if (sourceAcc == null)
            throw new InvalidOperationException("Source bank account not found.");

        var destAcc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == request.DestinationAccountGlobalId && !a.IsDeleted);
        if (destAcc == null)
            throw new InvalidOperationException("Destination bank account not found.");

        if (sourceAcc.MemberGlobalId != destAcc.MemberGlobalId)
            throw new InvalidOperationException("Transfers must be between bank accounts of the same profile.");

        var memberId = request.MemberGlobalId ?? sourceAcc.MemberGlobalId;
        var transferTag = $"ref:{Guid.NewGuid():N}"[..12];
        var descOut = string.IsNullOrWhiteSpace(request.Description)
            ? $"Transfer to {destAcc.AccountName}"
            : request.Description.Trim();
        var descIn = string.IsNullOrWhiteSpace(request.Description)
            ? $"Transfer from {sourceAcc.AccountName}"
            : request.Description.Trim();

        var outflow = new Expense
        {
            GlobalId = Guid.NewGuid(),
            Amount = Math.Abs(request.Amount),
            Description = descOut,
            TransactionDate = request.TransactionDate,
            Account = sourceAcc.AccountName,
            AccountGlobalId = sourceAcc.GlobalId,
            BankAccountId = sourceAcc.Id,
            MemberGlobalId = memberId,
            Subcategory = "Transfer",
            Tags = $"transfer,transfer-out,{transferTag}",
            Note = string.IsNullOrWhiteSpace(request.Note)
                ? $"Transferred to {destAcc.AccountName}"
                : $"{request.Note.Trim()} (To: {destAcc.AccountName})",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            DeviceId = "web",
            Currency = "INR"
        };

        var inflow = new Expense
        {
            GlobalId = Guid.NewGuid(),
            Amount = Math.Abs(request.Amount),
            Description = descIn,
            TransactionDate = request.TransactionDate,
            Account = destAcc.AccountName,
            AccountGlobalId = destAcc.GlobalId,
            BankAccountId = destAcc.Id,
            MemberGlobalId = memberId,
            Subcategory = "Transfer",
            Tags = $"transfer,transfer-in,{transferTag}",
            Note = string.IsNullOrWhiteSpace(request.Note)
                ? $"Transferred from {sourceAcc.AccountName}"
                : $"{request.Note.Trim()} (From: {sourceAcc.AccountName})",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            DeviceId = "web",
            Currency = "INR"
        };

        db.Expenses.Add(outflow);
        db.Expenses.Add(inflow);
        await db.SaveChangesAsync();

        return (outflow, inflow);
    }

    public async Task<List<BankAccount>> GetBankAccountsAsync(Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.BankAccounts.Where(a => !a.IsDeleted);

        if (memberGlobalId.HasValue)
            query = query.Where(a => a.MemberGlobalId == memberGlobalId.Value);

        return await query.OrderBy(a => a.AccountName).ToListAsync();
    }

    public async Task<BankAccount?> GetBankAccountAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BankAccounts
            .FirstOrDefaultAsync(a => a.GlobalId == globalId && !a.IsDeleted);
    }

    public async Task<BankAccount> CreateBankAccountAsync(BankAccount account, Guid memberGlobalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        account.GlobalId = Guid.NewGuid();
        account.MemberGlobalId = memberGlobalId;
        account.CreatedAt = DateTime.UtcNow;
        account.ModifiedAt = DateTime.UtcNow;
        account.DeviceId = "web";
        account.Colour = string.IsNullOrWhiteSpace(account.Colour) ? "#6366f1" : account.Colour;
        account.Icon = string.IsNullOrWhiteSpace(account.Icon) ? "🏦" : account.Icon;

        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    public async Task<BankAccount?> UpdateBankAccountAsync(BankAccount updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.GlobalId == updated.GlobalId && !a.IsDeleted);

        if (existing is null) return null;

        existing.AccountName = updated.AccountName.Trim();
        existing.InitialBalance = updated.InitialBalance;
        existing.BankName = updated.BankName?.Trim();
        existing.AccountType = updated.AccountType?.Trim();
        existing.AccountNumberSuffix = updated.AccountNumberSuffix?.Trim();
        existing.Colour = string.IsNullOrWhiteSpace(updated.Colour) ? "#6366f1" : updated.Colour;
        existing.Icon = string.IsNullOrWhiteSpace(updated.Icon) ? "🏦" : updated.Icon;
        if (updated.MemberGlobalId != Guid.Empty)
            existing.MemberGlobalId = updated.MemberGlobalId;
        existing.ModifiedAt = DateTime.UtcNow;

        // Also sync Account name on existing expenses linked to this account
        var expenses = await db.Expenses
            .Where(e => e.AccountGlobalId == existing.GlobalId)
            .ToListAsync();
        foreach (var exp in expenses)
        {
            exp.Account = existing.AccountName;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteBankAccountAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.GlobalId == globalId && !a.IsDeleted);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<BankAccountWithBalance>> GetBankAccountsWithBalancesAsync(Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var members = await db.FamilyMembers.Where(m => !m.IsDeleted).ToListAsync();
        var memberMap = members.ToDictionary(m => m.GlobalId);

        var accountsQuery = db.BankAccounts.Where(a => !a.IsDeleted);
        if (memberGlobalId.HasValue)
            accountsQuery = accountsQuery.Where(a => a.MemberGlobalId == memberGlobalId.Value);

        var accounts = await accountsQuery.OrderBy(a => a.AccountName).ToListAsync();

        var expensesQuery = db.Expenses.Where(e => !e.IsDeleted);
        if (memberGlobalId.HasValue)
            expensesQuery = expensesQuery.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        var expenses = await expensesQuery.ToListAsync();

        var results = new List<BankAccountWithBalance>();

        foreach (var acc in accounts)
        {
            var matchedExpenses = expenses
                .Where(e => e.AccountGlobalId == acc.GlobalId ||
                           (!e.AccountGlobalId.HasValue && !string.IsNullOrEmpty(e.Account) && string.Equals(e.Account, acc.AccountName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var totalSpent = matchedExpenses.Where(e => !IsIncomeTransaction(e) && !IsTransferInTransaction(e)).Sum(e => e.Amount);
            var totalReceived = matchedExpenses.Where(e => IsIncomeTransaction(e) || IsTransferInTransaction(e)).Sum(e => Math.Abs(e.Amount));
            var currentBal = acc.InitialBalance + totalReceived - totalSpent; // Can be negative!
            var member = memberMap.TryGetValue(acc.MemberGlobalId, out var m) ? m : null;

            results.Add(new BankAccountWithBalance(
                acc,
                member,
                acc.InitialBalance,
                totalSpent,
                currentBal,
                matchedExpenses.Count,
                totalReceived
            ));
        }

        return results;
    }

    // ── Financial KPIs & Balance Trend ───────────────────────────────────────

    public record BalanceTrendPoint(
        DateTime Date,
        string DateLabel,
        decimal Balance,
        decimal DailyExpense,
        decimal DailyIncome);

    public record FinancialKpiSummary(
        decimal TotalNetBalance,
        decimal TotalInitialBalance,
        int ActiveAccountsCount,
        int NegativeAccountsCount,
        decimal CurrentMonthExpense,
        decimal CurrentMonthIncome,
        decimal NetMonthlyCashFlow,
        double SavingsRate,
        decimal DailyBurnRate,
        int DaysInMonth,
        int DaysElapsed,
        decimal ProjectedMonthEndSpend,
        List<BalanceTrendPoint> BalanceHistory);

    public async Task<FinancialKpiSummary> GetFinancialKpiSummaryAsync(Guid? memberGlobalId = null, int days = 30)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var accountsQuery = db.BankAccounts.Where(a => !a.IsDeleted);
        if (memberGlobalId.HasValue)
            accountsQuery = accountsQuery.Where(a => a.MemberGlobalId == memberGlobalId.Value);

        var accounts = await accountsQuery.ToListAsync();
        var totalInitialBalance = accounts.Sum(a => a.InitialBalance);
        var activeAccountsCount = accounts.Count;

        var expensesQuery = db.Expenses
            .Include(e => e.Category)
            .Where(e => !e.IsDeleted);

        if (memberGlobalId.HasValue)
            expensesQuery = expensesQuery.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        var allExpenses = await expensesQuery.OrderBy(e => e.TransactionDate).ToListAsync();

        var totalSpentAllTime = allExpenses.Where(e => !IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => e.Amount);
        var totalIncomeAllTime = allExpenses.Where(e => IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => Math.Abs(e.Amount));
        var totalNetBalance = totalInitialBalance + totalIncomeAllTime - totalSpentAllTime;

        // Current Month Metrics (Internal transfers excluded from monthly spend / income)
        var now = DateTime.Now;
        var thisMonthExpensesList = allExpenses
            .Where(e => e.TransactionDate.Year == now.Year && e.TransactionDate.Month == now.Month)
            .ToList();

        var currentMonthExpense = thisMonthExpensesList.Where(e => !IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => e.Amount);
        var currentMonthIncome = thisMonthExpensesList.Where(e => IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => Math.Abs(e.Amount));
        var netMonthlyCashFlow = currentMonthIncome - currentMonthExpense;

        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var daysElapsed = Math.Max(1, now.Day);
        var dailyBurnRate = daysElapsed > 0 ? currentMonthExpense / daysElapsed : 0m;
        var projectedMonthEndSpend = dailyBurnRate * daysInMonth;
        var savingsRate = currentMonthIncome > 0 ? (double)(netMonthlyCashFlow / currentMonthIncome * 100m) : 0.0;

        // Count negative accounts based on live balances
        int negativeAccountsCount = 0;
        foreach (var acc in accounts)
        {
            var accExpenses = allExpenses.Where(e => (e.AccountGlobalId == acc.GlobalId ||
                (!e.AccountGlobalId.HasValue && string.Equals(e.Account, acc.AccountName, StringComparison.OrdinalIgnoreCase))) && (!IsIncomeTransaction(e) && !IsTransferInTransaction(e))).Sum(e => e.Amount);
            var accIncome = allExpenses.Where(e => (e.AccountGlobalId == acc.GlobalId ||
                (!e.AccountGlobalId.HasValue && string.Equals(e.Account, acc.AccountName, StringComparison.OrdinalIgnoreCase))) && (IsIncomeTransaction(e) || IsTransferInTransaction(e))).Sum(e => Math.Abs(e.Amount));
            if ((acc.InitialBalance + accIncome - accExpenses) < 0)
                negativeAccountsCount++;
        }

        // Daily Balance Trend (last X days ending today)
        var points = new List<BalanceTrendPoint>();
        var safeDays = Math.Max(2, days);
        var startDate = now.Date.AddDays(-safeDays + 1);
        var endDate = now.Date;

        var expensesBeforeStart = allExpenses.Where(e => e.TransactionDate.Date < startDate && !IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => e.Amount);
        var incomeBeforeStart = allExpenses.Where(e => e.TransactionDate.Date < startDate && IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => Math.Abs(e.Amount));
        var runningBalance = totalInitialBalance + incomeBeforeStart - expensesBeforeStart;

        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            var dayExpenses = allExpenses.Where(e => e.TransactionDate.Date == d && !IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => e.Amount);
            var dayIncome = allExpenses.Where(e => e.TransactionDate.Date == d && IsIncomeTransaction(e) && !IsTransferTransaction(e)).Sum(e => Math.Abs(e.Amount));
            runningBalance = runningBalance + dayIncome - dayExpenses;

            points.Add(new BalanceTrendPoint(
                d,
                d.ToString("dd MMM"),
                runningBalance,
                dayExpenses,
                dayIncome
            ));
        }

        return new FinancialKpiSummary(
            totalNetBalance,
            totalInitialBalance,
            activeAccountsCount,
            negativeAccountsCount,
            currentMonthExpense,
            currentMonthIncome,
            netMonthlyCashFlow,
            savingsRate,
            dailyBurnRate,
            daysInMonth,
            daysElapsed,
            projectedMonthEndSpend,
            points
        );
    }

    // ── Summary ────────────────────────────────────────────────────────────

    public async Task<decimal> GetMonthlyTotalAsync(int year, int month, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Expenses
            .Where(e => !e.IsDeleted
                     && e.TransactionDate.Year == year
                     && e.TransactionDate.Month == month);

        if (memberGlobalId.HasValue)
            query = query.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        return await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;
    }

    public async Task<Dictionary<string, decimal>> GetMonthlyTotalsByCategoryAsync(int year, int month, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Expenses
            .Include(e => e.Category)
            .Where(e => !e.IsDeleted
                     && e.TransactionDate.Year == year
                     && e.TransactionDate.Month == month);

        if (memberGlobalId.HasValue)
            query = query.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        return await query
            .GroupBy(e => e.Category != null ? e.Category.Name : "Uncategorised")
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Category, x => x.Total);
    }

    public record CategorySummaryItem(string Name, string? Icon, string? Colour, decimal Total, int Count, Guid? CategoryGlobalId = null);

    public async Task<List<CategorySummaryItem>> GetDetailedCategorySummaryAsync(int year, int month, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Expenses
            .Include(e => e.Category)
            .Where(e => !e.IsDeleted && e.TransactionDate.Year == year && e.TransactionDate.Month == month);

        if (memberGlobalId.HasValue)
            query = query.Where(e => e.MemberGlobalId == memberGlobalId.Value);

        var expenses = await query.ToListAsync();

        return expenses
            .GroupBy(e => e.Category != null ? e.Category.Name : "Uncategorised")
            .Select(g =>
            {
                var firstCat = g.FirstOrDefault()?.Category;
                return new CategorySummaryItem(
                    g.Key,
                    firstCat?.Icon ?? "📁",
                    firstCat?.Colour ?? "#6c757d",
                    g.Sum(e => e.Amount),
                    g.Count(),
                    firstCat?.GlobalId
                );
            })
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    public record MemberSpendingSummaryItem(Guid? MemberGlobalId, string MemberName, string Avatar, string Colour, decimal Total, int Count, double Percentage);

    public async Task<List<MemberSpendingSummaryItem>> GetMemberSpendingSummaryAsync(int year, int month)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var members = await db.FamilyMembers.Where(m => !m.IsDeleted).ToListAsync();
        var memberMap = members.ToDictionary(m => m.GlobalId);

        var expenses = await db.Expenses
            .Where(e => !e.IsDeleted && e.TransactionDate.Year == year && e.TransactionDate.Month == month)
            .ToListAsync();

        var monthTotal = expenses.Sum(e => e.Amount);

        return expenses
            .GroupBy(e => e.MemberGlobalId)
            .Select(g =>
            {
                var mem = g.Key.HasValue && memberMap.TryGetValue(g.Key.Value, out var m) ? m : null;
                var total = g.Sum(e => e.Amount);
                var pct = monthTotal > 0 ? (double)Math.Round((total / monthTotal) * 100, 1) : 0;
                return new MemberSpendingSummaryItem(
                    g.Key,
                    mem?.Name ?? "Unassigned",
                    mem?.Avatar ?? "👤",
                    mem?.Colour ?? "#6c757d",
                    total,
                    g.Count(),
                    pct
                );
            })
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    public record MonthlyTrendItem(int Year, int Month, string MonthName, decimal Total);

    public async Task<List<MonthlyTrendItem>> GetSpendingTrendsAsync(int monthsCount = 6, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var result = new List<MonthlyTrendItem>();
        var current = DateTime.Today;

        for (int i = monthsCount - 1; i >= 0; i--)
        {
            var date = current.AddMonths(-i);
            var query = db.Expenses
                .Where(e => !e.IsDeleted && e.TransactionDate.Year == date.Year && e.TransactionDate.Month == date.Month);

            if (memberGlobalId.HasValue)
                query = query.Where(e => e.MemberGlobalId == memberGlobalId.Value);

            var total = await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;
            result.Add(new MonthlyTrendItem(date.Year, date.Month, date.ToString("MMM yyyy"), total));
        }

        return result;
    }

    public record CategoryBudgetStatus(
        Guid CategoryId,
        string CategoryName,
        string? Icon,
        string? Colour,
        decimal Spent,
        decimal? Budget,
        double PercentUsed,
        bool IsOverBudget,
        bool IsNearLimit
    );

    public async Task<List<CategoryBudgetStatus>> GetCategoryBudgetStatusesAsync(int year, int month, Guid? memberGlobalId = null)
    {
        var categories = await GetCategoriesAsync();
        var monthlyTotals = await GetMonthlyTotalsByCategoryAsync(year, month, memberGlobalId);

        return categories.Select(c =>
        {
            var spent = monthlyTotals.TryGetValue(c.Name, out var val) ? val : 0m;
            var budget = c.MonthlyBudget;
            var pct = (budget.HasValue && budget.Value > 0) ? Math.Round((double)(spent / budget.Value * 100), 1) : 0;
            var isOver = budget.HasValue && budget.Value > 0 && spent > budget.Value;
            var isNear = budget.HasValue && budget.Value > 0 && spent >= (budget.Value * 0.8m) && !isOver;

            return new CategoryBudgetStatus(
                c.GlobalId,
                c.Name,
                c.Icon,
                c.Colour,
                spent,
                budget,
                pct,
                isOver,
                isNear
            );
        }).ToList();
    }

    // ── Pending Transactions (SMS / Detected) ───────────────────────────────

    public async Task<List<PendingTransaction>> GetPendingTransactionsAsync(PendingTransactionStatus? status = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.PendingTransactions.Where(p => !p.IsDeleted);
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        return await query
            .OrderByDescending(p => p.DetectedDate ?? p.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.PendingTransactions
            .Where(p => !p.IsDeleted && p.Status == PendingTransactionStatus.Pending)
            .CountAsync();
    }

    public async Task<Expense?> ApprovePendingTransactionAsync(
        Guid pendingId,
        Guid? categoryGlobalId,
        decimal amount,
        string description,
        DateTime transactionDate,
        Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pending = await db.PendingTransactions
            .FirstOrDefaultAsync(p => p.GlobalId == pendingId && !p.IsDeleted);

        if (pending is null) return null;

        var expense = new Expense
        {
            GlobalId = Guid.NewGuid(),
            Amount = amount,
            Description = description,
            TransactionDate = transactionDate,
            CategoryGlobalId = categoryGlobalId,
            MemberGlobalId = memberGlobalId,
            DeviceId = "web",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        if (categoryGlobalId.HasValue)
        {
            var cat = await db.Categories
                .FirstOrDefaultAsync(c => c.GlobalId == categoryGlobalId.Value);
            expense.CategoryId = cat?.Id;
        }

        db.Expenses.Add(expense);

        pending.Status = PendingTransactionStatus.Approved;
        pending.ApprovedAsExpenseGlobalId = expense.GlobalId;
        pending.ModifiedAt = DateTime.UtcNow;
        pending.DeviceId = "web";

        await db.SaveChangesAsync();
        return expense;
    }

    public async Task<bool> RejectPendingTransactionAsync(Guid pendingId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pending = await db.PendingTransactions
            .FirstOrDefaultAsync(p => p.GlobalId == pendingId && !p.IsDeleted);

        if (pending is null) return false;

        pending.Status = PendingTransactionStatus.Rejected;
        pending.ModifiedAt = DateTime.UtcNow;
        pending.DeviceId = "web";

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PendingTransaction> SimulateSmsTransactionAsync(
        string rawSms,
        decimal amount,
        string merchant,
        string bank,
        string account,
        DateTime date,
        TransactionType type = TransactionType.Debit)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pending = new PendingTransaction
        {
            GlobalId = Guid.NewGuid(),
            RawSmsBody = rawSms,
            DetectedAmount = amount,
            DetectedMerchant = merchant,
            DetectedBank = bank,
            DetectedAccount = account,
            DetectedType = type,
            DetectedDate = date,
            Status = PendingTransactionStatus.Pending,
            DeviceId = "web-sim",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        db.PendingTransactions.Add(pending);
        await db.SaveChangesAsync();
        return pending;
    }

    public record ImportExpenseItem(
        Guid? GlobalId,
        decimal Amount,
        DateTime Date,
        string Title,
        string? Note,
        string? Account,
        string? Currency,
        string? CategoryName,
        string? Subcategory,
        string? Tags);

    public record ImportResult(int InsertedCount, int UpdatedCount, int SkippedCount, List<string> Errors);

    public async Task<ImportResult> ImportExpensesBatchAsync(List<ImportExpenseItem> items, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var errors = new List<string>();
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        // Cache existing categories to avoid redundant queries
        var existingCategories = await db.Categories.ToListAsync();

        foreach (var item in items)
        {
            try
            {
                // Find or create category if specified
                Category? matchedCategory = null;
                if (!string.IsNullOrWhiteSpace(item.CategoryName))
                {
                    var catTrim = item.CategoryName.Trim();
                    matchedCategory = existingCategories.FirstOrDefault(c =>
                        string.Equals(c.Name, catTrim, StringComparison.OrdinalIgnoreCase) && !c.IsDeleted);

                    if (matchedCategory is null)
                    {
                        // Create category on the fly
                        matchedCategory = new Category
                        {
                            GlobalId = Guid.NewGuid(),
                            Name = catTrim,
                            Icon = "📁",
                            Colour = "#6366f1",
                            DeviceId = "import",
                            CreatedAt = DateTime.UtcNow,
                            ModifiedAt = DateTime.UtcNow
                        };
                        db.Categories.Add(matchedCategory);
                        await db.SaveChangesAsync();
                        existingCategories.Add(matchedCategory);
                    }
                }

                // Normalise amount: if uploaded as negative (e.g. -409), store as positive expense
                var normalizedAmount = Math.Abs(item.Amount);
                var normalizedCurrency = string.IsNullOrWhiteSpace(item.Currency) ? "INR" : item.Currency.Trim();
                var title = string.IsNullOrWhiteSpace(item.Title) ? "Untitled Expense" : item.Title.Trim();

                // Check if already exists by GlobalId (if provided)
                Expense? existingExpense = null;
                if (item.GlobalId.HasValue && item.GlobalId.Value != Guid.Empty)
                {
                    existingExpense = await db.Expenses.FirstOrDefaultAsync(e => e.GlobalId == item.GlobalId.Value);
                }

                if (existingExpense is not null)
                {
                    // Update existing
                    existingExpense.Amount = normalizedAmount;
                    existingExpense.Description = title;
                    existingExpense.TransactionDate = item.Date;
                    existingExpense.CategoryId = matchedCategory?.Id;
                    existingExpense.CategoryGlobalId = matchedCategory?.GlobalId;
                    existingExpense.Note = item.Note;
                    existingExpense.Account = item.Account;
                    existingExpense.Currency = normalizedCurrency;
                    existingExpense.Subcategory = item.Subcategory;
                    existingExpense.Tags = item.Tags;
                    if (memberGlobalId.HasValue && !existingExpense.MemberGlobalId.HasValue)
                        existingExpense.MemberGlobalId = memberGlobalId.Value;
                    existingExpense.IsDeleted = false;
                    existingExpense.ModifiedAt = DateTime.UtcNow;
                    existingExpense.DeviceId = "excel-import";
                    await db.SaveChangesAsync();
                    updated++;
                }
                else
                {
                    // Create new expense
                    var newExpense = new Expense
                    {
                        GlobalId = item.GlobalId ?? Guid.NewGuid(),
                        Amount = normalizedAmount,
                        Description = title,
                        TransactionDate = item.Date,
                        CategoryId = matchedCategory?.Id,
                        CategoryGlobalId = matchedCategory?.GlobalId,
                        MemberGlobalId = memberGlobalId,
                        Note = item.Note,
                        Account = item.Account,
                        Currency = normalizedCurrency,
                        Subcategory = item.Subcategory,
                        Tags = item.Tags,
                        DeviceId = "excel-import",
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow
                    };
                    db.Expenses.Add(newExpense);
                    await db.SaveChangesAsync();
                    inserted++;
                }
            }
            catch (Exception ex)
            {
                var innermost = ex;
                while (innermost.InnerException is not null)
                {
                    innermost = innermost.InnerException;
                }
                errors.Add($"Row '{item.Title}': {innermost.Message}");
                skipped++;
            }
        }

        return new ImportResult(inserted, updated, skipped, errors);
    }

    // ── Family Members & Profiles ──────────────────────────────────────────
    public async Task<List<FamilyMember>> GetFamilyMembersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.FamilyMembers
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.Id)
            .ToListAsync();
    }

    public async Task<FamilyMember?> GetFamilyMemberAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.FamilyMembers
            .FirstOrDefaultAsync(m => m.GlobalId == globalId && !m.IsDeleted);
    }

    public async Task<FamilyMember> CreateFamilyMemberAsync(FamilyMember member)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        member.GlobalId = Guid.NewGuid();
        member.CreatedAt = DateTime.UtcNow;
        member.ModifiedAt = DateTime.UtcNow;
        member.DeviceId = "web";

        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    public async Task<FamilyMember?> UpdateFamilyMemberAsync(FamilyMember updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.FamilyMembers
            .FirstOrDefaultAsync(m => m.GlobalId == updated.GlobalId);

        if (existing is null) return null;

        existing.Name = updated.Name;
        existing.Role = updated.Role;
        existing.Avatar = updated.Avatar;
        existing.Colour = updated.Colour;
        existing.PinHash = updated.PinHash;
        existing.IsDefault = updated.IsDefault;
        if (!string.IsNullOrWhiteSpace(updated.Username))
            existing.Username = updated.Username.Trim().ToLower();
        if (!string.IsNullOrEmpty(updated.PasswordHash))
            existing.PasswordHash = updated.PasswordHash;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.DeviceId = "web";

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> VerifyMemberPinAsync(Guid memberGlobalId, string pin)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.GlobalId == memberGlobalId);
        if (member is null) return false;
        if (string.IsNullOrEmpty(member.PinHash)) return true; // No PIN set

        return member.PinHash == pin;
    }

    // ── Login / Password ───────────────────────────────────────────────────

    /// <summary>
    /// Hash a plain-text password using PBKDF2-SHA256 with a random salt.
    /// Returns "base64salt:base64hash".
    /// </summary>
    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verify a plain-text password against a stored "salt:hash" string.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] expectedHash = Convert.FromBase64String(parts[1]);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] actualHash = pbkdf2.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Validates a username + password combination.
    /// Returns the FamilyMember if successful, or null if invalid credentials.
    /// If PasswordHash is null (first login), any password is accepted and the password is set.
    /// </summary>
    public async Task<FamilyMember?> ValidateLoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var member = await db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Username == username.Trim().ToLower() && !m.IsDeleted);

        if (member is null) return null;

        // First-time login: no password set yet — accept any and store it
        if (string.IsNullOrEmpty(member.PasswordHash))
        {
            member.PasswordHash = HashPassword(password);
            member.ModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return member;
        }

        return VerifyPassword(password, member.PasswordHash) ? member : null;
    }

    /// <summary>
    /// Sets (or resets) the password for a family member.
    /// </summary>
    public async Task<bool> SetPasswordAsync(Guid memberGlobalId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) return false;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.GlobalId == memberGlobalId);
        if (member is null) return false;

        member.PasswordHash = HashPassword(newPassword);
        member.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Sets the PIN for a family member. Stores it as plain text (4-digit PIN).
    /// </summary>
    public async Task<bool> SetPinAsync(Guid memberGlobalId, string pin)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.GlobalId == memberGlobalId);
        if (member is null) return false;

        member.PinHash = string.IsNullOrWhiteSpace(pin) ? null : pin.Trim();
        member.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Soft-deletes a family member profile.
    /// </summary>
    public async Task<bool> DeleteFamilyMemberAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.FamilyMembers
            .FirstOrDefaultAsync(m => m.GlobalId == globalId);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    // ── Financial Goals & Sinking Funds ──────────────────────────────────────

    public record GoalsSummary(
        int TotalGoals,
        int ActiveGoals,
        int CompletedGoals,
        decimal TotalSaved,
        decimal TotalTarget,
        double OverallProgressPct,
        List<FinancialGoal> TopGoals
    );

    public async Task<List<FinancialGoal>> GetGoalsAsync(Guid? memberGlobalId = null, string? status = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.FinancialGoals
            .Include(g => g.BankAccount)
            .Include(g => g.Contributions.Where(c => !c.IsDeleted))
            .Where(g => !g.IsDeleted);

        if (memberGlobalId.HasValue)
        {
            query = query.Where(g => !g.MemberGlobalId.HasValue || g.MemberGlobalId.Value == memberGlobalId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(g => g.Status == status);
        }

        var list = await query.ToListAsync();

        // Sort: Active first, then by target date ascending (or created date)
        return list
            .OrderBy(g => g.Status == "Completed" ? 1 : 0)
            .ThenBy(g => g.TargetDate ?? DateTime.MaxValue)
            .ThenByDescending(g => g.CreatedAt)
            .ToList();
    }

    public async Task<FinancialGoal?> GetGoalAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.FinancialGoals
            .Include(g => g.BankAccount)
            .Include(g => g.Contributions.Where(c => !c.IsDeleted).OrderByDescending(c => c.TransactionDate))
            .FirstOrDefaultAsync(g => g.GlobalId == globalId && !g.IsDeleted);
    }

    public async Task<FinancialGoal> CreateGoalAsync(FinancialGoal goal, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        goal.GlobalId = Guid.NewGuid();
        goal.CreatedAt = DateTime.UtcNow;
        goal.ModifiedAt = DateTime.UtcNow;
        goal.DeviceId = "web";
        goal.Colour = string.IsNullOrWhiteSpace(goal.Colour) ? "#6366f1" : goal.Colour;
        goal.Icon = string.IsNullOrWhiteSpace(goal.Icon) ? "🎯" : goal.Icon;
        goal.Status = goal.CurrentAmount >= goal.TargetAmount && goal.TargetAmount > 0 ? "Completed" : "Active";

        if (memberGlobalId.HasValue && !goal.MemberGlobalId.HasValue)
        {
            goal.MemberGlobalId = memberGlobalId.Value;
        }

        if (goal.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == goal.AccountGlobalId.Value && !a.IsDeleted);
            if (acc != null)
            {
                goal.BankAccountId = acc.Id;
            }
        }

        db.FinancialGoals.Add(goal);

        // If an initial amount was specified, record an initial contribution record
        if (goal.CurrentAmount > 0)
        {
            var contribution = new GoalContribution
            {
                GlobalId = Guid.NewGuid(),
                GoalGlobalId = goal.GlobalId,
                FinancialGoalId = goal.Id,
                Amount = goal.CurrentAmount,
                TransactionDate = DateTime.Today,
                Type = "Deposit",
                Note = "Initial goal balance",
                AccountGlobalId = goal.AccountGlobalId,
                AccountName = goal.BankAccount?.AccountName,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                DeviceId = "web"
            };
            db.GoalContributions.Add(contribution);
        }

        await db.SaveChangesAsync();
        return goal;
    }

    public async Task<FinancialGoal?> UpdateGoalAsync(FinancialGoal updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.FinancialGoals
            .FirstOrDefaultAsync(g => g.GlobalId == updated.GlobalId && !g.IsDeleted);

        if (existing is null) return null;

        existing.Name = updated.Name.Trim();
        existing.TargetAmount = updated.TargetAmount;
        existing.TargetDate = updated.TargetDate;
        existing.Colour = string.IsNullOrWhiteSpace(updated.Colour) ? "#6366f1" : updated.Colour;
        existing.Icon = string.IsNullOrWhiteSpace(updated.Icon) ? "🎯" : updated.Icon;
        existing.Category = updated.Category?.Trim();
        existing.Note = updated.Note?.Trim();
        existing.MemberGlobalId = updated.MemberGlobalId;
        existing.AccountGlobalId = updated.AccountGlobalId;
        existing.ModifiedAt = DateTime.UtcNow;

        if (updated.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == updated.AccountGlobalId.Value && !a.IsDeleted);
            existing.BankAccountId = acc?.Id;
        }
        else
        {
            existing.BankAccountId = null;
        }

        // Auto-update status if target reached or reopened
        if (existing.CurrentAmount >= existing.TargetAmount && existing.TargetAmount > 0)
        {
            existing.Status = "Completed";
        }
        else if (existing.Status == "Completed" && existing.CurrentAmount < existing.TargetAmount)
        {
            existing.Status = "Active";
        }
        else if (!string.IsNullOrWhiteSpace(updated.Status))
        {
            existing.Status = updated.Status;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteGoalAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.FinancialGoals
            .Include(g => g.Contributions)
            .FirstOrDefaultAsync(g => g.GlobalId == globalId && !g.IsDeleted);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;

        foreach (var c in existing.Contributions)
        {
            c.IsDeleted = true;
            c.ModifiedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<GoalContribution> AddGoalContributionAsync(
        Guid goalGlobalId,
        decimal amount,
        string type = "Deposit", // "Deposit" or "Withdrawal"
        string? note = null,
        Guid? accountGlobalId = null,
        DateTime? date = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var goal = await db.FinancialGoals
            .FirstOrDefaultAsync(g => g.GlobalId == goalGlobalId && !g.IsDeleted);

        if (goal is null)
            throw new InvalidOperationException("Financial goal not found.");

        string? accountName = null;
        if (accountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == accountGlobalId.Value && !a.IsDeleted);
            accountName = acc?.AccountName;
        }

        var isDeposit = string.Equals(type, "Deposit", StringComparison.OrdinalIgnoreCase);

        if (isDeposit)
        {
            goal.CurrentAmount += amount;
            if (goal.CurrentAmount >= goal.TargetAmount && goal.TargetAmount > 0)
            {
                goal.Status = "Completed";
            }
        }
        else
        {
            goal.CurrentAmount = Math.Max(0, goal.CurrentAmount - amount);
            if (goal.Status == "Completed" && goal.CurrentAmount < goal.TargetAmount)
            {
                goal.Status = "Active";
            }
        }

        goal.ModifiedAt = DateTime.UtcNow;

        var contribution = new GoalContribution
        {
            GlobalId = Guid.NewGuid(),
            GoalGlobalId = goal.GlobalId,
            FinancialGoalId = goal.Id,
            Amount = amount,
            TransactionDate = date ?? DateTime.Today,
            Type = isDeposit ? "Deposit" : "Withdrawal",
            Note = note?.Trim(),
            AccountGlobalId = accountGlobalId,
            AccountName = accountName,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            DeviceId = "web"
        };

        db.GoalContributions.Add(contribution);
        await db.SaveChangesAsync();

        return contribution;
    }

    public async Task<GoalsSummary> GetGoalsSummaryAsync(Guid? memberGlobalId = null)
    {
        var allGoals = await GetGoalsAsync(memberGlobalId);
        var totalGoals = allGoals.Count;
        var activeGoals = allGoals.Count(g => g.Status == "Active");
        var completedGoals = allGoals.Count(g => g.Status == "Completed");
        var totalSaved = allGoals.Sum(g => g.CurrentAmount);
        var totalTarget = allGoals.Sum(g => g.TargetAmount);
        var overallProgress = totalTarget > 0 ? (double)(totalSaved / totalTarget * 100m) : 0.0;
        var topGoals = allGoals.Take(4).ToList();

        return new GoalsSummary(
            totalGoals,
            activeGoals,
            completedGoals,
            totalSaved,
            totalTarget,
            overallProgress,
            topGoals
        );
    }

    // ── Recurring Transactions & Subscription Manager ─────────────────────────

    public record RecurringSummary(
        int TotalCount,
        int ActiveCount,
        int PausedCount,
        decimal MonthlyEquivalentExpense,
        decimal AnnualProjectedExpense,
        int DueNext7DaysCount,
        decimal DueNext7DaysAmount,
        int OverdueCount,
        decimal OverdueAmount,
        List<RecurringTransaction> UpcomingItems
    );

    public static decimal CalculateMonthlyEquivalent(decimal amount, string frequency) =>
        frequency?.ToLowerInvariant() switch
        {
            "daily" => amount * 30.41m,
            "weekly" => amount * 4.33m,
            "bi-weekly" => amount * 2.16m,
            "monthly" => amount,
            "quarterly" => amount / 3m,
            "semi-annually" => amount / 6m,
            "yearly" => amount / 12m,
            _ => amount
        };

    public async Task<List<RecurringTransaction>> GetRecurringTransactionsAsync(Guid? memberGlobalId = null, string? status = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.BankAccount)
            .Where(r => !r.IsDeleted);

        if (memberGlobalId.HasValue)
        {
            query = query.Where(r => !r.MemberGlobalId.HasValue || r.MemberGlobalId.Value == memberGlobalId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(r => r.Status == status);
        }

        var list = await query.ToListAsync();

        return list
            .OrderBy(r => r.Status == "Active" ? 0 : 1)
            .ThenBy(r => r.NextDueDate)
            .ThenBy(r => r.Name)
            .ToList();
    }

    public async Task<RecurringTransaction?> GetRecurringTransactionAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.BankAccount)
            .FirstOrDefaultAsync(r => r.GlobalId == globalId && !r.IsDeleted);
    }

    public async Task<RecurringTransaction> CreateRecurringTransactionAsync(RecurringTransaction recurring, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        recurring.GlobalId = Guid.NewGuid();
        recurring.CreatedAt = DateTime.UtcNow;
        recurring.ModifiedAt = DateTime.UtcNow;
        recurring.DeviceId = "web";
        recurring.Status = string.IsNullOrWhiteSpace(recurring.Status) ? "Active" : recurring.Status;
        recurring.Frequency = string.IsNullOrWhiteSpace(recurring.Frequency) ? "Monthly" : recurring.Frequency;
        recurring.Colour = string.IsNullOrWhiteSpace(recurring.Colour) ? "#6366f1" : recurring.Colour;
        recurring.Icon = string.IsNullOrWhiteSpace(recurring.Icon) ? "🔁" : recurring.Icon;

        if (memberGlobalId.HasValue && !recurring.MemberGlobalId.HasValue)
        {
            recurring.MemberGlobalId = memberGlobalId.Value;
        }

        if (recurring.CategoryGlobalId.HasValue)
        {
            var cat = await db.Categories.FirstOrDefaultAsync(c => c.GlobalId == recurring.CategoryGlobalId.Value);
            recurring.CategoryId = cat?.Id;
        }

        if (recurring.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == recurring.AccountGlobalId.Value && !a.IsDeleted);
            recurring.BankAccountId = acc?.Id;
        }

        db.RecurringTransactions.Add(recurring);
        await db.SaveChangesAsync();
        return recurring;
    }

    public async Task<RecurringTransaction?> UpdateRecurringTransactionAsync(RecurringTransaction updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.RecurringTransactions
            .FirstOrDefaultAsync(r => r.GlobalId == updated.GlobalId && !r.IsDeleted);

        if (existing is null) return null;

        existing.Name = updated.Name.Trim();
        existing.Amount = updated.Amount;
        existing.Type = updated.Type;
        existing.Frequency = updated.Frequency;
        existing.NextDueDate = updated.NextDueDate;
        existing.Status = updated.Status;
        existing.AutoLog = updated.AutoLog;
        existing.Colour = string.IsNullOrWhiteSpace(updated.Colour) ? "#6366f1" : updated.Colour;
        existing.Icon = string.IsNullOrWhiteSpace(updated.Icon) ? "🔁" : updated.Icon;
        existing.Note = updated.Note?.Trim();
        existing.MemberGlobalId = updated.MemberGlobalId;
        existing.CategoryGlobalId = updated.CategoryGlobalId;
        existing.AccountGlobalId = updated.AccountGlobalId;
        existing.ModifiedAt = DateTime.UtcNow;

        if (updated.CategoryGlobalId.HasValue)
        {
            var cat = await db.Categories.FirstOrDefaultAsync(c => c.GlobalId == updated.CategoryGlobalId.Value);
            existing.CategoryId = cat?.Id;
        }
        else existing.CategoryId = null;

        if (updated.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == updated.AccountGlobalId.Value && !a.IsDeleted);
            existing.BankAccountId = acc?.Id;
        }
        else existing.BankAccountId = null;

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteRecurringTransactionAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.RecurringTransactions
            .FirstOrDefaultAsync(r => r.GlobalId == globalId && !r.IsDeleted);

        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<Expense> MarkRecurringAsPaidAsync(
        Guid globalId,
        decimal? customAmount = null,
        DateTime? paidDate = null,
        Guid? accountGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var item = await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.BankAccount)
            .FirstOrDefaultAsync(r => r.GlobalId == globalId && !r.IsDeleted);

        if (item is null)
            throw new InvalidOperationException("Recurring transaction not found.");

        var pDate = paidDate ?? DateTime.Today;
        var pAmount = customAmount.HasValue && customAmount.Value > 0 ? customAmount.Value : item.Amount;
        var accGuid = accountGlobalId ?? item.AccountGlobalId;

        // Resolve BankAccount details
        string? accName = item.BankAccount?.AccountName;
        int? bankAccountId = item.BankAccountId;
        if (accountGlobalId.HasValue && accountGlobalId.Value != item.AccountGlobalId)
        {
            var matchedAcc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == accountGlobalId.Value && !a.IsDeleted);
            accName = matchedAcc?.AccountName;
            bankAccountId = matchedAcc?.Id;
        }

        // 1. Create logged expense
        var expense = new Expense
        {
            GlobalId = Guid.NewGuid(),
            Amount = pAmount,
            Description = $"{item.Name} ({item.Frequency} recurring)",
            TransactionDate = pDate,
            Account = accName,
            AccountGlobalId = accGuid,
            BankAccountId = bankAccountId,
            CategoryId = item.CategoryId,
            CategoryGlobalId = item.CategoryGlobalId,
            MemberGlobalId = item.MemberGlobalId,
            Tags = item.Type == "Income" ? "income,recurring" : "recurring,subscription",
            Subcategory = item.Category?.Name ?? "Subscriptions",
            Note = $"Recurring bill logged on {pDate:dd MMM yyyy}. Next due: {CalculateNextDueDate(item.NextDueDate, item.Frequency):dd MMM yyyy}.",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            DeviceId = "web",
            Currency = "INR"
        };

        db.Expenses.Add(expense);

        // 2. Advance the NextDueDate
        item.LastPaidDate = pDate;
        item.NextDueDate = CalculateNextDueDate(item.NextDueDate, item.Frequency);
        item.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return expense;
    }

    public static DateTime CalculateNextDueDate(DateTime currentDue, string frequency) =>
        frequency?.ToLowerInvariant() switch
        {
            "daily" => currentDue.AddDays(1),
            "weekly" => currentDue.AddDays(7),
            "bi-weekly" => currentDue.AddDays(14),
            "monthly" => currentDue.AddMonths(1),
            "quarterly" => currentDue.AddMonths(3),
            "semi-annually" => currentDue.AddMonths(6),
            "yearly" => currentDue.AddYears(1),
            _ => currentDue.AddMonths(1)
        };

    public async Task<RecurringSummary> GetRecurringSummaryAsync(Guid? memberGlobalId = null)
    {
        var all = await GetRecurringTransactionsAsync(memberGlobalId);
        var active = all.Where(r => r.Status == "Active").ToList();
        var paused = all.Where(r => r.Status == "Paused").ToList();

        var today = DateTime.Today;
        var in7Days = today.AddDays(7);

        var dueNext7 = active.Where(r => r.NextDueDate.Date >= today && r.NextDueDate.Date <= in7Days).ToList();
        var overdue = active.Where(r => r.NextDueDate.Date < today).ToList();

        decimal monthlyExpense = active.Where(r => r.Type == "Expense")
            .Sum(r => CalculateMonthlyEquivalent(r.Amount, r.Frequency));
        decimal yearlyExpense = monthlyExpense * 12m;

        var upcoming = active.OrderBy(r => r.NextDueDate).Take(5).ToList();

        return new RecurringSummary(
            TotalCount: all.Count,
            ActiveCount: active.Count,
            PausedCount: paused.Count,
            MonthlyEquivalentExpense: monthlyExpense,
            AnnualProjectedExpense: yearlyExpense,
            DueNext7DaysCount: dueNext7.Count,
            DueNext7DaysAmount: dueNext7.Where(r => r.Type == "Expense").Sum(r => r.Amount),
            OverdueCount: overdue.Count,
            OverdueAmount: overdue.Where(r => r.Type == "Expense").Sum(r => r.Amount),
            UpcomingItems: upcoming
        );
    }

    // ── Loans & EMI Repayment Manager ─────────────────────────────────────────

    public record LoansSummary(
        int TotalLoansCount,
        int ActiveLoansCount,
        int ClosedLoansCount,
        decimal TotalOutstandingPrincipal,
        decimal TotalMonthlyEmi,
        decimal TotalOriginalPrincipal,
        decimal TotalRepaidAmount,
        double OverallRepaidPct,
        int DueNext7DaysCount,
        decimal DueNext7DaysAmount,
        int OverdueCount,
        decimal OverdueAmount,
        List<Loan> UpcomingEmis
    );

    public record AmortizationRow(
        int MonthNumber,
        DateTime PaymentDate,
        decimal BeginningBalance,
        decimal EmiAmount,
        decimal PrincipalPaid,
        decimal InterestPaid,
        decimal EndingBalance
    );

    public static decimal CalculateEmi(decimal principal, decimal annualRatePct, int tenureMonths)
    {
        if (principal <= 0 || tenureMonths <= 0) return 0;
        if (annualRatePct <= 0) return Math.Round(principal / tenureMonths, 2);

        double r = (double)(annualRatePct / 12m / 100m);
        double p = (double)principal;
        int n = tenureMonths;

        double emi = p * r * Math.Pow(1 + r, n) / (Math.Pow(1 + r, n) - 1);
        return Math.Round((decimal)emi, 2);
    }

    public static List<AmortizationRow> GenerateAmortizationSchedule(
        decimal principal,
        decimal annualRatePct,
        int tenureMonths,
        decimal emiAmount,
        DateTime startDate)
    {
        var schedule = new List<AmortizationRow>();
        if (principal <= 0 || tenureMonths <= 0) return schedule;

        decimal monthlyRate = annualRatePct > 0 ? (annualRatePct / 12m / 100m) : 0m;
        decimal balance = principal;
        var date = startDate;

        for (int i = 1; i <= tenureMonths && balance > 0; i++)
        {
            decimal interest = Math.Round(balance * monthlyRate, 2);
            decimal actualEmi = emiAmount;

            // In final month or if EMI exceeds balance + interest, pay balance off
            if (balance + interest < actualEmi || i == tenureMonths)
            {
                actualEmi = balance + interest;
            }

            decimal principalPaid = Math.Min(balance, Math.Max(0m, actualEmi - interest));
            decimal ending = Math.Max(0m, balance - principalPaid);

            schedule.Add(new AmortizationRow(
                MonthNumber: i,
                PaymentDate: date,
                BeginningBalance: balance,
                EmiAmount: actualEmi,
                PrincipalPaid: principalPaid,
                InterestPaid: interest,
                EndingBalance: ending
            ));

            balance = ending;
            date = date.AddMonths(1);
        }

        return schedule;
    }

    public async Task<List<Loan>> GetLoansAsync(Guid? memberGlobalId = null, string? status = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Loans
            .Include(l => l.BankAccount)
            .Include(l => l.Payments.Where(p => !p.IsDeleted).OrderByDescending(p => p.PaymentDate))
            .Where(l => !l.IsDeleted);

        if (memberGlobalId.HasValue)
        {
            query = query.Where(l => l.MemberGlobalId == memberGlobalId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(l => l.Status == status);
        }

        return await query
            .OrderBy(l => l.Status == "Active" ? 0 : 1)
            .ThenBy(l => l.NextDueDate)
            .ThenBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<Loan?> GetLoanAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Loans
            .Include(l => l.BankAccount)
            .Include(l => l.Payments.Where(p => !p.IsDeleted).OrderByDescending(p => p.PaymentDate))
            .FirstOrDefaultAsync(l => l.GlobalId == globalId && !l.IsDeleted);
    }

    public async Task<Loan> CreateLoanAsync(Loan loan, Guid? memberGlobalId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        loan.GlobalId = Guid.NewGuid();
        loan.CreatedAt = DateTime.UtcNow;
        loan.ModifiedAt = DateTime.UtcNow;
        loan.DeviceId = "web";
        loan.Colour = string.IsNullOrWhiteSpace(loan.Colour) ? "#f43f5e" : loan.Colour;
        loan.Icon = string.IsNullOrWhiteSpace(loan.Icon) ? "💳" : loan.Icon;
        loan.Status = loan.OutstandingBalance <= 0 && loan.PrincipalAmount > 0 ? "Closed" : "Active";

        if (memberGlobalId.HasValue && !loan.MemberGlobalId.HasValue)
        {
            loan.MemberGlobalId = memberGlobalId.Value;
        }

        if (loan.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == loan.AccountGlobalId.Value && !a.IsDeleted);
            if (acc != null)
            {
                loan.BankAccountId = acc.Id;
            }
        }

        // If EMI was not computed, calculate automatically
        if (loan.EmiAmount <= 0)
        {
            loan.EmiAmount = CalculateEmi(loan.PrincipalAmount, loan.InterestRate, loan.TenureMonths);
        }

        if (loan.OutstandingBalance <= 0 && loan.PrincipalAmount > 0)
        {
            loan.OutstandingBalance = loan.PrincipalAmount;
        }

        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return loan;
    }

    public async Task<Loan?> UpdateLoanAsync(Loan updated)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Loans.FirstOrDefaultAsync(l => l.GlobalId == updated.GlobalId && !l.IsDeleted);
        if (existing is null) return null;

        existing.Name = updated.Name;
        existing.Lender = updated.Lender;
        existing.LoanType = updated.LoanType;
        existing.PrincipalAmount = updated.PrincipalAmount;
        existing.InterestRate = updated.InterestRate;
        existing.TenureMonths = updated.TenureMonths;
        existing.EmiAmount = updated.EmiAmount;
        existing.OutstandingBalance = updated.OutstandingBalance;
        existing.TotalPaidAmount = updated.TotalPaidAmount;
        existing.EmisPaid = updated.EmisPaid;
        existing.StartDate = updated.StartDate;
        existing.NextDueDate = updated.NextDueDate;
        existing.Status = updated.OutstandingBalance <= 0 ? "Closed" : updated.Status;
        existing.Colour = updated.Colour;
        existing.Icon = updated.Icon;
        existing.Note = updated.Note;
        existing.MemberGlobalId = updated.MemberGlobalId;
        existing.AccountGlobalId = updated.AccountGlobalId;
        existing.ModifiedAt = DateTime.UtcNow;

        if (updated.AccountGlobalId.HasValue)
        {
            var acc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == updated.AccountGlobalId.Value && !a.IsDeleted);
            existing.BankAccountId = acc?.Id;
        }
        else existing.BankAccountId = null;

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteLoanAsync(Guid globalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Loans.FirstOrDefaultAsync(l => l.GlobalId == globalId && !l.IsDeleted);
        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(LoanPayment Payment, Expense ExpenseRecord)> RecordLoanPaymentAsync(
        Guid loanGlobalId,
        decimal amount,
        DateTime? paymentDate = null,
        Guid? accountGlobalId = null,
        string paymentType = "EMI",
        string? note = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var loan = await db.Loans
            .Include(l => l.BankAccount)
            .FirstOrDefaultAsync(l => l.GlobalId == loanGlobalId && !l.IsDeleted);

        if (loan is null)
            throw new InvalidOperationException("Loan account not found.");

        var pDate = paymentDate ?? DateTime.Today;
        var accGuid = accountGlobalId ?? loan.AccountGlobalId;

        // Resolve BankAccount details
        string? accName = loan.BankAccount?.AccountName;
        int? bankAccountId = loan.BankAccountId;
        if (accountGlobalId.HasValue && accountGlobalId.Value != loan.AccountGlobalId)
        {
            var matchedAcc = await db.BankAccounts.FirstOrDefaultAsync(a => a.GlobalId == accountGlobalId.Value && !a.IsDeleted);
            accName = matchedAcc?.AccountName;
            bankAccountId = matchedAcc?.Id;
        }

        // Calculate interest component
        decimal monthlyRate = loan.InterestRate > 0 ? (loan.InterestRate / 12m / 100m) : 0m;
        decimal interestPart = 0m;
        decimal principalPart = amount;

        if (paymentType == "EMI" && loan.InterestRate > 0)
        {
            interestPart = Math.Min(amount, Math.Round(loan.OutstandingBalance * monthlyRate, 2));
            principalPart = Math.Max(0m, amount - interestPart);
        }

        // Reduce outstanding balance
        loan.OutstandingBalance = Math.Max(0m, loan.OutstandingBalance - principalPart);
        loan.TotalPaidAmount += amount;

        if (paymentType == "EMI")
        {
            loan.EmisPaid++;
            loan.NextDueDate = loan.NextDueDate.AddMonths(1);
        }

        if (loan.OutstandingBalance <= 0 || (paymentType == "EMI" && loan.EmisPaid >= loan.TenureMonths))
        {
            loan.Status = "Closed";
        }

        loan.ModifiedAt = DateTime.UtcNow;

        // 1. Record LoanPayment
        var payment = new LoanPayment
        {
            GlobalId = Guid.NewGuid(),
            LoanGlobalId = loan.GlobalId,
            LoanId = loan.Id,
            PaymentDate = pDate,
            Amount = amount,
            PrincipalComponent = principalPart,
            InterestComponent = interestPart,
            PaymentType = paymentType,
            AccountGlobalId = accGuid,
            AccountName = accName,
            Note = note?.Trim(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            DeviceId = "web"
        };
        db.LoanPayments.Add(payment);

        // 2. Record Expense in ledger
        var expense = new Expense
        {
            GlobalId = Guid.NewGuid(),
            Amount = amount,
            Description = $"{loan.Name} ({paymentType})",
            TransactionDate = pDate,
            Account = accName,
            AccountGlobalId = accGuid,
            BankAccountId = bankAccountId,
            Tags = "loan,emi,debt",
            Subcategory = loan.LoanType,
            Note = $"{paymentType} payment for {loan.Name}. Principal: ₹{principalPart:N2}, Interest: ₹{interestPart:N2}. Outstanding: ₹{loan.OutstandingBalance:N2}.",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            DeviceId = "web",
            Currency = "INR"
        };
        db.Expenses.Add(expense);

        await db.SaveChangesAsync();
        return (payment, expense);
    }

    public async Task<LoansSummary> GetLoansSummaryAsync(Guid? memberGlobalId = null)
    {
        var all = await GetLoansAsync(memberGlobalId);
        var active = all.Where(l => l.Status == "Active").ToList();
        var closed = all.Where(l => l.Status == "Closed").ToList();

        var today = DateTime.Today;
        var in7Days = today.AddDays(7);

        var dueNext7 = active.Where(l => l.NextDueDate.Date >= today && l.NextDueDate.Date <= in7Days).ToList();
        var overdue = active.Where(l => l.NextDueDate.Date < today).ToList();

        decimal totalOutstanding = active.Sum(l => l.OutstandingBalance);
        decimal totalMonthlyEmi = active.Sum(l => l.EmiAmount);
        decimal totalOriginal = all.Sum(l => l.PrincipalAmount);
        decimal totalRepaid = all.Sum(l => l.TotalPaidAmount);
        double overallRepaidPct = totalOriginal > 0 ? (double)(totalRepaid / totalOriginal * 100m) : 0;

        var upcoming = active.OrderBy(l => l.NextDueDate).Take(5).ToList();

        return new LoansSummary(
            TotalLoansCount: all.Count,
            ActiveLoansCount: active.Count,
            ClosedLoansCount: closed.Count,
            TotalOutstandingPrincipal: totalOutstanding,
            TotalMonthlyEmi: totalMonthlyEmi,
            TotalOriginalPrincipal: totalOriginal,
            TotalRepaidAmount: totalRepaid,
            OverallRepaidPct: Math.Min(100.0, Math.Round(overallRepaidPct, 1)),
            DueNext7DaysCount: dueNext7.Count,
            DueNext7DaysAmount: dueNext7.Sum(l => l.EmiAmount),
            OverdueCount: overdue.Count,
            OverdueAmount: overdue.Sum(l => l.EmiAmount),
            UpcomingEmis: upcoming
        );
    }
}
