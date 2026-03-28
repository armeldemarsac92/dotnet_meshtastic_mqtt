using System.Text.RegularExpressions;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Authentication;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Exceptions;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface IUserAccountService
{
    Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<AppUser> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    Task<AppUser?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed partial class UserAccountService : IUserAccountService
{
    private const int MaximumUsernameLength = 32;
    private const int MinimumPasswordLength = 8;

    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ILogger<UserAccountService> _logger;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkspaceProvisioningService _workspaceProvisioningService;

    public UserAccountService(
        IUserAccountRepository userAccountRepository,
        IPasswordHashingService passwordHashingService,
        IWorkspaceProvisioningService workspaceProvisioningService,
        IUnitOfWork unitOfWork,
        ILogger<UserAccountService> logger)
    {
        _userAccountRepository = userAccountRepository;
        _passwordHashingService = passwordHashingService;
        _workspaceProvisioningService = workspaceProvisioningService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var record = await _userAccountRepository.GetByIdAsync(userId.Trim(), cancellationToken);
        return record?.ToAppUser();
    }

    public async Task<AppUser> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var username = NormalizeUsernameForStorage(request.Username);
        var normalizedUsername = NormalizeUsernameForLookup(username);
        ValidatePassword(request.Password);

        var existingUser = await _userAccountRepository.GetByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (existingUser is not null)
        {
            throw new ConflictException($"Username '{username}' is already taken.");
        }

        var createRequest = request.ToCreateUserAccountRequest(
            username,
            normalizedUsername,
            _passwordHashingService.HashPassword(request.Password));

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _userAccountRepository.InsertAsync(createRequest, cancellationToken);
            await _workspaceProvisioningService.ProvisionAsync(user.Id, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Registered user {Username} with workspace {WorkspaceId}", user.Username, user.Id);

            return user;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AppUser?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var record = await _userAccountRepository.GetByNormalizedUsernameAsync(
            NormalizeUsernameForLookup(username),
            cancellationToken);

        if (record is null)
        {
            return null;
        }

        return _passwordHashingService.VerifyPassword(password, record.PasswordHash)
            ? record.ToAppUser()
            : null;
    }

    private static string NormalizeUsernameForStorage(string username)
    {
        var trimmedUsername = username?.Trim() ?? string.Empty;

        if (trimmedUsername.Length is < 3 or > MaximumUsernameLength)
        {
            throw new BadRequestException($"Username must be between 3 and {MaximumUsernameLength} characters.");
        }

        if (!UsernamePattern().IsMatch(trimmedUsername))
        {
            throw new BadRequestException("Username may contain letters, numbers, '.', '-', and '_', and must start and end with a letter or number.");
        }

        return trimmedUsername;
    }

    private static string NormalizeUsernameForLookup(string username)
    {
        return username.Trim().ToUpperInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
        {
            throw new BadRequestException($"Password must be at least {MinimumPasswordLength} characters long.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9._-]{1,30}[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
