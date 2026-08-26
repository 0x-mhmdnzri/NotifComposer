namespace IdentityService.Application.DTOs;

public record CreateUserRequest(string FullName, string Mobile);

public record UserResponse(Guid Id, string FullName, string Mobile, bool IsActive, DateTime CreatedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
