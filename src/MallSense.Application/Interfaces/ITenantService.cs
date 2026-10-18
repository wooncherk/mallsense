using MallSense.Application.DTOs;
using MallSense.Domain.Enums;

namespace MallSense.Application.Interfaces;

public interface ITenantService
{
    Task<IReadOnlyList<TenantDto>> GetAllTenantsAsync(StoreCategory? category = null, CancellationToken cancellationToken = default);
    Task<TenantDto> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantDto> CreateTenantAsync(CreateStoreRequest request, CancellationToken cancellationToken = default);
    Task<TenantDto> UpdateTenantAsync(Guid id, UpdateStoreRequest request, CancellationToken cancellationToken = default);
    Task DeleteTenantAsync(Guid id, CancellationToken cancellationToken = default);
}
