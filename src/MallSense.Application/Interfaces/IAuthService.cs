using MallSense.Application.DTOs;

namespace MallSense.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Authenticates an existing TenantStaff or Admin user by email and password.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Registers a shopper account and returns an authenticated session.</summary>
    Task<AuthResponse> RegisterShopperAsync(RegisterShopperRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates a frictionless anonymous guest session, issuing a JWT with Role=Guest.</summary>
    Task<AuthResponse> CreateGuestSessionAsync(GuestSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the current user profile from the given userId.</summary>
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Lists all guest and registered identities for mall administrators.</summary>
    Task<IReadOnlyList<ManagedUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a registered user with the requested role and tenant assignment.</summary>
    Task<ManagedUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes a registered user's role and tenant assignment.</summary>
    Task<ManagedUserDto> UpdateUserAccessAsync(Guid userId, UpdateUserAccessRequest request, CancellationToken cancellationToken = default);
}
