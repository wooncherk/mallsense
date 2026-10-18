using MallSense.Application.DTOs;
using MallSense.Application.Services;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.UnitTests.Fakes;
using Xunit;

namespace MallSense.UnitTests.Services;

public class TenantPromotionServiceTests
{
    private readonly FakePromotionRepository _promotionRepo;
    private readonly FakeTenantRepository _tenantRepo;
    private readonly FakeGeminiClient _geminiClient;
    private readonly TenantPromotionService _service;

    public TenantPromotionServiceTests()
    {
        _promotionRepo = new FakePromotionRepository();
        _tenantRepo = new FakeTenantRepository();
        _geminiClient = new FakeGeminiClient();
        _service = new TenantPromotionService(_promotionRepo, _tenantRepo, _geminiClient);
    }

    [Fact]
    public async Task ExtractFromPosterAsync_ShouldExtractStructuredFieldsAndMissingQuestions()
    {
        // Act
        var result = await _service.ExtractFromPosterAsync(new byte[] { 1, 2, 3 }, "image/jpeg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mid-Autumn Mooncake Heritage Special", result.Title);
        Assert.Contains("15% off", result.OfferSummary);
        Assert.NotEmpty(result.MissingInformationQuestions);
    }

    [Fact]
    public async Task CreatePromotionAsync_ShouldCalculateInitialReadinessScore()
    {
        // Arrange
        var tenant = new Tenant { Name = "Baker Cottage", Category = StoreCategory.FoodAndBeverage, FloorNumber = 1, UnitNumber = "L1-05" };
        await _tenantRepo.AddAsync(tenant);

        var extraction = new PosterExtractionResultDto(
            Title: "Mid-Autumn Mooncake Special",
            Type: PromotionType.Discount,
            OfferSummary: "15% discount on sets",
            ValidFrom: DateTime.UtcNow,
            ValidTo: DateTime.UtcNow.AddDays(20),
            TargetAudience: "Family",
            Occasions: "Festival",
            DietaryFlags: null,
            PriceRange: "RM 80 - 150",
            SuggestedCategories: new[] { "Food" },
            MissingInformationQuestions: new[] { "Is advance ordering needed?" }
        );

        var request = new CreatePromotionFromExtractionRequest(tenant.Id, extraction, null);

        // Act
        var created = await _service.CreatePromotionAsync(request);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(tenant.Id, created.TenantId);
        Assert.True(created.ReadinessScore >= 80); // Title, Offer, Audience, Occasions, Price all present
    }

    [Fact]
    public async Task ClarifyAndEnrichPromotionAsync_ShouldIncreaseReadinessScoreUponAnsweringQuestions()
    {
        // Arrange
        var tenant = new Tenant { Name = "Popular", Category = StoreCategory.Entertainment, FloorNumber = 2, UnitNumber = "L2-01" };
        await _tenantRepo.AddAsync(tenant);

        var promo = new Promotion
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            Title = "Reading Week",
            OfferSummary = "20% off",
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddDays(10),
            ReadinessScore = 60
        };
        await _promotionRepo.AddAsync(promo);

        var clarification = new EnrichmentClarificationRequest(
            PromotionId: promo.Id,
            SuitableForGifting: "Children's gifts, Birthdays",
            SuitableForCorporate: "School libraries, bulk awards",
            AdvanceOrderRequired: "None, instant in-store",
            DietaryInformation: null,
            ApproximatePriceRange: "RM 20 - RM 100"
        );

        // Act
        var enriched = await _service.ClarifyAndEnrichPromotionAsync(clarification);

        // Assert
        Assert.True(enriched.NewReadinessScore >= 90);
        Assert.Contains("High discovery readiness", enriched.ReadinessFeedback);
    }
}
