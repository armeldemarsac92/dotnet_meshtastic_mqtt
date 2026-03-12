using MeshBoard.Api.SDK.Abstractions;

namespace MeshBoard.Api.SDK.Infrastructure;

public sealed class AntiforgeryTokenHeaderHandler : DelegatingHandler
{
    private static readonly HashSet<string> SafeMethods =
    [
        HttpMethod.Get.Method,
        HttpMethod.Head.Method,
        HttpMethod.Options.Method,
        HttpMethod.Trace.Method
    ];

    private readonly IAntiforgeryRequestTokenProvider _antiforgeryRequestTokenProvider;

    public AntiforgeryTokenHeaderHandler(IAntiforgeryRequestTokenProvider antiforgeryRequestTokenProvider)
    {
        _antiforgeryRequestTokenProvider = antiforgeryRequestTokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!SafeMethods.Contains(request.Method.Method))
        {
            var requestToken = await _antiforgeryRequestTokenProvider.GetAsync(cancellationToken: cancellationToken);
            request.Headers.Add("X-CSRF-TOKEN", requestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
