using MallSense.Domain.Common;

namespace MallSense.Domain.Entities;

/// <summary>
/// An anonymous, structured shopper interaction. These events are deliberately kept
/// separate from chat transcripts so ordinary browsing does not become fake conversation turns.
/// </summary>
public class ShopperBehaviorEvent : BaseEntity
{
    public string SessionId { get; set; } = string.Empty;
    public string SurfaceContext { get; set; } = "DealsEvents";
    public string EventType { get; set; } = string.Empty;
    public string? Query { get; set; }
    public string? SelectedFilter { get; set; }
    public int? ResultCount { get; set; }
    public Guid? TargetKnowledgeSourceId { get; set; }
    public Guid? TargetTenantId { get; set; }
    public string? TargetTitle { get; set; }
    public string RelatedTenantIds { get; set; } = string.Empty;
    public string RelatedKnowledgeSourceIds { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
