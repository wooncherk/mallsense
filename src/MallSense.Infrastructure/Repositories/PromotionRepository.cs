using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public class PromotionRepository : IPromotionRepository
{
    private readonly MallDbContext _context;

    public PromotionRepository(MallDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Promotion>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Promotions
            .Include(p => p.Tenant)
            .AsNoTracking();

        if (activeOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(p => p.IsActive && p.ValidFrom <= now && p.ValidTo >= now);
        }

        return await query.OrderByDescending(p => p.ReadinessScore).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Promotion>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Promotions
            .Include(p => p.Tenant)
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Promotions
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Promotion> AddAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        await _context.Promotions.AddAsync(promotion, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return promotion;
    }

    public async Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        _context.Promotions.Update(promotion);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        _context.Promotions.Remove(promotion);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
