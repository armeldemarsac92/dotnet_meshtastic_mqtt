using System.Text.Json;
using Refit;

namespace MeshBoard.Client.Services;

public static class ApiProblemDetailsParser
{
    public static string GetMessage(IApiResponse response, string fallbackMessage)
    {
        return GetMessage(response.Error, fallbackMessage);
    }

    public static string GetMessage(ApiException? exception, string fallbackMessage)
    {
        if (exception is null)
        {
            return fallbackMessage;
        }

        return GetMessage(exception.Content, fallbackMessage);
    }

    public static string GetMessage(string? content, string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return fallbackMessage;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }

            if (root.TryGetProperty("title", out var title) &&
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
