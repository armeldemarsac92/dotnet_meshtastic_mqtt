using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.IntegrationTests;

public sealed class ApiEndpointIntegrationTests
{
    [Fact]
    public async Task GetCurrentUser_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldEstablishAuthenticatedSession()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var registerResponse = await PostJsonAsync(
            client,
            host,
            "/api/auth/register",
            new RegisterUserRequest
            {
                Username = CreateUsername(),
                Password = "password-123"
            });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registeredUser = await registerResponse.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
        Assert.NotNull(registeredUser);
        Assert.False(string.IsNullOrWhiteSpace(registeredUser!.WorkspaceId));

        var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var currentUser = await meResponse.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
        Assert.NotNull(currentUser);
        Assert.Equal(registeredUser.Id, currentUser!.Id);
        Assert.Equal(registeredUser.WorkspaceId, currentUser.WorkspaceId);
    }

    [Fact]
    public async Task Register_WhenUsernameAlreadyExists_ShouldReturnConflict()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        var username = CreateUsername();

        var firstResponse = await PostJsonAsync(
            client,
            host,
            "/api/auth/register",
            new RegisterUserRequest
            {
                Username = username,
                Password = "password-123"
            });

        var duplicateResponse = await PostJsonAsync(
            client,
            host,
            "/api/auth/register",
            new RegisterUserRequest
            {
                Username = username,
                Password = "password-123"
            });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var response = await PostJsonAsync(
            client,
            host,
            "/api/auth/login",
            new LoginUserRequest
            {
                Username = "missing-user",
                Password = "bad-password"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldInvalidateAuthenticatedSession()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var logoutResponse = await PostJsonAsync(client, host, "/api/auth/logout", payload: null);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Preferences_WhenAuthenticated_ShouldReturnProvisionedBrokerAndTopicPresetData()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var brokersResponse = await client.GetAsync("/api/preferences/brokers");
        var activeBrokerResponse = await client.GetAsync("/api/preferences/brokers/active");
        var presetsResponse = await client.GetAsync("/api/preferences/topic-presets");

        Assert.Equal(HttpStatusCode.OK, brokersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activeBrokerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, presetsResponse.StatusCode);

        var brokers = await brokersResponse.Content.ReadFromJsonAsync<List<SavedBrokerServerProfile>>();
        var activeBroker = await activeBrokerResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        var presets = await presetsResponse.Content.ReadFromJsonAsync<List<SavedTopicPreset>>();

        Assert.NotNull(brokers);
        Assert.NotEmpty(brokers!);
        Assert.NotNull(activeBroker);
        Assert.True(activeBroker!.IsActive);
        Assert.NotNull(presets);
        Assert.NotEmpty(presets!);
    }

    [Fact]
    public async Task Favorites_ShouldRoundTripAndReturnNotFoundWhenDeletingMissingNode()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var saveResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/favorites",
            new SaveFavoriteNodeRequest
            {
                NodeId = "!abc123",
                ShortName = "ABC",
                LongName = "Alpha Bravo"
            });

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/preferences/favorites");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var favorites = await getResponse.Content.ReadFromJsonAsync<List<FavoriteNode>>();
        Assert.NotNull(favorites);
        Assert.Contains(favorites!, favorite => favorite.NodeId == "!abc123");

        var deleteResponse = await DeleteAsync(client, host, "/api/preferences/favorites/%21abc123");
        var deleteMissingResponse = await DeleteAsync(client, host, "/api/preferences/favorites/%21abc123");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteMissingResponse.StatusCode);
    }

    private static string CreateUsername()
    {
        return $"user-{Guid.NewGuid():N}".Substring(0, 17);
    }

    private static async Task<AuthenticatedUserResponse> RegisterAsync(HttpClient client, ApiIntegrationTestHost host)
    {
        var response = await PostJsonAsync(
            client,
            host,
            "/api/auth/register",
            new RegisterUserRequest
            {
                Username = CreateUsername(),
                Password = "password-123"
            });

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>()
            ?? throw new InvalidOperationException("The API returned an empty register payload.");
    }

    private static async Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        ApiIntegrationTestHost host,
        string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Add("X-CSRF-TOKEN", await host.GetAntiforgeryTokenAsync(client));

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        ApiIntegrationTestHost host,
        string uri,
        object? payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("X-CSRF-TOKEN", await host.GetAntiforgeryTokenAsync(client));

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return await client.SendAsync(request);
    }
}
