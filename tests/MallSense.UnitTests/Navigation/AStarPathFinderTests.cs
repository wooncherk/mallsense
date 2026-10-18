using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Infrastructure.Navigation;
using Xunit;

namespace MallSense.UnitTests.Navigation;

public class AStarPathFinderTests
{
    [Fact]
    public void FindPath_SingleFloor_ReturnsCorrectShortestRoute()
    {
        // Arrange
        var nodeA = new NavigationNode { Id = Guid.NewGuid(), Label = "Entrance A", FloorNumber = 1, CoordinateX = 0, CoordinateY = 0 };
        var nodeB = new NavigationNode { Id = Guid.NewGuid(), Label = "Junction 1", FloorNumber = 1, CoordinateX = 10, CoordinateY = 0 };
        var nodeC = new NavigationNode { Id = Guid.NewGuid(), Label = "Shop C", FloorNumber = 1, CoordinateX = 20, CoordinateY = 0 };

        var edge1 = new NavigationEdge { Id = Guid.NewGuid(), SourceNodeId = nodeA.Id, TargetNodeId = nodeB.Id, DistanceMeters = 10, DirectionHint = "walk_straight", IsBidirectional = true, IsAccessible = true };
        var edge2 = new NavigationEdge { Id = Guid.NewGuid(), SourceNodeId = nodeB.Id, TargetNodeId = nodeC.Id, DistanceMeters = 10, DirectionHint = "turn_left", IsBidirectional = true, IsAccessible = true };

        var finder = new AStarPathFinder();

        // Act
        var result = finder.FindPath(new[] { nodeA, nodeB, nodeC }, new[] { edge1, edge2 }, nodeA.Id, nodeC.Id);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(20, result.TotalDistanceMeters);
        Assert.Equal(0, result.FloorChanges);
        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("Entrance A", result.Nodes[0].Label);
        Assert.Equal("Shop C", result.Nodes[2].Label);
    }

    [Fact]
    public void FindPath_MultiFloor_NavigatesThroughEscalator()
    {
        // Arrange: Floor 1 -> Escalator -> Floor 2
        var l1Start = new NavigationNode { Id = Guid.NewGuid(), Label = "L1 Start", FloorNumber = 1, CoordinateX = 0, CoordinateY = 0 };
        var l1Esc = new NavigationNode { Id = Guid.NewGuid(), Label = "L1 Escalator", FloorNumber = 1, CoordinateX = 10, CoordinateY = 0 };
        var l2Esc = new NavigationNode { Id = Guid.NewGuid(), Label = "L2 Escalator Arrival", FloorNumber = 2, CoordinateX = 10, CoordinateY = 0 };
        var l2Dest = new NavigationNode { Id = Guid.NewGuid(), Label = "L2 Popular Bookstore", FloorNumber = 2, CoordinateX = 25, CoordinateY = 0 };

        var e1 = new NavigationEdge { Id = Guid.NewGuid(), SourceNodeId = l1Start.Id, TargetNodeId = l1Esc.Id, DistanceMeters = 10, DirectionHint = "walk_straight", IsBidirectional = true };
        var e2 = new NavigationEdge { Id = Guid.NewGuid(), SourceNodeId = l1Esc.Id, TargetNodeId = l2Esc.Id, DistanceMeters = 12, DirectionHint = "take_escalator_up", FloorChange = 1, IsAccessible = false, IsBidirectional = true };
        var e3 = new NavigationEdge { Id = Guid.NewGuid(), SourceNodeId = l2Esc.Id, TargetNodeId = l2Dest.Id, DistanceMeters = 15, DirectionHint = "turn_right", IsBidirectional = true };

        var nodes = new[] { l1Start, l1Esc, l2Esc, l2Dest };
        var edges = new[] { e1, e2, e3 };
        var finder = new AStarPathFinder();

        // Act
        var result = finder.FindPath(nodes, edges, l1Start.Id, l2Dest.Id);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(37, result.TotalDistanceMeters);
        Assert.Equal(1, result.FloorChanges);
        Assert.Contains(result.Steps, s => s.DirectionHint == "take_escalator_up");
    }

    [Fact]
    public void FindPath_AccessibleOnly_SkipsEscalatorAndUsesLift()
    {
        // Arrange: Path with both Escalator (non-accessible) and Lift (accessible)
        var start = new NavigationNode { Id = Guid.NewGuid(), Label = "Start", FloorNumber = 1, CoordinateX = 0, CoordinateY = 0 };
        var escL1 = new NavigationNode { Id = Guid.NewGuid(), Label = "Escalator L1", FloorNumber = 1, CoordinateX = 5, CoordinateY = 0 };
        var escL2 = new NavigationNode { Id = Guid.NewGuid(), Label = "Escalator L2", FloorNumber = 2, CoordinateX = 5, CoordinateY = 0 };

        var liftL1 = new NavigationNode { Id = Guid.NewGuid(), Label = "Lift L1", FloorNumber = 1, CoordinateX = 20, CoordinateY = 0 };
        var liftL2 = new NavigationNode { Id = Guid.NewGuid(), Label = "Lift L2", FloorNumber = 2, CoordinateX = 20, CoordinateY = 0 };

        var dest = new NavigationNode { Id = Guid.NewGuid(), Label = "Dest L2", FloorNumber = 2, CoordinateX = 25, CoordinateY = 0 };

        var edges = new List<NavigationEdge>
        {
            // Shorter escalator route (non-accessible)
            new() { Id = Guid.NewGuid(), SourceNodeId = start.Id, TargetNodeId = escL1.Id, DistanceMeters = 5, IsAccessible = true, IsBidirectional = true },
            new() { Id = Guid.NewGuid(), SourceNodeId = escL1.Id, TargetNodeId = escL2.Id, DistanceMeters = 8, FloorChange = 1, IsAccessible = false, IsBidirectional = true },
            new() { Id = Guid.NewGuid(), SourceNodeId = escL2.Id, TargetNodeId = dest.Id, DistanceMeters = 20, IsAccessible = true, IsBidirectional = true },

            // Slightly longer lift route (wheelchair accessible)
            new() { Id = Guid.NewGuid(), SourceNodeId = start.Id, TargetNodeId = liftL1.Id, DistanceMeters = 20, IsAccessible = true, IsBidirectional = true },
            new() { Id = Guid.NewGuid(), SourceNodeId = liftL1.Id, TargetNodeId = liftL2.Id, DistanceMeters = 10, FloorChange = 1, IsAccessible = true, IsBidirectional = true },
            new() { Id = Guid.NewGuid(), SourceNodeId = liftL2.Id, TargetNodeId = dest.Id, DistanceMeters = 5, IsAccessible = true, IsBidirectional = true }
        };

        var allNodes = new[] { start, escL1, escL2, liftL1, liftL2, dest };
        var finder = new AStarPathFinder();

        // Act with accessibleOnly = true
        var result = finder.FindPath(allNodes, edges, start.Id, dest.Id, accessibleOnly: true);

        // Assert
        Assert.True(result.Found);
        Assert.DoesNotContain(result.Nodes, n => n.Label.Contains("Escalator"));
        Assert.Contains(result.Nodes, n => n.Label.Contains("Lift"));
    }
}
