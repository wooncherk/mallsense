using MallSense.Domain;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Data;

public sealed class MallDbContext(DbContextOptions<MallDbContext> options) : DbContext(options)
{
    public DbSet<Mall> Malls => Set<Mall>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<NavigationNode> NavigationNodes => Set<NavigationNode>();
    public DbSet<NavigationEdge> NavigationEdges => Set<NavigationEdge>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<PlaceTag> PlaceTags => Set<PlaceTag>();
    public DbSet<OpeningHours> OpeningHours => Set<OpeningHours>();
    public DbSet<MallEvent> MallEvents => Set<MallEvent>();
    public DbSet<StaffMembership> StaffMemberships => Set<StaffMembership>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionOutlet> PromotionOutlets => Set<PromotionOutlet>();
    public DbSet<PromotionTag> PromotionTags => Set<PromotionTag>();
    public DbSet<PromotionSubmission> PromotionSubmissions => Set<PromotionSubmission>();
    public DbSet<PromotionClarification> PromotionClarifications => Set<PromotionClarification>();
    public DbSet<DiscoverySession> DiscoverySessions => Set<DiscoverySession>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<ShopperIntent> ShopperIntents => Set<ShopperIntent>();
    public DbSet<IntentPreference> IntentPreferences => Set<IntentPreference>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RouteJourney> RouteJourneys => Set<RouteJourney>();
    public DbSet<RouteStep> RouteSteps => Set<RouteStep>();
    public DbSet<ShopperInteraction> ShopperInteractions => Set<ShopperInteraction>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PlaceTag>().HasKey(x => new { x.PlaceId, x.Value });
        model.Entity<PromotionOutlet>().HasKey(x => new { x.PromotionId, x.PlaceId });
        model.Entity<PromotionTag>().HasKey(x => new { x.PromotionId, x.Kind, x.Value });
        model.Entity<IntentPreference>().HasKey(x => new { x.IntentId, x.Value, x.IsExcluded });
        model.Entity<RouteStep>().HasKey(x => new { x.RouteJourneyId, x.Sequence });

