using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Infrastructure.Repositories;

public sealed class ShopperConversationRepository(MallDbContext context) : IShopperConversationRepository
{
    public Task<ShopperConversation?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.ShopperConversations
            .AsNoTracking()
            .Include(conversation => conversation.Turns.OrderBy(turn => turn.Sequence))
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc ?? conversation.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ShopperConversation>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.ShopperConversations
            .AsNoTracking()
            .Include(conversation => conversation.Turns.OrderBy(turn => turn.Sequence))
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc ?? conversation.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShopperConversation>> GetAllBySurfaceAsync(Guid userId, string surfaceContext,
        CancellationToken cancellationToken = default) =>
        await context.ShopperConversations
            .AsNoTracking()
            .Include(conversation => conversation.Turns.OrderByDescending(turn => turn.CreatedAtUtc))
            .Where(conversation => conversation.UserId == userId && conversation.SurfaceContext == surfaceContext)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc ?? conversation.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<ShopperConversation?> GetBySessionIdAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default) =>
        context.ShopperConversations
            .AsNoTracking()
            .Include(conversation => conversation.Turns.OrderBy(turn => turn.Sequence))
            .FirstOrDefaultAsync(conversation => conversation.UserId == userId && conversation.SessionId == sessionId, cancellationToken);

    public async Task<bool> DeleteAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var conversation = await context.ShopperConversations
            .FirstOrDefaultAsync(item => item.UserId == userId && item.SessionId == sessionId, cancellationToken);
        if (conversation == null) return false;

        context.ShopperConversations.Remove(conversation);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AppendExchangeAsync(Guid userId, string sessionId, string surfaceContext,
        string userMessage, string assistantMessage, string? structuredIntentJson,
        string? recommendationsJson, string? replySuggestionsJson, CancellationToken cancellationToken = default)
    {
        var conversation = await context.ShopperConversations
            .FirstOrDefaultAsync(item => item.UserId == userId && item.SessionId == sessionId, cancellationToken);

        if (conversation == null)
        {
            conversation = new ShopperConversation
            {
                UserId = userId,
                SessionId = sessionId,
                SurfaceContext = surfaceContext
            };
            context.ShopperConversations.Add(conversation);
            await context.SaveChangesAsync(cancellationToken);
        }

        var lastSequence = await context.ShopperConversationTurns
            .Where(turn => turn.ConversationId == conversation.Id)
            .Select(turn => (int?)turn.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
        context.ShopperConversationTurns.Add(new ShopperConversationTurn
        {
            ConversationId = conversation.Id,
            Sequence = lastSequence + 1,
            Role = "user",
            Message = userMessage.Trim()
        });
        context.ShopperConversationTurns.Add(new ShopperConversationTurn
        {
            ConversationId = conversation.Id,
            Sequence = lastSequence + 2,
            Role = "assistant",
            Message = assistantMessage.Trim(),
            StructuredIntentJson = structuredIntentJson,
            RecommendationsJson = recommendationsJson,
            ReplySuggestionsJson = replySuggestionsJson
        });
        conversation.SurfaceContext = surfaceContext;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShopperConversationTurn> AppendAssistantMessageAsync(Guid userId, string sessionId,
        string surfaceContext, string message, string messageType, string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        var conversation = await context.ShopperConversations
            .FirstOrDefaultAsync(item => item.UserId == userId && item.SessionId == sessionId, cancellationToken);
        if (conversation == null)
        {
            conversation = new ShopperConversation { UserId = userId, SessionId = sessionId, SurfaceContext = surfaceContext };
            context.ShopperConversations.Add(conversation);
            await context.SaveChangesAsync(cancellationToken);
        }

        var lastSequence = await context.ShopperConversationTurns
            .Where(turn => turn.ConversationId == conversation.Id)
            .Select(turn => (int?)turn.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
        var turn = new ShopperConversationTurn
        {
            ConversationId = conversation.Id,
            Sequence = lastSequence + 1,
            Role = "assistant",
            MessageType = messageType,
            Message = message.Trim(),
            StructuredIntentJson = metadataJson
        };
        context.ShopperConversationTurns.Add(turn);
        conversation.SurfaceContext = surfaceContext;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return turn;
    }
}
