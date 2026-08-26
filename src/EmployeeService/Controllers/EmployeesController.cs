using EmployeeService.Application.DTOs;
using EmployeeService.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace EmployeeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly EmployeeAppService _service;
    private readonly IOutputCacheStore _cacheStore;

    public EmployeesController(EmployeeAppService service, IOutputCacheStore cacheStore)
    {
        _service = service;
        _cacheStore = cacheStore;
    }

    /// <summary>Create employee — path includes sync gRPC; notification is async (Outbox) so not on critical path</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        await _cacheStore.EvictByTagAsync("employees", ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = "EmployeeById")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        await _cacheStore.EvictByTagAsync("employees", ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/preferences")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences(Guid id, [FromBody] UpdatePreferencesRequest request, CancellationToken ct)
    {
        var result = await _service.UpdatePreferencesAsync(id, request, ct);
        await _cacheStore.EvictByTagAsync("employees", ct);
        return Ok(result);
    }

    [HttpGet]
    [OutputCache]
    [ProducesResponseType(typeof(PagedResult<EmployeeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? department,
        [FromQuery] string? position,
        [FromQuery] Guid? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(department, position, userId, page, pageSize, ct);
        return Ok(result);
    }
}
