using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Services;

public sealed class NotificationAppService
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationAppService> _logger;

    public NotificationAppService(INotificationRepository repository, ILogger<NotificationAppService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<NotificationResponse> CreateAsync(CreateNotificationRequest request, CancellationToken ct = default)
    {
        var notification = Notification.Create(request.UserId, request.Title, request.Message);
        await _repository.AddAsync(notification, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Notification created manually. Id={Id}, UserId={UserId}", notification.Id, notification.UserId);

        return Map(notification);
    }

    public async Task<NotificationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var n = await _repository.GetByIdAsync(id, ct);
        return n is null ? null : Map(n);
    }

    public async Task<PagedResult<NotificationResponse>> GetListAsync(
        Guid? userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repository.GetPagedAsync(userId, page, pageSize, ct);
        return new PagedResult<NotificationResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    private static NotificationResponse Map(Notification n) =>
        new(n.Id, n.UserId, n.Title, n.Message, n.CreatedAt);
}
