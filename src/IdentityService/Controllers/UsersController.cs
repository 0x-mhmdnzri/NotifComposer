using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserAppService _service;

    public UsersController(UserAppService service)
    {
        _service = service;
    }

    /// <summary>Create a new user</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get user by id</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get list of users with pagination and filtering</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(search, isActive, page, pageSize, ct);
        return Ok(result);
    }
}
