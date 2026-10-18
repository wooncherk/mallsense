using MallSense.Domain.Common;

namespace MallSense.Domain.Entities;

public class NavigationEdge : BaseEntity
{
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public double DistanceMeters { get; set; }
    public string DirectionHint { get; set; } = "straight"; // straight, turn_left, turn_right, take_escalator_up, take_escalator_down, take_lift
    public int FloorChange { get; set; } = 0; // 0: same floor, 1: up 1 level, -1: down 1 level
    public bool IsAccessible { get; set; } = true; // false for escalators/stairs
    public bool IsBidirectional { get; set; } = true;

    // Navigation properties
    public virtual NavigationNode SourceNode { get; set; } = null!;
    public virtual NavigationNode TargetNode { get; set; } = null!;
}
