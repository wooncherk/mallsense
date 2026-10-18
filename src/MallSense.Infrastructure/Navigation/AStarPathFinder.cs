using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;

namespace MallSense.Infrastructure.Navigation;

public class AStarPathFinder : IPathFindingService
{
    public CalculatedRoute FindPath(
        IEnumerable<NavigationNode> nodes,
        IEnumerable<NavigationEdge> edges,
        Guid startNodeId,
        Guid targetNodeId,
        bool accessibleOnly = false)
    {
        var nodeDict = nodes.ToDictionary(n => n.Id);
        var adjacency = new Dictionary<Guid, List<NavigationEdge>>();

        foreach (var node in nodeDict.Values)
        {
            adjacency[node.Id] = new List<NavigationEdge>();
        }

        foreach (var edge in edges)
        {
            if (adjacency.ContainsKey(edge.SourceNodeId))
            {
                adjacency[edge.SourceNodeId].Add(edge);
            }

            if (edge.IsBidirectional && adjacency.ContainsKey(edge.TargetNodeId))
            {
                var reverseEdge = new NavigationEdge
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = edge.TargetNodeId,
                    TargetNodeId = edge.SourceNodeId,
                    DistanceMeters = edge.DistanceMeters,
                    DirectionHint = InvertDirection(edge.DirectionHint),
                    FloorChange = -edge.FloorChange,
                    IsAccessible = edge.IsAccessible,
                    IsBidirectional = true
                };
                adjacency[edge.TargetNodeId].Add(reverseEdge);
            }
        }

        if (!nodeDict.ContainsKey(startNodeId) || !nodeDict.ContainsKey(targetNodeId))
        {
            return new CalculatedRoute(Array.Empty<NavigationNode>(), Array.Empty<NavigationStep>(), 0, 0, false);
        }

        if (startNodeId == targetNodeId)
        {
            var node = nodeDict[startNodeId];
            return new CalculatedRoute(new[] { node }, Array.Empty<NavigationStep>(), 0, 0, true);
        }

        var targetNode = nodeDict[targetNodeId];
        var openSet = new PriorityQueue<Guid, double>();
        var gScore = new Dictionary<Guid, double>();
        var cameFrom = new Dictionary<Guid, (Guid PreviousNodeId, NavigationEdge Edge)>();

        foreach (var id in nodeDict.Keys)
        {
            gScore[id] = double.PositiveInfinity;
        }

        gScore[startNodeId] = 0;
        openSet.Enqueue(startNodeId, Heuristic(nodeDict[startNodeId], targetNode));

        var visited = new HashSet<Guid>();

        while (openSet.Count > 0)
        {
            var currentId = openSet.Dequeue();

            if (currentId == targetNodeId)
            {
                return ReconstructRoute(currentId, cameFrom, nodeDict);
            }

            if (!visited.Add(currentId))
            {
                continue;
            }

            if (!adjacency.TryGetValue(currentId, out var outgoingEdges))
            {
                continue;
            }

            foreach (var edge in outgoingEdges)
            {
                if (accessibleOnly && !edge.IsAccessible)
                {
                    continue; // Skip escalators or non-accessible stairs
                }

                var neighborId = edge.TargetNodeId;
                if (!nodeDict.ContainsKey(neighborId))
                {
                    continue;
                }

                var tentativeGScore = gScore[currentId] + edge.DistanceMeters;

                // Add penalty for floor change to favor same-floor movement when equal
                if (edge.FloorChange != 0)
                {
                    tentativeGScore += 5.0;
                }

                if (tentativeGScore < gScore[neighborId])
                {
                    cameFrom[neighborId] = (currentId, edge);
                    gScore[neighborId] = tentativeGScore;
                    var fScore = tentativeGScore + Heuristic(nodeDict[neighborId], targetNode);
                    openSet.Enqueue(neighborId, fScore);
                }
            }
        }

        return new CalculatedRoute(Array.Empty<NavigationNode>(), Array.Empty<NavigationStep>(), 0, 0, false);
    }

    private static CalculatedRoute ReconstructRoute(
        Guid targetNodeId,
        Dictionary<Guid, (Guid PreviousNodeId, NavigationEdge Edge)> cameFrom,
        Dictionary<Guid, NavigationNode> nodeDict)
    {
        var pathNodes = new List<NavigationNode>();
        var steps = new List<NavigationStep>();
        var currentId = targetNodeId;

        pathNodes.Add(nodeDict[currentId]);
        double totalDistance = 0;
        int totalFloorChanges = 0;

        while (cameFrom.TryGetValue(currentId, out var parent))
        {
            var prevNode = nodeDict[parent.PreviousNodeId];
            var currentNode = nodeDict[currentId];
            var edge = parent.Edge;

            totalDistance += edge.DistanceMeters;
            if (edge.FloorChange != 0)
            {
                totalFloorChanges += Math.Abs(edge.FloorChange);
            }

            steps.Insert(0, new NavigationStep(
                prevNode.Id,
                prevNode.Label,
                currentNode.Id,
                currentNode.Label,
                edge.DistanceMeters,
                edge.DirectionHint,
                edge.FloorChange,
                prevNode.FloorNumber
            ));

            pathNodes.Insert(0, prevNode);
            currentId = parent.PreviousNodeId;
        }

        return new CalculatedRoute(pathNodes, steps, totalDistance, totalFloorChanges, true);
    }

    private static double Heuristic(NavigationNode a, NavigationNode b)
    {
        var dx = a.CoordinateX - b.CoordinateX;
        var dy = a.CoordinateY - b.CoordinateY;
        var spatialDist = Math.Sqrt(dx * dx + dy * dy);
        var floorDiff = Math.Abs(a.FloorNumber - b.FloorNumber) * 20.0;
        return spatialDist + floorDiff;
    }

    private static string InvertDirection(string direction) => direction switch
    {
        "turn_left" => "turn_right",
        "turn_right" => "turn_left",
        "take_escalator_up" => "take_escalator_down",
        "take_escalator_down" => "take_escalator_up",
        _ => direction
    };
}
