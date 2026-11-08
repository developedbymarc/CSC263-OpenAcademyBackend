using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Entities.Tokens;
using OpenAcademy.Data;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace OpenAcademy.Services.ApplicationServices.TokenService;

public sealed class RefreshTokenService : AbstractRefreshTokenService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        ApplicationDbContext dbContext,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        ILogger<RefreshTokenService> logger) : base(refreshTokenOptions)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override string GeneratePlain()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes);
    }

    public override string Hash(string plainToken)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
            _refreshTokenOptions.SigningKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToBase64String(hash);
    }

    public override async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > now)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(rt => rt.RevokedAt, now), ct);
    }

    // UserId is part of the filter so one user can never revoke another user's family.
    public override async Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.FamilyId == familyId && !rt.IsRevoked)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(rt => rt.RevokedAt, now), ct);
    }

    public override async Task RotateTokenAsync(RefreshToken oldToken, RefreshToken newToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                oldToken.RevokedAt = DateTimeOffset.UtcNow.UtcDateTime;

                _dbContext.RefreshTokens.Update(oldToken);
                _dbContext.RefreshTokens.Add(newToken);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating refresh token for user {UserId}", oldToken.UserId);
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
