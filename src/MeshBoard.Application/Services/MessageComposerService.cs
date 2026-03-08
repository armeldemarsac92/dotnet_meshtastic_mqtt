using System.Text.Json;
using System.Text.RegularExpressions;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Workspaces;
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
    private readonly BrokerOptions _fallbackBrokerOptions;
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly IWorkspaceBrokerSessionManager _brokerSessionManager;
    private readonly ILogger<MessageComposerService> _logger;
    private readonly ISendCapabilityService _sendCapabilityService;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public MessageComposerService(
        IWorkspaceBrokerSessionManager brokerSessionManager,
        ISendCapabilityService sendCapabilityService,
        IBrokerServerProfileService brokerServerProfileService,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<MessageComposerService> logger)
    {
        _brokerSessionManager = brokerSessionManager;
        _sendCapabilityService = sendCapabilityService;
        _brokerServerProfileService = brokerServerProfileService;
        _workspaceContextAccessor = workspaceContextAccessor;
        _fallbackBrokerOptions = brokerOptions.Value;
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
        var capability = await _sendCapabilityService.GetStatus(cancellationToken);

        if (!capability.IsEnabled)
        {
            throw new BadRequestException($"Send is currently blocked: {string.Join(" ", capability.BlockingReasons)}");
        }

        _logger.LogInformation(
            "Attempting to publish compose request. Private: {IsPrivate}; ToNodeId: {ToNodeId}",
            toNodeId is not null,
            toNodeId);

        var activeServer = await ResolveActiveServerProfile(cancellationToken);
        var downlinkTopic = activeServer?.DownlinkTopic ?? _fallbackBrokerOptions.DownlinkTopic;

        var payload = toNodeId is null
            ? JsonSerializer.Serialize(new { type = "sendtext", payload = messageText })
            : JsonSerializer.Serialize(new { type = "sendtext", payload = messageText, to = toNodeId });

        await _brokerSessionManager.PublishAsync(
            _workspaceContextAccessor.GetWorkspaceId(),
            downlinkTopic,
            payload,
            cancellationToken);

        return new ComposeTextMessageResult
        {
            Topic = downlinkTopic,
            IsPrivate = toNodeId is not null,
            ToNodeId = toNodeId,
            SentAtUtc = DateTimeOffset.UtcNow,
            StatusMessage = toNodeId is null
                ? "Published public send request."
                : $"Published private send request to {toNodeId}."
        };
    }

    private async Task<BrokerServerProfile?> ResolveActiveServerProfile(CancellationToken cancellationToken)
    {
        try
        {
            return await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        }
        catch (NotFoundException)
        {
            return null;
        }
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
