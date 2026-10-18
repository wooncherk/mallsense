using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

public class NavigationService : INavigationService
{
    private readonly INavigationGraphRepository _graphRepository;
    private readonly IPathFindingService _pathFindingService;
    private readonly IGeminiClient _geminiClient;

    public NavigationService(
        INavigationGraphRepository graphRepository,
        IPathFindingService pathFindingService,
        IGeminiClient geminiClient)
    {
        _graphRepository = graphRepository;
        _pathFindingService = pathFindingService;
        _geminiClient = geminiClient;
    }

    public async Task<RouteResponse> CalculateRouteAsync(
        RouteRequest request,
        CancellationToken cancellationToken = default)
    {
        var (route, error) = await FindRouteAsync(request, cancellationToken);
        if (route == null)
        {
            return new RouteResponse(false, 0, 0, error!, Array.Empty<RouteStepDto>(), Array.Empty<Guid>());
        }

        var stepDtos = route.Steps.Select(s => new RouteStepDto(
            FromNodeId: s.FromNodeId,
            FromNodeLabel: s.FromNodeLabel,
            ToNodeId: s.ToNodeId,
            ToNodeLabel: s.ToNodeLabel,
            DistanceMeters: s.DistanceMeters,
            DirectionHint: s.DirectionHint,
            FloorChange: s.FloorChange,
            CurrentFloor: s.CurrentFloor,
            Instruction: FormatPhysicalInstruction(s)
        )).ToList();

        return new RouteResponse(
            Found: true,
            TotalDistanceMeters: Math.Round(route.TotalDistanceMeters, 1),
            FloorChanges: route.FloorChanges,
            ConversationalDirections: GenerateTemplateDirections(route),
            Steps: stepDtos,
            PathNodeIds: route.Nodes.Select(n => n.Id).ToList()
        );
    }

    public async Task<RouteOverviewResponse> GenerateRouteOverviewAsync(
        RouteRequest request,
        CancellationToken cancellationToken = default)
    {
        var (route, error) = await FindRouteAsync(request, cancellationToken);
        if (route == null) return new RouteOverviewResponse(false, error!);

        var directions = await TranslateRouteToLandmarkDirectionsAsync(route, cancellationToken);
        return new RouteOverviewResponse(true, directions);
    }

    private async Task<(CalculatedRoute? Route, string? Error)> FindRouteAsync(
        RouteRequest request,
        CancellationToken cancellationToken)
    {
        var nodes = await _graphRepository.GetAllNodesAsync(cancellationToken);
        var edges = await _graphRepository.GetAllEdgesAsync(cancellationToken);

        Guid targetNodeId;
        if (request.DestinationNodeId.HasValue)
        {
            targetNodeId = request.DestinationNodeId.Value;
        }
        else if (request.DestinationTenantId.HasValue)
        {
            var targetNode = await _graphRepository.GetNodeByTenantIdAsync(request.DestinationTenantId.Value, cancellationToken);
            if (targetNode == null) return (null, "Destination node not found in mall graph.");
            targetNodeId = targetNode.Id;
        }
        else
        {
            return (null, "No destination specified.");
        }

        var route = _pathFindingService.FindPath(nodes, edges, request.StartNodeId, targetNodeId, request.AccessibleOnly);
        return route.Found
            ? (route, null)
            : (null, "No walkable path found between the specified locations.");
    }

