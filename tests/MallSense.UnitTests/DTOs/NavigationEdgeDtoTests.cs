using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Domain.Entities;

namespace MallSense.UnitTests.DTOs;

public class NavigationEdgeDtoTests
{
    [Fact]
    public void FromEntity_ProducesSerializableFlatShape_WhenEntityGraphContainsCycle()
    {
        var source = new NavigationNode { Id = Guid.NewGuid(), Label = "West Car Park Mall Entrance" };
        var target = new NavigationNode { Id = Guid.NewGuid(), Label = "Retail Concourse" };
        var edge = new NavigationEdge
        {
            Id = Guid.NewGuid(),
            SourceNodeId = source.Id,
            TargetNodeId = target.Id,
            SourceNode = source,
            TargetNode = target,
            DistanceMeters = 4.5,
            DirectionHint = "east",
            IsAccessible = true,
            IsBidirectional = true
        };
        source.OutgoingEdges.Add(edge);
        target.IncomingEdges.Add(edge);

        var dto = NavigationEdgeDto.FromEntity(edge);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains($"\"id\":\"{edge.Id}\"", json);
        Assert.Contains("\"distanceMeters\":4.5", json);
        Assert.DoesNotContain("\"sourceNode\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"outgoingEdges\":", json, StringComparison.OrdinalIgnoreCase);
    }
}
