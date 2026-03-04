using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ITopicPresetService
{
    Task<IReadOnlyCollection<TopicPreset>> GetTopicPresets(CancellationToken cancellationToken = default);

    Task<TopicPreset> SaveTopicPreset(SaveTopicPresetRequest request, CancellationToken cancellationToken = default);
}

public sealed class TopicPresetService : ITopicPresetService
{
    private readonly ILogger<TopicPresetService> _logger;
    private readonly ITopicEncryptionKeyResolver? _topicEncryptionKeyResolver;
    private readonly ITopicPresetRepository _topicPresetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TopicPresetService(
        ITopicPresetRepository topicPresetRepository,
        IUnitOfWork unitOfWork,
        ITopicEncryptionKeyResolver topicEncryptionKeyResolver,
        ILogger<TopicPresetService> logger)
    {
        _topicPresetRepository = topicPresetRepository;
        _unitOfWork = unitOfWork;
        _topicEncryptionKeyResolver = topicEncryptionKeyResolver;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicPreset>> GetTopicPresets(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to get topic presets");

        var topicPresets = await _topicPresetRepository.GetAllAsync(cancellationToken);

        _logger.LogDebug("Retrieved {TopicPresetCount} topic presets", topicPresets.Count);

        return topicPresets;
    }

    public async Task<TopicPreset> SaveTopicPreset(
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        var topicPattern = request.TopicPattern.Trim();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(topicPattern))
        {
            throw new BadRequestException("A topic pattern is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("A preset name is required.");
        }

        var normalizedEncryptionKeyBase64 = NormalizeEncryptionKey(request.EncryptionKeyBase64);

        _logger.LogInformation("Attempting to save topic preset: {TopicPattern}", topicPattern);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var topicPreset = await _topicPresetRepository.UpsertAsync(
                new SaveTopicPresetRequest
                {
                    Name = name,
                    TopicPattern = topicPattern,
                    EncryptionKeyBase64 = normalizedEncryptionKeyBase64,
                    IsDefault = request.IsDefault
                },
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
            _topicEncryptionKeyResolver?.InvalidateCache();

            _logger.LogInformation("Saved topic preset: {TopicPattern}", topicPreset.TopicPattern);

            return topicPreset;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? NormalizeEncryptionKey(string? encryptionKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptionKeyBase64))
        {
            return null;
        }

        if (!TopicEncryptionKey.TryParse(encryptionKeyBase64, out var keyBytes))
        {
            throw new BadRequestException(
                "Encryption key must be a valid AES key in base64 or hexadecimal format (16, 24, or 32 bytes).");
        }

        return Convert.ToBase64String(keyBytes);
    }
}
