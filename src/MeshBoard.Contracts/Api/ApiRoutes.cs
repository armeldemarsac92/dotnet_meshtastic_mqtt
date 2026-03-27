namespace MeshBoard.Contracts.Api;

public static class ApiRoutes
{
    public static class Auth
    {
        public const string Group = "/api/auth";
        public const string Antiforgery = "/antiforgery";
        public const string Register = "/register";
        public const string Login = "/login";
        public const string Logout = "/logout";
        public const string Me = "/me";
    }

    public static class Preferences
    {
        public static class Brokers
        {
            public const string Group = "/api/preferences/brokers";
            public const string Root = "/";
            public const string ById = "/{id}";
            public const string Active = "/active";
            public const string Activate = "/{id}/activate";
        }

        public static class Favorites
        {
            public const string Group = "/api/preferences/favorites";
            public const string Root = "/";
            public const string ByNodeId = "/{nodeId}";
        }
    }

    public static class Realtime
    {
        public const string Group = "/api/realtime";
        public const string Jwks = "/.well-known/jwks.json";
        public const string Session = "/session";
    }

    public static class VernemqWebhook
    {
        public const string Group = "/internal/realtime/vernemq";
        public const string AuthOnRegisterM5 = "/auth-on-register-m5";
        public const string AuthOnSubscribeM5 = "/auth-on-subscribe-m5";
        public const string AuthOnPublishM5 = "/auth-on-publish-m5";
    }

    public static class PublicCollector
    {
        public const string Group = "/api/public/collector";
        public const string Servers = "/servers";
        public const string Channels = "/channels";
        public const string Snapshot = "/snapshot";
        public const string StatsChannelPackets = "/stats/channel-packets";
        public const string StatsNodePackets = "/stats/node-packets";
        public const string StatsNeighborLinks = "/stats/neighbor-links";
        public const string Overview = "/overview";
        public const string Nodes = "/nodes";
        public const string ChannelsPage = "/channels-page";
        public const string Topology = "/topology";
    }
}
