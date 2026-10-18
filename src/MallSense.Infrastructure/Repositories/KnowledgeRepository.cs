using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Infrastructure.Repositories;

public class KnowledgeRepository(MallDbContext context) : IKnowledgeRepository
{
    public async Task<IReadOnlyList<KnowledgeCollection>> GetCollectionsAsync(CancellationToken cancellationToken = default) =>
        await context.KnowledgeCollections.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<KnowledgeCollection?> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.KnowledgeCollections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<KnowledgeSource>> GetSourcesAsync(IEnumerable<Guid> collectionIds, CancellationToken cancellationToken = default)
    {
        var ids = collectionIds.Distinct().ToArray();
        return await context.KnowledgeSources.AsNoTracking().Include(x => x.Collection).Include(x => x.Conversation)
            .Include(x => x.Suggestions).Include(x => x.Attachments)
            .Where(x => ids.Contains(x.CollectionId)).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<KnowledgeSource?> GetSourceAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.KnowledgeSources.Include(x => x.Collection).Include(x => x.Conversation)
            .Include(x => x.Suggestions).Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<KnowledgeSource> AddSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        context.KnowledgeSources.Add(source);
        await context.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task UpdateSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        source.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        context.KnowledgeSources.Remove(source);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSuggestionAsync(KnowledgeSuggestion suggestion, IEnumerable<KnowledgeSourceAttachment> attachments, CancellationToken cancellationToken = default)
    {
        context.KnowledgeSuggestions.Add(suggestion);
        context.KnowledgeSourceAttachments.AddRange(attachments);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSuggestionAsync(KnowledgeSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        suggestion.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTurnAsync(KnowledgeConversationTurn turn, CancellationToken cancellationToken = default)
    {
        context.KnowledgeConversationTurns.Add(turn);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FloorPlanDraft> AddFloorPlanDraftAsync(FloorPlanDraft draft, CancellationToken cancellationToken = default)
    {
        context.FloorPlanDrafts.Add(draft);
        await context.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public Task<FloorPlanDraft?> GetFloorPlanDraftAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FloorPlanDrafts.Include(x => x.Source).ThenInclude(x => x.Collection)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task UpdateFloorPlanDraftAsync(FloorPlanDraft draft, CancellationToken cancellationToken = default)
    {
        draft.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddNavigationGraphAsync(IEnumerable<NavigationNode> nodes, IEnumerable<NavigationEdge> edges, CancellationToken cancellationToken = default)
    {
        context.NavigationNodes.AddRange(nodes);
        context.NavigationEdges.AddRange(edges);
        await context.SaveChangesAsync(cancellationToken);
    }
}
