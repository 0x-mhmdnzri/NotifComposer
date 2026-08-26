using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EmployeeService.Infrastructure.Clients;

public class NotificationOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5005";
}

public class NotificationHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationHttpClient> _logger;

    public NotificationHttpClient(HttpClient httpClient, IOptions<NotificationOptions> options, ILogger<NotificationHttpClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                userId,
                title,
                message
            };

            var response = await _httpClient.PostAsJsonAsync("api/notifications", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Notification Service returned {StatusCode}: {Body}", response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Notification sent successfully for UserId {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget: do not throw – just log
            _logger.LogError(ex, "Failed to send notification for UserId {UserId}. Employee creation will not be rolled back.", userId);
        }
    }
}
