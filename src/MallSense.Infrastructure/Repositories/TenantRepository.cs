using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly MallDbContext _context;

    public TenantRepository(MallDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.FloorNumber)
            .ThenBy(t => t.UnitNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetByCategoryAsync(StoreCategory category, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Category == category)
            .OrderBy(t => t.FloorNumber)
            .ThenBy(t => t.UnitNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(tenant, cancellationToken);
        await _context.KnowledgeCollections.AddAsync(new KnowledgeCollection
        {
            Name = tenant.Name,
            Description = "Private source material and AI investigation drafts.",
            ScopeType = "Tenant",
            ScopeReferenceId = tenant.Id,
            Visibility = KnowledgeVisibility.Private
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Update(tenant);
        var collection = await _context.KnowledgeCollections
            .FirstOrDefaultAsync(x => x.ScopeType == "Tenant" && x.ScopeReferenceId == tenant.Id, cancellationToken);
        if (collection is not null)
        {
            collection.Name = tenant.Name;
            collection.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UnitNumberExistsAsync(int floorNumber, string unitNumber, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Tenants
            .Where(t => t.FloorNumber == floorNumber && t.UnitNumber.ToLower() == unitNumber.ToLower());

        if (excludeTenantId.HasValue)
            query = query.Where(t => t.Id != excludeTenantId.Value);

        return await query.AnyAsync(cancellationToken);
    }
}
