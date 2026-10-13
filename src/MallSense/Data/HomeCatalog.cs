using MallSense.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MallSense.Data;

public sealed record PlaceSummary(Guid Id, string Name, string Category, string Description, string Floor, string? Unit, string[] Tags);
public sealed record PromotionSummary(string Title, string Offer, string? Terms, string? Poster, string PlaceName, string Location, DateOnly ValidUntil);
public sealed record CatalogSnapshot(string MallName, string? Logo, List<PlaceSummary> Places, List<PromotionSummary> Promotions);

public sealed class HomeCatalog(MallDbContext db, ILogger<HomeCatalog> logger)
{
    public async Task<CatalogSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mall = await db.Malls.AsNoTracking().SingleOrDefaultAsync(x => x.Id == DemoSeeder.MallId, cancellationToken);
            if (mall is null) return null;
            var places = await (from place in db.Places.AsNoTracking()
                join node in db.NavigationNodes on place.NavigationNodeId equals node.Id
                join floor in db.Floors on node.FloorId equals floor.Id
                where place.MallId == mall.Id && place.IsActive
                orderby floor.Level, place.Name
                select new { place.Id, place.Name, place.Category, place.Description, Floor = floor.Name, Unit = place.UnitNumber })
                .ToListAsync(cancellationToken);
            var tags = await (from tag in db.PlaceTags.AsNoTracking()
                join place in db.Places on tag.PlaceId equals place.Id
                where place.MallId == mall.Id
                select tag).ToListAsync(cancellationToken);
            var promotions = await (from promotion in db.Promotions.AsNoTracking()
                join outlet in db.PromotionOutlets on promotion.Id equals outlet.PromotionId
                join place in db.Places on outlet.PlaceId equals place.Id
                join tenant in db.Tenants on promotion.TenantId equals tenant.Id
                join node in db.NavigationNodes on place.NavigationNodeId equals node.Id
                join floor in db.Floors on node.FloorId equals floor.Id
                where place.MallId == mall.Id && place.IsActive && tenant.IsActive
                    && promotion.Status == PromotionStatus.Published
                    && promotion.ValidFrom <= DemoSeeder.DemoDate && promotion.ValidUntil >= DemoSeeder.DemoDate
                orderby promotion.Title
                select new PromotionSummary(promotion.Title, promotion.Offer, promotion.Terms,
                    promotion.PosterAssetPath, place.Name, floor.Name + " · " + place.UnitNumber, promotion.ValidUntil))
                .ToListAsync(cancellationToken);
            return new CatalogSnapshot(mall.Name, mall.LogoAssetPath,
                places.Select(p => new PlaceSummary(p.Id, p.Name, p.Category, p.Description, p.Floor, p.Unit,
                    tags.Where(t => t.PlaceId == p.Id).Select(t => t.Value).ToArray())).ToList(), promotions);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Demo database has not been initialized.");
            return null;
        }
    }
}
