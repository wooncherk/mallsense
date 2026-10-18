using MallSense.Domain.Common;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Entities;

/// <summary>
/// Represents a retail tenant/merchant leasing a physical unit inside the mall.
/// This entity consolidates the former Store concept with its physical placement attributes.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public StoreCategory Category { get; set; } = StoreCategory.Other;
    public int FloorNumber { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? OpeningHours { get; set; }
    public string? StorefrontImageUrl { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string PriceLevel { get; set; } = "mid"; // budget, mid, premium
    public int? SearchValidationRating { get; set; }
    public string? SearchValidationSummary { get; set; }
    public string? SearchValidationDescriptionHash { get; set; }
    public string? SearchValidationQuestionsJson { get; set; }
    public DateTime? SearchValidationAtUtc { get; set; }
    public Guid? NavigationNodeId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual NavigationNode? NavigationNode { get; set; }
    public virtual ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
    public virtual ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
}
