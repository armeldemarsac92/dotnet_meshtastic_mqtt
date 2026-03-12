using MeshBoard.Api.SDK.Abstractions;

namespace MeshBoard.Api.SDK.Infrastructure;

public sealed class MeshBoardApiRequestConfigurationHandler : DelegatingHandler
{
    private readonly IReadOnlyList<IMeshBoardApiRequestConfigurator> _configurators;

    public MeshBoardApiRequestConfigurationHandler(IEnumerable<IMeshBoardApiRequestConfigurator> configurators)
    {
        _configurators = configurators.ToArray();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        foreach (var configurator in _configurators)
        {
            await configurator.ConfigureAsync(request, cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
