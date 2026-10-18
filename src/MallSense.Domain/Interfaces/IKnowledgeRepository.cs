using MallSense.Domain.Entities;

namespace MallSense.Domain.Interfaces;

public interface IKnowledgeRepository
{
    Task<IReadOnlyList<KnowledgeCollection>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeCollection?> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSource>> GetSourcesAsync(IEnumerable<Guid> collectionIds, CancellationToken cancellationToken = default);
    Task<KnowledgeSource?> GetSourceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KnowledgeSource> AddSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
    Task UpdateSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
    Task DeleteSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
    Task AddSuggestionAsync(KnowledgeSuggestion suggestion, IEnumerable<KnowledgeSourceAttachment> attachments, CancellationToken cancellationToken = default);
    Task UpdateSuggestionAsync(KnowledgeSuggestion suggestion, CancellationToken cancellationToken = default);
    Task AddTurnAsync(KnowledgeConversationTurn turn, CancellationToken cancellationToken = default);
    Task<FloorPlanDraft> AddFloorPlanDraftAsync(FloorPlanDraft draft, CancellationToken cancellationToken = default);
    Task<FloorPlanDraft?> GetFloorPlanDraftAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateFloorPlanDraftAsync(FloorPlanDraft draft, CancellationToken cancellationToken = default);
    Task AddNavigationGraphAsync(IEnumerable<NavigationNode> nodes, IEnumerable<NavigationEdge> edges, CancellationToken cancellationToken = default);
}
