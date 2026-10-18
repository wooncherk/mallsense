using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.UnitTests.Fakes;

public class FakePromotionRepository : IPromotionRepository
{
    private readonly List<Promotion> _promotions = new();

    public Task<IReadOnlyList<Promotion>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var list = activeOnly ? _promotions.Where(p => p.IsActive).ToList() : _promotions.ToList();
        return Task.FromResult<IReadOnlyList<Promotion>>(list);
    }

    public Task<IReadOnlyList<Promotion>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var list = _promotions.Where(p => p.TenantId == tenantId).ToList();
        return Task.FromResult<IReadOnlyList<Promotion>>(list);
    }

    public Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_promotions.FirstOrDefault(p => p.Id == id));
    }

    public Task<Promotion> AddAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        _promotions.Add(promotion);
        return Task.FromResult(promotion);
    }

    public Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        var index = _promotions.FindIndex(p => p.Id == promotion.Id);
        if (index >= 0) _promotions[index] = promotion;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        _promotions.Remove(promotion);
        return Task.CompletedTask;
    }
}
