using MeshBoard.Contracts.Authentication;
using Microsoft.AspNetCore.Antiforgery;

namespace MeshBoard.Api.Authentication;

internal static class AntiforgeryTokenMappingExtensions
{
    public static AntiforgeryTokenResponse ToAntiforgeryTokenResponse(this AntiforgeryTokenSet tokens)
    {
        return new AntiforgeryTokenResponse
        {
            RequestToken = tokens.RequestToken ?? string.Empty
        };
    }
}
