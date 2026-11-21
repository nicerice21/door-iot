using System.Net.Http.Json;
using System.Text.Json;
using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Gateway.Cloud;

/// <summary>Azure Functions への HTTPS 送信。認証は X-Api-Key ヘッダで行う。</summary>
public sealed class CloudApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private readonly string _apiKey;

    public CloudApiClient(string apiBaseUrl, string apiKey)
    {
        _baseUri = new Uri(apiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        _apiKey = apiKey;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public Task SendTelemetryAsync(TelemetryReport report, CancellationToken cancellationToken)
        => PostAsync("api/telemetry", report, cancellationToken);

    public Task SendHeartbeatAsync(GatewayHeartbeat heartbeat, CancellationToken cancellationToken)
        => PostAsync("api/heartbeat", heartbeat, cancellationToken);

    private async Task PostAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, path))
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Add("X-Api-Key", _apiKey);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}

public sealed record GatewayHeartbeat(string GatewayId, DateTimeOffset SentAt);
