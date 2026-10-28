namespace FalimyCalc.Data.Entities;

public class FamilyMember : SyncableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Member"; // "Admin" or "Member"
    public string Avatar { get; set; } = "👤";  // Emoji or avatar representation
    public string? Colour { get; set; } = "#4f46e5";
    public string? PinHash { get; set; }        // 4-digit PIN hash for profile lock
    public bool IsDefault { get; set; }

    // Login credentials
    public string? Username { get; set; }       // Login username (unique, nullable for legacy)
    public string? PasswordHash { get; set; }   // PBKDF2-hashed password
}
