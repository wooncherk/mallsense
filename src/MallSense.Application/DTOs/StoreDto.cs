using MallSense.Domain.Entities;
using MallSense.Domain.Enums;

namespace MallSense.Application.DTOs;

/// <summary>Tenant shop DTO (replaces StoreDto).</summary>
public record TenantDto(
    Guid Id,
    string Name,
    string Description,
    StoreCategory Category,
    int FloorNumber,
    string UnitNumber,
    string? ContactEmail,
    string? ContactPhone,
    string? OpeningHours,
    string? StorefrontImageUrl,
    bool IsActive,
    Guid? NavigationNodeId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int? SearchValidationRating = null,
    string? SearchValidationSummary = null,
    string? SearchValidationDescriptionHash = null,
    DateTime? SearchValidationAtUtc = null,
    bool SearchValidationIsCurrent = false
)
{
    public static TenantDto FromEntity(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Description,
        tenant.Category,
        tenant.FloorNumber,
        tenant.UnitNumber,
        tenant.ContactEmail,
        tenant.ContactPhone,
        tenant.OpeningHours,
        tenant.StorefrontImageUrl,
        tenant.IsActive,
        tenant.NavigationNodeId,
        tenant.CreatedAtUtc,
        tenant.UpdatedAtUtc,
        tenant.SearchValidationRating,
        tenant.SearchValidationSummary,
        tenant.SearchValidationDescriptionHash,
        tenant.SearchValidationAtUtc,
        tenant.SearchValidationDescriptionHash == TenantSearchValidationHash.Compute(tenant.Description)
    );
}

/// <summary>Backward-compatible alias for frontend /api/stores endpoint.</summary>
public record StoreDto(
    Guid Id,
    string Name,
    string Description,
    StoreCategory Category,
    int FloorNumber,
    string UnitNumber,
    string? ContactEmail,
    string? ContactPhone,
    string? OpeningHours,
    string? StorefrontImageUrl,
    bool IsActive,
    Guid? NavigationNodeId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int? SearchValidationRating = null,
    string? SearchValidationSummary = null,
    string? SearchValidationDescriptionHash = null,
    DateTime? SearchValidationAtUtc = null,
    bool SearchValidationIsCurrent = false
)
{
    public static StoreDto FromEntity(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Description,
        tenant.Category,
        tenant.FloorNumber,
        tenant.UnitNumber,
        tenant.ContactEmail,
        tenant.ContactPhone,
        tenant.OpeningHours,
        tenant.StorefrontImageUrl,
        tenant.IsActive,
        tenant.NavigationNodeId,
        tenant.CreatedAtUtc,
        tenant.UpdatedAtUtc,
        tenant.SearchValidationRating,
        tenant.SearchValidationSummary,
        tenant.SearchValidationDescriptionHash,
        tenant.SearchValidationAtUtc,
        tenant.SearchValidationDescriptionHash == TenantSearchValidationHash.Compute(tenant.Description)
    );
}
