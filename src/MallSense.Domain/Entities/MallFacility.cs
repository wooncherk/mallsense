using MallSense.Domain.Common;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Entities;

public class MallFacility : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public NodeType FacilityType { get; set; } = NodeType.Restroom;
    public Guid NavigationNodeId { get; set; }
    public string? Description { get; set; }
    public bool IsAccessible { get; set; } = true;

    // Navigation property
    public virtual NavigationNode NavigationNode { get; set; } = null!;
}
