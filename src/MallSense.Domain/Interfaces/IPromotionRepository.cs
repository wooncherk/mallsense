using MallSense.Domain.Entities;

namespace MallSense.Domain.Interfaces;

public interface IPromotionRepository
{
    Task<IReadOnlyList<Promotion>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Promotion>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Promotion> AddAsync(Promotion promotion, CancellationToken cancellationToken = default);
    Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default);
    Task DeleteAsync(Promotion promotion, CancellationToken cancellationToken = default);
}