    public async Task<LocationRecoveryResponse> RecoverLocationAsync(
        LocationRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var allNodes = await _graphRepository.GetAllNodesAsync(cancellationToken);

        // Case 1: Photo uploaded -> Gemini Vision analysis
        if (!string.IsNullOrWhiteSpace(request.PhotoBase64))
        {
            try
            {
                var imageBytes = Convert.FromBase64String(request.PhotoBase64);
                var prompt = "Identify visible shops, signage, floor level markers, and landmarks in this mall photo. Return JSON with detected_landmarks, estimated_location, estimated_floor, confidence_percentage, confirmation_question.";
                var visionJson = await _geminiClient.AnalyzeImageAsync(imageBytes, request.MimeType ?? "image/jpeg", prompt, cancellationToken);

                using var doc = JsonDocument.Parse(visionJson);
                var root = doc.RootElement;

                var landmarks = new List<string>();
                if (root.TryGetProperty("detected_landmarks", out var dl) && dl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dl.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) landmarks.Add(s);
                    }
                }

                var matchedNode = MatchLandmarksToNode(allNodes, landmarks);

                return new LocationRecoveryResponse(
                    EstimatedNodeId: matchedNode?.Id,
                    EstimatedLocationLabel: matchedNode?.Label ?? (root.TryGetProperty("estimated_location", out var el) ? el.GetString() ?? "Near detected landmarks" : "Level 2 Junction"),
                    EstimatedFloor: matchedNode?.FloorNumber ?? 2,
                    ConfidencePercentage: root.TryGetProperty("confidence_percentage", out var cp) ? cp.GetInt32() : 90,
                    DetectedClues: landmarks,
                    ConfirmationQuestion: root.TryGetProperty("confirmation_question", out var cq) ? cq.GetString() ?? "I think you're on Level 2. Can you see Guardian Pharmacy?" : "I think you're on Level 2. Can you see Guardian Pharmacy?"
                );
            }
            catch
            {
                // Fallback
            }
        }

        // Case 2: Text clue or list of visible landmarks
        var clues = request.VisibleLandmarks?.ToList() ?? new List<string>();
        if (!string.IsNullOrWhiteSpace(request.ClueDescription))
        {
            var parts = request.ClueDescription.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            clues.AddRange(parts);
        }

        var candidateNode = MatchLandmarksToNode(allNodes, clues);

        if (candidateNode != null)
        {
            return new LocationRecoveryResponse(
                EstimatedNodeId: candidateNode.Id,
                EstimatedLocationLabel: candidateNode.Label,
                EstimatedFloor: candidateNode.FloorNumber,
                ConfidencePercentage: 88,
                DetectedClues: clues,
                ConfirmationQuestion: $"I think you're near {candidateNode.Label} on Level {candidateNode.FloorNumber}. Is that right?"
            );
        }

        var defaultNode = allNodes.FirstOrDefault(n => n.Label.Contains("Atrium", StringComparison.OrdinalIgnoreCase)) ?? allNodes[0];
        return new LocationRecoveryResponse(
            EstimatedNodeId: defaultNode.Id,
            EstimatedLocationLabel: defaultNode.Label,
            EstimatedFloor: defaultNode.FloorNumber,
            ConfidencePercentage: 65,
            DetectedClues: clues,
            ConfirmationQuestion: $"Are you near the {defaultNode.Label} on Level {defaultNode.FloorNumber}?"
        );
    }

    private static NavigationNode? MatchLandmarksToNode(IReadOnlyList<NavigationNode> nodes, IReadOnlyList<string> landmarks)
    {
        foreach (var landmark in landmarks)
        {
            var node = nodes.FirstOrDefault(n =>
                n.Label.Contains(landmark, StringComparison.OrdinalIgnoreCase) ||
                (n.Description != null && n.Description.Contains(landmark, StringComparison.OrdinalIgnoreCase)));

            if (node != null) return node;
        }
        return null;
    }

    private async Task<string> TranslateRouteToLandmarkDirectionsAsync(
        CalculatedRoute route,
        CancellationToken cancellationToken)
    {
        var nodeContext = route.Nodes.ToDictionary(n => n.Id);
        var stepsSummary = string.Join("\n", route.Steps.Select((s, index) =>
        {
            nodeContext.TryGetValue(s.FromNodeId, out var from);
            nodeContext.TryGetValue(s.ToNodeId, out var to);
            return $"{index + 1}. {s.FromNodeLabel} [Level {s.CurrentFloor}; {from?.Description ?? "no extra landmark note"}] to {s.ToNodeLabel} [{to?.Description ?? "no extra landmark note"}] — {s.DirectionHint}, {s.DistanceMeters}m, floor change {s.FloorChange}.";
        }));

        var prompt = $$"""
            Convert the following admin-maintained, deterministic navigation graph route into friendly, natural landmark-based directions for a shopping mall visitor.
            The graph is the sole source of truth. Preserve the step order, floors, accessibility route, distances and named landmarks. Never add a shop, connector, turn or distance that is not supplied.
            Return only concise shopper instructions; do not mention nodes, edges, graphs, coordinates, admin tools, or internal IDs.

            Route steps:
            {{stepsSummary}}
            """;

        try
        {
            var text = await _geminiClient.GenerateContentAsync(prompt, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        catch
        {
            // Fallback template
        }

        return GenerateTemplateDirections(route);
    }

    private static string GenerateTemplateDirections(CalculatedRoute route)
    {
        var parts = new List<string>();
        foreach (var step in route.Steps)
        {
            parts.Add(FormatPhysicalInstruction(step));
        }
        return string.Join(" ", parts);
    }

    private static string FormatPhysicalInstruction(NavigationStep step)
    {
        if (step.FloorChange > 0)
        {
            return $"Take the {step.FromNodeLabel} up to Level {step.CurrentFloor + step.FloorChange}.";
        }
        if (step.FloorChange < 0)
        {
            return $"Take the {step.FromNodeLabel} down to Level {step.CurrentFloor + step.FloorChange}.";
        }

        return step.DirectionHint switch
        {
            "turn_left" => $"Turn left and walk {step.DistanceMeters}m towards {step.ToNodeLabel}.",
            "turn_right" => $"Turn right and walk {step.DistanceMeters}m towards {step.ToNodeLabel}.",
            "walk_straight" => $"Continue straight for {step.DistanceMeters}m towards {step.ToNodeLabel}.",
            _ => $"Walk {step.DistanceMeters}m towards {step.ToNodeLabel}."
        };
    }
}
