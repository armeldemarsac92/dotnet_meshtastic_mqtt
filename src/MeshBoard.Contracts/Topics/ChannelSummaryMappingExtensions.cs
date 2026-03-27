namespace MeshBoard.Contracts.Topics;

public static class ChannelSummaryMappingExtensions
{
    public static ChannelSummary ToEmptyChannelSummary(this (string? Region, string? Channel) _)
    {
        return new ChannelSummary();
    }
}
