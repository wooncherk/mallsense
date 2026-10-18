using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.UnitTests.Fakes;

public class FakeUserRepository : IUserRepository
{
    private readonly List<ApplicationUser> _users = new();

    public Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ApplicationUser>>(_users.ToList());

    public Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

    public Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user =>
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<ApplicationUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user =>
            string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase)));

    public Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.FromResult(user);
    }

    public Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
