using EmployeeService.Application.DTOs;
using EmployeeService.Domain.Entities;
using EmployeeService.Infrastructure.Clients;
using EmployeeService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Application.Services;

public class EmployeeAppService
{
    private readonly EmployeeDbContext _db;
    private readonly IdentityGrpcClient _identityClient;
    private readonly NotificationHttpClient _notificationClient;
    private readonly ILogger<EmployeeAppService> _logger;

    public EmployeeAppService(
        EmployeeDbContext db,
        IdentityGrpcClient identityClient,
        NotificationHttpClient notificationClient,
        ILogger<EmployeeAppService> logger)
    {
        _db = db;
        _identityClient = identityClient;
        _notificationClient = notificationClient;
        _logger = logger;
    }

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        // 1. Check user existence via gRPC
        var (exists, isActive) = await _identityClient.UserExistsAsync(request.UserId, ct);
        if (!exists)
            throw new InvalidOperationException("User does not exist in Identity Service.");
        if (!isActive)
            throw new InvalidOperationException("User is not active.");

        // 2. Prevent duplicate employee for same user
        var alreadyExists = await _db.Employees.AnyAsync(e => e.UserId == request.UserId, ct);
        if (alreadyExists)
            throw new InvalidOperationException("An employee record already exists for this user.");

        // 3. Create
        var employee = Employee.Create(
            request.UserId,
            request.Department,
            request.Position,
            request.EmploymentDate,
            request.Preferences);

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        // 4. Fire-and-forget notification (no rollback on failure)
        _ = _notificationClient.SendNotificationAsync(
            request.UserId,
            "Employee Created",
            $"Your employee profile has been created. Department: {request.Department}, Position: {request.Position}",
            CancellationToken.None);

        return Map(employee);
    }

    public async Task<EmployeeResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return emp is null ? null : Map(emp);
    }

    public async Task<EmployeeResponse> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Employee not found.");

        emp.Update(request.Department, request.Position, request.EmploymentDate);
        await _db.SaveChangesAsync(ct);
        return Map(emp);
    }

    public async Task<EmployeeResponse> UpdatePreferencesAsync(Guid id, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Employee not found.");

        emp.UpdatePreferences(request.Preferences);
        await _db.SaveChangesAsync(ct);
        return Map(emp);
    }

    public async Task<PagedResult<EmployeeResponse>> GetListAsync(
        string? department,
        string? position,
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

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

        return new PagedResult<EmployeeResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    private static EmployeeResponse Map(Employee e) => new(
        e.Id,
        e.UserId,
        e.Department,
        e.Position,
        e.EmploymentDate,
        e.GetPreferences(),
        e.CreatedAt,
        e.UpdatedAt);
}
