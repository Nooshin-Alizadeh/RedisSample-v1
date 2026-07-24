using RedisSample.Shared.Features.Identity.Dtos;
using RedisSample.Server.Api.Features.Identity.Models;
using RedisSample.Shared.Features.Identity;
using Microsoft.AspNetCore.SignalR;
using RedisSample.Server.Api.Infrastructure.SignalR;
using RedisSample.Server.Api.Features.PushNotification;

namespace RedisSample.Server.Api.Features.Identity;

[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]")]
[Authorize(Policy = AuthPolicies.PRIVILEGED_ACCESS),
    Authorize(Policy = AppFeatures.Management.Roles_Manage)]
public partial class RoleManagementController : AppControllerBase, IRoleManagementController
{
    [AutoInject] private IHubContext<AppHub> appHubContext = default!;

    [AutoInject] private PushNotificationService pushNotificationService = default!;

    [AutoInject] private UserManager<User> userManager = default!;
    [AutoInject] private RoleManager<Role> roleManager = default!;


    [HttpGet, EnableQuery]
    public IQueryable<RoleDto> GetAllRoles()
    {
        var isUserGlobalAdmin = User.IsInRole(AppRoles.GlobalAdmin);

        return roleManager.Roles
                          .WhereIf(isUserGlobalAdmin is false, r => r.Name != AppRoles.GlobalAdmin)
                          .Project();
    }

    [HttpGet, EnableQuery]
    public IQueryable<UserDto> GetAllUsers()
    {
        var query = userManager.Users
                          .Where(u => u.EmailConfirmed || u.PhoneNumberConfirmed || u.Logins.Any() /*External sign-in*/);


        return query.Project();
    }

    [HttpGet("{roleId}"), EnableQuery]
    public IQueryable<UserDto> GetUsers(Guid roleId)
    {
        var query = userManager.Users.Where(u => u.Roles.Any(r => r.RoleId == roleId));


        return query.Project();
    }

    [HttpGet("{roleId}"), EnableQuery]
    public IQueryable<ClaimDto> GetClaims(Guid roleId)
    {
        var query = DbContext.RoleClaims.Where(rc => rc.RoleId == roleId);


        return query.Project();
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task<RoleDto> Create(RoleDto roleDto, CancellationToken cancellationToken)
    {
        var role = roleDto.Map();

        if (AppRoles.IsBuiltInRole(role.Name!))
            throw new BadRequestException(Localizer[nameof(AppStrings.CanNotChangeBuiltInRole), role.Name!]);


        var result = await roleManager.CreateAsync(role);

        if (result.Succeeded is false)
            throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());

        return role.Map();
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task<RoleDto> Update(RoleDto roleDto, CancellationToken cancellationToken)
    {
        var role = await GetRoleById(roleDto.Id, cancellationToken);

        // Checked BEFORE Patch, against BOTH names: role.Name blocks editing/renaming an existing built-in role (e.g.
        // renaming t-admin/g-admin away, which would strip everyone's admin features), and roleDto.Name blocks renaming a
        // custom role TO a reserved built-in name (which would escalate to global admin, since built-in names become
        // elevated feature grants at token-read time - See AppJwtSecureDataFormat.Unprotect).
        if (AppRoles.IsBuiltInRole(role.Name!) || AppRoles.IsBuiltInRole(roleDto.Name!))
            throw new BadRequestException(Localizer[nameof(AppStrings.CanNotChangeBuiltInRole), role.Name!]);

        roleDto.Patch(role);

        var result = await roleManager.UpdateAsync(role);

        if (result.Succeeded is false)
            throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());

        return role.Map();
    }

