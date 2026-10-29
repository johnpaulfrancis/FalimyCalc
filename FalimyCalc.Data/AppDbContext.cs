using FalimyCalc.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FalimyCalc.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PendingTransaction> PendingTransactions => Set<PendingTransaction>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<FinancialGoal> FinancialGoals => Set<FinancialGoal>();
    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── SyncableEntity base configuration ──────────────────────────────
        // Applied to all entities that inherit SyncableEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(SyncableEntity).IsAssignableFrom(e.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(SyncableEntity.GlobalId))
                .IsUnique();

            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(SyncableEntity.RowVersion));
        }

        // ── Category ───────────────────────────────────────────────────────
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(e => e.Name)
                  .IsRequired()
                  .HasMaxLength(100);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Colour).HasMaxLength(20);
            entity.Property(e => e.MonthlyBudget).HasPrecision(18, 2);
            entity.Property(e => e.DeviceId).HasMaxLength(100);
        });

        // ── Expense ────────────────────────────────────────────────────────
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2);
            entity.Property(e => e.Description)
                  .HasMaxLength(500);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Account).HasMaxLength(100);
            entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("INR");
            entity.Property(e => e.Subcategory).HasMaxLength(100);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.MemberGlobalId);   // nullable Guid — which member added this
            entity.HasIndex(e => e.MemberGlobalId);
            entity.Property(e => e.AccountGlobalId);  // nullable Guid — which bank account was used
            entity.HasIndex(e => e.AccountGlobalId);

            // Soft FK: CategoryId is the local SQL Server FK
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Expenses)
                  .HasForeignKey(e => e.CategoryId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            // Bank Account relationship
            entity.HasOne(e => e.BankAccount)
                  .WithMany(a => a.Expenses)
                  .HasForeignKey(e => e.BankAccountId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── BankAccount ────────────────────────────────────────────────────
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("BankAccounts");
            entity.Property(e => e.AccountName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.InitialBalance).HasPrecision(18, 2);
            entity.Property(e => e.AccountNumberSuffix).HasMaxLength(20);
            entity.Property(e => e.AccountType).HasMaxLength(50);
            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.Colour).HasMaxLength(20);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.MemberGlobalId);
            entity.Property(e => e.IsJoint).HasDefaultValue(false);
            entity.Property(e => e.JointMemberGlobalIds).HasMaxLength(1000);
            entity.HasIndex(e => e.MemberGlobalId);
            entity.HasIndex(e => e.IsJoint);
        });

        // ── PendingTransaction ─────────────────────────────────────────────
        modelBuilder.Entity<PendingTransaction>(entity =>
        {
            entity.ToTable("PendingTransactions");
            entity.Property(e => e.RawSmsBody).HasMaxLength(1000);
            entity.Property(e => e.DetectedMerchant).HasMaxLength(200);
            entity.Property(e => e.DetectedBank).HasMaxLength(100);
            entity.Property(e => e.DetectedAccount).HasMaxLength(50);
            entity.Property(e => e.DetectedAmount).HasPrecision(18, 2);
            entity.Property(e => e.DeviceId).HasMaxLength(100);

            entity.HasIndex(e => e.Status);
        });

        // ── FamilyMember ───────────────────────────────────────────────────
        modelBuilder.Entity<FamilyMember>(entity =>
        {
            entity.ToTable("FamilyMembers");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.Avatar).HasMaxLength(20);
            entity.Property(e => e.Colour).HasMaxLength(20);
            entity.Property(e => e.PinHash).HasMaxLength(256);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.HasIndex(e => e.Username).IsUnique().HasFilter("[Username] IS NOT NULL");
            entity.Property(e => e.DeviceId).HasMaxLength(100);
        });

        // ── FinancialGoal ───────────────────────────────────────────────────
        modelBuilder.Entity<FinancialGoal>(entity =>
        {
            entity.ToTable("FinancialGoals");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.TargetAmount).HasPrecision(18, 2);
            entity.Property(e => e.CurrentAmount).HasPrecision(18, 2);
            entity.Property(e => e.Colour).HasMaxLength(30);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Active");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.DeviceId).HasMaxLength(100);

            entity.HasIndex(e => e.MemberGlobalId);
            entity.HasIndex(e => e.AccountGlobalId);

            entity.HasOne(e => e.BankAccount)
                  .WithMany()
                  .HasForeignKey(e => e.BankAccountId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Contributions)
                  .WithOne(c => c.FinancialGoal)
                  .HasForeignKey(c => c.FinancialGoalId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── GoalContribution ────────────────────────────────────────────────
        modelBuilder.Entity<GoalContribution>(entity =>
        {
            entity.ToTable("GoalContributions");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Type).HasMaxLength(20).HasDefaultValue("Deposit");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.AccountName).HasMaxLength(100);
            entity.Property(e => e.DeviceId).HasMaxLength(100);

            entity.HasIndex(e => e.GoalGlobalId);
        });

        // ── RecurringTransaction ───────────────────────────────────────────
        modelBuilder.Entity<RecurringTransaction>(entity =>
        {
            entity.ToTable("RecurringTransactions");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Type).HasMaxLength(20).HasDefaultValue("Expense");
            entity.Property(e => e.Frequency).HasMaxLength(30).HasDefaultValue("Monthly");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Active");
            entity.Property(e => e.Colour).HasMaxLength(30);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.DeviceId).HasMaxLength(100);

            entity.HasIndex(e => e.MemberGlobalId);
            entity.HasIndex(e => e.AccountGlobalId);
            entity.HasIndex(e => e.NextDueDate);

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.BankAccount)
                  .WithMany()
                  .HasForeignKey(e => e.BankAccountId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Loan ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("Loans");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Lender).HasMaxLength(150);
            entity.Property(e => e.LoanType).HasMaxLength(50).HasDefaultValue("Personal Loan");
            entity.Property(e => e.PrincipalAmount).HasPrecision(18, 2);
            entity.Property(e => e.InterestRate).HasPrecision(8, 3);
            entity.Property(e => e.EmiAmount).HasPrecision(18, 2);
            entity.Property(e => e.OutstandingBalance).HasPrecision(18, 2);
            entity.Property(e => e.TotalPaidAmount).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Active");
            entity.Property(e => e.Colour).HasMaxLength(30);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.DeviceId).HasMaxLength(100);

            entity.HasIndex(e => e.MemberGlobalId);
            entity.HasIndex(e => e.AccountGlobalId);
            entity.HasIndex(e => e.NextDueDate);

            entity.HasOne(e => e.BankAccount)
                  .WithMany()
                  .HasForeignKey(e => e.BankAccountId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.Loan)
                  .HasForeignKey(p => p.LoanId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── LoanPayment ─────────────────────────────────────────────────────
        modelBuilder.Entity<LoanPayment>(entity =>
        {
            entity.ToTable("LoanPayments");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PrincipalComponent).HasPrecision(18, 2);
            entity.Property(e => e.InterestComponent).HasPrecision(18, 2);
            entity.Property(e => e.PaymentType).HasMaxLength(30).HasDefaultValue("EMI");
            entity.Property(e => e.AccountName).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasIndex(e => e.LoanGlobalId);
            entity.HasIndex(e => e.PaymentDate);
        });

        // ── Seed default categories ────────────────────────────────────────
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, GlobalId = new Guid("a1000000-0000-0000-0000-000000000001"), Name = "Food & Dining",   Icon = "🍽️",  Colour = "#FF6B6B", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 1 },
            new Category { Id = 2, GlobalId = new Guid("a1000000-0000-0000-0000-000000000002"), Name = "Transport",       Icon = "🚗",  Colour = "#4ECDC4", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 2 },
            new Category { Id = 3, GlobalId = new Guid("a1000000-0000-0000-0000-000000000003"), Name = "Shopping",        Icon = "🛍️",  Colour = "#45B7D1", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 3 },
            new Category { Id = 4, GlobalId = new Guid("a1000000-0000-0000-0000-000000000004"), Name = "Utilities",       Icon = "💡",  Colour = "#96CEB4", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 4 },
            new Category { Id = 5, GlobalId = new Guid("a1000000-0000-0000-0000-000000000005"), Name = "Health",          Icon = "🏥",  Colour = "#FFEAA7", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 5 },
            new Category { Id = 6, GlobalId = new Guid("a1000000-0000-0000-0000-000000000006"), Name = "Entertainment",   Icon = "🎬",  Colour = "#DDA0DD", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 6 },
            new Category { Id = 7, GlobalId = new Guid("a1000000-0000-0000-0000-000000000007"), Name = "Other",           Icon = "📦",  Colour = "#B0B0B0", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 7 }
        );

        // ── Seed default family members ─────────────────────────────────────
        modelBuilder.Entity<FamilyMember>().HasData(
            new FamilyMember { Id = 1, GlobalId = new Guid("b1000000-0000-0000-0000-000000000001"), Name = "Dad",      Role = "Admin",  Avatar = "👨‍💼", Colour = "#3b82f6", IsDefault = true,  Username = "dad", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 1 },
            new FamilyMember { Id = 2, GlobalId = new Guid("b1000000-0000-0000-0000-000000000002"), Name = "Mom",      Role = "Admin",  Avatar = "👩‍💼", Colour = "#ec4899", IsDefault = false, Username = "mom", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 2 },
            new FamilyMember { Id = 3, GlobalId = new Guid("b1000000-0000-0000-0000-000000000003"), Name = "Son",      Role = "Member", Avatar = "👦",  Colour = "#10b981", IsDefault = false, DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 3 },
            new FamilyMember { Id = 4, GlobalId = new Guid("b1000000-0000-0000-0000-000000000004"), Name = "Daughter", Role = "Member", Avatar = "👧",  Colour = "#f59e0b", IsDefault = false, DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, RowVersion = 4 }
        );
    }

    /// <summary>
    /// Assigns RowVersion on insert/update automatically.
    /// Call SaveChangesAsync via this override — never bypass it.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignRowVersions();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        AssignRowVersions();
        return base.SaveChanges();
    }

    private void AssignRowVersions()
    {
        var entries = ChangeTracker.Entries<SyncableEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        // RowVersion = max existing RowVersion + 1, per save batch
        // This is safe for a single-server setup.
        // For multi-server, use a sequence or timestamp strategy instead.
        if (!entries.Any()) return;

        var maxRowVersion = GetCurrentMaxRowVersion();
        long next = maxRowVersion + 1;

        foreach (var entry in entries)
        {
            entry.Entity.RowVersion = next++;
        }
    }

    private long GetCurrentMaxRowVersion()
    {
        // Query the max RowVersion across all syncable tables.
        // We union the tables manually because EF doesn't support cross-table max natively.
        long max = 0;

        if (Expenses.Any()) max = Math.Max(max, Expenses.Max(e => e.RowVersion));
        if (Categories.Any()) max = Math.Max(max, Categories.Max(e => e.RowVersion));
        if (PendingTransactions.Any()) max = Math.Max(max, PendingTransactions.Max(e => e.RowVersion));
        if (FamilyMembers.Any()) max = Math.Max(max, FamilyMembers.Max(e => e.RowVersion));
        if (FinancialGoals.Any()) max = Math.Max(max, FinancialGoals.Max(g => g.RowVersion));
        if (RecurringTransactions.Any()) max = Math.Max(max, RecurringTransactions.Max(r => r.RowVersion));
        if (Loans.Any()) max = Math.Max(max, Loans.Max(l => l.RowVersion));

        return max;
    }
}
