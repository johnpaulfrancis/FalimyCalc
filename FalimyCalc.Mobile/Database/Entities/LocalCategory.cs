using FalimyCalc.Shared.Enums;
using SQLite;

namespace FalimyCalc.Mobile.Database.Entities;

[Table("Categories")]
public class LocalCategory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public Guid GlobalId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Colour { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }

    [Indexed]
    public SyncStatus SyncStatus { get; set; } = SyncStatus.PendingCreate;
}
