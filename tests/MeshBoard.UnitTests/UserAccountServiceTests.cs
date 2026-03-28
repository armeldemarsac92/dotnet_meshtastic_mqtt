using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Authentication;
using MeshBoard.Application.Workspaces;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class UserAccountServiceTests
{
    [Fact]
    public async Task RegisterAsync_ShouldCreateUserAndProvisionWorkspace()
    {
        var repository = new FakeUserAccountRepository();
        var provisioningService = new FakeWorkspaceProvisioningService();
        var unitOfWork = new FakeUnitOfWork();
        var service = new UserAccountService(
            repository,
            new FakePasswordHashingService(),
            provisioningService,
            unitOfWork,
            NullLogger<UserAccountService>.Instance);

        var user = await service.RegisterAsync(
            new RegisterUserRequest
            {
                Username = "alpha.user",
                Password = "secret-pass"
            });

        Assert.Equal("alpha.user", user.Username);
        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
        Assert.Equal(user.Id, provisioningService.ProvisionedWorkspaceId);
        Assert.NotNull(await service.ValidateCredentialsAsync("alpha.user", "secret-pass"));
    }

    [Fact]
    public async Task RegisterAsync_ShouldRejectDuplicateUsernames_IgnoringCase()
    {
        var repository = new FakeUserAccountRepository();
        var service = new UserAccountService(
            repository,
            new FakePasswordHashingService(),
            new FakeWorkspaceProvisioningService(),
            new FakeUnitOfWork(),
            NullLogger<UserAccountService>.Instance);

        await service.RegisterAsync(
            new RegisterUserRequest
            {
                Username = "alpha.user",
                Password = "secret-pass"
            });

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.RegisterAsync(
                new RegisterUserRequest
                {
                    Username = "ALPHA.USER",
                    Password = "secret-pass"
                }));

        Assert.Contains("already taken", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ShouldReturnNull_WhenPasswordDoesNotMatch()
    {
        var repository = new FakeUserAccountRepository();
        var service = new UserAccountService(
            repository,
            new FakePasswordHashingService(),
            new FakeWorkspaceProvisioningService(),
            new FakeUnitOfWork(),
            NullLogger<UserAccountService>.Instance);

        await service.RegisterAsync(
            new RegisterUserRequest
            {
                Username = "alpha.user",
                Password = "secret-pass"
            });

        var user = await service.ValidateCredentialsAsync("alpha.user", "wrong-pass");

        Assert.Null(user);
    }

    private sealed class FakeUserAccountRepository : IUserAccountRepository
    {
        private readonly Dictionary<string, UserAccountRecord> _usersById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UserAccountRecord> _usersByNormalizedUsername = new(StringComparer.Ordinal);

        public Task<UserAccountRecord?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            _usersById.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<UserAccountRecord?> GetByNormalizedUsernameAsync(
            string normalizedUsername,
            CancellationToken cancellationToken = default)
        {
            _usersByNormalizedUsername.TryGetValue(normalizedUsername, out var user);
            return Task.FromResult(user);
        }

        public Task<AppUser> InsertAsync(CreateUserAccountRequest request, CancellationToken cancellationToken = default)
        {
            var record = new UserAccountRecord
            {
                Id = request.Id,
                Username = request.Username,
                NormalizedUsername = request.NormalizedUsername,
                PasswordHash = request.PasswordHash,
                CreatedAtUtc = request.CreatedAtUtc
            };

            _usersById[record.Id] = record;
            _usersByNormalizedUsername[record.NormalizedUsername] = record;

            return Task.FromResult(record.ToAppUser());
        }
    }

    private sealed class FakeWorkspaceProvisioningService : IWorkspaceProvisioningService
    {
        public string? ProvisionedWorkspaceId { get; private set; }

        public Task ProvisionAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ProvisionedWorkspaceId = workspaceId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHashingService : IPasswordHashingService
    {
        public string HashPassword(string password)
        {
            return $"hash::{password}";
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return string.Equals(passwordHash, $"hash::{password}", StringComparison.Ordinal);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }
}
