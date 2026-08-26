using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public class NotificationAppService
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<NotificationAppService> _logger;

    public NotificationAppService(NotificationDbContext db, ILogger<NotificationAppService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<NotificationResponse> CreateAsync(CreateNotificationRequest request, CancellationToken ct = default)
    {
        var notification = Notification.Create(request.UserId, request.Title, request.Message);
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Notification created. Id={Id}, UserId={UserId}, Title={Title}",
            notification.Id, notification.UserId, notification.Title);

        return Map(notification);
    }

    public async Task<NotificationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var n = await _db.Notifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return n is null ? null : Map(n);
    }

    public async Task<PagedResult<NotificationResponse>> GetListAsync(
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Notifications.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(n => n.UserId == userId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<NotificationResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    private static NotificationResponse Map(Notification n) =>
        new(n.Id, n.UserId, n.Title, n.Message, n.CreatedAt);
}
