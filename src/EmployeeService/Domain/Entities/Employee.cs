using System.Text.Json;

namespace EmployeeService.Domain.Entities;

public class Employee
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Department { get; private set; } = null!;
    public string Position { get; private set; } = null!;
    public DateTime EmploymentDate { get; private set; }
    public string PreferencesJson { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Employee() { }

    public static Employee Create(Guid userId, string department, string position, DateTime employmentDate, object? preferences = null)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Department = department.Trim(),
            Position = position.Trim(),
            EmploymentDate = employmentDate,
            PreferencesJson = preferences is null
                ? JsonSerializer.Serialize(new { language = "fa", theme = "light", receiveEmail = true, receiveSms = false })
                : JsonSerializer.Serialize(preferences),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string department, string position, DateTime employmentDate)
    {
        Department = department.Trim();
        Position = position.Trim();
        EmploymentDate = employmentDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePreferences(object preferences)
    {
        PreferencesJson = JsonSerializer.Serialize(preferences);
        UpdatedAt = DateTime.UtcNow;
    }

    public Dictionary<string, object?> GetPreferences()
    {
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(PreferencesJson)
               ?? new Dictionary<string, object?>();
    }
}
