using FalimyCalc.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FalimyCalc.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PendingTransaction> PendingTransactions => Set<PendingTransaction>();

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
            entity.Property(e => e.DeviceId).HasMaxLength(100);

            // Soft FK: CategoryId is the local SQL Server FK
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Expenses)
                  .HasForeignKey(e => e.CategoryId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
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

        return max;
    }
}
