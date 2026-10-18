using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Infrastructure.Data;

/// <summary>
/// Imports the captured Sunway Kluang Mall directory, deals and events into the
/// relational model. The source JSON is embedded by MallSense.Infrastructure so
/// seeding works from tests, local runs and published applications.
/// </summary>
public static class SunwayKluangMallDataSeeder
{
    private const string ResourcePrefix = "MallSense.Infrastructure.SeedData.SunwayKluangMall";
    private const string PromptVersion = "sunway-kluang-import-v1";
    private const string MallOperatorName = "Sunway Kluang Mall";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAsync(MallDbContext context)
    {
        var stores = ReadResource<List<StoreRecord>>("stores.json");
        var deals = ReadResource<List<DealRecord>>("deals.json");
        var events = ReadResource<List<EventRecord>>("events.json");

        var tenants = await context.Tenants.ToListAsync();
        var importedTenantProfiles = await context.KnowledgeSources
            .Where(source => source.SourceKind == "TenantProfile" && source.PromptVersion == PromptVersion)
            .ToListAsync();
        context.KnowledgeSources.RemoveRange(importedTenantProfiles);
        var originalTenantsByName = tenants
            .GroupBy(tenant => Normalize(tenant.Name))
            .ToDictionary(group => group.Key, group => group.ToList());
        var tenantsByName = tenants
            .GroupBy(tenant => Normalize(tenant.Name))
            .ToDictionary(group => group.Key, group => group.First());
        var tenantsById = tenants.ToDictionary(tenant => tenant.Id);
        var claimedOriginalTenants = new HashSet<Guid>();
        var usedLocations = tenants
            .Select(tenant => LocationKey(tenant.FloorNumber, tenant.UnitNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tenantsByStoreId = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);

        foreach (var store in stores)
        {
            var name = Limit(Clean(store.Title), 100);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var floor = ParseFloor(store.Level, store.Venue);
            var tenantId = StableGuid($"tenant:{store.Id ?? store.Slug ?? name}");
            if (!tenantsById.TryGetValue(tenantId, out var tenant))
            {
                tenant = originalTenantsByName.TryGetValue(Normalize(name), out var matchingOriginals)
                    ? matchingOriginals.FirstOrDefault(item => claimedOriginalTenants.Add(item.Id))
                    : null;
                if (tenant == null)
                {
                    var unit = AllocateUnitNumber(floor, store.Venue, store.Slug, usedLocations);
                    tenant = new Tenant
                    {
                        Id = tenantId,
                        Name = name,
                        Description = Limit(Clean(store.Text), 500),
                        Category = MapCategory(store),
                        FloorNumber = floor,
                        UnitNumber = unit,
                        ContactEmail = NullIfEmpty(Limit(store.Contact?.Email, 150)),
                        ContactPhone = NullIfEmpty(Limit(store.Contact?.Phone, 50)),
                        OpeningHours = "10:00 AM - 10:00 PM",
                        Tags = BuildTenantTags(store),
                        PriceLevel = "mid",
                        IsActive = true
                    };
                    context.Tenants.Add(tenant);
                    tenants.Add(tenant);
                    tenantsById[tenant.Id] = tenant;
                    tenantsByName.TryAdd(Normalize(name), tenant);
                }
            }
            else
            {
                claimedOriginalTenants.Add(tenant.Id);
            }

            // The captured directory is authoritative for tenant locations. Keep
            // this assignment outside the creation branch so startup seeding also
            // repairs databases imported with an older/incorrect floor mapping.
            tenant.FloorNumber = floor;

            if (string.IsNullOrWhiteSpace(tenant.StorefrontImageUrl))
                tenant.StorefrontImageUrl = NullIfEmpty(ImageUrl(store.MainImage));

            if (!string.IsNullOrWhiteSpace(store.Id)) tenantsByStoreId[store.Id] = tenant;
            if (!string.IsNullOrWhiteSpace(store.Slug)) tenantsByStoreId[store.Slug] = tenant;
        }

        var mallOperator = GetOrCreateMallOperator(context, tenants, tenantsByName, usedLocations);
        await context.SaveChangesAsync();

        var collections = await EnsureTenantCollectionsAsync(context, tenantsByStoreId.Values.Append(mallOperator));
        await SeedDealsAsync(context, deals, tenantsByStoreId, tenantsByName, collections);
        await SeedEventsAsync(context, events, mallOperator, collections[mallOperator.Id]);
        await context.SaveChangesAsync();
        await RemoveDataOutsideCapturedCatalogueAsync(
            context, stores, deals, events, tenantsByStoreId, tenantsByName, collections, mallOperator);
    }

    private static Tenant GetOrCreateMallOperator(
        MallDbContext context,
        List<Tenant> tenants,
        IDictionary<string, Tenant> tenantsByName,
        ISet<string> usedLocations)
    {
        if (tenantsByName.TryGetValue(Normalize(MallOperatorName), out var existing)) return existing;

        var tenant = new Tenant
        {
            Id = StableGuid("tenant:sunway-kluang-mall-operator"),
            Name = MallOperatorName,
            Description = "Mall-wide campaigns, services and events operated by Sunway Kluang Mall.",
            Category = StoreCategory.Services,
            FloorNumber = 0,
            UnitNumber = AllocateUnitNumber(0, "MALL", "sunway-kluang-mall", usedLocations),
            ContactEmail = "skmconcierge@sunway.com.my",
            ContactPhone = "+607 772 6655",
            OpeningHours = "10:00 AM - 10:00 PM",
            Tags = "sunway kluang mall, concierge, mall events, mall-wide campaigns",
            PriceLevel = "mid",
            IsActive = true
        };
        context.Tenants.Add(tenant);
        tenants.Add(tenant);
        tenantsByName[Normalize(tenant.Name)] = tenant;
        return tenant;
    }

    private static async Task<Dictionary<Guid, KnowledgeCollection>> EnsureTenantCollectionsAsync(
        MallDbContext context,
        IEnumerable<Tenant> importedTenants)
    {
        var tenantIds = importedTenants.Select(tenant => tenant.Id).Distinct().ToHashSet();
        var collections = await context.KnowledgeCollections
            .Where(collection => collection.ScopeType == "Tenant" &&
                collection.ScopeReferenceId != null &&
                tenantIds.Contains(collection.ScopeReferenceId.Value))
            .ToDictionaryAsync(collection => collection.ScopeReferenceId!.Value);

        foreach (var tenant in importedTenants.DistinctBy(tenant => tenant.Id))
        {
            if (collections.ContainsKey(tenant.Id)) continue;
            var collection = new KnowledgeCollection
            {
                Id = StableGuid($"knowledge-collection:tenant:{tenant.Id}"),
                Name = tenant.Name,
                Description = "Imported Sunway Kluang Mall deals and events.",
                ScopeType = "Tenant",
                ScopeReferenceId = tenant.Id,
                Visibility = KnowledgeVisibility.Published
            };
            context.KnowledgeCollections.Add(collection);
            collections[tenant.Id] = collection;
        }

        await context.SaveChangesAsync();
        return collections;
    }

    private static async Task SeedDealsAsync(
        MallDbContext context,
        IEnumerable<DealRecord> deals,
        IReadOnlyDictionary<string, Tenant> tenantsByStoreId,
        IReadOnlyDictionary<string, Tenant> tenantsByName,
        IReadOnlyDictionary<Guid, KnowledgeCollection> collections)
    {
        var existingPromotions = await context.Promotions.ToListAsync();
        var existingSources = await ImportedSourceKeysAsync(context, "Deal");

        foreach (var deal in deals)
        {
            if (!TryResolveStoreTenant(deal.ShopMeta?.ExternalId ?? deal.ShopMeta?.Id, deal.ShopMeta?.Slug, deal.ShopMeta?.Title,
                    tenantsByStoreId, tenantsByName, out var tenant) ||
                !collections.TryGetValue(tenant.Id, out var collection)) continue;

            var title = Limit(Clean(deal.Title), 150);
            if (string.IsNullOrWhiteSpace(title)) continue;
            var start = ParseIsoDate(deal.DateStart, DateTime.UtcNow.Date);
            var end = ParseIsoDate(deal.DateEnd, start.AddMonths(1));
            var summary = Limit(FirstUsefulText(deal.Description, deal.Text, title), 300);
            var promotion = existingPromotions.FirstOrDefault(item =>
                item.TenantId == tenant.Id && item.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (promotion == null)
            {
                promotion = new Promotion
                {
                    Id = StableGuid($"promotion:deal:{deal.Id ?? deal.Slug ?? title}"),
                    TenantId = tenant.Id,
                    Title = title,
                    Type = InferDealType(title, deal.Description),
                    OfferSummary = summary,
                    ValidFrom = start,
                    ValidTo = end,
                    TargetAudience = "Sunway Kluang Mall shoppers",
                    Occasions = "Shopping, dining and lifestyle",
                    DietaryFlags = InferDietaryFlags(deal.Description),
                    PriceRange = InferPriceRange(deal.Description),
                    AdvanceOrderRequired = NullIfEmpty(Limit(Clean(deal.Term), 300)),
                    PosterImageUrl = NullIfEmpty(ImageUrl(deal.MainImage)),
                    ReadinessScore = 95,
                    ReadinessNotes = "Imported from the public Sunway Kluang Mall deals catalogue.",
                    IsActive = end.Date >= DateTime.UtcNow.Date
                };
                context.Promotions.Add(promotion);
                existingPromotions.Add(promotion);
            }

            var sourceKey = SourceKey(collection.Id, "Deal", title);
            if (!existingSources.Add(sourceKey)) continue;
            var sourceUrl = $"https://www.sunwaykluangmall.com/promotions/{deal.Slug}";
            var image = ImageUrl(deal.MainImage);
            var terms = string.IsNullOrWhiteSpace(deal.Term) ? string.Empty : $"\n\n## Terms and conditions\n\n{Clean(deal.Term)}";
            var content = $"""
                ---
                kind: deal
                tenant_id: {tenant.Id}
                tenant: {tenant.Name}
                source_id: {deal.Id}
                source_url: {sourceUrl}
                available_from: {start:yyyy-MM-dd}
                available_until: {end:yyyy-MM-dd}
                ---

                {(string.IsNullOrWhiteSpace(image) ? string.Empty : $"![{title}]({image})")}

                # {title}

                {Clean(deal.Description)}{terms}
                """;
            context.KnowledgeSources.Add(CreateKnowledgeSource(collection.Id, title, "Deal", content, summary));
        }
    }

    private static async Task SeedEventsAsync(
        MallDbContext context,
        IEnumerable<EventRecord> events,
        Tenant mallOperator,
        KnowledgeCollection collection)
    {
        var existingPromotions = await context.Promotions.ToListAsync();
        var existingSources = await ImportedSourceKeysAsync(context, "Event");

        foreach (var item in events)
        {
            var title = Limit(Clean(item.Title), 150);
            if (string.IsNullOrWhiteSpace(title)) continue;
            var (start, end) = ParseDisplayDateRange(item.DateDisplay);
            var summary = Limit(FirstUsefulText(item.DetailText, item.Description, title), 300);
            var promotion = existingPromotions.FirstOrDefault(existing =>
                existing.TenantId == mallOperator.Id && existing.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (promotion == null)
            {
                promotion = new Promotion
                {
                    Id = StableGuid($"promotion:event:{item.Slug ?? title}"),
                    TenantId = mallOperator.Id,
                    Title = title,
                    Type = PromotionType.EventOrWorkshop,
                    OfferSummary = summary,
                    ValidFrom = start,
                    ValidTo = end,
                    TargetAudience = "Mall visitors",
                    Occasions = "Mall event and community activity",
                    AdvanceOrderRequired = NullIfEmpty(Limit(Clean(item.TermsAndConditions), 300)),
                    PosterImageUrl = NullIfEmpty(item.ImageUrls?.FirstOrDefault() ?? item.ListingImageUrl),
                    ReadinessScore = 90,
                    ReadinessNotes = "Imported from the public Sunway Kluang Mall events catalogue.",
                    IsActive = end.Date >= DateTime.UtcNow.Date
                };
                context.Promotions.Add(promotion);
                existingPromotions.Add(promotion);
            }

            var sourceKey = SourceKey(collection.Id, "Event", title);
            if (!existingSources.Add(sourceKey)) continue;
            var image = item.ImageUrls?.FirstOrDefault() ?? item.ListingImageUrl;
            var terms = string.IsNullOrWhiteSpace(item.TermsAndConditions)
                ? string.Empty
                : $"\n\n## Terms and conditions\n\n{Clean(item.TermsAndConditions)}";
            var content = $"""
                ---
                kind: event
                tenant_id: {mallOperator.Id}
                tenant: {mallOperator.Name}
                source_url: {item.SourceUrl}
                available_from: {start:yyyy-MM-dd}
                available_until: {end:yyyy-MM-dd}
                schedule: {item.DateDisplay}
                location: Sunway Kluang Mall
                ---

                {(string.IsNullOrWhiteSpace(image) ? string.Empty : $"![{title}]({image})")}

                # {title}

                {FirstUsefulText(item.DetailText, item.Description, title)}{terms}
                """;
            context.KnowledgeSources.Add(CreateKnowledgeSource(collection.Id, title, "Event", content, summary));
        }
    }

    private static KnowledgeSource CreateKnowledgeSource(
        Guid collectionId,
        string title,
        string kind,
        string content,
        string summary) => new()
        {
            Id = StableGuid($"knowledge-source:{kind}:{collectionId}:{title}"),
            CollectionId = collectionId,
            Title = Limit(title, 200),
            SourceKind = kind,
            MimeType = "text/markdown",
            Content = content,
            AiSummary = summary,
            ClaimsJson = JsonSerializer.Serialize(new[] { summary }),
            Status = KnowledgeStatus.Published,
            PromptVersion = PromptVersion
        };

    private static async Task RemoveDataOutsideCapturedCatalogueAsync(
        MallDbContext context,
        IEnumerable<StoreRecord> stores,
        IEnumerable<DealRecord> deals,
        IEnumerable<EventRecord> events,
        IReadOnlyDictionary<string, Tenant> tenantsByStoreId,
        IReadOnlyDictionary<string, Tenant> tenantsByName,
        IReadOnlyDictionary<Guid, KnowledgeCollection> collections,
        Tenant mallOperator)
    {
        var validTenantIds = tenantsByStoreId.Values
            .Append(mallOperator)
            .Select(tenant => tenant.Id)
            .ToHashSet();
        var validSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validPromotionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var deal in deals)
        {
            if (!TryResolveStoreTenant(deal.ShopMeta?.ExternalId ?? deal.ShopMeta?.Id, deal.ShopMeta?.Slug,
                    deal.ShopMeta?.Title, tenantsByStoreId, tenantsByName, out var tenant) ||
                !collections.TryGetValue(tenant.Id, out var collection)) continue;
            var title = Limit(Clean(deal.Title), 150);
            validSourceKeys.Add(SourceKey(collection.Id, "Deal", title));
            validPromotionKeys.Add(PromotionKey(tenant.Id, title));
        }

        foreach (var item in events)
        {
            var title = Limit(Clean(item.Title), 150);
            validSourceKeys.Add(SourceKey(collections[mallOperator.Id].Id, "Event", title));
            validPromotionKeys.Add(PromotionKey(mallOperator.Id, title));
        }

        var catalogueSources = await context.KnowledgeSources
            .Where(source => source.SourceKind == "Deal" || source.SourceKind == "Event")
            .ToListAsync();
        context.KnowledgeSources.RemoveRange(catalogueSources.Where(source =>
            !validSourceKeys.Contains(SourceKey(source.CollectionId, source.SourceKind, source.Title))));

        var promotions = await context.Promotions.ToListAsync();
        context.Promotions.RemoveRange(promotions.Where(promotion =>
            !validPromotionKeys.Contains(PromotionKey(promotion.TenantId, promotion.Title))));

        var obsoleteTenantIds = (await context.Tenants.Select(tenant => tenant.Id).ToListAsync())
            .Where(id => !validTenantIds.Contains(id))
            .ToHashSet();
        if (obsoleteTenantIds.Count > 0)
        {
            var obsoleteCollections = await context.KnowledgeCollections
                .Where(collection => collection.ScopeType == "Tenant" &&
                    collection.ScopeReferenceId != null &&
                    obsoleteTenantIds.Contains(collection.ScopeReferenceId.Value))
                .ToListAsync();
            context.KnowledgeCollections.RemoveRange(obsoleteCollections);

            var obsoleteNodes = await context.NavigationNodes
                .Where(node => node.TenantId != null && obsoleteTenantIds.Contains(node.TenantId.Value))
                .ToListAsync();
            var obsoleteNodeIds = obsoleteNodes.Select(node => node.Id).ToHashSet();
            if (obsoleteNodeIds.Count > 0)
            {
                context.NavigationEdges.RemoveRange(await context.NavigationEdges
                    .Where(edge => obsoleteNodeIds.Contains(edge.SourceNodeId) ||
                        obsoleteNodeIds.Contains(edge.TargetNodeId))
                    .ToListAsync());
                context.Facilities.RemoveRange(await context.Facilities
                    .Where(facility => obsoleteNodeIds.Contains(facility.NavigationNodeId))
                    .ToListAsync());
                context.NavigationNodes.RemoveRange(obsoleteNodes);
            }

            context.Tenants.RemoveRange(await context.Tenants
                .Where(tenant => obsoleteTenantIds.Contains(tenant.Id))
                .ToListAsync());
        }

        await context.SaveChangesAsync();
    }

    private static string PromotionKey(Guid tenantId, string title) =>
        $"{tenantId:N}|{Normalize(title)}";

    private static async Task<HashSet<string>> ImportedSourceKeysAsync(MallDbContext context, string kind)
    {
        return (await context.KnowledgeSources
                .Where(source => source.SourceKind == kind)
                .Select(source => new { source.CollectionId, source.SourceKind, source.Title })
                .ToListAsync())
            .Select(source => SourceKey(source.CollectionId, source.SourceKind, source.Title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string SourceKey(Guid collectionId, string kind, string title) =>
        $"{collectionId:N}|{kind}|{Normalize(title)}";

    private static bool TryResolveStoreTenant(
        string? id,
        string? slug,
        string? title,
        IReadOnlyDictionary<string, Tenant> tenantsByStoreId,
        IReadOnlyDictionary<string, Tenant>? tenantsByName,
        out Tenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(id) && tenantsByStoreId.TryGetValue(id, out tenant!)) return true;
        if (!string.IsNullOrWhiteSpace(slug) && tenantsByStoreId.TryGetValue(slug, out tenant!)) return true;
        if (tenantsByName != null && !string.IsNullOrWhiteSpace(title) &&
            tenantsByName.TryGetValue(Normalize(title), out tenant!)) return true;
        tenant = null!;
        return false;
    }

    private static StoreCategory MapCategory(StoreRecord store)
    {
        var value = string.Join(' ', store.CategoryTree?.Select(category => category.Title) ?? []) + " " + store.Category?.Title;
        value = value.ToLowerInvariant();
        if (ContainsAny(value, "food", "beverage", "ice cream", "noodle", "cake", "korean")) return StoreCategory.FoodAndBeverage;
        if (ContainsAny(value, "fashion", "shoe", "bag", "jewellery", "timepiece", "optical", "sport", "fitness")) return StoreCategory.Fashion;
        if (ContainsAny(value, "gadget", "electronic", "appliance", "it/accessories")) return StoreCategory.Electronics;
        if (ContainsAny(value, "health", "beauty", "pharmacy")) return StoreCategory.BeautyAndWellness;
        if (ContainsAny(value, "cinema", "entertainment", "gift", "book", "toy", "stationary", "stationery")) return StoreCategory.Entertainment;
        if (value.Contains("department")) return StoreCategory.DepartmentStore;
        if (value.Contains("anchor")) return StoreCategory.DepartmentStore;
        if (value.Contains("service")) return StoreCategory.Services;
        return StoreCategory.Other;
    }

    private static PromotionType InferDealType(string title, string? description)
    {
        var value = $"{title} {description}".ToLowerInvariant();
        if (ContainsAny(value, "free ", "complimentary", "gift with purchase")) return PromotionType.FreeGift;
        if (ContainsAny(value, "bundle", "set for", "combo")) return PromotionType.BundleOffer;
        if (ContainsAny(value, "flash", "limited time")) return PromotionType.FlashSale;
        if (ContainsAny(value, "christmas", "festive", "seasonal")) return PromotionType.Seasonal;
        if (ContainsAny(value, "save ", "% off", "discount")) return PromotionType.Discount;
        return PromotionType.Other;
    }

    private static string? InferDietaryFlags(string? description)
    {
        var value = description?.ToLowerInvariant() ?? string.Empty;
        var flags = new List<string>();
        if (value.Contains("vegetarian")) flags.Add("Vegetarian options mentioned");
        if (value.Contains("halal")) flags.Add("Halal mentioned");
        return flags.Count == 0 ? null : string.Join(", ", flags);
    }

    private static string? InferPriceRange(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(description, @"\bRM\s?\d+(?:\.\d{1,2})?(?:\s?[-–]\s?RM?\s?\d+(?:\.\d{1,2})?)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static string BuildTenantTags(StoreRecord store)
    {
        var tags = new List<string>
        {
            store.Title ?? string.Empty,
            store.Keywords ?? string.Empty,
            store.Category?.Title ?? string.Empty
        };
        tags.AddRange(store.CategoryTree?.Select(category => category.Title ?? string.Empty) ?? []);
        return string.Join(", ", tags.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string AllocateUnitNumber(int floor, string? venue, string? slug, ISet<string> usedLocations)
    {
        var baseUnit = Limit(Clean(venue), 20);
        if (string.IsNullOrWhiteSpace(baseUnit)) baseUnit = Limit($"UNLISTED-{slug}", 20);
        var candidate = baseUnit;
        var suffix = 2;
        while (!usedLocations.Add(LocationKey(floor, candidate)))
        {
            var marker = $"~{suffix++}";
            candidate = Limit(baseUnit, 20 - marker.Length) + marker;
        }
        return candidate;
    }

    private static string LocationKey(int floor, string unit) => $"{floor}|{unit.Trim()}";

    private static int ParseFloor(JsonElement level, string? venue)
    {
        var value = level.ValueKind switch
        {
            JsonValueKind.String => level.GetString(),
            JsonValueKind.Object when level.TryGetProperty("code", out var code) => code.GetString(),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(value))
            value = Clean(venue).Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        value = value?.Trim().ToUpperInvariant();
        if (value?.StartsWith("LG", StringComparison.Ordinal) == true) return -1;
        if (value?.StartsWith("G", StringComparison.Ordinal) == true) return 0;
        var digits = new string((value ?? string.Empty).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var floor) ? floor : 0;
    }

    private static (DateTime Start, DateTime End) ParseDisplayDateRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1));
        var parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return (DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1));
        var end = ParseDisplayDate(parts[1], DateTime.UtcNow.Year);
        var start = ParseDisplayDate(parts[0], end.Year);
        if (start > end) start = start.AddYears(-1);
        return (start, end);
    }

    private static DateTime ParseDisplayDate(string value, int fallbackYear)
    {
        var formats = new[] { "d MMM yyyy", "dd MMM yyyy", "d MMM", "dd MMM" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            if (!value.Any(char.IsDigit) || value.Count(char.IsDigit) <= 2)
                parsed = new DateTime(fallbackYear, parsed.Month, parsed.Day);
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        }
        return DateTime.UtcNow.Date;
    }

    private static DateTime ParseIsoDate(string? value, DateTime fallback)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : fallback;
    }

    private static string ImageUrl(ImageRecord? image)
    {
        if (image?.Meta == null || string.IsNullOrWhiteSpace(image.Meta.ThumbsUrl) || string.IsNullOrWhiteSpace(image.Meta.Extension))
            return string.Empty;
        return $"{image.Meta.ThumbsUrl}w768{image.Meta.Extension}";
    }

    private static T ReadResource<T>(string fileName)
    {
        var assembly = typeof(SunwayKluangMallDataSeeder).Assembly;
        var resourceName = $"{ResourcePrefix}.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded seed resource '{resourceName}' was not found.");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded seed resource '{resourceName}' was empty or invalid.");
    }

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    private static string FirstUsefulText(params string?[] values) =>
        values.Select(Clean).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Imported catalogue item.";

    private static string Normalize(string? value) => Clean(value).ToUpperInvariant();
    private static string Clean(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Limit(string? value, int length) => Clean(value).Length <= length ? Clean(value) : Clean(value)[..length];
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);

    private sealed record StoreRecord
    {
        [JsonPropertyName("_id")] public string? ExternalId { get; init; }
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Text { get; init; }
        public string? Venue { get; init; }
        public JsonElement Level { get; init; }
        public string? Slug { get; init; }
        public string? Website { get; init; }
        public string? Keywords { get; init; }
        public ContactRecord? Contact { get; init; }
        public CategoryRecord? Category { get; init; }
        public List<CategoryRecord>? CategoryTree { get; init; }
        public ImageRecord? MainImage { get; init; }
    }

    private sealed record DealRecord
    {
        [JsonPropertyName("_id")] public string? ExternalId { get; init; }
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Text { get; init; }
        public string? Description { get; init; }
        public string? Term { get; init; }
        public string? DateStart { get; init; }
        public string? DateEnd { get; init; }
        public string? Slug { get; init; }
        public ShopMetaRecord? ShopMeta { get; init; }
        public ImageRecord? MainImage { get; init; }
    }

    private sealed record EventRecord
    {
        public string? Title { get; init; }
        [JsonPropertyName("source_url")] public string? SourceUrl { get; init; }
        public string? Description { get; init; }
        [JsonPropertyName("detail_text")] public string? DetailText { get; init; }
        [JsonPropertyName("terms_and_conditions")] public string? TermsAndConditions { get; init; }
        [JsonPropertyName("image_urls")] public List<string>? ImageUrls { get; init; }
        public string? Slug { get; init; }
        [JsonPropertyName("date_display")] public string? DateDisplay { get; init; }
        [JsonPropertyName("listing_image_url")] public string? ListingImageUrl { get; init; }
    }

    private sealed record ShopMetaRecord
    {
        [JsonPropertyName("_id")] public string? ExternalId { get; init; }
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Slug { get; init; }
    }

    private sealed record ContactRecord(string? Email, string? Phone);
    private sealed record CategoryRecord(string? Title, string? Refcode);

    private sealed record ImageRecord
    {
        [JsonPropertyName("_meta")] public ImageMetaRecord? Meta { get; init; }
    }

    private sealed record ImageMetaRecord
    {
        [JsonPropertyName("thumbsUrl")] public string? ThumbsUrl { get; init; }
        [JsonPropertyName("ext")] public string? Extension { get; init; }
    }
}
