namespace EmployeeService.Application.DTOs;

public record CreateEmployeeRequest(
    Guid UserId,
    string Department,
    string Position,
    DateTime EmploymentDate,
    Dictionary<string, object?>? Preferences);

public record UpdateEmployeeRequest(
    string Department,
    string Position,
    DateTime EmploymentDate);

public record UpdatePreferencesRequest(Dictionary<string, object?> Preferences);

public record EmployeeResponse(
    Guid Id,
    Guid UserId,
    string Department,
    string Position,
    DateTime EmploymentDate,
    Dictionary<string, object?> Preferences,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
