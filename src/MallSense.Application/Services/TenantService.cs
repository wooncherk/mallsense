using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;

    public TenantService(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<IReadOnlyList<TenantDto>> GetAllTenantsAsync(
        StoreCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var tenants = category.HasValue
            ? await _tenantRepository.GetByCategoryAsync(category.Value, cancellationToken)
            : await _tenantRepository.GetAllAsync(cancellationToken);
        return tenants.Select(TenantDto.FromEntity).ToList();
    }

    public async Task<TenantDto> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);
        return TenantDto.FromEntity(tenant);
    }

    public async Task<TenantDto> CreateTenantAsync(
        CreateStoreRequest request,
        CancellationToken cancellationToken = default)
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
        return TenantDto.FromEntity(created);
    }

    public async Task<TenantDto> UpdateTenantAsync(
        Guid id,
        UpdateStoreRequest request,
        CancellationToken cancellationToken = default)
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
        return TenantDto.FromEntity(tenant);
    }

    public async Task DeleteTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);
        await _tenantRepository.DeleteAsync(tenant, cancellationToken);
    }
}
