using MallSense.Domain.Common;

namespace MallSense.Domain.Entities;

/// <summary>Role assigned to an ApplicationUser in the MallSense system.</summary>
public enum UserRole
{
    /// <summary>Anonymous visitor — frictionless, auto-provisioned on first visit.</summary>
    Guest = 0,
    /// <summary>Retail merchant staff — scoped strictly to their own Tenant.</summary>
    TenantStaff = 1,
    /// <summary>Mall management / operator — global access to all tenants and analytics.</summary>
    Admin = 2,
    /// <summary>Registered mall customer with an email/password account.</summary>
    Shopper = 3
}

/// <summary>
/// Unified user identity for guest visitors, registered shoppers, tenant staff, and mall admins.
/// Guests are auto-provisioned with no credentials; all other roles use email + password.
/// </summary>
public class ApplicationUser : BaseEntity
{
    public string UserName { get; set; } = string.Empty;  // email or guest_xxxxxxxx
    public string? Email { get; set; }                     // null for Guest sessions
    public string? PasswordHash { get; set; }              // null for Guest sessions
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Guest;
    public Guid? TenantId { get; set; }                    // set only for TenantStaff
    public DateTime LastActiveAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Tenant? Tenant { get; set; }
    public virtual ICollection<ShopperConversation> Conversations { get; set; } = new List<ShopperConversation>();
}
