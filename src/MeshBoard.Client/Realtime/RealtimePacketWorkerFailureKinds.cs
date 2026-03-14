namespace MeshBoard.Client.Realtime;

public static class RealtimePacketWorkerFailureKinds
{
    public const string DecryptFailure = "DecryptFailure";
    public const string MalformedPayload = "MalformedPayload";
    public const string NoMatchingKey = "NoMatchingKey";
    public const string ProtobufParseFailure = "ProtobufParseFailure";
    public const string UnsupportedPortNum = "UnsupportedPortNum";
}
