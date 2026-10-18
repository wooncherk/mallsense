using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _configuration = configuration;
    }

    /// <summary>Authenticates a TenantStaff or Admin user by email and password.</summary>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.Role == UserRole.Guest)
            throw new UnauthorizedAccessException("Guest accounts cannot use password login.");

        if (string.IsNullOrWhiteSpace(user.PasswordHash) || !VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        user.LastActiveAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        string? tenantName = null;
        if (user.TenantId.HasValue)
        {
            var tenant = await _tenantRepository.GetByIdAsync(user.TenantId.Value, cancellationToken);
            tenantName = tenant?.Name;
        }

        var token = GenerateJwt(user);
        return BuildResponse(token, user, tenantName);
    }

    /// <summary>Registers a password-backed shopper account.</summary>
    public async Task<AuthResponse> RegisterShopperAsync(RegisterShopperRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.GetByEmailAsync(email, cancellationToken) != null)
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = HashPassword(request.Password),
            Role = UserRole.Shopper,
            LastActiveAtUtc = DateTime.UtcNow
        };

        var created = await _userRepository.AddAsync(user, cancellationToken);
        return BuildResponse(GenerateJwt(created), created, null);
    }

    /// <summary>Creates a frictionless anonymous guest session with a JWT (Role=Guest).</summary>
    public async Task<AuthResponse> CreateGuestSessionAsync(GuestSessionRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedHint = request.DeviceHint?.Trim();
        var guestTag = string.IsNullOrWhiteSpace(normalizedHint)
            ? Guid.NewGuid().ToString("N")[..16]
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHint))).ToLowerInvariant()[..16];
        var userName = $"guest_{guestTag}";
        var existing = await _userRepository.GetByUserNameAsync(userName, cancellationToken);
        if (existing is { Role: UserRole.Guest })
        {
            existing.LastActiveAtUtc = DateTime.UtcNow;
            await _userRepository.UpdateAsync(existing, cancellationToken);
            return BuildResponse(GenerateJwt(existing), existing, null);
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            FullName = "Mall Visitor",
            Role = UserRole.Guest,
            LastActiveAtUtc = DateTime.UtcNow
        };

        var created = await _userRepository.AddAsync(user, cancellationToken);
        var token = GenerateJwt(created);
        return BuildResponse(token, created, null);
    }

    /// <summary>Returns the current user profile.</summary>
    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return null;

        user.LastActiveAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        string? tenantName = null;
        if (user.TenantId.HasValue)
        {
            var tenant = await _tenantRepository.GetByIdAsync(user.TenantId.Value, cancellationToken);
            tenantName = tenant?.Name;
        }

        return new UserProfileDto(
            user.Id, user.UserName, user.Email, user.FullName,
            user.Role.ToString(), user.TenantId, tenantName, user.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ManagedUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var result = new List<ManagedUserDto>(users.Count);

        foreach (var user in users)
            result.Add(await BuildManagedUserAsync(user, cancellationToken));

        return result;
    }

    public async Task<ManagedUserDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.GetByEmailAsync(email, cancellationToken) != null)
            throw new InvalidOperationException("An account with this email already exists.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role) || role == UserRole.Guest)
            throw new InvalidOperationException("Role must be Shopper, TenantStaff, or Admin.");

        Guid? tenantId = null;
        if (role == UserRole.TenantStaff)
        {
            if (!request.TenantId.HasValue || await _tenantRepository.GetByIdAsync(request.TenantId.Value, cancellationToken) == null)
                throw new InvalidOperationException("Tenant staff must be assigned to a valid tenant.");
            tenantId = request.TenantId;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = HashPassword(request.Password),
            Role = role,
            TenantId = tenantId,
            LastActiveAtUtc = DateTime.UtcNow
        };

        var created = await _userRepository.AddAsync(user, cancellationToken);
        return await BuildManagedUserAsync(created, cancellationToken);
    }

    public async Task<ManagedUserDto> UpdateUserAccessAsync(
        Guid userId,
        UpdateUserAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.Role == UserRole.Guest)
            throw new InvalidOperationException("Guest sessions are read-only identities.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role) || role == UserRole.Guest)
            throw new InvalidOperationException("Role must be Shopper, TenantStaff, or Admin.");

        if (role == UserRole.TenantStaff)
        {
            if (!request.TenantId.HasValue || await _tenantRepository.GetByIdAsync(request.TenantId.Value, cancellationToken) == null)
                throw new InvalidOperationException("Tenant staff must be assigned to a valid tenant.");
            user.TenantId = request.TenantId;
        }
        else
        {
            user.TenantId = null;
        }

        user.Role = role;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return await BuildManagedUserAsync(user, cancellationToken);
    }

    private async Task<ManagedUserDto> BuildManagedUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        string? tenantName = null;
        if (user.TenantId.HasValue)
            tenantName = (await _tenantRepository.GetByIdAsync(user.TenantId.Value, cancellationToken))?.Name;

        return new ManagedUserDto(
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.TenantId,
            tenantName,
            user.CreatedAtUtc,
            user.LastActiveAtUtc);
    }

    private string GenerateJwt(ApplicationUser user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "MallSense_Super_Secret_Key_For_Dev_2026_Do_Not_Use_In_Production!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        if (user.Email != null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        var expireHours = user.Role == UserRole.Guest ? 24 : 8;
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "MallSense",
            audience: _configuration["Jwt:Audience"] ?? "MallSenseClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthResponse BuildResponse(string token, ApplicationUser user, string? tenantName) => new(
        AccessToken: token,
        TokenType: "Bearer",
        ExpiresInSeconds: user.Role == UserRole.Guest ? 86400 : 28800,
        UserId: user.Id.ToString(),
        UserName: user.UserName,
        Role: user.Role.ToString(),
        TenantId: user.TenantId,
        TenantName: tenantName);

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
