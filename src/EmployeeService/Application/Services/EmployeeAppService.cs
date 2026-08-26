using EmployeeService.Application.DTOs;
using EmployeeService.Application.Interfaces;
using EmployeeService.Application.Messages;
using EmployeeService.Domain.Entities;
using TransactionalBox;

namespace EmployeeService.Application.Services;

/// <summary>
/// Orchestrates employee use-cases. Depends only on abstractions (DIP).
/// Uses Transactional Outbox so the event is stored in the same DB transaction.
/// </summary>
public sealed class EmployeeAppService
{
    private readonly IEmployeeRepository _repository;
    private readonly IIdentityClient _identityClient;
    private readonly IOutbox _outbox;
    private readonly ILogger<EmployeeAppService> _logger;

    public EmployeeAppService(
        IEmployeeRepository repository,
        IIdentityClient identityClient,
        IOutbox outbox,
        ILogger<EmployeeAppService> logger)
    {
        _repository = repository;
        _identityClient = identityClient;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        // 1. Sync check via gRPC
        var (exists, isActive) = await _identityClient.UserExistsAsync(request.UserId, ct);
        if (!exists)
            throw new InvalidOperationException("User does not exist in Identity Service.");
        if (!isActive)
            throw new InvalidOperationException("User is not active.");

        if (await _repository.ExistsByUserIdAsync(request.UserId, ct))
            throw new InvalidOperationException("An employee record already exists for this user.");

        var employee = Employee.Create(
            request.UserId,
            request.Department,
            request.Position,
            request.EmploymentDate,
            request.Preferences);

        // 2. Business data + Outbox message in the SAME transaction
        await _repository.AddAsync(employee, ct);

        await _outbox.Add(new EmployeeCreatedEvent
        {
            EmployeeId = employee.Id,
            UserId = employee.UserId,
            Department = employee.Department,
            Position = employee.Position
        });

        await _repository.SaveChangesAsync(ct);

        // 3. Signal that transaction committed → background job will send to Kafka
        await _outbox.TransactionCommited();

        _logger.LogInformation(
            "Employee {EmployeeId} created and EmployeeCreatedEvent added to Outbox", employee.Id);

        return Map(employee);
    }

    public async Task<EmployeeResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var emp = await _repository.GetByIdAsync(id, ct);
        return emp is null ? null : Map(emp);
    }

    public async Task<EmployeeResponse> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var emp = await _repository.GetByIdForUpdateAsync(id, ct)
            ?? throw new KeyNotFoundException("Employee not found.");

        emp.Update(request.Department, request.Position, request.EmploymentDate);
        await _repository.SaveChangesAsync(ct);
        return Map(emp);
    }

    public async Task<EmployeeResponse> UpdatePreferencesAsync(Guid id, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        var emp = await _repository.GetByIdForUpdateAsync(id, ct)
            ?? throw new KeyNotFoundException("Employee not found.");

        emp.UpdatePreferences(request.Preferences);
        await _repository.SaveChangesAsync(ct);
        return Map(emp);
    }

    public async Task<PagedResult<EmployeeResponse>> GetListAsync(
        string? department, string? position, Guid? userId,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repository.GetPagedAsync(department, position, userId, page, pageSize, ct);
        return new PagedResult<EmployeeResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    private static EmployeeResponse Map(Employee e) => new(
        e.Id, e.UserId, e.Department, e.Position, e.EmploymentDate,
        e.GetPreferences(), e.CreatedAt, e.UpdatedAt);
}
