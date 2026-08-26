namespace EmployeeService.Application.Interfaces;

/// <summary>
/// Abstraction over Identity Service (DIP).
/// Implementation uses gRPC; can be swapped without touching application layer.
/// </summary>
public interface IIdentityClient
{
    Task<(bool Exists, bool IsActive)> UserExistsAsync(Guid userId, CancellationToken ct = default);
}
