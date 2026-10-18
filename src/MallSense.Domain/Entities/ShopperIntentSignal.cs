using MallSense.Domain.Common;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Entities;

public class ShopperIntentSignal : BaseEntity
{
    public string SessionId { get; set; } = string.Empty;
    public IntentCategory IntentCategory { get; set; } = IntentCategory.GeneralBrowsing;
    public string RawQuery { get; set; } = string.Empty;
    public string? TargetRecipient { get; set; } // e.g., "mother", "7-year-old nephew"
    public string CategoryInterests { get; set; } = string.Empty; // e.g., "toys, books"
    public string ExcludedCategories { get; set; } = string.Empty; // e.g., "fashion"
    public string Preferences { get; set; } = string.Empty; // e.g., "cooking, quiet"
    public string? BudgetSignal { get; set; } // "budget", "mid", "luxury"
    public int? FloorLocation { get; set; }
    public string RecommendedTenantIds { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
