using System.ComponentModel.DataAnnotations;
using MallSense.Domain.Entities;

namespace MallSense.Application.DTOs;

/// <summary>Login request for TenantStaff or Admin users.</summary>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

/// <summary>Registers a customer account for a mall shopper.</summary>
public record RegisterShopperRequest(
    [Required, StringLength(200, MinimumLength = 2)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

/// <summary>Creates a frictionless anonymous guest session — no credentials needed.</summary>
public record GuestSessionRequest(
    string? DeviceHint = null
);

/// <summary>Returned after successful login or guest session creation.</summary>
public record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string UserId,
    string UserName,
    string Role,
    Guid? TenantId,
    string? TenantName
);

/// <summary>Currently authenticated user profile.</summary>
public record UserProfileDto(
    Guid Id,
    string UserName,
    string? Email,
    string FullName,
    string Role,
    Guid? TenantId,
    string? TenantName,
    DateTime CreatedAtUtc
);

/// <summary>Admin-facing user record, including guest activity and tenant assignment.</summary>
public record ManagedUserDto(
    Guid Id,
    string UserName,
    string? Email,
    string FullName,
    string Role,
    Guid? TenantId,
    string? TenantName,
    DateTime CreatedAtUtc,
    DateTime LastActiveAtUtc
);

/// <summary>Creates a password-backed user account from the administration area.</summary>
public record CreateUserRequest(
    [Required, StringLength(200, MinimumLength = 2)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Role,
    Guid? TenantId
);

/// <summary>Updates a registered user's access role and optional tenant assignment.</summary>
public record UpdateUserAccessRequest(
    [Required] string Role,
    Guid? TenantId
);
