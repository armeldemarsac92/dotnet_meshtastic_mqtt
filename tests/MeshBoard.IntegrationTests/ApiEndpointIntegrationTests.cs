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
    public async Task BrokerPreferences_PostShouldCreateBrokerProfileWithSafeDto()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var request = CreateBrokerRequest(
            name: "Portable gateway",
            host: "portable-gateway.example.com",
            password: "secret-pass",
            defaultTopicPattern: "msh/EU/2/e/Portable/#",
            downlinkTopic: "msh/EU/2/json/mqtt/portable/");

        var createResponse = await PostJsonAsync(client, host, "/api/preferences/brokers", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdProfile = await createResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        Assert.NotNull(createdProfile);
        Assert.Equal(request.Name, createdProfile!.Name);
        Assert.Equal(request.Host, createdProfile.Host);
        Assert.Equal(request.DefaultTopicPattern, createdProfile.DefaultTopicPattern);
        Assert.Equal(request.DownlinkTopic, createdProfile.DownlinkTopic);
        Assert.True(createdProfile.HasPasswordConfigured);

        var brokers = await client.GetFromJsonAsync<List<SavedBrokerServerProfile>>("/api/preferences/brokers");
        Assert.NotNull(brokers);
        Assert.Contains(brokers!, broker => broker.Id == createdProfile.Id);
    }

    [Fact]
    public async Task BrokerPreferences_PutShouldUpdateBrokerProfileAndHonorSafePasswordFlags()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var createResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/brokers",
            CreateBrokerRequest(
                name: "Field gateway",
                host: "field-gateway.example.com",
                password: "initial-secret",
                defaultTopicPattern: "msh/US/2/e/Field/#",
                downlinkTopic: "msh/US/2/json/mqtt/field/"));

        var createdProfile = await createResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        Assert.NotNull(createdProfile);

        var updateResponse = await PutJsonAsync(
            client,
            host,
            $"/api/preferences/brokers/{createdProfile!.Id}",
            CreateBrokerRequest(
                name: "Field gateway updated",
                host: "field-gateway-updated.example.com",
                password: null,
                defaultTopicPattern: "msh/US/2/e/FieldUpdated/#",
                downlinkTopic: "msh/US/2/json/mqtt/field-updated/",
                clearPassword: true,
                enableSend: true));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedProfile = await updateResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        Assert.NotNull(updatedProfile);
        Assert.Equal(createdProfile.Id, updatedProfile!.Id);
        Assert.Equal("Field gateway updated", updatedProfile.Name);
        Assert.Equal("field-gateway-updated.example.com", updatedProfile.Host);
        Assert.Equal("msh/US/2/e/FieldUpdated/#", updatedProfile.DefaultTopicPattern);
        Assert.Equal("msh/US/2/json/mqtt/field-updated/", updatedProfile.DownlinkTopic);
        Assert.True(updatedProfile.EnableSend);
        Assert.False(updatedProfile.HasPasswordConfigured);
    }

    [Fact]
    public async Task BrokerPreferences_ActivateShouldSwitchActiveProfile()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var seededActive = await client.GetFromJsonAsync<SavedBrokerServerProfile>("/api/preferences/brokers/active");
        Assert.NotNull(seededActive);

        var createResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/brokers",
            CreateBrokerRequest(
                name: "Backpack relay",
                host: "backpack-relay.example.com",
                password: null,
                defaultTopicPattern: "msh/EU/2/e/Backpack/#",
                downlinkTopic: "msh/EU/2/json/mqtt/backpack/"));

        var createdProfile = await createResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        Assert.NotNull(createdProfile);

        var activateResponse = await PostJsonAsync(
            client,
            host,
            $"/api/preferences/brokers/{createdProfile!.Id}/activate",
            payload: null);

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var activeProfile = await activateResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        Assert.NotNull(activeProfile);
        Assert.Equal(createdProfile.Id, activeProfile!.Id);
        Assert.True(activeProfile.IsActive);

        var persistedActive = await client.GetFromJsonAsync<SavedBrokerServerProfile>("/api/preferences/brokers/active");
        Assert.NotNull(persistedActive);
        Assert.Equal(createdProfile.Id, persistedActive!.Id);
        Assert.NotEqual(seededActive!.Id, persistedActive.Id);
    }

    [Fact]
    public async Task TopicPresetPreferences_PostShouldCreateAndUpdatePresetByPattern()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var topicPattern = $"msh/US/2/e/{Guid.NewGuid():N}/#";

        var createResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/topic-presets",
            new SaveTopicPresetPreferenceRequest
            {
                Name = "Portable preset",
                TopicPattern = topicPattern,
                IsDefault = false
            });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdPreset = await createResponse.Content.ReadFromJsonAsync<SavedTopicPreset>();
        Assert.NotNull(createdPreset);
        Assert.Equal("Portable preset", createdPreset!.Name);
        Assert.Equal(topicPattern, createdPreset.TopicPattern);
        Assert.False(createdPreset.IsDefault);

        var updateResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/topic-presets",
            new SaveTopicPresetPreferenceRequest
            {
                Name = "Portable preset updated",
                TopicPattern = topicPattern,
                IsDefault = true
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedPreset = await updateResponse.Content.ReadFromJsonAsync<SavedTopicPreset>();
        Assert.NotNull(updatedPreset);
        Assert.Equal(createdPreset.Id, updatedPreset!.Id);
        Assert.Equal("Portable preset updated", updatedPreset.Name);
        Assert.True(updatedPreset.IsDefault);

        var presets = await client.GetFromJsonAsync<List<SavedTopicPreset>>("/api/preferences/topic-presets");
        Assert.NotNull(presets);

        var matchingPresets = presets!.Where(preset => preset.TopicPattern == topicPattern).ToList();
        var persistedPreset = Assert.Single(matchingPresets);

        Assert.Equal(updatedPreset.Id, persistedPreset.Id);
        Assert.Equal("Portable preset updated", persistedPreset.Name);
        Assert.True(persistedPreset.IsDefault);
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

    private static SaveBrokerPreferenceRequest CreateBrokerRequest(
        string name,
        string host,
        string? password,
        string defaultTopicPattern,
        string downlinkTopic,
        bool clearPassword = false,
        bool enableSend = false)
    {
        return new SaveBrokerPreferenceRequest
        {
            Name = name,
            Host = host,
            Port = 1883,
            UseTls = false,
            Username = "mesh-user",
            Password = password,
            ClearPassword = clearPassword,
            DefaultTopicPattern = defaultTopicPattern,
            DownlinkTopic = downlinkTopic,
            EnableSend = enableSend
        };
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

    private static async Task<HttpResponseMessage> PutJsonAsync(
        HttpClient client,
        ApiIntegrationTestHost host,
        string uri,
        object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.Add("X-CSRF-TOKEN", await host.GetAntiforgeryTokenAsync(client));
        request.Content = JsonContent.Create(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

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
