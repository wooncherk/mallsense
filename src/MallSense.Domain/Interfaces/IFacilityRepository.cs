using MallSense.Domain.Entities;

namespace MallSense.Domain.Interfaces;

public interface IFacilityRepository
{
    Task<IReadOnlyList<MallFacility>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MallFacility>> GetByFloorAsync(int floorNumber, CancellationToken cancellationToken = default);
}
