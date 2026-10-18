using MallSense.Application.DTOs;
using MallSense.Application.Services;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.UnitTests.Fakes;
using Xunit;

namespace MallSense.UnitTests.Services;

public class ShopperChatServiceTests
{
    private readonly FakeTenantRepository _tenantRepo;
    private readonly FakePromotionRepository _promoRepo;
    private readonly FakeIntentSignalRepository _intentRepo;
    private readonly FakeGeminiClient _geminiClient;
    private readonly ShopperChatService _service;

    public ShopperChatServiceTests()
    {
        _tenantRepo = new FakeTenantRepository();
        _promoRepo = new FakePromotionRepository();
        _intentRepo = new FakeIntentSignalRepository();
        _geminiClient = new FakeGeminiClient();

        _service = new ShopperChatService(_geminiClient, _tenantRepo, _promoRepo, _intentRepo,
            new SemanticSearchService(_geminiClient));
    }

    [Fact]
    public async Task ProcessShopperMessageAsync_ShouldExtractIntentAndExcludeFashionWhenExcluded()
    {
        // Arrange
        var popular = new Tenant
        {
            Name = "Popular Bookstore",
            Category = StoreCategory.Entertainment,
            FloorNumber = 2,
            UnitNumber = "L2-01",
            Tags = "books, toys, children, education"
        };
        var zara = new Tenant
        {
            Name = "Zara",
            Category = StoreCategory.Fashion,
            FloorNumber = 1,
            UnitNumber = "L1-20",
            Tags = "fashion, clothing, apparel"
        };

        await _tenantRepo.AddAsync(popular);
        await _tenantRepo.AddAsync(zara);

        var request = new ShopperChatRequest(
            SessionId: "session_001",
            Message: "I need a gift for my 8-year-old nephew, but he isn't into clothes",
            CurrentNodeId: null,
            CurrentFloor: 1,
            PreviousIntent: null
        );

        // Act
        var response = await _service.ProcessShopperMessageAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("session_001", response.SessionId);
        Assert.Contains(response.Recommendations, r => r.TenantName == "Popular Bookstore");
        Assert.DoesNotContain(response.Recommendations, r => r.TenantName == "Zara");
        Assert.Contains("8-year-old nephew", response.Reply);
        Assert.Contains("Popular Bookstore", response.Reply);
        Assert.DoesNotContain("top match", response.Reply, StringComparison.OrdinalIgnoreCase);

        // Verify intent signal was logged for BI
        var loggedSignals = await _intentRepo.GetRecentSignalsAsync();
        Assert.Single(loggedSignals);
        Assert.Equal("session_001", loggedSignals[0].SessionId);
    }

    [Fact]
    public async Task ProcessShopperMessageAsync_CanReturnAStoreNamedDirectly()
    {
        var apple = new Tenant
        {
            Name = "Apple Store",
            Category = StoreCategory.Electronics,
            FloorNumber = 1,
            UnitNumber = "L1-01",
            Description = "Phones, computers and technical support.",
            Tags = "electronics, phones, computers"
        };
        await _tenantRepo.AddAsync(apple);

        var response = await _service.ProcessShopperMessageAsync(new ShopperChatRequest(
            "session_002", "Where is the Apple Store?", null, null, null, "DealsEvents"));

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Equal("Apple Store", recommendation.TenantName);
        Assert.Equal("Stores", response.StructuredIntent.ResultFocus);
    }

    [Fact]
    public async Task ProcessShopperMessageAsync_KeepsGreetingConversational()
    {
        await _tenantRepo.AddAsync(new Tenant
        {
            Name = "Popular Bookstore",
            Category = StoreCategory.Entertainment,
            FloorNumber = 2,
            UnitNumber = "L2-01",
            Tags = "books, toys, children"
        });

        var response = await _service.ProcessShopperMessageAsync(new ShopperChatRequest(
            "session_social", "hi", null, null, null, "AskAI"));

        Assert.Empty(response.Recommendations);
        Assert.StartsWith("Hi!", response.Reply);
        Assert.Equal("conversation", response.StructuredIntent.Intent);
    }

    [Fact]
    public async Task ProcessShopperMessageAsync_ReportsRealPipelineStagesAndCanDeferReplyGeneration()
    {
        await _tenantRepo.AddAsync(new Tenant
        {
            Name = "Popular Bookstore",
            Category = StoreCategory.Entertainment,
            FloorNumber = 2,
            UnitNumber = "L2-01",
            Tags = "books, toys, children, education"
        });
        var progress = new List<ShopperChatProgressDto>();
        var request = new ShopperChatRequest(
            "session_stream", "gift for a child who likes reading", null, 1, null, "AskAI", null,
            GenerateReply: false);

        var response = await _service.ProcessShopperMessageAsync(request, progress: (item, _) =>
        {
            progress.Add(item);
            return Task.CompletedTask;
        });

        Assert.Empty(response.Reply);
        Assert.Collection(progress,
            item => Assert.Equal("understanding", item.Stage),
            item => Assert.Equal("searching", item.Stage));
        Assert.Contains(response.Recommendations, item => item.TenantName == "Popular Bookstore");
    }
}
