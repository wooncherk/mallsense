using MallSense.Application.Services;
using MallSense.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Infrastructure.Data;

public static class MallDbContextSeed
{
    private const string AdminEmail = "admin@test.com";
    private const string TenantEmail = "test@skechers.com";
    private const string PreviousTenantEmail = "tenant@test.com";

    private static readonly string[] LegacyTenantNames =
    [
        "Apple Store",
        "Nike Flagship",
        "Starbucks Reserve",
        "Zara",
        "Sephora",
        "Popular Bookstore",
        "Toys\"R\"Us",
        "Guardian Pharmacy",
        "Uniqlo",
        "Watsons",
        "Cineplex IMAX & Arcade",
        "Din Tai Fung",
        "Haidilao Hotpot",
        "Kidz World Playground"
    ];

    private static readonly string[] LegacyUserNames =
    [
        "admin@thegalleria.com",
        "popular@tenant.com",
        "starbucks@tenant.com"
    ];

    public static async Task SeedAsync(MallDbContext context)
    {
        await RemoveLegacyDemoSeedAsync(context);
        await SunwayKluangMallDataSeeder.SeedAsync(context);
        await EnsureSeedUsersAsync(context);
    }

    private static async Task RemoveLegacyDemoSeedAsync(MallDbContext context)
    {
        var legacyTenants = await context.Tenants
            .Where(tenant => LegacyTenantNames.Contains(tenant.Name))
            .ToListAsync();
        var legacyTenantIds = legacyTenants.Select(tenant => tenant.Id).ToHashSet();

        if (legacyTenantIds.Count > 0)
        {
            var tenantCollections = await context.KnowledgeCollections
                .Where(collection => collection.ScopeType == "Tenant" &&
                    collection.ScopeReferenceId != null &&
                    legacyTenantIds.Contains(collection.ScopeReferenceId.Value))
                .ToListAsync();
            context.KnowledgeCollections.RemoveRange(tenantCollections);

            var promotions = await context.Promotions
                .Where(promotion => legacyTenantIds.Contains(promotion.TenantId))
                .ToListAsync();
            context.Promotions.RemoveRange(promotions);
            context.Tenants.RemoveRange(legacyTenants);
        }

        var legacySources = await context.KnowledgeSources
            .Where(source => source.PromptVersion.StartsWith("seed-"))
            .ToListAsync();
        context.KnowledgeSources.RemoveRange(legacySources);

        var legacyUsers = await context.Users
            .Where(user => LegacyUserNames.Contains(user.UserName) ||
                (user.Email != null && LegacyUserNames.Contains(user.Email)))
            .ToListAsync();
        context.Users.RemoveRange(legacyUsers);

        var legacyIntentQueries = new[]
        {
            "I need a gift for my 8-year-old nephew, something fun or educational",
            "Where can I have dinner with my parents near the cinema?",
            "Something nice to bring to my in-laws this weekend, not clothing"
        };
        var legacyIntentSignals = await context.IntentSignals
            .Where(signal => legacyIntentQueries.Contains(signal.RawQuery))
            .ToListAsync();
        context.IntentSignals.RemoveRange(legacyIntentSignals);

        // The old demo created the entire navigation graph when these fictional
        // anchors were present. Remove that graph as one unit during an upgrade.
        var hasLegacyNavigationGraph = await context.NavigationNodes.AnyAsync(node =>
            node.Label == "Apple Store" || node.Label == "Haidilao Hotpot");
        if (hasLegacyNavigationGraph)
        {
            context.NavigationEdges.RemoveRange(await context.NavigationEdges.ToListAsync());
            context.Facilities.RemoveRange(await context.Facilities.ToListAsync());
            context.NavigationNodes.RemoveRange(await context.NavigationNodes.ToListAsync());
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureSeedUsersAsync(MallDbContext context)
    {
        var tenant = await context.Tenants
            .Where(item => item.Name == "SKECHERS")
            .OrderBy(item => item.FloorNumber)
            .ThenBy(item => item.UnitNumber)
            .FirstOrDefaultAsync()
            ?? await context.Tenants
                .Where(item => item.Name != "Sunway Kluang Mall")
                .OrderBy(item => item.Name)
                .FirstAsync();

        var admin = await context.Users.FirstOrDefaultAsync(user => user.UserName == AdminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser();
            context.Users.Add(admin);
        }
        admin.UserName = AdminEmail;
        admin.Email = AdminEmail;
        admin.FullName = "Sunway Kluang Mall Administrator";
        admin.PasswordHash = AuthService.HashPassword("Admin123!");
        admin.Role = UserRole.Admin;
        admin.TenantId = null;

        var tenantUser = await context.Users.FirstOrDefaultAsync(user => user.UserName == TenantEmail)
            ?? await context.Users.FirstOrDefaultAsync(user => user.UserName == PreviousTenantEmail);
        if (tenantUser == null)
        {
            tenantUser = new ApplicationUser();
            context.Users.Add(tenantUser);
        }
        tenantUser.UserName = TenantEmail;
        tenantUser.Email = TenantEmail;
        tenantUser.FullName = $"{tenant.Name} Manager";
        tenantUser.PasswordHash = AuthService.HashPassword("Tenant123!");
        tenantUser.Role = UserRole.TenantStaff;
        tenantUser.TenantId = tenant.Id;

        await context.SaveChangesAsync();
    }
}
