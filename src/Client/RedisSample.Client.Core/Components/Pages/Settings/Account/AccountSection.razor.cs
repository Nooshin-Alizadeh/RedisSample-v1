using RedisSample.Shared.Features.Identity.Dtos;

namespace RedisSample.Client.Core.Components.Pages.Settings.Account;

public partial class AccountSection
{
    [CascadingParameter] public UserDto? CurrentUser { get; set; }
}
