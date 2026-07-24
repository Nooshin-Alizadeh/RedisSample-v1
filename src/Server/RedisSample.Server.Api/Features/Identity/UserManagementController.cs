using RedisSample.Shared.Features.Identity.Dtos;
using RedisSample.Server.Api.Features.Identity.Models;
using RedisSample.Shared.Features.Identity;
using Microsoft.AspNetCore.SignalR;
using RedisSample.Server.Api.Infrastructure.SignalR;

namespace RedisSample.Server.Api.Features.Identity;

[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]")]
[Authorize(Policy = AuthPolicies.PRIVILEGED_ACCESS),
    Authorize(Policy = AppFeatures.Management.Users_Manage)]
public partial class UserManagementController : AppControllerBase, IUserManagementController
{
    [AutoInject] private UserManager<User> userManager = default!;
    [AutoInject] private IHubContext<AppHub> appHubContext = default!;
    [AutoInject] private ServerApiSettings serverApiSettings = default!;


    [HttpGet, EnableQuery]
    public IQueryable<UserDto> GetAllUsers()
    {
        return userManager.Users.Project();
    }

    [HttpGet]
    public async Task<int> GetOnlineUsersCount(CancellationToken cancellationToken)
    {
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();

        var usersQuery = DbContext.Users.AsQueryable();


        return await usersQuery.CountAsync(u => u.Sessions.Any(us => (now - (us.RenewedOn ?? us.StartedOn)) < serverApiSettings.Identity.BearerTokenExpiration.TotalSeconds), cancellationToken);
    }

    [HttpGet("{userId}"), EnableQuery]
    public IQueryable<UserSessionDto> GetUserSessions(Guid userId)
    {
        var query = DbContext.UserSessions.Where(us => us.UserId == userId);


        return query.Project();
    }

    [HttpPost("{userId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task Delete(Guid userId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() == userId)
            throw new BadRequestException(Localizer[nameof(AppStrings.UserCantRemoveItselfErrorMessage)]);


        var user = await GetUserById(userId, cancellationToken);

        if (await userManager.IsInRoleAsync(user, AppRoles.GlobalAdmin))
        {
            if (User.IsInRole(AppRoles.GlobalAdmin) is false)
                throw new BadRequestException(Localizer[nameof(AppStrings.UserCantRemoveSuperAdminErrorMessage)]);
        }

        var userSessionConnectionIds = await DbContext.UserSessions.Where(us => us.UserId == userId && us.SignalRConnectionId != null)
                                                                   .Select(us => us.SignalRConnectionId!)
                                                                   .ToListAsync(cancellationToken);

        var strategy = DbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

            await DbContext.UserSessions.Where(us => us.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            await userManager.DeleteAsync(user);

            await transaction.CommitAsync(cancellationToken);
        });

        foreach (var id in userSessionConnectionIds)
        {
            await RevokeSession(id, cancellationToken);
        }
    }

    [HttpPost("{id}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task RevokeUserSession(Guid id, CancellationToken cancellationToken)
    {
        if (id == User.GetSessionId())
            throw new BadRequestException(Localizer[nameof(AppStrings.UserCantRemoveItsCurrentSessionsErrorMessage)]);

        var entityToDelete = await DbContext.UserSessions.FindAsync([id], cancellationToken)
            ?? throw new ResourceNotFoundException().WithData("Reason", "User session not found.");


        DbContext.Remove(entityToDelete);

        await DbContext.SaveChangesAsync(cancellationToken);

        if (entityToDelete.SignalRConnectionId is not null)
        {
            await RevokeSession(entityToDelete.SignalRConnectionId, cancellationToken);
        }
    }

    [HttpPost("{userId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task RevokeAllUserSessions(Guid userId, CancellationToken cancellationToken)
    {

        var userSessionId = User.GetSessionId();

        var sessionsToRevokeQuery = DbContext.UserSessions.Where(us => us.Id != userSessionId && us.UserId == userId);


        var userSessionConnectionIds = await sessionsToRevokeQuery.Where(us => us.SignalRConnectionId != null)
                                                                  .Select(us => us.SignalRConnectionId!)
                                                                  .ToListAsync(cancellationToken);

        await sessionsToRevokeQuery.ExecuteDeleteAsync(cancellationToken);

        foreach (var id in userSessionConnectionIds)
        {
            await RevokeSession(id, cancellationToken);
        }
    }


    private async Task<User> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                    ?? throw new ResourceNotFoundException().WithData("Reason", "User not found.");

        return user;
    }


    private async Task RevokeSession(string connectionId, CancellationToken cancellationToken)
    {
        // Check out AppHub's comments for more info.
        await appHubContext.Clients.Client(connectionId)
            .Publish(SharedAppMessages.SESSION_REVOKED, null, cancellationToken);
    }
}
