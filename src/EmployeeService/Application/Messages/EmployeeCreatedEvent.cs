using TransactionalBox;

namespace EmployeeService.Application.Messages;

/// <summary>
/// Outbox message published after successful employee creation.
/// Name must be unique within the service.
/// </summary>
public sealed class EmployeeCreatedEvent : OutboxMessage
{
    public Guid EmployeeId { get; init; }
    public Guid UserId { get; init; }
    public string Department { get; init; } = null!;
    public string Position { get; init; } = null!;
}
