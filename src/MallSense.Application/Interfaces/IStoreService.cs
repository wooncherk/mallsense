using MallSense.Application.DTOs;
using MallSense.Domain.Enums;

namespace MallSense.Application.Interfaces;

/// <summary>Backward-compatible store service interface. Delegates to ITenantRepository.</summary>
public interface IStoreService
{
    Task<IReadOnlyList<StoreDto>> GetAllStoresAsync(StoreCategory? category = null, CancellationToken cancellationToken = default);
    Task<StoreDto> GetStoreByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StoreDto> CreateStoreAsync(CreateStoreRequest request, CancellationToken cancellationToken = default);
    Task<StoreDto> UpdateStoreAsync(Guid id, UpdateStoreRequest request, CancellationToken cancellationToken = default);
    Task<StoreDto> UpdateStorefrontImageAsync(Guid id, string storefrontImageUrl, CancellationToken cancellationToken = default);
    Task DeleteStoreAsync(Guid id, CancellationToken cancellationToken = default);
}
