using NotificationService.Application.Interfaces;
using NotificationService.Application.Messages;
using NotificationService.Domain.Entities;
using TransactionalBox;

namespace NotificationService.Application.Handlers;

/// <summary>
/// Exactly-once handler (TransactionalBox guarantees via IdempotentInboxKey).
/// Single responsibility: turn EmployeeCreatedEvent into a Notification.
/// </summary>
internal sealed class EmployeeCreatedEventHandler : IInboxHandler<EmployeeCreatedEvent>
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<EmployeeCreatedEventHandler> _logger;

    public EmployeeCreatedEventHandler(
        INotificationRepository repository,
        ILogger<EmployeeCreatedEventHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(EmployeeCreatedEvent message, IExecutionContext executionContext)
    {
        var notification = Notification.Create(
            message.UserId,
            "Employee Created",
            $"Your employee profile has been created. Department: {message.Department}, Position: {message.Position}");

        await _repository.AddAsync(notification, executionContext.CancellationToken);
        await _repository.SaveChangesAsync(executionContext.CancellationToken);

        _logger.LogInformation(
            "Notification {NotificationId} created from EmployeeCreatedEvent (EmployeeId={EmployeeId}, UserId={UserId})",
            notification.Id, message.EmployeeId, message.UserId);
    }
}
