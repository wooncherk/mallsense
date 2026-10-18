using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public sealed class ShopperBehaviorEventRepository(MallDbContext context) : IShopperBehaviorEventRepository
{
    public async Task<ShopperBehaviorEvent> AddAsync(
        ShopperBehaviorEvent behaviorEvent, CancellationToken cancellationToken = default)
    {
        await context.ShopperBehaviorEvents.AddAsync(behaviorEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return behaviorEvent;
    }

    public async Task<IReadOnlyList<ShopperBehaviorEvent>> GetBetweenAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        await context.ShopperBehaviorEvents
            .AsNoTracking()
            .Where(item => item.TimestampUtc >= fromUtc && item.TimestampUtc < toUtc)
            .OrderBy(item => item.TimestampUtc)
            .ToListAsync(cancellationToken);
}
