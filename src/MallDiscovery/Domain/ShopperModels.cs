using System.ComponentModel.DataAnnotations;

namespace MallDiscovery.Domain;

// No account or shopper PII is required for discovery.
public sealed class DiscoverySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallId { get; set; }
    public Guid? CurrentNodeId { get; set; }
    public bool RequiresStepFreeRoute { get; set; }
    public bool IsSimulated { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
}

public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int Sequence { get; set; }
    public MessageRole Role { get; set; }
    [MaxLength(8000)] public required string Content { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// Append a new snapshot when intent changes; never overwrite earlier analytics context.
public sealed class ShopperIntent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid SourceMessageId { get; set; }
    public int Revision { get; set; }
    public IntentKind Kind { get; set; }
    [MaxLength(80)] public string? Recipient { get; set; }
    public int? RecipientAge { get; set; }
    public int? BudgetMinimumMinor { get; set; }
    public int? BudgetMaximumMinor { get; set; }
    public int? AvailableMinutes { get; set; }
    [MaxLength(80)] public string? Occasion { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IntentPreference
{
    public Guid IntentId { get; set; }
    [MaxLength(80)] public required string Value { get; set; }
    public bool IsExcluded { get; set; }
}

public sealed class Recommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IntentId { get; set; }
    public Guid PlaceId { get; set; }
    public Guid? PromotionId { get; set; }
    public int Rank { get; set; }
    [MaxLength(2000)] public required string Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RouteJourney
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid DestinationPlaceId { get; set; }
    public Guid StartingNodeId { get; set; }
    public int Revision { get; set; } = 1;
    public bool RequiresStepFreeRoute { get; set; }
    public RouteStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RouteStep
{
    public Guid RouteJourneyId { get; set; }
    public int Sequence { get; set; }
    public Guid NavigationEdgeId { get; set; }
}

// Structured operational facts, separate from private conversation text.
public sealed class ShopperInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid IntentId { get; set; }
    public Guid? RecommendationId { get; set; }
    public Guid? RouteJourneyId { get; set; }
    public InteractionKind Kind { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
