namespace IdentityService.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Mobile { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public static User Create(string fullName, string mobile)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Mobile = mobile.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
