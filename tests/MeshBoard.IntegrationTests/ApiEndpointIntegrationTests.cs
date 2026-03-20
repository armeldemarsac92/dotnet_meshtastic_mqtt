using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Contracts.Realtime;
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
    public async Task RealtimeSession_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var response = await PostJsonAsync(client, host, "/api/realtime/session", payload: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RealtimeSession_WhenAuthenticated_ShouldReturnConfiguredBrokerPayloadAndJwtClaims()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var user = await RegisterAsync(client, host);
        var topicFilter = RealtimeTopicNames.BuildWorkspaceLiveWildcard(user.WorkspaceId);

        var sessionResponse = await PostJsonAsync(client, host, "/api/realtime/session", payload: null);

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);

        var sessionPayload = await sessionResponse.Content.ReadFromJsonAsync<RealtimeSessionPayload>();
        Assert.NotNull(sessionPayload);
        Assert.Equal(ApiIntegrationTestHost.RealtimeBrokerUrl, sessionPayload!.BrokerUrl);
        Assert.False(string.IsNullOrWhiteSpace(sessionPayload.ClientId));
        Assert.False(string.IsNullOrWhiteSpace(sessionPayload.Token));
        Assert.NotEqual(default, sessionPayload.ExpiresAtUtc);
        Assert.True(sessionPayload.ExpiresAtUtc > DateTimeOffset.UtcNow);

        var allowedTopicPatterns = Assert.Single(sessionPayload.AllowedTopicPatterns);
        Assert.Equal(topicFilter, allowedTopicPatterns);

        var (header, payload) = DecodeJwt(sessionPayload.Token);

        Assert.Equal("JWT", header.GetProperty("typ").GetString());
        Assert.Equal(ApiIntegrationTestHost.RealtimeKeyId, header.GetProperty("kid").GetString());

        Assert.Equal(ApiIntegrationTestHost.RealtimeIssuer, payload.GetProperty("iss").GetString());
        Assert.Equal(ApiIntegrationTestHost.RealtimeAudience, GetSingleOrFirstString(payload, "aud"));
        Assert.Equal(user.Id, payload.GetProperty("user_id").GetString());
        Assert.Equal(user.WorkspaceId, payload.GetProperty("workspace_id").GetString());
        Assert.Equal(sessionPayload.ClientId, payload.GetProperty("client_id").GetString());
        Assert.Equal(topicFilter, Assert.Single(GetStringArray(payload, "allowed_topic_patterns")));
        Assert.Equal(topicFilter, Assert.Single(GetStringArray(payload.GetProperty("acl"), "subscribe")));
        Assert.Empty(GetStringArray(payload.GetProperty("acl"), "publish"));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("jti").GetString()));

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(payload.GetProperty("iat").GetInt64());
        var notBefore = DateTimeOffset.FromUnixTimeSeconds(payload.GetProperty("nbf").GetInt64());
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.GetProperty("exp").GetInt64());

        Assert.True(notBefore >= issuedAt);
        Assert.True(expiresAt > issuedAt);
        Assert.Equal(
            expiresAt.ToUnixTimeSeconds(),
            sessionPayload.ExpiresAtUtc.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task RealtimeSession_WhenRequestOriginBrokerModeEnabled_ShouldUseRequestOriginHost()
    {
        await using var host = new ApiIntegrationTestHost(useRequestOriginBrokerUrl: true);
        using var client = host.CreateApiClient(new Uri("https://meshboard-public.example.com"));

        await RegisterAsync(client, host);

        var sessionResponse = await PostJsonAsync(client, host, "/api/realtime/session", payload: null);

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);

        var sessionPayload = await sessionResponse.Content.ReadFromJsonAsync<RealtimeSessionPayload>();
        Assert.NotNull(sessionPayload);
        Assert.Equal("wss://meshboard-public.example.com/mqtt", sessionPayload!.BrokerUrl);
    }

    [Fact]
    public async Task RealtimeJwks_ShouldExposeVerificationKeyThatValidatesIssuedBrokerTokens()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var sessionResponse = await PostJsonAsync(client, host, "/api/realtime/session", payload: null);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);

        var sessionPayload = await sessionResponse.Content.ReadFromJsonAsync<RealtimeSessionPayload>();
        Assert.NotNull(sessionPayload);

        var jwksResponse = await client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);
        Assert.Contains("public", jwksResponse.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=300", jwksResponse.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);

        var jwks = await jwksResponse.Content.ReadFromJsonAsync<JsonWebKeyDocument>();
        Assert.NotNull(jwks);

        var key = Assert.Single(jwks!.Keys);
        Assert.Equal("RSA", key.Kty);
        Assert.Equal("sig", key.Use);
        Assert.Equal("RS256", key.Alg);
        Assert.Equal(ApiIntegrationTestHost.RealtimeKeyId, key.Kid);
        Assert.False(string.IsNullOrWhiteSpace(key.N));
        Assert.False(string.IsNullOrWhiteSpace(key.E));

        var (header, _) = DecodeJwt(sessionPayload!.Token);
        Assert.Equal(key.Kid, header.GetProperty("kid").GetString());
        Assert.True(VerifyJwtSignature(sessionPayload.Token, key));
    }

    [Fact]
    public async Task VernemqAuthOnRegisterM5_WhenSessionTokenMatchesClientId_ShouldReturnOk()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);
        var session = await CreateRealtimeSessionAsync(client, host);

        using var response = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-register-m5",
            new
            {
                client_id = session.ClientId,
                clean_start = true,
                password = session.ClientId,
                username = session.Token
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", payload.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public async Task VernemqAuthOnRegisterM5_ShouldAllowConfiguredInternalBridgePublisher()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        using var response = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-register-m5",
            new
            {
                client_id = ApiIntegrationTestHost.RealtimeDownstreamBridgeClientId,
                clean_start = true,
                password = ApiIntegrationTestHost.RealtimeDownstreamBridgePassword,
                username = ApiIntegrationTestHost.RealtimeDownstreamBridgeUsername
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", payload.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public async Task VernemqAuthOnSubscribeM5_WhenTopicEscapesWorkspace_ShouldRejectThatSubscription()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var user = await RegisterAsync(client, host);
        var session = await CreateRealtimeSessionAsync(client, host);

        using var response = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-subscribe-m5",
            new
            {
                client_id = session.ClientId,
                username = session.Token,
                topics = new object[]
                {
                    new
                    {
                        qos = 1,
                        topic = RealtimeTopicNames.BuildWorkspacePacketTopic(user.WorkspaceId)
                    },
                    new
                    {
                        qos = 1,
                        topic = RealtimeTopicNames.BuildWorkspacePacketTopic("workspace-b")
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", payload.RootElement.GetProperty("result").GetString());

        var topics = payload.RootElement.GetProperty("modifiers").GetProperty("topics").EnumerateArray().ToArray();
        Assert.Equal(2, topics.Length);
        Assert.Equal(1, topics[0].GetProperty("qos").GetInt32());
        Assert.Equal(135, topics[1].GetProperty("qos").GetInt32());
    }

    [Fact]
    public async Task VernemqAuthOnPublishM5_WhenClientHasNoPublishAcl_ShouldReturnNotAllowed()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var user = await RegisterAsync(client, host);
        var session = await CreateRealtimeSessionAsync(client, host);

        using var response = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-publish-m5",
            new
            {
                client_id = session.ClientId,
                topic = RealtimeTopicNames.BuildWorkspacePacketTopic(user.WorkspaceId),
                username = session.Token
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_allowed", payload.RootElement.GetProperty("result").GetProperty("error").GetString());
    }

    [Fact]
    public async Task VernemqAuthOnPublishM5_ShouldAllowConfiguredInternalBridgePublisher_ForPacketTopicsOnly()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        using var allowedResponse = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-publish-m5",
            new
            {
                client_id = ApiIntegrationTestHost.RealtimeDownstreamBridgeClientId,
                topic = "meshboard/workspaces/test-workspace/live/packets",
                username = ApiIntegrationTestHost.RealtimeDownstreamBridgeUsername
            });

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);

        using var allowedPayload = JsonDocument.Parse(await allowedResponse.Content.ReadAsStringAsync());
        Assert.Equal("ok", allowedPayload.RootElement.GetProperty("result").GetString());

        using var deniedResponse = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-publish-m5",
            new
            {
                client_id = ApiIntegrationTestHost.RealtimeDownstreamBridgeClientId,
                topic = "meshboard/workspaces/test-workspace/live/other",
                username = ApiIntegrationTestHost.RealtimeDownstreamBridgeUsername
            });

        Assert.Equal(HttpStatusCode.OK, deniedResponse.StatusCode);

        using var deniedPayload = JsonDocument.Parse(await deniedResponse.Content.ReadAsStringAsync());
        Assert.Equal("not_allowed", deniedPayload.RootElement.GetProperty("result").GetProperty("error").GetString());
    }

    [Fact]
    public async Task VernemqAuthOnRegisterM5_WhenSessionTokenIsExpired_ShouldReturnNotAllowed()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var user = await RegisterAsync(client, host);
        var expiredToken = CreateRealtimeBrokerToken(
            user.Id,
            user.WorkspaceId,
            "meshboard-expired-client",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            [RealtimeTopicNames.BuildWorkspaceLiveWildcard(user.WorkspaceId)]);

        using var response = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-register-m5",
            new
            {
                client_id = "meshboard-expired-client",
                clean_start = true,
                password = "meshboard-expired-client",
                username = expiredToken
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_allowed", payload.RootElement.GetProperty("result").GetProperty("error").GetString());
    }

    [Fact]
    public async Task VernemqAuthOnSubscribeM5_WhenClientIdDoesNotMatchToken_ShouldRejectRequestedTopics()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        var user = await RegisterAsync(client, host);
        var mismatchedToken = CreateRealtimeBrokerToken(
            user.Id,
            user.WorkspaceId,
            "meshboard-token-client",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5),
            [RealtimeTopicNames.BuildWorkspaceLiveWildcard(user.WorkspaceId)]);

        using var response = await client.PostAsJsonAsync(
            "/internal/realtime/vernemq/auth-on-subscribe-m5",
            new
            {
                client_id = "meshboard-request-client",
                username = mismatchedToken,
                topics = new object[]
                {
                    new
                    {
                        qos = 1,
                        topic = RealtimeTopicNames.BuildWorkspacePacketTopic(user.WorkspaceId)
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var topic = Assert.Single(payload.RootElement.GetProperty("modifiers").GetProperty("topics").EnumerateArray().ToArray());
        Assert.Equal(135, topic.GetProperty("qos").GetInt32());
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
        Assert.All(
            presets,
            preset =>
            {
                Assert.NotEqual(Guid.Empty, preset.ServerProfileId);
                Assert.False(string.IsNullOrWhiteSpace(preset.ServerProfileName));
                Assert.False(string.IsNullOrWhiteSpace(preset.ServerAddress));
            });
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
        var activeProfile = await client.GetFromJsonAsync<SavedBrokerServerProfile>("/api/preferences/brokers/active");
        Assert.NotNull(activeProfile);

        var topicPattern = $"msh/US/2/e/{Guid.NewGuid():N}/#";

        var createResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/topic-presets",
            new SaveTopicPresetPreferenceRequest
            {
                ServerProfileId = activeProfile!.Id,
                Name = "Portable preset",
                TopicPattern = topicPattern,
                IsDefault = false
            });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdPreset = await createResponse.Content.ReadFromJsonAsync<SavedTopicPreset>();
        Assert.NotNull(createdPreset);
        Assert.Equal("Portable preset", createdPreset!.Name);
        Assert.Equal(activeProfile.Id, createdPreset.ServerProfileId);
        Assert.Equal(activeProfile.Name, createdPreset.ServerProfileName);
        Assert.Equal(activeProfile.ServerAddress, createdPreset.ServerAddress);
        Assert.Equal(topicPattern, createdPreset.TopicPattern);
        Assert.False(createdPreset.IsDefault);

        var updateResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/topic-presets",
            new SaveTopicPresetPreferenceRequest
            {
                ServerProfileId = activeProfile.Id,
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
        Assert.Equal(activeProfile.Id, persistedPreset.ServerProfileId);
        Assert.Equal("Portable preset updated", persistedPreset.Name);
        Assert.True(persistedPreset.IsDefault);
    }

    [Fact]
    public async Task TopicPresetPreferences_ShouldAllowSamePatternAcrossServersWithSameAddress()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);
        var activeProfile = await client.GetFromJsonAsync<SavedBrokerServerProfile>("/api/preferences/brokers/active");
        Assert.NotNull(activeProfile);

        var createServerResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/brokers",
            CreateBrokerRequest(
                name: "Duplicate address profile",
                host: activeProfile!.Host,
                password: null,
                defaultTopicPattern: activeProfile.DefaultTopicPattern,
                downlinkTopic: activeProfile.DownlinkTopic));

        Assert.Equal(HttpStatusCode.Created, createServerResponse.StatusCode);

        var secondProfile = await createServerResponse.Content.ReadFromJsonAsync<SavedBrokerServerProfile>();
        Assert.NotNull(secondProfile);
        Assert.NotEqual(activeProfile.Id, secondProfile!.Id);
        Assert.Equal(activeProfile.ServerAddress, secondProfile.ServerAddress);

        var sharedTopicPattern = $"msh/US/2/e/{Guid.NewGuid():N}/#";

        var firstPresetResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/topic-presets",
            new SaveTopicPresetPreferenceRequest
            {
                ServerProfileId = activeProfile.Id,
                Name = "Primary same-address preset",
                TopicPattern = sharedTopicPattern,
                IsDefault = false
            });

        Assert.Equal(HttpStatusCode.OK, firstPresetResponse.StatusCode);

        var secondPresetResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/topic-presets",
            new SaveTopicPresetPreferenceRequest
            {
                ServerProfileId = secondProfile.Id,
                Name = "Secondary same-address preset",
                TopicPattern = sharedTopicPattern,
                IsDefault = false
            });

        Assert.Equal(HttpStatusCode.OK, secondPresetResponse.StatusCode);

        var presets = await client.GetFromJsonAsync<List<SavedTopicPreset>>("/api/preferences/topic-presets");
        Assert.NotNull(presets);

        var matchingPresets = presets!
            .Where(preset => preset.TopicPattern == sharedTopicPattern)
            .OrderBy(preset => preset.ServerProfileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(2, matchingPresets.Count);
        Assert.Contains(matchingPresets, preset => preset.ServerProfileId == activeProfile.Id && preset.Name == "Primary same-address preset");
        Assert.Contains(matchingPresets, preset => preset.ServerProfileId == secondProfile.Id && preset.Name == "Secondary same-address preset");
    }

    [Fact]
    public async Task ChannelPreferences_ShouldRoundTripSavedChannelFilters()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();

        await RegisterAsync(client, host);

        var topicFilter = $"msh/US/2/e/{Guid.NewGuid():N}/#";

        var createResponse = await PostJsonAsync(
            client,
            host,
            "/api/preferences/channels",
            new SaveChannelFilterRequest
            {
                TopicFilter = topicFilter,
                Label = "Portable feed"
            });

        Assert.True(
            createResponse.IsSuccessStatusCode,
            $"Expected channel create to succeed but received {(int)createResponse.StatusCode} ({createResponse.StatusCode}).");

        var getResponse = await client.GetAsync("/api/preferences/channels");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var savedChannels = await getResponse.Content.ReadFromJsonAsync<List<SavedChannelFilter>>();
        Assert.NotNull(savedChannels);

        var persistedChannel = Assert.Single(savedChannels!, channel => channel.TopicFilter == topicFilter);
        Assert.NotEqual(Guid.Empty, persistedChannel.Id);
        Assert.NotEqual(Guid.Empty, persistedChannel.BrokerServerProfileId);
        Assert.Equal("Portable feed", persistedChannel.Label);
        Assert.True(persistedChannel.CreatedAtUtc > DateTimeOffset.MinValue);
        Assert.True(persistedChannel.UpdatedAtUtc > DateTimeOffset.MinValue);

        var encodedTopicFilter = string.Join(
            '/',
            topicFilter
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var deleteResponse = await DeleteAsync(
            client,
            host,
            $"/api/preferences/channels/{encodedTopicFilter}");

        Assert.True(
            deleteResponse.IsSuccessStatusCode,
            $"Expected channel delete to succeed but received {(int)deleteResponse.StatusCode} ({deleteResponse.StatusCode}).");

        var getAfterDeleteResponse = await client.GetAsync("/api/preferences/channels");
        Assert.Equal(HttpStatusCode.OK, getAfterDeleteResponse.StatusCode);

        var channelsAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<List<SavedChannelFilter>>();
        Assert.NotNull(channelsAfterDelete);
        Assert.DoesNotContain(channelsAfterDelete!, channel => channel.TopicFilter == topicFilter);
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

    private static async Task<RealtimeSessionPayload> CreateRealtimeSessionAsync(HttpClient client, ApiIntegrationTestHost host)
    {
        var response = await PostJsonAsync(client, host, "/api/realtime/session", payload: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<RealtimeSessionPayload>()
               ?? throw new InvalidOperationException("The API returned an empty realtime session payload.");
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

    private static (JsonElement Header, JsonElement Payload) DecodeJwt(string token)
    {
        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);

        using var headerDocument = JsonDocument.Parse(DecodeBase64Url(segments[0]));
        using var payloadDocument = JsonDocument.Parse(DecodeBase64Url(segments[1]));

        return (headerDocument.RootElement.Clone(), payloadDocument.RootElement.Clone());
    }

    private static bool VerifyJwtSignature(string token, JsonWebKey key)
    {
        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);

        using var rsa = RSA.Create();
        rsa.ImportParameters(
            new RSAParameters
            {
                Modulus = DecodeBase64UrlToBytes(key.N),
                Exponent = DecodeBase64UrlToBytes(key.E)
            });

        return rsa.VerifyData(
            Encoding.UTF8.GetBytes($"{segments[0]}.{segments[1]}"),
            DecodeBase64UrlToBytes(segments[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private static string DecodeBase64Url(string value)
    {
        return Encoding.UTF8.GetString(DecodeBase64UrlToBytes(value));
    }

    private static string CreateRealtimeBrokerToken(
        string userId,
        string workspaceId,
        string clientId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        IReadOnlyList<string> subscribeTopicPatterns)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "RS256",
            ["kid"] = ApiIntegrationTestHost.RealtimeKeyId,
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = ApiIntegrationTestHost.RealtimeIssuer,
            ["aud"] = ApiIntegrationTestHost.RealtimeAudience,
            ["sub"] = userId,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = issuedAtUtc.ToUnixTimeSeconds(),
            ["nbf"] = issuedAtUtc.ToUnixTimeSeconds(),
            ["exp"] = expiresAtUtc.ToUnixTimeSeconds(),
            ["workspace_id"] = workspaceId,
            ["user_id"] = userId,
            ["client_id"] = clientId,
            ["allowed_topic_patterns"] = subscribeTopicPatterns,
            ["acl"] = new Dictionary<string, object?>
            {
                ["subscribe"] = subscribeTopicPatterns,
                ["publish"] = Array.Empty<string>()
            }
        };

        var encodedHeader = EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(ApiIntegrationTestHost.RealtimeSigningPrivateKeyPem.AsSpan());

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{EncodeBase64Url(signature)}";
    }

    private static byte[] DecodeBase64UrlToBytes(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');

        return Convert.FromBase64String(normalized);
    }

    private static string EncodeBase64Url(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GetSingleOrFirstString(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Array => property.EnumerateArray().Select(item => item.GetString()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
            _ => string.Empty
        };
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);

        return property.ValueKind switch
        {
            JsonValueKind.String => [property.GetString() ?? string.Empty],
            JsonValueKind.Array => property.EnumerateArray()
                .Select(item => item.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToList(),
            _ => []
        };
    }

    private sealed record RealtimeSessionPayload
    {
        public string BrokerUrl { get; init; } = string.Empty;

        public string ClientId { get; init; } = string.Empty;

        public string Token { get; init; } = string.Empty;

        public DateTimeOffset ExpiresAtUtc { get; init; }

        public List<string> AllowedTopicPatterns { get; init; } = [];
    }
}
