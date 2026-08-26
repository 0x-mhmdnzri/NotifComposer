using TransactionalBox;

namespace NotificationService.Application.Messages;

/// <summary>
/// Subscribe to events published by EmployeeService.
/// </summary>
public sealed class EmployeeCreatedEventDefinition : InboxDefinition<EmployeeCreatedEvent>
{
    public EmployeeCreatedEventDefinition()
    {
        PublishedBy = "EmployeeService";
    }
}
