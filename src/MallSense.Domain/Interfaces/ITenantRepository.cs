using MallSense.Domain.Entities;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Interfaces;

/// <summary>Repository for Tenant entities (replaces IStoreRepository).</summary>
public interface ITenantRepository
{
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetByCategoryAsync(StoreCategory category, CancellationToken cancellationToken = default);
    Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task DeleteAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task<bool> UnitNumberExistsAsync(int floorNumber, string unitNumber, Guid? excludeTenantId = null, CancellationToken cancellationToken = default);
}
