using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;

    public NotificationRepository(NotificationDbContext db) => _db = db;

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
        => await _db.Notifications.AddAsync(notification, ct);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedAsync(
        Guid? userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(n => n.UserId == userId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
