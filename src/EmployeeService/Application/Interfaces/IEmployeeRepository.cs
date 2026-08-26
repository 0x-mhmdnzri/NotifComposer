using EmployeeService.Domain.Entities;

namespace EmployeeService.Application.Interfaces;

public interface IEmployeeRepository
{
    Task AddAsync(Employee employee, CancellationToken ct = default);
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetPagedAsync(
        string? department, string? position, Guid? userId,
        int page, int pageSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
