using MallSense.Application.DTOs;
using MallSense.Domain.Entities;

namespace MallSense.Application.Interfaces;

public interface INavigationGraphAdminService
{
    Task<NavigationNode> CreateNodeAsync(SaveNavigationNodeRequest request, CancellationToken cancellationToken = default);
    Task<NavigationNode> UpdateNodeAsync(Guid id, SaveNavigationNodeRequest request, CancellationToken cancellationToken = default);
    Task DeleteNodeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NavigationEdge> CreateEdgeAsync(SaveNavigationEdgeRequest request, CancellationToken cancellationToken = default);
    Task<NavigationEdge> UpdateEdgeAsync(Guid id, SaveNavigationEdgeRequest request, CancellationToken cancellationToken = default);
    Task DeleteEdgeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NavigationGraphValidationDto> ValidateAsync(CancellationToken cancellationToken = default);
    Task<NavigationAiDraftDto> GenerateAiDraftAsync(NavigationAiDraftRequest request, IReadOnlyList<NavigationAiEvidenceDto> evidence, CancellationToken cancellationToken = default);
}
