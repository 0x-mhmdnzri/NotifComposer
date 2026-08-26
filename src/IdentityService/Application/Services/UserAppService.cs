using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Services;

/// <summary>
/// Application service – single responsibility: orchestrate user use-cases.
/// Depends only on abstractions (DIP).
/// </summary>
public sealed class UserAppService
{
    private readonly IUserRepository _repository;

    public UserAppService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await _repository.ExistsByMobileAsync(request.Mobile, ct))
            throw new InvalidOperationException("A user with this mobile already exists.");

        var user = User.Create(request.FullName, request.Mobile);
        await _repository.AddAsync(user, ct);
        await _repository.SaveChangesAsync(ct);

        return Map(user);
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        return user is null ? null : Map(user);
    }

    public async Task<PagedResult<UserResponse>> GetListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repository.GetPagedAsync(search, isActive, page, pageSize, ct);
        return new PagedResult<UserResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    public Task<(bool Exists, bool IsActive)> UserExistsAsync(Guid id, CancellationToken ct = default)
        => _repository.ExistsAsync(id, ct);

    private static UserResponse Map(User u) =>
        new(u.Id, u.FullName, u.Mobile, u.IsActive, u.CreatedAt);
}
