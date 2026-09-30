namespace FalimyCalc.Data.Entities;

/// <summary>
/// Base class for all server-side entities.
/// GlobalId is the stable identity used across mobile and server.
/// RowVersion is a server-assigned monotonically increasing number
/// used for incremental sync — the mobile device requests only records
/// with RowVersion greater than its last known value.
/// </summary>
public abstract class SyncableEntity
{
    /// <summary>
    /// Surrogate primary key for SQL Server (auto-increment).
    /// Never exposed to the mobile client.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Globally unique identity shared across all devices.
    /// Created on the mobile device before the record ever reaches the server.
    /// </summary>
    public Guid GlobalId { get; set; }

    /// <summary>
    /// Which device last wrote this record.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the record was first created (on any device).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent change (on any device).
    /// Used for Last-Write-Wins conflict resolution.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Soft delete flag. Records are never hard-deleted once synced.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Server-assigned monotonically increasing version number.
    /// Set automatically by the server on every insert/update.
    /// Mobile clients use this for incremental pull: "give me everything
    /// with RowVersion greater than X".
    /// </summary>
    public long RowVersion { get; set; }
}
