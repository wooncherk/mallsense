using MallSense.Domain.Entities;
using MallSense.Domain.Enums;

namespace MallSense.Domain.Interfaces;

public interface IIntentSignalRepository
{
    Task<ShopperIntentSignal> AddAsync(ShopperIntentSignal signal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShopperIntentSignal>> GetRecentSignalsAsync(int days = 7, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShopperIntentSignal>> GetSignalsByCategoryAsync(IntentCategory category, int days = 7, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<ShopperIntentSignal> signals, CancellationToken cancellationToken = default);
}
