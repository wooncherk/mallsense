using MallSense.Domain.Entities;

namespace MallSense.Domain.Interfaces;

public interface IShopperBehaviorEventRepository
{
    Task<ShopperBehaviorEvent> AddAsync(ShopperBehaviorEvent behaviorEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShopperBehaviorEvent>> GetBetweenAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
