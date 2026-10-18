using Microsoft.EntityFrameworkCore;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.Data;

namespace MallSense.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly MallDbContext _context;

    public UserRepository(MallDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.LastActiveAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task<ApplicationUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.UserName.ToLower() == userName.ToLower(), cancellationToken);

    public async Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
