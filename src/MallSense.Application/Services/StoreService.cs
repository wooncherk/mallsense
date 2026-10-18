using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

/// <summary>Backward-compatible store service delegating to ITenantRepository.</summary>
public class StoreService : IStoreService
{
    private readonly ITenantRepository _tenantRepository;

    public StoreService(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<IReadOnlyList<StoreDto>> GetAllStoresAsync(
        StoreCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var tenants = category.HasValue
            ? await _tenantRepository.GetByCategoryAsync(category.Value, cancellationToken)
            : await _tenantRepository.GetAllAsync(cancellationToken);
        return tenants.Select(StoreDto.FromEntity).ToList();
    }

    public async Task<StoreDto> GetStoreByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);
        return StoreDto.FromEntity(tenant);
    }

    public async Task<StoreDto> CreateStoreAsync(CreateStoreRequest request, CancellationToken cancellationToken = default)
    {
        if (await _tenantRepository.UnitNumberExistsAsync(request.FloorNumber, request.UnitNumber, null, cancellationToken))
            throw new BusinessRuleException($"Unit '{request.UnitNumber}' on floor {request.FloorNumber} is already occupied.");

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Category = request.Category,
            FloorNumber = request.FloorNumber,
            UnitNumber = request.UnitNumber.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            OpeningHours = request.OpeningHours?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _tenantRepository.AddAsync(tenant, cancellationToken);
        return StoreDto.FromEntity(created);
    }

    public async Task<StoreDto> UpdateStoreAsync(Guid id, UpdateStoreRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        if (await _tenantRepository.UnitNumberExistsAsync(request.FloorNumber, request.UnitNumber, id, cancellationToken))
            throw new BusinessRuleException($"Unit '{request.UnitNumber}' on floor {request.FloorNumber} is already occupied.");

        tenant.Name = request.Name.Trim();
        tenant.Description = request.Description?.Trim() ?? string.Empty;
        tenant.Category = request.Category;
        tenant.FloorNumber = request.FloorNumber;
        tenant.UnitNumber = request.UnitNumber.Trim();
        tenant.ContactEmail = request.ContactEmail?.Trim();
        tenant.ContactPhone = request.ContactPhone?.Trim();
        tenant.OpeningHours = request.OpeningHours?.Trim();
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        return StoreDto.FromEntity(tenant);
    }

    public async Task DeleteStoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);
        await _tenantRepository.DeleteAsync(tenant, cancellationToken);
    }

    public async Task<StoreDto> UpdateStorefrontImageAsync(Guid id, string storefrontImageUrl, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);
        tenant.StorefrontImageUrl = storefrontImageUrl.Trim();
        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        return StoreDto.FromEntity(tenant);
    }

}
