using IdentityService.Application.DTOs;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Application.Services;

public class UserAppService
{
    private readonly IdentityDbContext _db;

    public UserAppService(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var exists = await _db.Users.AnyAsync(u => u.Mobile == request.Mobile, ct);
        if (exists)
            throw new InvalidOperationException("A user with this mobile already exists.");

        var user = User.Create(request.FullName, request.Mobile);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Map(user);
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : Map(user);
    }

    public async Task<PagedResult<UserResponse>> GetListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(search) || u.Mobile.Contains(search));
        }

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<UserResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<(bool Exists, bool IsActive)> UserExistsAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync(ct);

        return user is null ? (false, false) : (true, user.IsActive);
    }

    private static UserResponse Map(User u) =>
        new(u.Id, u.FullName, u.Mobile, u.IsActive, u.CreatedAt);
}
