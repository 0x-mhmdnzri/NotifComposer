namespace NotificationService.Application.DTOs;

public record CreateNotificationRequest(Guid UserId, string Title, string Message);

public record NotificationResponse(Guid Id, Guid UserId, string Title, string Message, DateTime CreatedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
