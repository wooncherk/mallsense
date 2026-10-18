using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;

namespace MallSense.Infrastructure.Data;

public class MallDbContext : DbContext
{
    public MallDbContext(DbContextOptions<MallDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<NavigationNode> NavigationNodes => Set<NavigationNode>();
    public DbSet<NavigationEdge> NavigationEdges => Set<NavigationEdge>();
    public DbSet<MallFacility> Facilities => Set<MallFacility>();
    public DbSet<ShopperIntentSignal> IntentSignals => Set<ShopperIntentSignal>();
    public DbSet<ShopperConversation> ShopperConversations => Set<ShopperConversation>();
    public DbSet<ShopperConversationTurn> ShopperConversationTurns => Set<ShopperConversationTurn>();
    public DbSet<ShopperBehaviorEvent> ShopperBehaviorEvents => Set<ShopperBehaviorEvent>();
    public DbSet<KnowledgeCollection> KnowledgeCollections => Set<KnowledgeCollection>();
    public DbSet<KnowledgeSource> KnowledgeSources => Set<KnowledgeSource>();
    public DbSet<KnowledgeConversationTurn> KnowledgeConversationTurns => Set<KnowledgeConversationTurn>();
    public DbSet<FloorPlanDraft> FloorPlanDrafts => Set<FloorPlanDraft>();
    public DbSet<KnowledgeSuggestion> KnowledgeSuggestions => Set<KnowledgeSuggestion>();
    public DbSet<KnowledgeSourceAttachment> KnowledgeSourceAttachments => Set<KnowledgeSourceAttachment>();

    public async Task EnsureShopperConversationSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!Database.IsSqlite()) return;

