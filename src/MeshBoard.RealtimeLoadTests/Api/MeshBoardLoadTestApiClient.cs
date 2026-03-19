using System.Net;
using MeshBoard.Api.SDK.API;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Realtime;
using MeshBoard.RealtimeLoadTests.Configuration;

namespace MeshBoard.RealtimeLoadTests.Api;

internal sealed class MeshBoardLoadTestApiClient
{
    private readonly IAuthApi _authApi;
    private readonly IRealtimeSessionApi _realtimeSessionApi;
    private readonly RealtimeLoadTestOptions _options;

    public MeshBoardLoadTestApiClient(
        IAuthApi authApi,
        IRealtimeSessionApi realtimeSessionApi,
        RealtimeLoadTestOptions options)
    {
        _authApi = authApi;
        _realtimeSessionApi = realtimeSessionApi;
        _options = options;
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var currentUserResponse = await _authApi.GetCurrentUserAsync(cancellationToken);
        if (currentUserResponse.IsSuccessStatusCode)
        {
            return;
        }

        var loginResponse = await _authApi.LoginAsync(
            new LoginUserRequest
            {
                Username = _options.Username,
                Password = _options.Password
            },
            cancellationToken);

        if (loginResponse.IsSuccessStatusCode)
        {
            return;
        }

        if (_options.AutoRegisterIfMissing && loginResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            var registerResponse = await _authApi.RegisterAsync(
                new RegisterUserRequest
                {
                    Username = _options.Username,
                    Password = _options.Password
                },
                cancellationToken);

            if (registerResponse.IsSuccessStatusCode)
            {
                return;
            }

            throw new InvalidOperationException(
                LoadTestApiProblemDetailsParser.GetMessage(registerResponse, "Registering the load test user failed."));
        }

        throw new InvalidOperationException(
            LoadTestApiProblemDetailsParser.GetMessage(loginResponse, "Authenticating the load test user failed."));
    }

    public async Task<RealtimeSessionResponse> CreateRealtimeSessionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _realtimeSessionApi.CreateSessionAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                LoadTestApiProblemDetailsParser.GetMessage(response, "Opening the realtime session failed."));
        }

        return response.Content
               ?? throw new InvalidOperationException("The API returned an empty realtime session payload.");
    }
}
