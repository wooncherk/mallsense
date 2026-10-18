using MallSense.Application.DTOs;

namespace MallSense.Application.Interfaces;

public interface IKnowledgeService
{
    Task<IReadOnlyList<KnowledgeCollectionDto>> GetCollectionsAsync(KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSourceDto>> GetSourcesAsync(KnowledgeActorDto actor, Guid? collectionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicKnowledgeSourceDto>> GetPublishedSourcesAsync(string? kind, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> ImportAsync(ImportKnowledgeRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> UpdateSourceAsync(Guid sourceId, UpdateKnowledgeSourceRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> UpdateStatusAsync(Guid sourceId, UpdateKnowledgeStatusRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task DeleteSourceAsync(Guid sourceId, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> CreateSuggestionAsync(Guid sourceId, CreateKnowledgeSuggestionRequest request,
        IReadOnlyList<KnowledgeEvidenceDto> evidence, KnowledgeActorDto actor,
        Func<KnowledgeAiProgressDto, CancellationToken, Task>? progress = null, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> AcceptSuggestionAsync(Guid sourceId, Guid suggestionId, DecideKnowledgeSuggestionRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> RejectSuggestionAsync(Guid sourceId, Guid suggestionId, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> InvestigateAsync(Guid sourceId, InvestigateKnowledgeRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSourceDto> PublishAsync(Guid sourceId, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<KnowledgeSearchResponse> SearchAsync(string query, KnowledgeActorDto actor, bool publishedOnly,
        CancellationToken cancellationToken = default, IReadOnlyCollection<string>? sourceKinds = null,
        bool generateAnswer = true);
    Task<FloorPlanDraftDto> GenerateFloorPlanDraftAsync(Guid sourceId, FloorPlanDraftRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<FloorPlanDraftDto> UpdateFloorPlanDraftAsync(Guid draftId, UpdateFloorPlanDraftRequest request, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
    Task<FloorPlanDraftDto> PublishFloorPlanDraftAsync(Guid draftId, KnowledgeActorDto actor, CancellationToken cancellationToken = default);
}

public record KnowledgeEvidenceDto(string FileName, string MimeType, byte[] Bytes, string? TextContent);
