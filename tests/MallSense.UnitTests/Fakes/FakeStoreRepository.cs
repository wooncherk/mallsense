using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;

namespace MallSense.UnitTests.Fakes;

public class FakeTenantRepository : ITenantRepository
{
    private readonly List<Tenant> _tenants = new();

    public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Tenant>>(_tenants.ToList());
    }

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = _tenants.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(tenant);
    }

    public Task<IReadOnlyList<Tenant>> GetByCategoryAsync(StoreCategory category, CancellationToken cancellationToken = default)
    {
        var tenants = _tenants.Where(t => t.Category == category).ToList();
        return Task.FromResult<IReadOnlyList<Tenant>>(tenants);
    }

    public Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _tenants.Add(tenant);
        return Task.FromResult(tenant);
    }

    public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var index = _tenants.FindIndex(t => t.Id == tenant.Id);
        if (index >= 0)
        {
            _tenants[index] = tenant;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _tenants.Remove(tenant);
        return Task.CompletedTask;
    }

    public Task<bool> UnitNumberExistsAsync(int floorNumber, string unitNumber, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        var exists = _tenants.Any(t =>
            t.FloorNumber == floorNumber &&
            t.UnitNumber.Equals(unitNumber, StringComparison.OrdinalIgnoreCase) &&
            (!excludeTenantId.HasValue || t.Id != excludeTenantId.Value));

        return Task.FromResult(exists);
    }
}
