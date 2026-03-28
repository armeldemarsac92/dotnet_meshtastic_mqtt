using MeshBoard.Application.Topics;

namespace MeshBoard.UnitTests;

public sealed class TopicExplorerServiceTests
{
    [Fact]
    public void GetRecommendedTopics_ShouldIncludeCommonChannelPatterns()
    {
        var service = new TopicExplorerService();

        var topics = service.GetRecommendedTopics();

        Assert.Contains(topics, entry => entry.TopicPattern == "msh/US/2/e/LongFast/#");
        Assert.Contains(topics, entry => entry.TopicPattern == "msh/EU_868/2/e/MediumSlow/#");
        Assert.Contains(topics, entry => entry.TopicPattern == "msh/EU_868/2/e/MediumFast/#");
    }

    [Fact]
    public void GetDiscoveredTopics_ShouldExtractChannelPatterns_AndDeduplicate()
    {
        var service = new TopicExplorerService();

        var topics = service.GetDiscoveredTopics(
        [
            "msh/US/2/e/LongFast/!abc12345",
            "msh/US/2/e/LongFast/!def67890",
            "msh/US/2/json/LongFast/!fedcba09",
            "msh/EU_433/2/e/LongSlow/!00112233",
            "msh/EU_433/2/json/LongSlow/!99887766",
            "msh/US/2/e/#",
            "invalid/topic/value"
        ]);

        Assert.Collection(
            topics,
            entry =>
            {
                Assert.Equal("EU_433 · LongSlow", entry.Label);
                Assert.Equal("msh/EU_433/2/e/LongSlow/#", entry.TopicPattern);
            },
            entry =>
            {
                Assert.Equal("US · LongFast", entry.Label);
                Assert.Equal("msh/US/2/e/LongFast/#", entry.TopicPattern);
            });
    }

    [Fact]
    public void CreatePresetName_ShouldDifferentiateRecommendedAndObserved()
    {
        var service = new TopicExplorerService();

        var recommendedName = service.CreatePresetName(
            new MeshBoard.Contracts.Topics.TopicCatalogEntry
            {
                Label = "US · LongFast",
                Region = "US",
                Channel = "LongFast",
                IsRecommended = true
            });
        var observedName = service.CreatePresetName(
            new MeshBoard.Contracts.Topics.TopicCatalogEntry
            {
                Label = "US · LongFast",
                Region = "US",
                Channel = "LongFast",
                IsRecommended = false
            });

        Assert.Equal("US · LongFast", recommendedName);
        Assert.Equal("Observed US LongFast", observedName);
    }
}
