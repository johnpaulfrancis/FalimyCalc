namespace FalimyCalc.Shared.DTOs;

public class CategoryDto
{
    public Guid GlobalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Colour { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
}
