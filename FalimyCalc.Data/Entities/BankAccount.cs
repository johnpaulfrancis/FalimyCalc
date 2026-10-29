namespace FalimyCalc.Data.Entities;

public class BankAccount : SyncableEntity
{
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Initial opening balance when the account was added. Can be positive, zero, or negative (e.g. credit cards, overdrafts).
    /// </summary>
    public decimal InitialBalance { get; set; }

    public string? AccountNumberSuffix { get; set; } // e.g. last 4 digits
    public string? AccountType { get; set; }         // e.g. "Savings", "Current", "Credit Card", "Cash Wallet"
    public string? BankName { get; set; }            // e.g. "HDFC Bank", "SBI", "ICICI"
    public string? Colour { get; set; }              // e.g. "#6366f1"
    public string? Icon { get; set; }                // e.g. "🏦", "💳", "💵", "📱"

    /// <summary>
    /// Which family member/profile owns this bank account.
    /// </summary>
    public Guid MemberGlobalId { get; set; }

    /// <summary>
    /// Indicates whether this bank account is a joint account shared among multiple family members.
    /// </summary>
    public bool IsJoint { get; set; } = false;

    /// <summary>
    /// Comma-separated list of additional family member GlobalIds sharing this account.
    /// The primary creator/owner is stored in MemberGlobalId.
    /// </summary>
    public string? JointMemberGlobalIds { get; set; }

    /// <summary>
    /// Checks if a given member has access to this account (either as primary owner or as a joint member).
    /// </summary>
    public bool HasMemberAccess(Guid memberId)
    {
        if (MemberGlobalId == memberId) return true;
        if (!IsJoint || string.IsNullOrWhiteSpace(JointMemberGlobalIds)) return false;

        return JointMemberGlobalIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(id => Guid.TryParse(id, out var g) && g == memberId);
    }

    /// <summary>
    /// Returns the parsed list of joint member GlobalIds.
    /// </summary>
    public List<Guid> GetJointMemberGuids()
    {
        if (!IsJoint || string.IsNullOrWhiteSpace(JointMemberGlobalIds)) return [];

        return JointMemberGlobalIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty && g != MemberGlobalId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Returns all member GlobalIds associated with this account (primary owner + joint members).
    /// </summary>
    public List<Guid> GetAllMemberGuids()
    {
        var list = new List<Guid> { MemberGlobalId };
        list.AddRange(GetJointMemberGuids());
        return list.Distinct().ToList();
    }

    // Navigation
    public ICollection<Expense> Expenses { get; set; } = [];
}
