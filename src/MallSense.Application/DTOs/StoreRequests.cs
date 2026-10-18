using System.ComponentModel.DataAnnotations;
using MallSense.Domain.Enums;

namespace MallSense.Application.DTOs;

public record CreateStoreRequest(
    [Required, StringLength(100, MinimumLength = 2)]
    string Name,

    [StringLength(500)]
    string? Description,

    [Required]
    StoreCategory Category,

    [Required, Range(-5, 50)]
    int FloorNumber,

    [Required, StringLength(20, MinimumLength = 1)]
    string UnitNumber,

    [EmailAddress]
    string? ContactEmail,

    [Phone]
    string? ContactPhone,

    [StringLength(100)]
    string? OpeningHours
);

public record UpdateStoreRequest(
    [Required, StringLength(100, MinimumLength = 2)]
    string Name,

    [StringLength(500)]
    string? Description,

    [Required]
    StoreCategory Category,

    [Required, Range(-5, 50)]
    int FloorNumber,

    [Required, StringLength(20, MinimumLength = 1)]
    string UnitNumber,

    [EmailAddress]
    string? ContactEmail,

    [Phone]
    string? ContactPhone,

    [StringLength(100)]
    string? OpeningHours,

    bool IsActive
);

public record UpdateStorefrontImageRequest(
    [Required] string StorefrontImageUrl
);

