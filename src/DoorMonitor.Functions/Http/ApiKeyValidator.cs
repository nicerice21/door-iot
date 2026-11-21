using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace DoorMonitor.Functions.Http;

/// <summary>ゲートウェイからの書き込み API を X-Api-Key で保護する。</summary>
internal static class ApiKeyValidator
{
    private const string HeaderName = "X-Api-Key";

    public static bool IsAuthorized(HttpRequestData request, IConfiguration configuration)
    {
        var expected = configuration["ApiKey"];

        // ローカル開発用: 未設定なら認証なしで許可する。本番では必ず設定する。
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        foreach (var header in request.Headers)
        {
            if (string.Equals(header.Key, HeaderName, StringComparison.OrdinalIgnoreCase)
                && header.Value.Any(value => string.Equals(value, expected, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
