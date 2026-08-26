using TransactionalBox;

namespace NotificationService.Application.Messages;

/// <summary>
/// Inbox message – must match the payload shape published by EmployeeService.
/// </summary>
public sealed class EmployeeCreatedEvent : InboxMessage
{
    public Guid EmployeeId { get; init; }
    public Guid UserId { get; init; }
    public string Department { get; init; } = null!;
    public string Position { get; init; } = null!;
}
