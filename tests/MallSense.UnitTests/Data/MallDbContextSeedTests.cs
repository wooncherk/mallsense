using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Infrastructure.Data;
using MallSense.WebApi.Data;

namespace MallSense.UnitTests.Data;

public class MallDbContextSeedTests
{
    [Fact]
    public async Task Sqlite_ShouldCreateSchemaAndPersistKnowledge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MallDbContext>().UseSqlite(connection).Options;

        await using (var writeContext = new MallDbContext(options))
        {
            await writeContext.Database.EnsureCreatedAsync();
            writeContext.KnowledgeCollections.Add(new KnowledgeCollection
            {
                Name = "Mall-wide", ScopeType = "Global", Visibility = KnowledgeVisibility.Published,
            });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new MallDbContext(options);
        Assert.Equal("Mall-wide", (await readContext.KnowledgeCollections.SingleAsync()).Name);
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateTenantsUsersAndTenantRelationships()
    {
        var options = new DbContextOptionsBuilder<MallDbContext>()
            .UseInMemoryDatabase($"MallSenseSeedTests-{Guid.NewGuid()}")
            .Options;

        await using var context = new MallDbContext(options);
        await MallDbContextSeed.SeedAsync(context);

        Assert.Equal(125, await context.Tenants.CountAsync());
        Assert.Equal(2, await context.Users.CountAsync());
        var admin = await context.Users.SingleAsync(user => user.UserName == "admin@test.com");
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Null(admin.TenantId);
        var tenantUser = await context.Users.SingleAsync(user => user.UserName == "test@skechers.com");
        Assert.Equal(UserRole.TenantStaff, tenantUser.Role);
        Assert.NotNull(tenantUser.TenantId);
        Assert.Equal("SKECHERS", (await context.Tenants.FindAsync(tenantUser.TenantId))!.Name);
        Assert.All(
            await context.Promotions.Include(promotion => promotion.Tenant).ToListAsync(),
            promotion => Assert.NotNull(promotion.Tenant));
        Assert.DoesNotContain(await context.Tenants.ToListAsync(), tenant =>
            tenant.Name is "Apple Store" or "Haidilao Hotpot" or "Din Tai Fung");

        var dealSources = await context.KnowledgeSources.Where(source => source.SourceKind == "Deal").ToListAsync();
        Assert.Equal(9, dealSources.Count);
        var eventSources = await context.KnowledgeSources.Where(source => source.SourceKind == "Event").ToListAsync();
        Assert.Equal(4, eventSources.Count);
        Assert.All(eventSources, source =>
        {
            var promotion = context.Promotions.Single(item => item.Title == source.Title);
            var owner = context.KnowledgeCollections.Single(item => item.Id == source.CollectionId);
            Assert.Equal("Tenant", owner.ScopeType);
            Assert.Equal(promotion.TenantId, owner.ScopeReferenceId);
            Assert.Contains("schedule:", source.Content);
            Assert.Contains("location:", source.Content);
            Assert.Equal("sunway-kluang-import-v1", source.PromptVersion);
        });
        Assert.All(dealSources, source =>
        {
            var promotion = context.Promotions.Single(item => item.Title == source.Title);
            var owner = context.KnowledgeCollections.Single(item => item.Id == source.CollectionId);
            Assert.Equal("Tenant", owner.ScopeType);
            Assert.Equal(promotion.TenantId, owner.ScopeReferenceId);
            Assert.Equal("sunway-kluang-import-v1", source.PromptVersion);
        });

        context.KnowledgeSources.Remove(eventSources[0]);
        await context.SaveChangesAsync();
        await MallDbContextSeed.SeedAsync(context);
        Assert.Equal(4, await context.KnowledgeSources.CountAsync(source => source.SourceKind == "Event"));
    }

    [Fact]
    public async Task SeedAsync_ShouldImportCapturedSunwayStoresDealsAndEventsIdempotently()
    {
        var options = new DbContextOptionsBuilder<MallDbContext>()
            .UseInMemoryDatabase($"MallSenseSunwaySeedTests-{Guid.NewGuid()}")
            .Options;

        await using var context = new MallDbContext(options);
        await MallDbContextSeed.SeedAsync(context);

        Assert.False(await context.KnowledgeSources.AnyAsync(source => source.SourceKind == "TenantProfile"));
        Assert.Equal(124, await context.Tenants.CountAsync(tenant => tenant.StorefrontImageUrl != null));
        Assert.Equal(9, await context.KnowledgeSources.CountAsync(source =>
            source.SourceKind == "Deal" && source.PromptVersion == "sunway-kluang-import-v1"));
        Assert.Equal(4, await context.KnowledgeSources.CountAsync(source =>
            source.SourceKind == "Event" && source.PromptVersion == "sunway-kluang-import-v1"));

        Assert.Equal([-1, 0, 1, 2, 3], await context.Tenants
            .Where(tenant => tenant.Name != "Sunway Kluang Mall")
            .Select(tenant => tenant.FloorNumber)
            .Distinct()
            .OrderBy(floor => floor)
            .ToListAsync());
        Assert.Equal(-1, (await context.Tenants.SingleAsync(tenant => tenant.Name == "TEXAS CHICKEN")).FloorNumber);
        Assert.Equal(0, (await context.Tenants.SingleAsync(tenant => tenant.Name == "MARRYBROWN")).FloorNumber);
        Assert.Equal(1, (await context.Tenants.SingleAsync(tenant => tenant.Name == "SKECHERS")).FloorNumber);

        var importedDeals = await context.KnowledgeSources
            .Where(source => source.SourceKind == "Deal" && source.PromptVersion == "sunway-kluang-import-v1")
            .ToListAsync();
        Assert.All(importedDeals, source =>
        {
            var promotion = context.Promotions.Single(item => item.Title == source.Title);
            var owner = context.KnowledgeCollections.Single(item => item.Id == source.CollectionId);
            Assert.Equal(promotion.TenantId, owner.ScopeReferenceId);
            Assert.Contains("source_url: https://www.sunwaykluangmall.com/promotions/", source.Content);
        });

        var importedEvents = await context.KnowledgeSources
            .Where(source => source.SourceKind == "Event" && source.PromptVersion == "sunway-kluang-import-v1")
            .ToListAsync();
        var mallOperator = await context.Tenants.SingleAsync(tenant => tenant.Name == "Sunway Kluang Mall");
        Assert.All(importedEvents, source =>
        {
            var promotion = context.Promotions.Single(item => item.Title == source.Title);
            Assert.Equal(mallOperator.Id, promotion.TenantId);
            Assert.Equal(PromotionType.EventOrWorkshop, promotion.Type);
        });

        var countsBefore = new
        {
            Tenants = await context.Tenants.CountAsync(),
            Promotions = await context.Promotions.CountAsync(),
            Collections = await context.KnowledgeCollections.CountAsync(),
            Sources = await context.KnowledgeSources.CountAsync()
        };
        await MallDbContextSeed.SeedAsync(context);
        Assert.Equal(countsBefore.Tenants, await context.Tenants.CountAsync());
        Assert.Equal(countsBefore.Promotions, await context.Promotions.CountAsync());
        Assert.Equal(countsBefore.Collections, await context.KnowledgeCollections.CountAsync());
        Assert.Equal(countsBefore.Sources, await context.KnowledgeSources.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldRepairImportedTenantFloorNumbers()
    {
        var options = new DbContextOptionsBuilder<MallDbContext>()
            .UseInMemoryDatabase($"MallSenseFloorRepairTests-{Guid.NewGuid()}")
            .Options;

        await using var context = new MallDbContext(options);
        await MallDbContextSeed.SeedAsync(context);

        (await context.Tenants.SingleAsync(tenant => tenant.Name == "TEXAS CHICKEN")).FloorNumber = 0;
        (await context.Tenants.SingleAsync(tenant => tenant.Name == "MARRYBROWN")).FloorNumber = 1;
        await context.SaveChangesAsync();

        await MallDbContextSeed.SeedAsync(context);

        Assert.Equal(-1, (await context.Tenants.SingleAsync(tenant => tenant.Name == "TEXAS CHICKEN")).FloorNumber);
        Assert.Equal(0, (await context.Tenants.SingleAsync(tenant => tenant.Name == "MARRYBROWN")).FloorNumber);
    }

    [Fact]
    public async Task SeedAsync_ShouldRemoveLegacyDemoDatabaseRecords()
    {
        var options = new DbContextOptionsBuilder<MallDbContext>()
            .UseInMemoryDatabase($"MallSenseSeedRepairTests-{Guid.NewGuid()}")
            .Options;

        await using var context = new MallDbContext(options);
        var apple = new Tenant
        {
            Name = "Apple Store",
            Description = "Legacy demo tenant",
            FloorNumber = 1,
            UnitNumber = "L1-01"
        };
        var collection = new KnowledgeCollection
        {
            Name = apple.Name,
            ScopeType = "Tenant",
            ScopeReferenceId = apple.Id
        };
        var obsoleteTenant = new Tenant
        {
            Name = "Old Demo Shop",
            Description = "Not present in the captured directory",
            FloorNumber = 9,
            UnitNumber = "OLD-01"
        };
        var obsoleteCollection = new KnowledgeCollection
        {
            Name = obsoleteTenant.Name,
            ScopeType = "Tenant",
            ScopeReferenceId = obsoleteTenant.Id
        };
        context.Tenants.AddRange(apple, obsoleteTenant);
        context.NavigationNodes.Add(new NavigationNode
        {
            Label = "Haidilao Hotpot",
            FloorNumber = 3,
            NodeType = NodeType.Shop
        });
        context.KnowledgeCollections.AddRange(collection, obsoleteCollection);
        context.KnowledgeSources.Add(new KnowledgeSource
        {
            CollectionId = collection.Id,
            Title = "Legacy Apple deal",
            SourceKind = "Deal",
            MimeType = "text/plain",
            Content = "Old fictional content",
            PromptVersion = "seed-v2"
        });
        context.KnowledgeSources.Add(new KnowledgeSource
        {
            CollectionId = obsoleteCollection.Id,
            Title = "Old event",
            SourceKind = "Event",
            MimeType = "text/plain",
            Content = "No longer in the captured events catalogue",
            PromptVersion = "old-import-v0"
        });
        context.Promotions.Add(new Promotion
        {
            TenantId = obsoleteTenant.Id,
            Title = "Old event",
            Type = PromotionType.EventOrWorkshop,
            OfferSummary = "Obsolete event",
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddDays(1)
        });
        context.Users.Add(new ApplicationUser
        {
            UserName = "admin@thegalleria.com",
            Email = "admin@thegalleria.com",
            FullName = "Legacy Admin",
            Role = UserRole.Admin
        });
        await context.SaveChangesAsync();

        await MallDbContextSeed.SeedAsync(context);

        Assert.False(await context.Tenants.AnyAsync(tenant => tenant.Name == "Apple Store"));
        Assert.False(await context.Tenants.AnyAsync(tenant => tenant.Name == "Old Demo Shop"));
        Assert.False(await context.NavigationNodes.AnyAsync(node => node.Label == "Haidilao Hotpot"));
        Assert.False(await context.KnowledgeSources.AnyAsync(source => source.PromptVersion.StartsWith("seed-")));
        Assert.False(await context.KnowledgeSources.AnyAsync(source => source.Title == "Old event"));
        Assert.False(await context.Promotions.AnyAsync(promotion => promotion.Title == "Old event"));
        Assert.False(await context.Users.AnyAsync(user => user.UserName == "admin@thegalleria.com"));
        Assert.True(await context.Users.AnyAsync(user => user.UserName == "admin@test.com"));
        Assert.True(await context.Users.AnyAsync(user => user.UserName == "test@skechers.com"));
    }
}
