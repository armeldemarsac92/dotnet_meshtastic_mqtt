namespace MeshBoard.Client.Services;

public sealed class AntiforgeryTokenHeaderHandler : DelegatingHandler
{
    private static readonly HashSet<string> SafeMethods =
    [
        HttpMethod.Get.Method,
        HttpMethod.Head.Method,
        HttpMethod.Options.Method,
        HttpMethod.Trace.Method
    ];

    private readonly AntiforgeryTokenProvider _antiforgeryTokenProvider;

    public AntiforgeryTokenHeaderHandler(AntiforgeryTokenProvider antiforgeryTokenProvider)
    {
        _antiforgeryTokenProvider = antiforgeryTokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!SafeMethods.Contains(request.Method.Method))
        {
            var requestToken = await _antiforgeryTokenProvider.GetAsync(cancellationToken: cancellationToken);
            request.Headers.Remove("X-CSRF-TOKEN");
            request.Headers.Add("X-CSRF-TOKEN", requestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
