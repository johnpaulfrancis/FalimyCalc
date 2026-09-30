using FalimyCalc.Mobile.Database.Entities;
using SQLite;

namespace FalimyCalc.Mobile.Database;

/// <summary>
/// Manages the SQLite connection for the mobile app.
/// Registered as a singleton — one connection shared across the app lifetime.
/// </summary>
public class LocalDatabase
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly string _dbPath;

    public LocalDatabase()
    {
        // Store the database in the app's local data directory.
        // On Android this maps to /data/data/<package>/files/
        _dbPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "falimycalc.db3");
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        await _initLock.WaitAsync();
        try
        {
            if (_connection is not null)
                return _connection;

            _connection = new SQLiteAsyncConnection(_dbPath,
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create |
                SQLiteOpenFlags.SharedCache);

            await InitialiseTablesAsync(_connection);
            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task InitialiseTablesAsync(SQLiteAsyncConnection db)
    {
        await db.CreateTableAsync<LocalExpense>();
        await db.CreateTableAsync<LocalCategory>();
        await db.CreateTableAsync<LocalPendingTransaction>();
        await db.CreateTableAsync<SyncMetadata>();

        await SeedDefaultCategoriesAsync(db);
    }

    private static async Task SeedDefaultCategoriesAsync(SQLiteAsyncConnection db)
    {
        // Only seed if no categories exist yet
        var count = await db.Table<LocalCategory>().CountAsync();
        if (count > 0) return;

        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var defaults = new List<LocalCategory>
        {
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000001"), Name = "Food & Dining",  Icon = "🍽️", Colour = "#FF6B6B", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000002"), Name = "Transport",      Icon = "🚗", Colour = "#4ECDC4", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000003"), Name = "Shopping",       Icon = "🛍️", Colour = "#45B7D1", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000004"), Name = "Utilities",      Icon = "💡", Colour = "#96CEB4", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000005"), Name = "Health",         Icon = "🏥", Colour = "#FFEAA7", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000006"), Name = "Entertainment",  Icon = "🎬", Colour = "#DDA0DD", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
            new() { GlobalId = new Guid("a1000000-0000-0000-0000-000000000007"), Name = "Other",          Icon = "📦", Colour = "#B0B0B0", DeviceId = "seed", CreatedAt = seedDate, ModifiedAt = seedDate, SyncStatus = Shared.Enums.SyncStatus.Synced },
        };

        await db.InsertAllAsync(defaults);
    }
}

/// <summary>
/// Stores sync state for this device — specifically the last RowVersion
/// successfully pulled from the server, used for incremental sync.
/// </summary>
[Table("SyncMetadata")]
public class SyncMetadata
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public const string LastSyncedRowVersionKey = "LastSyncedRowVersion";
    public const string DeviceIdKey = "DeviceId";
}
