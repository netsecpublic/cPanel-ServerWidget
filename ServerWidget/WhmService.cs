using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ServerWidget;

public class WhmService
{
    private static readonly HttpClient Client;

    static WhmService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        Client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static async Task<(bool isOnline, int queueCount, double serverLoad, string errorDetails)> CheckQueueAsync(ServerConfig server)
    {
        if (string.IsNullOrWhiteSpace(server.Host))
            return (false, -1, 0.0, "No Host IP");

        string host = server.Host.Trim();
        string scheme = host.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "" : "https://";

        if (host.Contains(".php") || server.Port == 443 || server.Port == 80)
        {
            return await CheckPhpEndpointAsync($"{scheme}{host}", server.ApiToken);
        }

        return await CheckPhpEndpointAsync($"{scheme}{host}/queue.php", server.ApiToken);
    }

    private static async Task<(bool isOnline, int queueCount, double serverLoad, string errorDetails)> CheckPhpEndpointAsync(string url, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.TryAddWithoutValidation("X-Auth-Token", token.Trim());
            }

            using var response = await Client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return (false, -1, 0.0, $"HTTP {(int)response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "ok")
            {
                int queue = -1;
                double load = 0.0;

                if (root.TryGetProperty("count", out var countProp))
                    queue = ParseInt(countProp);

                if (root.TryGetProperty("load", out var loadProp))
                    load = ParseDouble(loadProp);

                return (true, queue, load, "");
            }

            return (false, -1, 0.0, "Bad PHP Output");
        }
        catch
        {
            return (false, -1, 0.0, "Offline");
        }
    }

    private static int ParseInt(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out int num))
            return num;
        if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), out int parsed))
            return parsed;

        return -1;
    }

    private static double ParseDouble(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDouble(out double num))
            return num;
        if (elem.ValueKind == JsonValueKind.String && double.TryParse(elem.GetString(), out double parsed))
            return parsed;
        return 0.0;
    }
}