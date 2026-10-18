using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public class IntentSignalRepository : IIntentSignalRepository
{
    private readonly MallDbContext _context;

    public IntentSignalRepository(MallDbContext context)
    {
        _context = context;
    }

    public async Task<ShopperIntentSignal> AddAsync(ShopperIntentSignal signal, CancellationToken cancellationToken = default)
    {
        await _context.IntentSignals.AddAsync(signal, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return signal;
    }

    public async Task AddRangeAsync(IEnumerable<ShopperIntentSignal> signals, CancellationToken cancellationToken = default)
    {
        await _context.IntentSignals.AddRangeAsync(signals, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShopperIntentSignal>> GetRecentSignalsAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _context.IntentSignals
            .AsNoTracking()
            .Where(s => s.TimestampUtc >= cutoff)
            .OrderByDescending(s => s.TimestampUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShopperIntentSignal>> GetSignalsByCategoryAsync(IntentCategory category, int days = 7, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _context.IntentSignals
            .AsNoTracking()
            .Where(s => s.TimestampUtc >= cutoff && s.IntentCategory == category)
            .OrderByDescending(s => s.TimestampUtc)
            .ToListAsync(cancellationToken);
    }
}
