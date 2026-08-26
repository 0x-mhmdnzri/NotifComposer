using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedAsync(
        Guid? userId, int page, int pageSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
