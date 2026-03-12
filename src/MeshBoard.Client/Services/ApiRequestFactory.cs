using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace MeshBoard.Client.Services;

public sealed class ApiRequestFactory
{
    public HttpRequestMessage Create(HttpMethod method, string uri, string? antiforgeryToken = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        if (!string.IsNullOrWhiteSpace(antiforgeryToken))
        {
            request.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        }

        return request;
    }

    public HttpRequestMessage CreateJson<TRequest>(
        HttpMethod method,
        string uri,
        TRequest payload,
        string? antiforgeryToken = null)
    {
        var request = Create(method, uri, antiforgeryToken);
        request.Content = JsonContent.Create(payload);
        return request;
    }

    public static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        string fallbackMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }

            if (document.RootElement.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
        }
        catch
        {
        }

        return fallbackMessage;
    }
}
