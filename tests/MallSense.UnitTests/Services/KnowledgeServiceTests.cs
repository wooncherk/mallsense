using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Application.Services;
using MallSense.Domain.Entities;
using MallSense.Infrastructure.Data;
using MallSense.Infrastructure.Repositories;
using MallSense.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace MallSense.UnitTests.Services;

public class KnowledgeServiceTests
{
    [Fact]
    public async Task TenantActor_CanOnlyAccessOwnedCollection()
    {
        await using var context = CreateContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var collectionA = TenantCollection("Tenant A", tenantA);
        var collectionB = TenantCollection("Tenant B", tenantB);
        var mallCollection = new KnowledgeCollection
        {
            Name = "Mall-wide", ScopeType = "Global", Visibility = KnowledgeVisibility.Published
        };
        context.AddRange(collectionA, collectionB, mallCollection);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var actor = new KnowledgeActorDto("TenantStaff", tenantA, "User:staff-a");

        var visible = await service.GetCollectionsAsync(actor);

        Assert.Single(visible);
        Assert.Equal(collectionA.Id, visible[0].Id);
        Assert.DoesNotContain(visible, item => item.Id == mallCollection.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ImportAsync(
            new ImportKnowledgeRequest(collectionB.Id, "Private memo", "Text", "text/plain", "secret", null, null, "AskFirst"), actor));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ImportAsync(
            new ImportKnowledgeRequest(mallCollection.Id, "Mall memo", "Text", "text/plain", "secret", null, null, "AskFirst"), actor));
    }

    [Fact]
    public async Task TenantActor_CannotUseMallNavigationActions()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var collection = TenantCollection("Tenant A", tenantId);
        var source = new KnowledgeSource
        {
            Collection = collection, Title = "Store plan", SourceKind = "File",
            MimeType = "image/png", Content = "base64:AQID", Status = KnowledgeStatus.Draft
        };
        context.Add(source);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var actor = new KnowledgeActorDto("TenantStaff", tenantId, "User:staff-a");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GenerateFloorPlanDraftAsync(
            source.Id, new FloorPlanDraftRequest(1, "Level 1", "AskFirst"), actor));
    }

    [Fact]
    public async Task GuestSearch_ReturnsPublishedKnowledgeAcrossOwners()
    {
        await using var context = CreateContext();
        var publicCollection = new KnowledgeCollection { Name = "Visitor knowledge", Visibility = KnowledgeVisibility.Published };
        var privateCollection = TenantCollection("Private operations", Guid.NewGuid());
        context.AddRange(publicCollection, privateCollection);
        context.KnowledgeSources.AddRange(
            new KnowledgeSource { Collection = publicCollection, Title = "Accessible entrance", SourceKind = "Event", Content = "The north entrance is wheelchair accessible.", AiSummary = "North entrance accessibility.", Status = KnowledgeStatus.Published },
            new KnowledgeSource { Collection = privateCollection, Title = "Private loading entrance", SourceKind = "Deal", Content = "The north loading entrance code is restricted.", AiSummary = "Restricted operations.", Status = KnowledgeStatus.Published });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchAsync("north entrance", new KnowledgeActorDto("Guest", null, "Anonymous"), true);

        Assert.Equal(2, result.Results.Count);
        Assert.Contains(result.Results, item => item.Title == "Accessible entrance");
        Assert.Contains(result.Results, item => item.Title == "Private loading entrance");

        var dealsOnly = await service.SearchAsync("north entrance", new KnowledgeActorDto("Guest", null, "Anonymous"), true,
            sourceKinds: ["Deal"]);
        Assert.Single(dealsOnly.Results);
        Assert.Equal("Private loading entrance", dealsOnly.Results[0].Title);
    }

    [Fact]
    public async Task DealSearch_ExpandsShopperIntentAndReturnsRealPublishedItems()
    {
        await using var context = CreateContext();
        var diningCollection = TenantCollection("Din Tai Fung", Guid.NewGuid());
        context.Add(diningCollection);
        context.KnowledgeSources.AddRange(
            new KnowledgeSource
            {
                Collection = diningCollection,
                Title = "Family Reunion Feast",
                SourceKind = "Deal",
                Content = "Order the family set and receive complimentary Xiao Long Bao.",
                Status = KnowledgeStatus.Published
            },
            new KnowledgeSource
            {
                Collection = diningCollection,
                Title = "Internal kitchen note",
                SourceKind = "General",
                Content = "Food preparation guidance.",
                Status = KnowledgeStatus.Published
            });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchAsync("food", new KnowledgeActorDto("Guest", null, "Anonymous"), true,
            sourceKinds: ["Deal", "Event"]);

        var item = Assert.Single(result.Results);
        Assert.Equal("Family Reunion Feast", item.Title);
    }

    [Fact]
    public async Task DealSearch_ReturnsSemanticMatch_WhenQueryHasNoSharedKeywords()
    {
        await using var context = CreateContext();
        var collection = TenantCollection("Family activities", Guid.NewGuid());
        context.Add(collection);
        context.KnowledgeSources.AddRange(
            new KnowledgeSource
            {
                Collection = collection,
                Title = "LEGO Creator Workshop",
                SourceKind = "Event",
                Content = "Young builders can join a guided brick-making session.",
                Status = KnowledgeStatus.Published
            },
            new KnowledgeSource
            {
                Collection = collection,
                Title = "Office Lunch Discount",
                SourceKind = "Deal",
                Content = "Corporate diners receive a weekday set-menu discount.",
                Status = KnowledgeStatus.Published
            });
        await context.SaveChangesAsync();
        var client = new FakeGeminiClient
        {
            EmbeddingFactory = (content, isQuery) => isQuery || content.Contains("LEGO", StringComparison.OrdinalIgnoreCase)
                ? new float[] { 1, 0 }
                : new float[] { 0, 1 }
        };
        var service = CreateServiceWithClient(context, client);

        var result = await service.SearchAsync("something fun for children",
            new KnowledgeActorDto("Guest", null, "Anonymous"), true, sourceKinds: ["Deal", "Event"]);

        Assert.NotEmpty(result.Results);
        Assert.Equal("LEGO Creator Workshop", result.Results[0].Title);
        Assert.True(result.Results.Count == 1 || result.Results[0].Score > result.Results[1].Score);
    }

    [Fact]
    public async Task Admin_CanGenerateAndPublishFloorPlanGraph()
    {
        await using var context = CreateContext();
        var collection = new KnowledgeCollection { Name = "Mall operations", Visibility = KnowledgeVisibility.Private };
        context.Add(collection);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var actor = new KnowledgeActorDto("Admin", null, "User:admin");
        var imported = await service.ImportAsync(new ImportKnowledgeRequest(
            collection.Id, "Level one plan", "File", "image/png", null, "level-one.png",
            Convert.ToBase64String([1, 2, 3]), "AskFirst"), actor);

        var draft = await service.GenerateFloorPlanDraftAsync(imported.Id, new FloorPlanDraftRequest(1, "Level 1", "AskFirst"), actor);
        var published = await service.PublishFloorPlanDraftAsync(draft.Id, actor);

        Assert.Equal("Published", published.Status);
        Assert.Equal(3, await context.NavigationNodes.CountAsync());
        Assert.Equal(2, await context.NavigationEdges.CountAsync());
        Assert.All(await context.NavigationEdges.ToListAsync(), edge => Assert.True(edge.IsAccessible));
    }

    [Fact]
    public async Task AiSuggestion_DoesNotChangeCanonicalMarkdownUntilAccepted()
    {
        await using var context = CreateContext();
        var collection = new KnowledgeCollection { Name = "Visitor guide", Visibility = KnowledgeVisibility.Private };
        context.Add(collection);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var admin = new KnowledgeActorDto("Admin", null, "User:admin");
        var imported = await service.ImportAsync(new ImportKnowledgeRequest(collection.Id, "Guide", "Text", "text/markdown",
            "# Visitor guide\n\nThe north entrance is wheelchair accessible.", null, null, "Suggest"), admin);

        var suggested = await service.CreateSuggestionAsync(imported.Id,
            new CreateKnowledgeSuggestionRequest("Add verified opening hours."),
            [new KnowledgeEvidenceDto("hours.md", "text/markdown", [], "Open daily from 10:00 AM to 10:00 PM.")], admin);

        Assert.Equal(imported.Content, suggested.Content);
        Assert.Equal("Pending", Assert.Single(suggested.Suggestions).Status);

        var accepted = await service.AcceptSuggestionAsync(imported.Id, suggested.Suggestions[0].Id,
            new DecideKnowledgeSuggestionRequest("AskFirst"), admin);

        Assert.Contains("## Hours", accepted.Content);
        Assert.Equal("Accepted", Assert.Single(accepted.Suggestions).Status);
        var search = await service.SearchAsync("grounded summary", admin, false);
        Assert.Empty(search.Results);
    }

    private static MallDbContext CreateContext() => new(new DbContextOptionsBuilder<MallDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static KnowledgeService CreateService(MallDbContext context) =>
        CreateServiceWithClient(context, new FakeGeminiClient());

    private static KnowledgeService CreateServiceWithClient(MallDbContext context, IGeminiClient client) =>
        new(new KnowledgeRepository(context), client, new SemanticSearchService(client));

    private static KnowledgeCollection TenantCollection(string name, Guid tenantId) => new()
    {
        Name = name, ScopeType = "Tenant", ScopeReferenceId = tenantId, Visibility = KnowledgeVisibility.Private
    };

}
