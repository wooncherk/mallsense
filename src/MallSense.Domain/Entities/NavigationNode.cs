using MallSense.Domain.Common;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Entities;

public class NavigationNode : BaseEntity
{
    public int FloorNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public NodeType NodeType { get; set; } = NodeType.Junction;
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public string? Description { get; set; }
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
    public virtual ICollection<NavigationEdge> OutgoingEdges { get; set; } = new List<NavigationEdge>();
    public virtual ICollection<NavigationEdge> IncomingEdges { get; set; } = new List<NavigationEdge>();
}
