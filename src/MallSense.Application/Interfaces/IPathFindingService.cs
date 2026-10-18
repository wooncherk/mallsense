using MallSense.Application.DTOs;
using MallSense.Domain.Entities;

namespace MallSense.Application.Interfaces;

public interface IPathFindingService
{
    CalculatedRoute FindPath(
        IEnumerable<NavigationNode> nodes,
        IEnumerable<NavigationEdge> edges,
        Guid startNodeId,
        Guid targetNodeId,
        bool accessibleOnly = false);
}
