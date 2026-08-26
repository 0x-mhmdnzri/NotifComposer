using EmployeeService.Application.Interfaces;
using EmployeeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Infrastructure.Persistence;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly EmployeeDbContext _db;

    public EmployeeRepository(EmployeeDbContext db) => _db = db;

    public async Task AddAsync(Employee employee, CancellationToken ct = default)
        => await _db.Employees.AddAsync(employee, ct);

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Employee?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
        => _db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _db.Employees.AnyAsync(e => e.UserId == userId, ct);

    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetPagedAsync(
        string? department, string? position, Guid? userId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.Department.ToLower().Contains(department.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(position))
            query = query.Where(e => e.Position.ToLower().Contains(position.Trim().ToLower()));

        if (userId.HasValue)
            query = query.Where(e => e.UserId == userId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
