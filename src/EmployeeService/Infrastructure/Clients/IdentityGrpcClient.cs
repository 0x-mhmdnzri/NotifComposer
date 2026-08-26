using EmployeeService.Application.Interfaces;
using Grpc.Net.Client;
using IdentityService.Grpc;
using Microsoft.Extensions.Options;

namespace EmployeeService.Infrastructure.Clients;

public class IdentityGrpcOptions
{
    public string GrpcAddress { get; set; } = "http://localhost:5002";
}

/// <summary>
/// gRPC implementation of IIdentityClient (DIP).
/// </summary>
public sealed class IdentityGrpcClient : IIdentityClient
{
    private readonly UserService.UserServiceClient _client;
    private readonly ILogger<IdentityGrpcClient> _logger;

    public IdentityGrpcClient(IOptions<IdentityGrpcOptions> options, ILogger<IdentityGrpcClient> logger)
    {
        _logger = logger;
        var channel = GrpcChannel.ForAddress(options.Value.GrpcAddress);
        _client = new UserService.UserServiceClient(channel);
    }

    public async Task<(bool Exists, bool IsActive)> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.UserExistsAsync(
                new UserExistsRequest { Id = userId.ToString() },
                cancellationToken: ct);

            return (response.Exists, response.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Identity gRPC UserExists for {UserId}", userId);
            throw new InvalidOperationException("Identity Service is unavailable.", ex);
        }
    }
}
