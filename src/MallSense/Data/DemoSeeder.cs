using MallSense.Domain;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Data;

public static class DemoSeeder
{
    public static readonly Guid MallId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly DateOnly DemoDate = new(2026, 10, 13);
    private static readonly DateTime DemoInstant = new(2026, 10, 13, 6, 0, 0, DateTimeKind.Utc);

    public static async Task SeedAsync(MallDbContext db, CancellationToken cancellationToken = default)
    {
        // Explicit prototype operation only. Switch to migrations before evolving a persisted schema.
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (await db.Malls.AnyAsync(x => x.Id == MallId, cancellationToken))
        {
            Console.WriteLine("Meridian Mall demo data already exists; no records changed.");
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Malls.Add(new Mall { Id = MallId, Name = "Meridian Mall", Address = "Fictional demo mall · Kuala Lumpur", LogoAssetPath = "/images/mallsense-logo.png" });
        var ground = new Floor { MallId = MallId, Name = "Ground Floor", Level = 0 };
        var upper = new Floor { MallId = MallId, Name = "Level 1", Level = 1 };
        db.Floors.AddRange(ground, upper);
        var entrance = new NavigationNode { FloorId = ground.Id, Label = "Main entrance", Kind = NodeKind.Entrance, X = 0, Y = 0 };
        var lowerLift = new NavigationNode { FloorId = ground.Id, Label = "Atrium lift · Ground Floor", Kind = NodeKind.Lift, X = 6, Y = 0 };
        var upperLift = new NavigationNode { FloorId = upper.Id, Label = "Atrium lift · Level 1", Kind = NodeKind.Lift, X = 0, Y = 0 };
        db.NavigationNodes.AddRange(entrance, lowerLift, upperLift);
        Connect(db, lowerLift, upperLift, 5, "Take the atrium lift to Level 1.", "Take the atrium lift to Ground Floor.");

        var entries = new[]
        {
            ("Bloom & Co", "Gifting", "Flowers, thoughtful keepsakes and gifts for everyday occasions.", "flowers,gift,mother,birthday", "G-01"),
            ("Common Ground", "Dining", "Coffee, pastries and a relaxed place for an afternoon break.", "coffee,dining,pastry,quiet", "G-02"),
            ("Little Bowl", "Dining", "Casual rice bowls and comforting lunches for the family.", "dining,lunch,family", "G-03"),
            ("Field Notes", "Lifestyle", "Stationery and everyday objects for creative people.", "gift,stationery,creative", "G-04"),
            ("Everyday Care", "Services", "Personal care essentials and everyday wellbeing products.", "care,essentials", "G-05"),
            ("Table Stories", "Home", "Kitchenware and table accessories worth browsing.", "cooking,mother,gift,home", "G-06"),
            ("Tiny Trail", "Family", "Playful toys and creative activities for younger explorers.", "child,kids,gift,toys,activity", "L1-01"),
            ("Frame House", "Lifestyle", "Prints, frames and personal touches for your home.", "gift,art,home", "L1-02"),
            ("Paper & Play", "Books", "Books, paper crafts and activity books for curious minds.", "child,kids,gift,books,making,activity", "L1-03"),
            ("Studio Weekend", "Activities", "A creative workshop space for making something together.", "activity,craft,making,family,kids", "L1-04"),
            ("Motion Club", "Sport", "Sportswear and accessories for an active day.", "sport,fitness", "L1-05"),
            ("Meridian Cinema", "Entertainment", "A place to catch a film and make an afternoon of it.", "cinema,movie,activity", "L1-06")
        };
        var places = new List<Place>();
        var previous = entrance;
        for (var i = 0; i < entries.Length; i++)
        {
            if (i == 6) { Connect(db, previous, lowerLift, 12); previous = upperLift; }
            var entry = entries[i];
            var floor = i < 6 ? ground : upper;
            var tenant = new Tenant { MallId = MallId, Name = entry.Item1, Description = entry.Item3 };
            var node = new NavigationNode { FloorId = floor.Id, Label = entry.Item1, Kind = NodeKind.Landmark, X = i % 6 + 1, Y = 0 };
            var place = new Place
            {
                MallId = MallId, TenantId = tenant.Id, NavigationNodeId = node.Id, Name = tenant.Name,
                Category = entry.Item2, UnitNumber = entry.Item5, Description = tenant.Description,
                Kind = entry.Item2 == "Dining" ? PlaceKind.Dining : entry.Item2 == "Services" ? PlaceKind.Service : entry.Item2 == "Activities" ? PlaceKind.EventSpace : PlaceKind.Store,
                Source = ContentSource.Mall, VerifiedAtUtc = DemoInstant
            };
            db.Tenants.Add(tenant); db.NavigationNodes.Add(node); db.Places.Add(place); places.Add(place);
            Connect(db, previous, node, 12); previous = node;
            foreach (var tag in entry.Item4.Split(',')) db.PlaceTags.Add(new PlaceTag { PlaceId = place.Id, Value = tag });
            foreach (var day in Enum.GetValues<DayOfWeek>())
                db.OpeningHours.Add(new OpeningHours { PlaceId = place.Id, Day = day, OpensAt = new TimeOnly(10, 0), ClosesAt = new TimeOnly(22, 0) });
        }
        db.Places.Add(new Place { MallId = MallId, NavigationNodeId = lowerLift.Id, Name = "Accessible restroom", Kind = PlaceKind.Facility, Category = "Facilities", Description = "Accessible restroom beside the atrium lift.", Source = ContentSource.Mall, VerifiedAtUtc = DemoInstant });
        db.Places.Add(new Place { MallId = MallId, NavigationNodeId = upperLift.Id, Name = "Family room", Kind = PlaceKind.Facility, Category = "Facilities", Description = "Family and nursing room beside the Level 1 lift.", Source = ContentSource.Mall, VerifiedAtUtc = DemoInstant });

        var books = AddPromotion(db, places[8], "Little makers. Big ideas.", "20% off activity books", "Selected activity books only. While stocks last.", "/images/poster-books.png");
        books.MinimumAge = 6; books.MaximumAge = 12; books.SuitableForGifting = true;
        var coffee = AddPromotion(db, places[1], "A little afternoon pause.", "Coffee + pastry · RM18", "Weekdays, 2–5 PM. Selected drinks and pastries. Dine-in only.", "/images/poster-cafe.png");
        coffee.ApproximateMinimumPriceMinor = 1800; coffee.ApproximateMaximumPriceMinor = 1800;
        db.PromotionTags.AddRange(
            new PromotionTag { PromotionId = books.Id, Kind = "audience", Value = "child" },
            new PromotionTag { PromotionId = books.Id, Kind = "interest", Value = "making" },
            new PromotionTag { PromotionId = coffee.Id, Kind = "occasion", Value = "afternoon-break" });
        db.MallEvents.Add(new MallEvent { PlaceId = places[9].Id, Title = "Weekend paper studio", Description = "A fictional family paper-craft session. Ask the studio for participation details.", StartsAtUtc = new DateTime(2026, 10, 17, 6, 0, 0, DateTimeKind.Utc), EndsAtUtc = new DateTime(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc), IsPublished = true });

        for (var i = 0; i < 6; i++)
        {
            var kind = i < 3 ? IntentKind.Gift : i < 5 ? IntentKind.Dining : IntentKind.Activity;
            var place = kind == IntentKind.Gift ? places[8] : kind == IntentKind.Dining ? places[1] : places[9];
            var session = new DiscoverySession { MallId = MallId, CurrentNodeId = entrance.Id, IsSimulated = true, StartedAtUtc = DemoInstant.AddMinutes(i) };
            var message = new ConversationMessage { SessionId = session.Id, Sequence = 1, Role = MessageRole.Shopper, Content = kind == IntentKind.Gift ? "A gift for a child who enjoys making things." : kind == IntentKind.Dining ? "Somewhere for coffee and a pastry." : "Something creative to do with the kids.", CreatedAtUtc = session.StartedAtUtc };
            var intent = new ShopperIntent { SessionId = session.Id, SourceMessageId = message.Id, Revision = 1, Kind = kind, Recipient = kind == IntentKind.Gift ? "child" : null, CapturedAtUtc = session.StartedAtUtc };
            var recommendation = new Recommendation { IntentId = intent.Id, PlaceId = place.Id, PromotionId = kind == IntentKind.Gift ? books.Id : kind == IntentKind.Dining ? coffee.Id : null, Rank = 1, Reason = "Matches the interests in this simulated demo session.", CreatedAtUtc = session.StartedAtUtc };
            db.DiscoverySessions.Add(session); db.ConversationMessages.Add(message); db.ShopperIntents.Add(intent); db.Recommendations.Add(recommendation);
            db.ShopperInteractions.Add(new ShopperInteraction { SessionId = session.Id, IntentId = intent.Id, RecommendationId = recommendation.Id, Kind = InteractionKind.RecommendationShown, OccurredAtUtc = session.StartedAtUtc });
            if (i % 2 == 0) db.ShopperInteractions.Add(new ShopperInteraction { SessionId = session.Id, IntentId = intent.Id, RecommendationId = recommendation.Id, Kind = InteractionKind.DestinationSelected, OccurredAtUtc = session.StartedAtUtc.AddSeconds(15) });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Console.WriteLine("Seeded Meridian Mall: 12 tenants, 2 floors, 2 promotions, 2 facilities and 6 simulated discovery sessions.");
    }

    private static Promotion AddPromotion(MallDbContext db, Place place, string title, string offer, string terms, string poster)
    {
        var promotion = new Promotion { TenantId = place.TenantId!.Value, Title = title, Description = offer, Offer = offer, Terms = terms, PosterAssetPath = poster, ValidFrom = new DateOnly(2026, 10, 1), ValidUntil = new DateOnly(2026, 10, 31), Status = PromotionStatus.Published, CreatedAtUtc = DemoInstant, ConfirmedAtUtc = DemoInstant, ConfirmedBySubject = "demo-seed" };
        db.Promotions.Add(promotion);
        db.PromotionOutlets.Add(new PromotionOutlet { PromotionId = promotion.Id, PlaceId = place.Id });
        db.PromotionSubmissions.Add(new PromotionSubmission { TenantId = promotion.TenantId, PromotionId = promotion.Id, AssetStorageKey = poster, ContentType = "image/png", Status = ExtractionStatus.Confirmed, SubmittedAtUtc = DemoInstant });
        return promotion;
    }

    private static void Connect(MallDbContext db, NavigationNode from, NavigationNode to, double distance,
        string? forward = null, string? backward = null)
    {
        db.NavigationEdges.AddRange(
            new NavigationEdge { FromNodeId = from.Id, ToNodeId = to.Id, DistanceMetres = distance, IsStepFree = true, Instruction = forward ?? $"Continue from {from.Label} towards {to.Label}." },
            new NavigationEdge { FromNodeId = to.Id, ToNodeId = from.Id, DistanceMetres = distance, IsStepFree = true, Instruction = backward ?? $"Continue from {to.Label} towards {from.Label}." });
    }
}
