using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace DoorMonitor.Functions.Http;

internal static class ApiResponses
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseData> JsonAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        object body,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");

        // Web フロントエンドを Static Web Apps とは別オリジンに置く場合に備える。
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
        await response.WriteStringAsync(json, cancellationToken).ConfigureAwait(false);
        return response;
    }

    public static Task<HttpResponseData> ErrorAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken)
        => JsonAsync(request, statusCode, new { error = message }, cancellationToken);

    public static async Task<T?> ReadBodyAsync<T>(HttpRequestData request, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer
                .DeserializeAsync<T>(request.Body, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
