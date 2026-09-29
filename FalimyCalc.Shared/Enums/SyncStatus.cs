namespace FalimyCalc.Shared.Enums;

/// <summary>
/// Tracks the sync state of a record on the mobile device.
/// Only used locally on the phone — never stored on the server.
/// </summary>
public enum SyncStatus
{
    Synced = 0,
    PendingCreate = 1,
    PendingUpdate = 2,
    PendingDelete = 3
}
