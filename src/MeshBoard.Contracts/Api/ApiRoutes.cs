namespace MeshBoard.Contracts.Api;

public static class ApiRoutes
{
    private const string ApiBase = "/api";
    private const string InternalBase = "/internal";

    public static class Auth
    {
        public const string GetAntiforgery = $"{ApiBase}/auth/antiforgery";
        public const string Register = $"{ApiBase}/auth/register";
        public const string Login = $"{ApiBase}/auth/login";
        public const string Logout = $"{ApiBase}/auth/logout";
        public const string GetCurrentUser = $"{ApiBase}/auth/me";
    }

    public static class Preferences
    {
        public static class Brokers
        {
            public const string GetAll = $"{ApiBase}/preferences/brokers";
            public const string GetActive = $"{ApiBase}/preferences/brokers/active";
            public const string Create = $"{ApiBase}/preferences/brokers";
            public const string Update = $"{ApiBase}/preferences/brokers/{{id}}";
            public const string Activate = $"{ApiBase}/preferences/brokers/{{id}}/activate";
        }

        public static class Favorites
        {
            public const string GetAll = $"{ApiBase}/preferences/favorites";
            public const string Save = $"{ApiBase}/preferences/favorites";
            public const string Remove = $"{ApiBase}/preferences/favorites/{{nodeId}}";
        }
    }

    public static class Realtime
    {
        public const string Jwks = "/.well-known/jwks.json";
        public const string CreateSession = $"{ApiBase}/realtime/session";
    }

    public static class VernemqWebhook
    {
        public const string AuthOnRegisterM5 = $"{InternalBase}/realtime/vernemq/auth-on-register-m5";
        public const string AuthOnSubscribeM5 = $"{InternalBase}/realtime/vernemq/auth-on-subscribe-m5";
        public const string AuthOnPublishM5 = $"{InternalBase}/realtime/vernemq/auth-on-publish-m5";
    }

    public static class PublicCollector
    {
        public const string GetServers = $"{ApiBase}/public/collector/servers";
        public const string GetChannels = $"{ApiBase}/public/collector/channels";
        public const string GetSnapshot = $"{ApiBase}/public/collector/snapshot";
        public const string GetChannelPackets = $"{ApiBase}/public/collector/stats/channel-packets";
        public const string GetNodePackets = $"{ApiBase}/public/collector/stats/node-packets";
        public const string GetNeighborLinks = $"{ApiBase}/public/collector/stats/neighbor-links";
        public const string GetOverview = $"{ApiBase}/public/collector/overview";
        public const string GetNodes = $"{ApiBase}/public/collector/nodes";
        public const string GetChannelsPage = $"{ApiBase}/public/collector/channels-page";
        public const string GetTopology = $"{ApiBase}/public/collector/topology";
    }
}