        model.Entity<Floor>().HasOne<Mall>().WithMany().HasForeignKey(x => x.MallId);
        model.Entity<NavigationNode>().HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId);
        model.Entity<NavigationEdge>().HasOne<NavigationNode>().WithMany().HasForeignKey(x => x.FromNodeId);
        model.Entity<NavigationEdge>().HasOne<NavigationNode>().WithMany().HasForeignKey(x => x.ToNodeId);
        model.Entity<Place>().HasOne<Mall>().WithMany().HasForeignKey(x => x.MallId);
        model.Entity<Place>().HasOne<NavigationNode>().WithMany().HasForeignKey(x => x.NavigationNodeId);
        model.Entity<Place>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        model.Entity<PlaceTag>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId);
        model.Entity<OpeningHours>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId);
        model.Entity<MallEvent>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId);
        model.Entity<StaffMembership>().HasOne<Mall>().WithMany().HasForeignKey(x => x.MallId);
        model.Entity<StaffMembership>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        model.Entity<Tenant>().HasOne<Mall>().WithMany().HasForeignKey(x => x.MallId);
        model.Entity<Promotion>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        model.Entity<PromotionOutlet>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId);
        model.Entity<PromotionOutlet>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId);
        model.Entity<PromotionTag>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId);
        model.Entity<PromotionSubmission>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        model.Entity<PromotionSubmission>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId);
        model.Entity<PromotionClarification>().HasOne<PromotionSubmission>().WithMany().HasForeignKey(x => x.SubmissionId);
        model.Entity<DiscoverySession>().HasOne<Mall>().WithMany().HasForeignKey(x => x.MallId);
        model.Entity<DiscoverySession>().HasOne<NavigationNode>().WithMany().HasForeignKey(x => x.CurrentNodeId);
        model.Entity<ConversationMessage>().HasOne<DiscoverySession>().WithMany().HasForeignKey(x => x.SessionId);
        model.Entity<ShopperIntent>().HasOne<DiscoverySession>().WithMany().HasForeignKey(x => x.SessionId);
        model.Entity<ConversationMessage>().HasAlternateKey(x => new { x.Id, x.SessionId });
        model.Entity<ShopperIntent>().HasOne<ConversationMessage>().WithMany()
            .HasForeignKey(x => new { x.SourceMessageId, x.SessionId })
            .HasPrincipalKey(x => new { x.Id, x.SessionId });
        model.Entity<IntentPreference>().HasOne<ShopperIntent>().WithMany().HasForeignKey(x => x.IntentId);
        model.Entity<Recommendation>().HasOne<ShopperIntent>().WithMany().HasForeignKey(x => x.IntentId);
        model.Entity<Recommendation>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId);
        model.Entity<Recommendation>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId);
        model.Entity<RouteJourney>().HasOne<DiscoverySession>().WithMany().HasForeignKey(x => x.SessionId);
        model.Entity<RouteJourney>().HasOne<Place>().WithMany().HasForeignKey(x => x.DestinationPlaceId);
        model.Entity<RouteJourney>().HasOne<NavigationNode>().WithMany().HasForeignKey(x => x.StartingNodeId);
        model.Entity<RouteStep>().HasOne<RouteJourney>().WithMany().HasForeignKey(x => x.RouteJourneyId);
        model.Entity<RouteStep>().HasOne<NavigationEdge>().WithMany().HasForeignKey(x => x.NavigationEdgeId);
        model.Entity<ShopperInteraction>().HasOne<DiscoverySession>().WithMany().HasForeignKey(x => x.SessionId);
        model.Entity<ShopperIntent>().HasAlternateKey(x => new { x.Id, x.SessionId });
        model.Entity<ShopperInteraction>().HasOne<ShopperIntent>().WithMany()
            .HasForeignKey(x => new { x.IntentId, x.SessionId })
            .HasPrincipalKey(x => new { x.Id, x.SessionId });
        model.Entity<Recommendation>().HasAlternateKey(x => new { x.Id, x.IntentId });
        model.Entity<ShopperInteraction>().HasOne<Recommendation>().WithMany()
            .HasForeignKey(x => new { x.RecommendationId, x.IntentId })
            .HasPrincipalKey(x => new { x.Id, x.IntentId });
        model.Entity<RouteJourney>().HasAlternateKey(x => new { x.Id, x.SessionId });
        model.Entity<ShopperInteraction>().HasOne<RouteJourney>().WithMany()
            .HasForeignKey(x => new { x.RouteJourneyId, x.SessionId })
            .HasPrincipalKey(x => new { x.Id, x.SessionId });

        model.Entity<Floor>().HasIndex(x => new { x.MallId, x.Level }).IsUnique();
        model.Entity<NavigationEdge>().HasIndex(x => new { x.FromNodeId, x.ToNodeId }).IsUnique();
        model.Entity<Place>().HasIndex(x => new { x.MallId, x.UnitNumber }).IsUnique();
        model.Entity<OpeningHours>().HasIndex(x => new { x.PlaceId, x.Day }).IsUnique();
        model.Entity<StaffMembership>().HasIndex(x => new { x.IdentitySubject, x.MallId, x.TenantId, x.Role }).IsUnique();
        model.Entity<StaffMembership>().HasIndex(x => new { x.IdentitySubject, x.MallId })
            .IsUnique().HasFilter("TenantId IS NULL");
        model.Entity<Promotion>().HasIndex(x => new { x.TenantId, x.Status, x.ValidUntil });
        model.Entity<ConversationMessage>().HasIndex(x => new { x.SessionId, x.Sequence }).IsUnique();
        model.Entity<ShopperIntent>().HasIndex(x => new { x.SessionId, x.Revision }).IsUnique();
        model.Entity<Recommendation>().HasIndex(x => new { x.IntentId, x.Rank }).IsUnique();
        model.Entity<RouteJourney>().HasIndex(x => new { x.SessionId, x.DestinationPlaceId, x.Revision }).IsUnique();
        model.Entity<DiscoverySession>().HasIndex(x => new { x.MallId, x.IsSimulated, x.StartedAtUtc });
        model.Entity<ShopperInteraction>().HasIndex(x => new { x.SessionId, x.Kind, x.OccurredAtUtc });

        model.Entity<NavigationEdge>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Edge_Distance", "DistanceMetres > 0");
            t.HasCheckConstraint("CK_Edge_DistinctNodes", "FromNodeId <> ToNodeId");
        });
        model.Entity<MallEvent>().ToTable(t => t.HasCheckConstraint("CK_Event_Period", "EndsAtUtc > StartsAtUtc"));
        model.Entity<DiscoverySession>().ToTable(t => t.HasCheckConstraint("CK_Session_Period", "EndedAtUtc IS NULL OR EndedAtUtc >= StartedAtUtc"));
        model.Entity<ConversationMessage>().ToTable(t => t.HasCheckConstraint("CK_Message_Sequence", "Sequence > 0"));
        model.Entity<Recommendation>().ToTable(t => t.HasCheckConstraint("CK_Recommendation_Rank", "Rank > 0"));
        model.Entity<RouteJourney>().ToTable(t => t.HasCheckConstraint("CK_Route_Revision", "Revision > 0"));
        model.Entity<RouteStep>().ToTable(t => t.HasCheckConstraint("CK_RouteStep_Sequence", "Sequence > 0"));
        model.Entity<Promotion>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Promotion_Dates", "ValidUntil >= ValidFrom");
            t.HasCheckConstraint("CK_Promotion_Ages", "(MinimumAge IS NULL OR MinimumAge >= 0) AND (MaximumAge IS NULL OR MaximumAge >= 0) AND (MinimumAge IS NULL OR MaximumAge IS NULL OR MaximumAge >= MinimumAge)");
            t.HasCheckConstraint("CK_Promotion_Prices", "(ApproximateMinimumPriceMinor IS NULL OR ApproximateMinimumPriceMinor >= 0) AND (ApproximateMaximumPriceMinor IS NULL OR ApproximateMaximumPriceMinor >= 0) AND (ApproximateMinimumPriceMinor IS NULL OR ApproximateMaximumPriceMinor IS NULL OR ApproximateMaximumPriceMinor >= ApproximateMinimumPriceMinor)");
            t.HasCheckConstraint("CK_Promotion_AdvanceOrder", "AdvanceOrderDays IS NULL OR AdvanceOrderDays >= 0");
            t.HasCheckConstraint("CK_Promotion_Confirmation", "Status <> 'Published' OR (ConfirmedAtUtc IS NOT NULL AND ConfirmedBySubject IS NOT NULL)");
        });
        model.Entity<ShopperIntent>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Intent_Revision", "Revision > 0");
            t.HasCheckConstraint("CK_Intent_Age", "RecipientAge IS NULL OR RecipientAge >= 0");
            t.HasCheckConstraint("CK_Intent_Time", "AvailableMinutes IS NULL OR AvailableMinutes > 0");
            t.HasCheckConstraint("CK_Intent_Budget", "(BudgetMinimumMinor IS NULL OR BudgetMinimumMinor >= 0) AND (BudgetMaximumMinor IS NULL OR BudgetMaximumMinor >= 0) AND (BudgetMinimumMinor IS NULL OR BudgetMaximumMinor IS NULL OR BudgetMaximumMinor >= BudgetMinimumMinor)");
        });
        model.Entity<StaffMembership>().ToTable(t => t.HasCheckConstraint("CK_Membership_Scope", "(Role = 'MallManager' AND TenantId IS NULL) OR (Role = 'TenantEditor' AND TenantId IS NOT NULL)"));

        foreach (var entity in model.Model.GetEntityTypes())
        {
            // Explicit deletion workflows must handle retained history and private messages.
            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;

            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType.IsEnum)
                    model.Entity(entity.ClrType).Property(property.Name).HasConversion<string>().HasMaxLength(40);
            }
        }
    }
}
