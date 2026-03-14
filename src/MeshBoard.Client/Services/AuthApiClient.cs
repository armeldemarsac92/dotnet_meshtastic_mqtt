using System.Net;
using MeshBoard.Api.SDK.API;
using MeshBoard.Client.Authentication;
using MeshBoard.Client.Channels;
using MeshBoard.Client.Maps;
using MeshBoard.Client.Messages;
using MeshBoard.Client.Nodes;
using MeshBoard.Client.Realtime;
using MeshBoard.Client.Vault;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Client.Services;

public sealed class AuthApiClient
{
    private readonly IAuthApi _authApi;
    private readonly AntiforgeryTokenProvider _antiforgeryTokenProvider;
    private readonly AuthSessionState _authSessionState;
    private readonly BrowserRealtimeClient _browserRealtimeClient;
    private readonly ChannelProjectionStore _channelProjectionStore;
    private readonly DecryptedMessageStore _decryptedMessageStore;
    private readonly LiveMessageFeedService _liveMessageFeedService;
    private readonly LocalVaultService _localVaultService;
    private readonly MapProjectionStore _mapProjectionStore;
    private readonly NodeProjectionStore _nodeProjectionStore;

    public AuthApiClient(
        IAuthApi authApi,
        AntiforgeryTokenProvider antiforgeryTokenProvider,
        AuthSessionState authSessionState,
        BrowserRealtimeClient browserRealtimeClient,
        ChannelProjectionStore channelProjectionStore,
        DecryptedMessageStore decryptedMessageStore,
        LiveMessageFeedService liveMessageFeedService,
        LocalVaultService localVaultService,
        MapProjectionStore mapProjectionStore,
        NodeProjectionStore nodeProjectionStore)
    {
        _authApi = authApi;
        _antiforgeryTokenProvider = antiforgeryTokenProvider;
        _authSessionState = authSessionState;
        _browserRealtimeClient = browserRealtimeClient;
        _channelProjectionStore = channelProjectionStore;
        _decryptedMessageStore = decryptedMessageStore;
        _liveMessageFeedService = liveMessageFeedService;
        _localVaultService = localVaultService;
        _mapProjectionStore = mapProjectionStore;
        _nodeProjectionStore = nodeProjectionStore;
    }

    public async Task<AuthenticatedUserResponse?> LoginAsync(
        LoginUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _authApi.LoginAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Login failed."));
        }

        var user = response.Content
            ?? throw new InvalidOperationException("The API returned an empty login payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _antiforgeryTokenProvider.GetAsync(forceRefresh: true, cancellationToken: cancellationToken);
            var response = await _authApi.LogoutAsync(cancellationToken);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    ApiProblemDetailsParser.GetMessage(response, "Logout failed."));
            }
        }
        finally
        {
            try
            {
                await _browserRealtimeClient.DisconnectAsync(cancellationToken);
            }
            catch
            {
            }

            try
            {
                await _localVaultService.LockAsync(cancellationToken);
            }
            catch
            {
            }

            _antiforgeryTokenProvider.Clear();
            _authSessionState.Clear();
            _channelProjectionStore.Clear();
            _decryptedMessageStore.Clear();
            _liveMessageFeedService.Clear();
            _mapProjectionStore.Clear();
            _nodeProjectionStore.Clear();
        }
    }

    public async Task<AuthenticatedUserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _authApi.RegisterAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Registration failed."));
        }

        var user = response.Content
            ?? throw new InvalidOperationException("The API returned an empty register payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }

    public async Task<AuthenticatedUserResponse?> TryLoadCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await _authApi.GetCurrentUserAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _authSessionState.Clear();
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _authSessionState.Clear();
            return null;
        }

        var user = response.Content
            ?? throw new InvalidOperationException("The API returned an empty current-user payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }
}
