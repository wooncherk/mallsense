using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public class NavigationGraphRepository : INavigationGraphRepository
{
    private readonly MallDbContext _context;

    public NavigationGraphRepository(MallDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NavigationNode>> GetAllNodesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.NavigationNodes
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NavigationEdge>> GetAllEdgesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.NavigationEdges
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<NavigationNode?> GetNodeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.NavigationNodes
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<NavigationNode?> GetNodeByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant?.NavigationNodeId == null)
        {
            return null;
        }

        return await _context.NavigationNodes
            .FirstOrDefaultAsync(n => n.Id == tenant.NavigationNodeId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<NavigationNode>> FindNodesByKeywordsAsync(string[] keywords, CancellationToken cancellationToken = default)
    {
        var allNodes = await _context.NavigationNodes.AsNoTracking().ToListAsync(cancellationToken);

        return allNodes.Where(n =>
            keywords.Any(k =>
                n.Label.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                (n.Description != null && n.Description.Contains(k, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    public async Task<NavigationNode> AddNodeAsync(NavigationNode node, CancellationToken cancellationToken = default)
    {
        _context.NavigationNodes.Add(node);
        await SynchronizeTenantLinkAsync(node, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return node;
    }

    public async Task<NavigationNode> UpdateNodeAsync(NavigationNode node, CancellationToken cancellationToken = default)
    {
        _context.NavigationNodes.Update(node);
        await SynchronizeTenantLinkAsync(node, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return node;
    }

    public async Task DeleteNodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _context.NavigationNodes.FindAsync([id], cancellationToken);
        if (node == null) return;
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.NavigationNodeId == id, cancellationToken);
        if (tenant != null) tenant.NavigationNodeId = null;
        _context.NavigationNodes.Remove(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<NavigationEdge?> GetEdgeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.NavigationEdges.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<NavigationEdge> AddEdgeAsync(NavigationEdge edge, CancellationToken cancellationToken = default)
    {
        _context.NavigationEdges.Add(edge);
        await _context.SaveChangesAsync(cancellationToken);
        return edge;
    }

    public async Task<NavigationEdge> UpdateEdgeAsync(NavigationEdge edge, CancellationToken cancellationToken = default)
    {
        _context.NavigationEdges.Update(edge);
        await _context.SaveChangesAsync(cancellationToken);
        return edge;
    }

    public async Task DeleteEdgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var edge = await _context.NavigationEdges.FindAsync([id], cancellationToken);
        if (edge == null) return;
        _context.NavigationEdges.Remove(edge);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SynchronizeTenantLinkAsync(NavigationNode node, CancellationToken cancellationToken)
    {
        var previous = await _context.Tenants.FirstOrDefaultAsync(t => t.NavigationNodeId == node.Id, cancellationToken);
        if (previous != null && previous.Id != node.TenantId) previous.NavigationNodeId = null;
        if (node.TenantId.HasValue)
        {
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == node.TenantId.Value, cancellationToken);
            if (tenant != null) tenant.NavigationNodeId = node.Id;
        }
    }
}
