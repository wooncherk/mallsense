using MallSense.Domain.Entities;

namespace MallSense.Domain.Interfaces;

public interface IShopperConversationRepository
{
    Task<ShopperConversation?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShopperConversation>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShopperConversation>> GetAllBySurfaceAsync(Guid userId, string surfaceContext,
        CancellationToken cancellationToken = default);
    Task<ShopperConversation?> GetBySessionIdAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default);
    Task AppendExchangeAsync(Guid userId, string sessionId, string surfaceContext, string userMessage,
        string assistantMessage, string? structuredIntentJson, string? recommendationsJson, string? replySuggestionsJson,
        CancellationToken cancellationToken = default);
    Task<ShopperConversationTurn> AppendAssistantMessageAsync(Guid userId, string sessionId, string surfaceContext,
        string message, string messageType, string? metadataJson, CancellationToken cancellationToken = default);
}
