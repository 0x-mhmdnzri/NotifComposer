using EmployeeService.Application.Interfaces;
using Grpc.Core;
using Grpc.Net.Client;
using IdentityService.Grpc;
using Microsoft.Extensions.Options;

namespace EmployeeService.Infrastructure.Clients;

public class IdentityGrpcOptions
{
    public string GrpcAddress { get; set; } = "http://localhost:5002";

    /// <summary>
    /// Hard deadline for the sync UserExists call.
    /// Prevents Employee create p99 from tracking Identity's unbounded tail latency.
    /// (Latency skill: hide/bound dependency latency with timeouts.)
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 150;
}

/// <summary>
/// gRPC implementation of IIdentityClient.
/// Channel is shared (singleton) — connection setup cost is amortized, not paid per request.
/// Deadline bounds the contribution of this hop to Employee create latency.
/// </summary>
public sealed class IdentityGrpcClient : IIdentityClient, IDisposable
{
    private readonly UserService.UserServiceClient _client;
    private readonly GrpcChannel _channel;
    private readonly TimeSpan _timeout;
    private readonly ILogger<IdentityGrpcClient> _logger;

    public IdentityGrpcClient(IOptions<IdentityGrpcOptions> options, ILogger<IdentityGrpcClient> logger)
    {
        _logger = logger;
        var opts = options.Value;
        _timeout = TimeSpan.FromMilliseconds(Math.Clamp(opts.TimeoutMilliseconds, 50, 5000));

        // One channel for the process lifetime → connection reuse (reduce setup latency)
        _channel = GrpcChannel.ForAddress(opts.GrpcAddress, new GrpcChannelOptions
        {
            // Keep HTTP/2 connections warm
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
            }
        });
        _client = new UserService.UserServiceClient(_channel);
    }

    public async Task<(bool Exists, bool IsActive)> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        // Deadline is the latency bound for this stage of the call graph
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var callOptions = new CallOptions(deadline: DateTime.UtcNow.Add(_timeout), cancellationToken: cts.Token);

        try
        {
            var response = await _client.UserExistsAsync(
                new UserExistsRequest { Id = userId.ToString() },
                callOptions);

            return (response.Exists, response.IsActive);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.DeadlineExceeded or StatusCode.Cancelled)
        {
            _logger.LogWarning(
                "Identity gRPC UserExists timed out after {TimeoutMs}ms for {UserId} — dependency latency exceeded budget",
                _timeout.TotalMilliseconds, userId);
            throw new InvalidOperationException(
                $"Identity Service did not respond within {_timeout.TotalMilliseconds}ms latency budget.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Identity gRPC UserExists for {UserId}", userId);
            throw new InvalidOperationException("Identity Service is unavailable.", ex);
        }
    }

    public void Dispose() => _channel.Dispose();
}
