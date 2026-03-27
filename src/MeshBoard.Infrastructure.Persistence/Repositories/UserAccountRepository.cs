using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Authentication;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<UserAccountRepository> _logger;

    public UserAccountRepository(IDbContext dbContext, ILogger<UserAccountRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserAccountRecord?> GetByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch user by id {UserId}", userId);

        var response = await _dbContext.QueryFirstOrDefaultAsync<UserAccountSqlResponse>(
            UserAccountQueries.GetById,
            new { Id = userId },
            cancellationToken);

        return response?.MapToUserAccountRecord();
    }

    public async Task<UserAccountRecord?> GetByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch user by normalized username {NormalizedUsername}", normalizedUsername);

        var response = await _dbContext.QueryFirstOrDefaultAsync<UserAccountSqlResponse>(
            UserAccountQueries.GetByNormalizedUsername,
            new { NormalizedUsername = normalizedUsername },
            cancellationToken);

        return response?.MapToUserAccountRecord();
    }

    public async Task<AppUser> InsertAsync(
        CreateUserAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to insert user {Username}", request.Username);

        try
        {
            await _dbContext.ExecuteAsync(
                UserAccountQueries.Insert,
                new
                {
                    request.Id,
                    request.Username,
                    request.NormalizedUsername,
                    request.PasswordHash,
                    request.CreatedAtUtc
                },
                cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException($"Username '{request.Username}' is already taken.");
        }

        return request.ToAppUser();
    }
}
