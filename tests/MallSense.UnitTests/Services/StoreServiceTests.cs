using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Services;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.UnitTests.Fakes;
using Xunit;

namespace MallSense.UnitTests.Services;

public class StoreServiceTests
{
    private readonly FakeTenantRepository _repository;
    private readonly StoreService _service;

    public StoreServiceTests()
    {
        _repository = new FakeTenantRepository();
        _service = new StoreService(_repository);
    }

    [Fact]
    public async Task GetAllStoresAsync_ShouldReturnAllStores_WhenNoCategoryProvided()
    {
        // Arrange
        await _repository.AddAsync(new Tenant { Name = "Store A", Category = StoreCategory.Fashion, FloorNumber = 1, UnitNumber = "01" });
        await _repository.AddAsync(new Tenant { Name = "Store B", Category = StoreCategory.Electronics, FloorNumber = 1, UnitNumber = "02" });

        // Act
        var result = await _service.GetAllStoresAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllStoresAsync_ShouldFilterByCategory_WhenCategoryProvided()
    {
        // Arrange
        await _repository.AddAsync(new Tenant { Name = "Store A", Category = StoreCategory.Fashion, FloorNumber = 1, UnitNumber = "01" });
        await _repository.AddAsync(new Tenant { Name = "Store B", Category = StoreCategory.Electronics, FloorNumber = 1, UnitNumber = "02" });

        // Act
        var result = await _service.GetAllStoresAsync(StoreCategory.Fashion);

        // Assert
        Assert.Single(result);
        Assert.Equal("Store A", result[0].Name);
    }

    [Fact]
    public async Task GetStoreByIdAsync_ShouldThrowNotFoundException_WhenStoreDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetStoreByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateStoreAsync_ShouldThrowBusinessRuleException_WhenUnitAlreadyOccupied()
    {
        // Arrange
        await _repository.AddAsync(new Tenant
        {
            Name = "Existing Store",
            FloorNumber = 2,
            UnitNumber = "L2-10"
        });

        var request = new CreateStoreRequest(
            Name: "New Competitor",
            Description: "Duplicate unit store",
            Category: StoreCategory.Fashion,
            FloorNumber: 2,
            UnitNumber: "L2-10",
            ContactEmail: "test@mall.com",
            ContactPhone: "123",
            OpeningHours: "9-5"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateStoreAsync(request));

        Assert.Contains("already occupied", ex.Message);
    }

    [Fact]
    public async Task CreateStoreAsync_ShouldSuccessfullyCreateStore_WhenUnitIsAvailable()
    {
        // Arrange
        var request = new CreateStoreRequest(
            Name: "New Bookstore",
            Description: "Books and cafe",
            Category: StoreCategory.Services,
            FloorNumber: 3,
            UnitNumber: "L3-05",
            ContactEmail: "books@mall.com",
            ContactPhone: "12345",
            OpeningHours: "10-9"
        );

        // Act
        var created = await _service.CreateStoreAsync(request);

        // Assert
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("New Bookstore", created.Name);
        Assert.Equal(3, created.FloorNumber);
        Assert.Equal("L3-05", created.UnitNumber);
    }

    [Fact]
    public async Task UpdateStorefrontImageAsync_ShouldStoreImageOnTenant()
    {
        var tenant = await _repository.AddAsync(new Tenant { Name = "Image Store", FloorNumber = 1, UnitNumber = "L1-20" });
        var updated = await _service.UpdateStorefrontImageAsync(tenant.Id, "data:image/png;base64,abc");
        Assert.Equal("data:image/png;base64,abc", updated.StorefrontImageUrl);
    }
}
