using MallSense.Domain.Entities;
using MallSense.Domain.Enums;

namespace MallSense.Application.DTOs;

public record NavigationStep(
    Guid FromNodeId,
    string FromNodeLabel,
    Guid ToNodeId,
    string ToNodeLabel,
    double DistanceMeters,
    string DirectionHint,
    int FloorChange,
    int CurrentFloor
);

public record CalculatedRoute(
    IReadOnlyList<NavigationNode> Nodes,
    IReadOnlyList<NavigationStep> Steps,
    double TotalDistanceMeters,
    int FloorChanges,
    bool Found
);

public record RouteRequest(
    Guid StartNodeId,
    Guid? DestinationTenantId,
    Guid? DestinationNodeId,
    bool AccessibleOnly = false
);

public record RouteStepDto(
    Guid FromNodeId,
    string FromNodeLabel,
    Guid ToNodeId,
    string ToNodeLabel,
    double DistanceMeters,
    string DirectionHint,
    int FloorChange,
    int CurrentFloor,
    string Instruction,
    string? VisibleStoreName
);

public record RouteResponse(
    bool Found,
    double TotalDistanceMeters,
    int FloorChanges,
    string ConversationalDirections,
    IReadOnlyList<RouteStepDto> Steps,
    IReadOnlyList<Guid> PathNodeIds
);

public record RouteOverviewResponse(
    bool Found,
    string ConversationalDirections
);

public record LocationRecoveryRequest(
    string? ClueDescription,
    IReadOnlyList<string>? VisibleLandmarks,
    string? PhotoBase64,
    string? MimeType
);

public record LocationRecoveryResponse(
    Guid? EstimatedNodeId,
    string EstimatedLocationLabel,
    int? EstimatedFloor,
    int ConfidencePercentage,
    IReadOnlyList<string> DetectedClues,
    string ConfirmationQuestion
);

public record SaveNavigationNodeRequest(
    int FloorNumber,
    string Label,
    NodeType NodeType,
    double CoordinateX,
    double CoordinateY,
    string? Description,
    Guid? TenantId
);

public record SaveNavigationEdgeRequest(
    Guid SourceNodeId,
    Guid TargetNodeId,
    double DistanceMeters,
    string DirectionHint,
    int FloorChange,
    bool IsAccessible,
    bool IsBidirectional
);

/// <summary>
/// Flat API representation of a navigation node. Domain navigation properties are excluded because
/// assigning a tenant can populate the bidirectional Tenant/NavigationNode relationship in EF.
/// </summary>
public record NavigationNodeDto(
    Guid Id,
    int FloorNumber,
    string Label,
    NodeType NodeType,
    double CoordinateX,
    double CoordinateY,
    string? Description,
    Guid? TenantId)
{
    public static NavigationNodeDto FromEntity(NavigationNode node) => new(
        node.Id,
        node.FloorNumber,
        node.Label,
        node.NodeType,
        node.CoordinateX,
        node.CoordinateY,
        node.Description,
        node.TenantId);
}

/// <summary>
/// Flat API representation of a graph connection. Domain navigation properties are intentionally
/// excluded because EF relationship fix-up can populate SourceNode/TargetNode with circular graphs.
/// </summary>
public record NavigationEdgeDto(
    Guid Id,
    Guid SourceNodeId,
    Guid TargetNodeId,
    double DistanceMeters,
    string DirectionHint,
    int FloorChange,
    bool IsAccessible,
    bool IsBidirectional)
{
    public static NavigationEdgeDto FromEntity(NavigationEdge edge) => new(
        edge.Id,
        edge.SourceNodeId,
        edge.TargetNodeId,
        edge.DistanceMeters,
        edge.DirectionHint,
        edge.FloorChange,
        edge.IsAccessible,
        edge.IsBidirectional);
}

public record NavigationGraphValidationDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

public record NavigationLocationDto(
    Guid Id,
    int FloorNumber,
    string Label,
    NodeType NodeType,
    string? Description,
    Guid? TenantId
);

public record NavigationAiTurnDto(string Role, string Message);
public record NavigationAiDraftNodeDto(string Key, int FloorNumber, string Label, string Type, double X, double Y, string? Description, double Confidence);
public record NavigationAiDraftEdgeDto(string From, string To, double DistanceMeters, bool Accessible, bool Bidirectional, string DirectionHint);
public record NavigationAiDraftRequest(
    int FloorNumber,
    string? Message,
    IReadOnlyList<NavigationAiTurnDto>? Conversation,
    IReadOnlyList<NavigationAiDraftNodeDto>? CurrentNodes,
    IReadOnlyList<NavigationAiDraftEdgeDto>? CurrentEdges
);
public record NavigationAiEvidenceDto(string FileName, string MimeType, byte[] Bytes);
public record NavigationAiDraftDto(
    string AssistantMessage,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> ClarificationQuestions,
    IReadOnlyList<NavigationAiDraftNodeDto> Nodes,
    IReadOnlyList<NavigationAiDraftEdgeDto> Edges,
    IReadOnlyList<string> Warnings,
    int ConfidencePercentage
);
