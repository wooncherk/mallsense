using MallSense.Application.Services;
using MallSense.Domain.Entities;
using MallSense.UnitTests.Fakes;
using Xunit;

namespace MallSense.UnitTests.Services;

public class MallAnalyticsServiceTests
{
    [Fact]
    public async Task DailySummary_ScopesTenantSignalsAndProducesApprovalGatedActions()
    {
        var tenants = new FakeTenantRepository();
        var popular = await tenants.AddAsync(new Tenant
        {
            Name = "Popular Bookstore",
            FloorNumber = 2,
            UnitNumber = "L2-01"
        });
        var otherTenantId = Guid.NewGuid();
        var behavior = new FakeShopperBehaviorEventRepository();
        var now = DateTime.UtcNow;

        await behavior.AddAsync(new ShopperBehaviorEvent
        {
            SessionId = "visit-1",
            EventType = "SearchResultsViewed",
            Query = "children's activities",
            ResultCount = 2,
            RelatedTenantIds = popular.Id.ToString(),
            TimestampUtc = now
        });
        await behavior.AddAsync(new ShopperBehaviorEvent
        {
            SessionId = "visit-1",
            EventType = "ContentOpened",
            TargetTenantId = popular.Id,
            TargetTitle = "Children's Reading Week",
            TimestampUtc = now
        });
        await behavior.AddAsync(new ShopperBehaviorEvent
        {
            SessionId = "visit-2",
            EventType = "SearchResultsViewed",
            Query = "fashion sale",
            ResultCount = 1,
            RelatedTenantIds = otherTenantId.ToString(),
            TimestampUtc = now
        });

        var service = new MallAnalyticsService(
            new FakeIntentSignalRepository(),
            new FakePromotionRepository(),
            new FakeGeminiClient(),
            behavior,
            tenants);

        var summary = await service.GetDailySummaryAsync(popular.Id);

        Assert.Equal("Tenant", summary.Scope);
        Assert.Equal("Popular Bookstore", summary.ScopeName);
        Assert.Equal(1, summary.TotalSearches);
        Assert.Equal(1, summary.ContentOpens);
        Assert.Equal("children's activities", Assert.Single(summary.TopQueries).Label);
        Assert.All(summary.PotentialActions, action => Assert.True(action.RequiresApproval));
    }

    [Fact]
    public async Task DailyNarrative_UsesTheRoleScopedSummary()
    {
        var tenants = new FakeTenantRepository();
        var tenant = await tenants.AddAsync(new Tenant
        {
            Name = "Popular Bookstore",
            FloorNumber = 2,
            UnitNumber = "L2-01"
        });
        var behavior = new FakeShopperBehaviorEventRepository();
        await behavior.AddAsync(new ShopperBehaviorEvent
        {
            SessionId = "visit-1",
            EventType = "SearchResultsViewed",
            Query = "children's books",
            ResultCount = 2,
            RelatedTenantIds = tenant.Id.ToString(),
            TimestampUtc = DateTime.UtcNow
        });
        var service = CreateService(behavior, tenants);

        var narrative = await service.GenerateDailyNarrativeAsync(tenant.Id);

        Assert.Equal("Tenant", narrative.Scope);
        Assert.Equal("Popular Bookstore", narrative.ScopeName);
        Assert.False(string.IsNullOrWhiteSpace(narrative.SummaryText));
    }

    [Fact]
    public async Task DailyChat_ReturnsAnEvidenceGroundedAnswer()
    {
        var service = CreateService(new FakeShopperBehaviorEventRepository(), new FakeTenantRepository());

        var response = await service.AskDailySummaryAsync("What needs attention today?");

        Assert.False(string.IsNullOrWhiteSpace(response.Answer));
    }

    [Fact]
    public async Task SummaryRange_IncludesActivityFromTheSelectedPeriod()
    {
        var behavior = new FakeShopperBehaviorEventRepository();
        await behavior.AddAsync(new ShopperBehaviorEvent
        {
            SessionId = "earlier-visit",
            EventType = "SearchSubmitted",
            Query = "weekend events",
            ResultCount = 1,
            TimestampUtc = DateTime.UtcNow.AddDays(-3)
        });
        var service = CreateService(behavior, new FakeTenantRepository());

        var today = await service.GetDailySummaryAsync(days: 1);
        var week = await service.GetDailySummaryAsync(days: 7);

        Assert.Equal(0, today.TotalSearches);
        Assert.Equal(1, week.TotalSearches);
        Assert.Contains("–", week.LocalDate);
    }

    private static MallAnalyticsService CreateService(
        FakeShopperBehaviorEventRepository behavior,
        FakeTenantRepository tenants) => new(
            new FakeIntentSignalRepository(),
            new FakePromotionRepository(),
            new FakeGeminiClient(),
            behavior,
            tenants);
}
