using MallSense.Domain.Entities;

namespace MallSense.Domain.Interfaces;

public interface INavigationGraphRepository
{
    Task<IReadOnlyList<NavigationNode>> GetAllNodesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NavigationEdge>> GetAllEdgesAsync(CancellationToken cancellationToken = default);
    Task<NavigationNode?> GetNodeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NavigationNode?> GetNodeByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NavigationNode>> FindNodesByKeywordsAsync(string[] keywords, CancellationToken cancellationToken = default);
    Task<NavigationNode> AddNodeAsync(NavigationNode node, CancellationToken cancellationToken = default);
    Task<NavigationNode> UpdateNodeAsync(NavigationNode node, CancellationToken cancellationToken = default);
    Task DeleteNodeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NavigationEdge?> GetEdgeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NavigationEdge> AddEdgeAsync(NavigationEdge edge, CancellationToken cancellationToken = default);
    Task<NavigationEdge> UpdateEdgeAsync(NavigationEdge edge, CancellationToken cancellationToken = default);
    Task DeleteEdgeAsync(Guid id, CancellationToken cancellationToken = default);
}
