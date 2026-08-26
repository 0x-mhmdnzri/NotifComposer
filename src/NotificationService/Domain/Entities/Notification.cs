namespace NotificationService.Domain.Entities;

public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(Guid userId, string title, string message)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
