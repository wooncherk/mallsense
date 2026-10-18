using MallSense.Domain.Common;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Entities;

public class Promotion : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public PromotionType Type { get; set; } = PromotionType.Discount;
    public string OfferSummary { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string TargetAudience { get; set; } = string.Empty;
    public string Occasions { get; set; } = string.Empty;
    public string? DietaryFlags { get; set; }
    public string? PriceRange { get; set; }
    public string? AdvanceOrderRequired { get; set; }
    public string? PosterImageUrl { get; set; }
    public int ReadinessScore { get; set; } = 50; // 0-100%
    public string ReadinessNotes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation property
    public virtual Tenant Tenant { get; set; } = null!;
}