    [HttpDelete("{roleId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task Delete(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await GetRoleById(roleId, cancellationToken);

        if (AppRoles.IsBuiltInRole(role.Name!))
            throw new BadRequestException(Localizer[nameof(AppStrings.CanNotChangeBuiltInRole), role.Name!]);

        await roleManager.DeleteAsync(role);
    }

    [HttpPost("{roleId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task AddClaims(Guid roleId, List<ClaimDto> claims, CancellationToken cancellationToken)
    {
        List<RoleClaim> entities = [];

        var role = await GetRoleById(roleId, cancellationToken);

        EnsureRoleClaimsAreEditable(role);

        EnsureCallerCanGrantClaims(claims);

        foreach (var claim in claims)
        {
            var result = await roleManager.AddClaimAsync(role, new(claim.ClaimType!, claim.ClaimValue!));

            if (result.Succeeded is false)
                throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());
        }
    }

    [HttpPost("{roleId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task UpdateClaims(Guid roleId, List<ClaimDto> claims, CancellationToken cancellationToken)
    {
        var role = await GetRoleById(roleId, cancellationToken);

        EnsureRoleClaimsAreEditable(role);

        EnsureCallerCanGrantClaims(claims);

        foreach (var claim in claims)
        {
            var result = await roleManager.RemoveClaimAsync(role, new(claim.ClaimType!, claim.ClaimValue!));

            if (result.Succeeded is false)
                throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());

            result = await roleManager.AddClaimAsync(role, new(claim.ClaimType!, claim.ClaimValue!));

            if (result.Succeeded is false)
                throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());
        }
    }

    [HttpPost("{roleId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task DeleteClaims(Guid roleId, List<ClaimDto> claims, CancellationToken cancellationToken)
    {
        var role = await GetRoleById(roleId, cancellationToken);

        EnsureRoleClaimsAreEditable(role);

        foreach (var claim in claims)
        {
            var result = await roleManager.RemoveClaimAsync(role, new(claim.ClaimType!, claim.ClaimValue!));

            if (result.Succeeded is false)
                throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());
        }
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task ToggleUserRole(UserRoleDto dto, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(dto.UserId.ToString())
            ?? throw new ResourceNotFoundException().WithData("Reason", "User not found.");

        var role = await GetRoleById(dto.RoleId, cancellationToken);

        var isGlobalAdminRole = role.Name == AppRoles.GlobalAdmin;
        var isGlobalAdminUser = User.IsInRole(AppRoles.GlobalAdmin);

        if (isGlobalAdminRole && isGlobalAdminUser is false)
            throw new UnauthorizedException();

        if (await userManager.IsInRoleAsync(user, role.Name!))
        {
            if (isGlobalAdminRole)
            {
                var otherGlobalAdminsCount = await userManager.Users.CountAsync(u => u.Roles.Any(r => r.RoleId == role.Id) && u.Id != user.Id, cancellationToken);

                if (otherGlobalAdminsCount == 0)
                    throw new BadRequestException(Localizer[nameof(AppStrings.UserCantUnassignAllSuperAdminsErrorMessage)]);
            }
            var result = await userManager.RemoveFromRoleAsync(user, role.Name!);
            if (result.Succeeded is false)
                throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());
        }
        else
        {
            var result = await userManager.AddToRoleAsync(user, role.Name!);
            if (result.Succeeded is false)
                throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray());
        }
    }

    [HttpPost("{roleId}")]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task RemoveAllUsersFromRole(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await GetRoleById(roleId, cancellationToken);

        await DbContext.UserRoles.Where(ur => ur.RoleId == roleId).ExecuteDeleteAsync(cancellationToken);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ELEVATED_ACCESS)]
    public async Task SendNotification(SendNotificationToRoleDto dto, CancellationToken cancellationToken)
    {
        // Ensure the target role exists and (for non global admins) belongs to the caller's tenant before broadcasting
        // to its users - otherwise a tenant admin could push an in-app notification to another tenant's users.
        var role = await GetRoleById(dto.RoleId, cancellationToken);

        var signalRConnectionIds = await DbContext.UserSessions.Where(us => us.NotificationStatus == UserSessionNotificationStatus.Allowed &&
                                                                            us.SignalRConnectionId != null &&
                                                                            us.User!.Roles.Any(r => r.RoleId == dto.RoleId))
                                                               .Select(us => us.SignalRConnectionId!).ToArrayAsync(cancellationToken);

        await appHubContext.Clients.Clients(signalRConnectionIds)
                                   .SendAsync(SharedAppMessages.SHOW_MESSAGE, dto.Message, dto.PageUrl is null ? null : new Dictionary<string, string?> { { "pageUrl", dto.PageUrl } }, cancellationToken);

        await pushNotificationService.RequestPush(new()
        {
            Message = dto.Message,
            PageUrl = dto.PageUrl,
            UserRelatedPush = true,
            RequesterUserSessionId = User.GetSessionId()
        }, customSubscriptionFilter: s => s.UserSession!.User!.Roles.Any(r => r.RoleId == dto.RoleId)
                                          , cancellationToken: cancellationToken);
    }


    private async Task<Role> GetRoleById(Guid id, CancellationToken cancellationToken)
    {
        var role = await roleManager.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                    ?? throw new ResourceNotFoundException().WithData("Reason", "Role not found.");


        return role;
    }

    private void EnsureRoleClaimsAreEditable(Role role)
    {
        if (role.Name is AppRoles.GlobalAdmin
            )
            throw new BadRequestException(Localizer[nameof(AppStrings.UserCantChangeSuperAdminRoleClaimsErrorMessage)]);
    }

    /// <summary>
    /// A role manager may only grant feature claims they themselves possess, so they cannot escalate privileges by
    /// assigning a feature they lack - for example granting a <see cref="AppFeatures.System"/> feature, or (under
    /// multi-tenant) the global-admin-only Tenants_Write_Global feature, to a role and thereby gaining those capabilities.
    /// Non-feature claims (e.g. <see cref="AppClaimTypes.MAX_PRIVILEGED_SESSIONS"/>) are not restricted here.
    /// </summary>
    private void EnsureCallerCanGrantClaims(IEnumerable<ClaimDto> claims)
    {
        foreach (var claim in claims)
        {
            if (claim.ClaimType is AppClaimTypes.FEATURES && User.HasFeature(claim.ClaimValue!) is false)
                throw new UnauthorizedException().WithData("Reason", $"Caller does not have the feature claim '{claim.ClaimValue}' and cannot grant it to a role.");
        }
    }
}