        await Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ShopperConversations" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ShopperConversations" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NULL,
                "UserId" TEXT NOT NULL,
                "SessionId" TEXT NOT NULL,
                "SurfaceContext" TEXT NOT NULL,
                CONSTRAINT "FK_ShopperConversations_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ShopperConversations_UserId_SessionId" ON "ShopperConversations" ("UserId", "SessionId");
            CREATE INDEX IF NOT EXISTS "IX_ShopperConversations_UserId_UpdatedAtUtc" ON "ShopperConversations" ("UserId", "UpdatedAtUtc");
            CREATE TABLE IF NOT EXISTS "ShopperConversationTurns" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ShopperConversationTurns" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NULL,
                "ConversationId" TEXT NOT NULL,
                "Sequence" INTEGER NOT NULL,
                "Role" TEXT NOT NULL,
                "MessageType" TEXT NOT NULL DEFAULT 'Message',
                "Message" TEXT NOT NULL,
                "StructuredIntentJson" TEXT NULL,
                "RecommendationsJson" TEXT NULL,
                "ReplySuggestionsJson" TEXT NULL,
                CONSTRAINT "FK_ShopperConversationTurns_ShopperConversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "ShopperConversations" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ShopperConversationTurns_ConversationId_Sequence" ON "ShopperConversationTurns" ("ConversationId", "Sequence");
            """, cancellationToken);

        var connection = Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ShopperConversationTurns') WHERE name = 'ReplySuggestionsJson';";
            var exists = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!exists)
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE ShopperConversationTurns ADD COLUMN ReplySuggestionsJson TEXT NULL;";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }

            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ShopperConversationTurns') WHERE name = 'MessageType';";
            exists = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!exists)
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE ShopperConversationTurns ADD COLUMN MessageType TEXT NOT NULL DEFAULT 'Message';";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    public async Task EnsureBehaviorAnalyticsSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!Database.IsSqlite()) return;

        await Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ShopperBehaviorEvents" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ShopperBehaviorEvents" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NULL,
                "SessionId" TEXT NOT NULL,
                "SurfaceContext" TEXT NOT NULL,
                "EventType" TEXT NOT NULL,
                "Query" TEXT NULL,
                "SelectedFilter" TEXT NULL,
                "ResultCount" INTEGER NULL,
                "TargetKnowledgeSourceId" TEXT NULL,
                "TargetTenantId" TEXT NULL,
                "TargetTitle" TEXT NULL,
                "RelatedTenantIds" TEXT NOT NULL,
                "RelatedKnowledgeSourceIds" TEXT NOT NULL,
                "TimestampUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_ShopperBehaviorEvents_TimestampUtc" ON "ShopperBehaviorEvents" ("TimestampUtc");
            CREATE INDEX IF NOT EXISTS "IX_ShopperBehaviorEvents_SessionId_TimestampUtc" ON "ShopperBehaviorEvents" ("SessionId", "TimestampUtc");
            CREATE INDEX IF NOT EXISTS "IX_ShopperBehaviorEvents_TargetTenantId_TimestampUtc" ON "ShopperBehaviorEvents" ("TargetTenantId", "TimestampUtc");
            """, cancellationToken);
    }

    public async Task EnsureTenantSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!Database.IsSqlite()) return;
        var connection = Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Tenants') WHERE name = 'StorefrontImageUrl';";
            if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Tenants ADD COLUMN StorefrontImageUrl TEXT NULL;";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
            // Add nullable AI search validation fields to existing tenant databases.
            var validationColumns = new (string Name, string SqlType)[]
            {
                ("SearchValidationRating", "INTEGER NULL"),
                ("SearchValidationSummary", "TEXT NULL"),
                ("SearchValidationDescriptionHash", "TEXT NULL"),
                ("SearchValidationQuestionsJson", "TEXT NULL"),
                ("SearchValidationAtUtc", "TEXT NULL")
            };
            foreach (var (name, sqlType) in validationColumns)
            {
                await using var columnCheck = connection.CreateCommand();
                columnCheck.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Tenants') WHERE name = '{name}';";
                var exists = Convert.ToInt32(await columnCheck.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!exists)
                {
                    await using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE Tenants ADD COLUMN {name} {sqlType};";
                    await alter.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenant configuration (replaces Store)
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1500);
            entity.Property(e => e.UnitNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ContactEmail).HasMaxLength(150);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.OpeningHours).HasMaxLength(100);
            entity.Property(e => e.StorefrontImageUrl);

            entity.HasIndex(e => new { e.FloorNumber, e.UnitNumber }).IsUnique();
            entity.HasIndex(e => e.Category);

        });

        // ApplicationUser configuration
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(200);

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.UserName).IsUnique();

            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.Staff)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Promotion configuration
        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(150);
            entity.Property(e => e.OfferSummary).IsRequired().HasMaxLength(300);

            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.Promotions)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Type);
        });

        // NavigationNode configuration
        modelBuilder.Entity<NavigationNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.FloorNumber);
            entity.HasIndex(e => e.NodeType);

            entity.HasOne(e => e.Tenant)
                  .WithOne(t => t.NavigationNode)
                  .HasForeignKey<NavigationNode>(e => e.TenantId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // NavigationEdge configuration
        modelBuilder.Entity<NavigationEdge>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.SourceNode)
                  .WithMany(n => n.OutgoingEdges)
                  .HasForeignKey(e => e.SourceNodeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetNode)
                  .WithMany(n => n.IncomingEdges)
                  .HasForeignKey(e => e.TargetNodeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // MallFacility configuration
        modelBuilder.Entity<MallFacility>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.NavigationNode)
                  .WithMany()
                  .HasForeignKey(e => e.NavigationNodeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ShopperIntentSignal configuration
        modelBuilder.Entity<ShopperIntentSignal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => e.IntentCategory);
        });

        modelBuilder.Entity<ShopperConversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SurfaceContext).IsRequired().HasMaxLength(40);
            entity.HasIndex(e => new { e.UserId, e.SessionId }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.UpdatedAtUtc });
            entity.HasOne(e => e.User).WithMany(e => e.Conversations).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShopperConversationTurn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(30).HasDefaultValue("Message");
            entity.Property(e => e.Message).IsRequired();
            entity.HasIndex(e => new { e.ConversationId, e.Sequence }).IsUnique();
            entity.HasOne(e => e.Conversation).WithMany(e => e.Turns).HasForeignKey(e => e.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShopperBehaviorEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SurfaceContext).IsRequired().HasMaxLength(40);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Query).HasMaxLength(500);
            entity.Property(e => e.SelectedFilter).HasMaxLength(40);
            entity.Property(e => e.TargetTitle).HasMaxLength(240);
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => new { e.SessionId, e.TimestampUtc });
            entity.HasIndex(e => new { e.TargetTenantId, e.TimestampUtc });
        });

        modelBuilder.Entity<KnowledgeCollection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(160);
            entity.Property(e => e.ScopeType).IsRequired().HasMaxLength(40);
            entity.HasIndex(e => new { e.ScopeType, e.ScopeReferenceId });
        });

        modelBuilder.Entity<KnowledgeSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SourceKind).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(120);
            entity.HasOne(e => e.Collection).WithMany(e => e.Sources).HasForeignKey(e => e.CollectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.CollectionId, e.Status });
        });

        modelBuilder.Entity<KnowledgeConversationTurn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Source).WithMany(e => e.Conversation).HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SourceId, e.Sequence }).IsUnique();
        });

        modelBuilder.Entity<KnowledgeSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Source).WithMany(e => e.Suggestions).HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SourceId, e.CreatedAtUtc });
        });

        modelBuilder.Entity<KnowledgeSourceAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(120);
            entity.HasOne(e => e.Source).WithMany(e => e.Attachments).HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SourceId);
        });

        modelBuilder.Entity<FloorPlanDraft>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Source).WithMany().HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SourceId, e.FloorNumber });
        });
    }
}
