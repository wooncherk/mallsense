using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using MallSense.Application.DTOs;
using MallSense.Application.Services;
using MallSense.Domain.Entities;
using MallSense.UnitTests.Fakes;

namespace MallSense.UnitTests.Services;

public class AuthServiceTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var configuration = new ConfigurationManager
        {
            ["Jwt:Key"] = "MallSense_Unit_Test_Key_That_Is_Long_Enough_For_HS256!",
            ["Jwt:Issuer"] = "MallSense.Tests",
            ["Jwt:Audience"] = "MallSense.Tests.Client"
        };
        _service = new AuthService(_userRepository, _tenantRepository, configuration);
    }

    [Fact]
    public void HashPassword_ShouldCreateVerifiableSaltedHash()
    {
        var first = AuthService.HashPassword("Tenant123!");
        var second = AuthService.HashPassword("Tenant123!");

        Assert.NotEqual(first, second);
        Assert.True(AuthService.VerifyPassword("Tenant123!", first));
        Assert.False(AuthService.VerifyPassword("wrong", first));
        Assert.False(AuthService.VerifyPassword("Tenant123!", "not-a-valid-hash"));
    }

    [Fact]
    public async Task LoginAsync_ShouldIssueRoleAndTenantClaims()
    {
        var tenant = await _tenantRepository.AddAsync(new Tenant
        {
            Name = "Popular Bookstore",
            FloorNumber = 2,
            UnitNumber = "L2-01"
        });
        await _userRepository.AddAsync(new ApplicationUser
        {
            UserName = "popular@tenant.com",
            Email = "popular@tenant.com",
            FullName = "Popular Manager",
            PasswordHash = AuthService.HashPassword("Tenant123!"),
            Role = UserRole.TenantStaff,
            TenantId = tenant.Id
        });

        var response = await _service.LoginAsync(new LoginRequest("POPULAR@TENANT.COM", "Tenant123!"));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        Assert.Equal("TenantStaff", response.Role);
        Assert.Equal(tenant.Id, response.TenantId);
        Assert.Equal("Popular Bookstore", response.TenantName);
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "TenantStaff");
        Assert.Contains(token.Claims, claim => claim.Type == "tenant_id" && claim.Value == tenant.Id.ToString());
    }

    [Fact]
    public async Task CreateGuestSessionAsync_ShouldPersistGuestAndIssueGuestToken()
    {
        var response = await _service.CreateGuestSessionAsync(new GuestSessionRequest("unit-test"));
        var persisted = await _userRepository.GetByIdAsync(Guid.Parse(response.UserId));

        Assert.NotNull(persisted);
        Assert.Equal(UserRole.Guest, persisted.Role);
        Assert.Equal("Guest", response.Role);
        Assert.Null(response.TenantId);
    }

    [Fact]
    public async Task CreateGuestSessionAsync_ShouldRestoreGuestForSameDevice()
    {
        var first = await _service.CreateGuestSessionAsync(new GuestSessionRequest("same-browser"));
        var restored = await _service.CreateGuestSessionAsync(new GuestSessionRequest("same-browser"));

        Assert.Equal(first.UserId, restored.UserId);
        Assert.Equal("Guest", restored.Role);
    }

    [Fact]
    public async Task RegisterShopperAsync_ShouldPersistShopperAndIssueToken()
    {
        var response = await _service.RegisterShopperAsync(
            new RegisterShopperRequest("Alicia Shopper", "ALICIA@example.com", "Shopper123!"));
        var persisted = await _userRepository.GetByIdAsync(Guid.Parse(response.UserId));

        Assert.NotNull(persisted);
        Assert.Equal(UserRole.Shopper, persisted.Role);
        Assert.Equal("alicia@example.com", persisted.Email);
        Assert.Equal("Shopper", response.Role);
        Assert.True(AuthService.VerifyPassword("Shopper123!", persisted.PasswordHash!));
    }

    [Fact]
    public async Task UpdateUserAccessAsync_ShouldAssignRegisteredShopperToTenant()
    {
        var tenant = await _tenantRepository.AddAsync(new Tenant
        {
            Name = "Book Nook",
            FloorNumber = 1,
            UnitNumber = "L1-08"
        });
        var shopper = await _userRepository.AddAsync(new ApplicationUser
        {
            UserName = "shopper@example.com",
            Email = "shopper@example.com",
            FullName = "Shopper",
            PasswordHash = AuthService.HashPassword("Shopper123!"),
            Role = UserRole.Shopper
        });

        var updated = await _service.UpdateUserAccessAsync(
            shopper.Id,
            new UpdateUserAccessRequest("TenantStaff", tenant.Id));

        Assert.Equal("TenantStaff", updated.Role);
        Assert.Equal(tenant.Id, updated.TenantId);
        Assert.Equal("Book Nook", updated.TenantName);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldCreateTenantStaffWithCredentialsAndAssignment()
    {
        var tenant = await _tenantRepository.AddAsync(new Tenant
        {
            Name = "Tech Hub",
            FloorNumber = 2,
            UnitNumber = "L2-11"
        });

        var created = await _service.CreateUserAsync(
            new CreateUserRequest("Taylor Manager", "TAYLOR@example.com", "Secure123!", "TenantStaff", tenant.Id));
        var persisted = await _userRepository.GetByIdAsync(created.Id);

        Assert.NotNull(persisted);
        Assert.Equal("taylor@example.com", persisted.Email);
        Assert.Equal(UserRole.TenantStaff, persisted.Role);
        Assert.Equal(tenant.Id, persisted.TenantId);
        Assert.Equal("Tech Hub", created.TenantName);
        Assert.True(AuthService.VerifyPassword("Secure123!", persisted.PasswordHash!));
    }

    [Fact]
    public async Task CreateUserAsync_ShouldRejectTenantStaffWithoutValidTenant()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateUserAsync(
            new CreateUserRequest("Taylor Manager", "taylor@example.com", "Secure123!", "TenantStaff", null)));

        Assert.Equal("Tenant staff must be assigned to a valid tenant.", exception.Message);
    }
}
