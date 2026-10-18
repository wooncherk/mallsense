using System.ComponentModel.DataAnnotations;

namespace MallSense.Application.DTOs;

public record KnowledgeActorDto(string Role, Guid? TenantId, string PrincipalKey);

public record KnowledgeCollectionDto(
    Guid Id, string Name, string Description, string ScopeType, Guid? ScopeReferenceId, string Visibility,
    IReadOnlyList<string> AllowedActions);

public record KnowledgeSourceDto(
    Guid Id, Guid CollectionId, string CollectionName, string Title, string SourceKind, string MimeType,
    string? StorageReference, string Content, string AiSummary, IReadOnlyList<string> SuggestedQuestions,
    IReadOnlyList<string> Claims, string Status, bool IsSimulated, string? ModelName, string PromptVersion,
    DateTime CreatedAtUtc, IReadOnlyList<KnowledgeTurnDto> Conversation,
    IReadOnlyList<KnowledgeSuggestionDto> Suggestions, IReadOnlyList<KnowledgeAttachmentDto> Attachments);

public record KnowledgeTurnDto(int Sequence, string Role, string Message, DateTime CreatedAtUtc);
public record KnowledgeSuggestionDto(Guid Id, string Instruction, string BaseMarkdown, string ProposedMarkdown,
    string Rationale, IReadOnlyList<string> Changes, IReadOnlyList<string> EvidenceNames, string Status,
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecidedBy);
public record KnowledgeAttachmentDto(Guid Id, string FileName, string MimeType, DateTime CreatedAtUtc);

public record ImportKnowledgeRequest(
    Guid CollectionId,
    [Required, StringLength(200)] string Title,
    [Required] string SourceKind,
    string? MimeType,
    string? Content,
    string? StorageReference,
    string? Base64Data,
    string ActionMode = "AskFirst");

public record InvestigateKnowledgeRequest([Required] string Message, string ActionMode = "AskFirst");
public record UpdateKnowledgeSourceRequest(
    [Required, StringLength(200)] string Title,
    Guid? CollectionId,
    string? SourceKind,
    string? Content,
    string? StorageReference,
    string? AiInstruction,
    string ActionMode = "AskFirst");
public record UpdateKnowledgeStatusRequest([Required] string Status);
public record CreateKnowledgeSuggestionRequest([Required] string Instruction);
public record KnowledgeAiProgressDto(string Stage, string Message, int Percent);
public record DecideKnowledgeSuggestionRequest(string ActionMode = "AskFirst");
public record KnowledgeSearchResultDto(Guid SourceId, string Title, string Snippet, string CollectionName, double Score, string Citation);
public record KnowledgeSearchResponse(string Query, IReadOnlyList<KnowledgeSearchResultDto> Results, string Answer);
public record PublicKnowledgeSourceDto(Guid Id, string Title, string SourceKind, string MarkdownContent,
    string CollectionName, DateTime UpdatedAtUtc);

public record FloorPlanDraftRequest(int FloorNumber, [Required] string FloorName, string ActionMode = "AskFirst");
public record FloorPlanNodeDto(string Key, string Label, string Type, double X, double Y, string? Description, double Confidence);
public record FloorPlanEdgeDto(string From, string To, double DistanceMeters, bool Accessible, bool Bidirectional, string DirectionHint);
public record UpdateFloorPlanDraftRequest(
    IReadOnlyList<FloorPlanNodeDto> Nodes,
    IReadOnlyList<FloorPlanEdgeDto> Edges,
    IReadOnlyList<string>? Warnings);
public record FloorPlanDraftDto(Guid Id, Guid SourceId, int FloorNumber, string FloorName,
    IReadOnlyList<FloorPlanNodeDto> Nodes, IReadOnlyList<FloorPlanEdgeDto> Edges,
    IReadOnlyList<string> Warnings, int ConfidencePercentage, string Status);
