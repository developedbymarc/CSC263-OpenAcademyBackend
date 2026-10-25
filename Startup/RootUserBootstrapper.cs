// Startup/RootUserBootstrapper.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities.Users;

namespace OpenAcademy.Startup;

public sealed record RootCredentials(string Email, string UserName, string Password);

public enum RootCreateOutcome { Created, AlreadyExists, Rejected }

public sealed record RootCreateResult(RootCreateOutcome Outcome, IReadOnlyList<string> Errors);

public sealed class RootUserBootstrapper(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
{
    // Arbitrary constant. Every instance must use the same value.
    private const long AdvisoryLockKey = 69_420_819_067L;

    public async Task<bool> RootExistsAsync()
    {
        var existing = await userManager.GetUsersInRoleAsync(RoleNames.Root);
        return existing.Count switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException(
                $"Found {existing.Count} users in role '{RoleNames.Root}'. There must be exactly one."),
        };
    }

    public async Task<RootCreateResult> TryCreateRootAsync(RootCredentials c, CancellationToken ct = default)
    {
        // The retry strategy requires user transactions to run inside ExecuteAsync
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear(); // the delegate can be re-run after a transient failure
            await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

            // Serializes concurrent callers. Released on commit or rollback.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", ct);
            
            if ((await userManager.GetUsersInRoleAsync(RoleNames.Root)).Count > 0)
                return new RootCreateResult(RootCreateOutcome.AlreadyExists, []);

            var user = new ApplicationUser
            {
                UserName = c.UserName,
                FirstName = c.UserName,
                LastName = c.UserName,
                Email = c.Email,
                EmailConfirmed = true,   // RequireConfirmedEmail would otherwise lock the root out
            };

            var created = await userManager.CreateAsync(user, c.Password);
            if (!created.Succeeded) return Rejected(created);

            var assigned = await userManager.AddToRoleAsync(user, RoleNames.Root);
            if (!assigned.Succeeded) return Rejected(assigned);

            await tx.CommitAsync(ct);   // user and role land together or not at all
            return new RootCreateResult(RootCreateOutcome.Created, []);
        });
    }

    // Identity's error descriptions never contain the password. Leaving without committing rolls back.
    private static RootCreateResult Rejected(IdentityResult r) =>
        new(RootCreateOutcome.Rejected, r.Errors.Select(e => e.Description).ToList());
}