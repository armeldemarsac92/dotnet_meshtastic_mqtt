using System.Text.Json;
using System.Text.RegularExpressions;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IMessageComposerService
{
    Task<ComposeTextMessageResult> SendTextMessage(
        ComposeTextMessageRequest request,
        CancellationToken cancellationToken = default);
}

public sealed partial class MessageComposerService : IMessageComposerService
{
    private readonly BrokerOptions _brokerOptions;
    private readonly ILogger<MessageComposerService> _logger;
    private readonly IMqttSession _mqttSession;
    private readonly ISendCapabilityService _sendCapabilityService;

    public MessageComposerService(
        IMqttSession mqttSession,
        ISendCapabilityService sendCapabilityService,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<MessageComposerService> logger)
    {
        _mqttSession = mqttSession;
        _sendCapabilityService = sendCapabilityService;
        _brokerOptions = brokerOptions.Value;
        _logger = logger;
    }

    public async Task<ComposeTextMessageResult> SendTextMessage(
        ComposeTextMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("A compose request is required.");
        }

        var messageText = request.Text?.Trim();

        if (string.IsNullOrWhiteSpace(messageText))
        {
            throw new BadRequestException("Message text is required.");
        }

        if (messageText.Length > 280)
        {
            throw new BadRequestException("Message text must be 280 characters or less.");
        }

        var toNodeId = NormalizeNodeId(request.ToNodeId);
        var capability = _sendCapabilityService.GetStatus();

        if (!capability.IsEnabled)
        {
            throw new BadRequestException($"Send is currently blocked: {string.Join(" ", capability.BlockingReasons)}");
        }

        _logger.LogInformation(
            "Attempting to publish compose request. Private: {IsPrivate}; ToNodeId: {ToNodeId}",
            toNodeId is not null,
            toNodeId);

        var payload = toNodeId is null
            ? JsonSerializer.Serialize(new { type = "sendtext", payload = messageText })
            : JsonSerializer.Serialize(new { type = "sendtext", payload = messageText, to = toNodeId });

        await _mqttSession.PublishAsync(_brokerOptions.DownlinkTopic, payload, cancellationToken);

        return new ComposeTextMessageResult
        {
            Topic = _brokerOptions.DownlinkTopic,
            IsPrivate = toNodeId is not null,
            ToNodeId = toNodeId,
            SentAtUtc = DateTimeOffset.UtcNow,
            StatusMessage = toNodeId is null
                ? "Published public send request."
                : $"Published private send request to {toNodeId}."
        };
    }

    private static string? NormalizeNodeId(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        var normalizedNodeId = nodeId.Trim();

        if (!NodeIdRegex().IsMatch(normalizedNodeId))
        {
            throw new BadRequestException("Private destination node ID must use format !xxxxxxxx.");
        }

        return normalizedNodeId.ToLowerInvariant();
    }

    [GeneratedRegex("^![0-9a-fA-F]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex NodeIdRegex();
}
