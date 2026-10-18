using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public class FacilityRepository : IFacilityRepository
{
    private readonly MallDbContext _context;

    public FacilityRepository(MallDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MallFacility>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Facilities
            .Include(f => f.NavigationNode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MallFacility>> GetByFloorAsync(int floorNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Facilities
            .Include(f => f.NavigationNode)
            .AsNoTracking()
            .Where(f => f.FloorNumber == floorNumber)
            .ToListAsync(cancellationToken);
    }
}
