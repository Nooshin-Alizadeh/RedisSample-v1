namespace RedisSample.Client.Core.Components.Pages.Settings;

public partial class SettingsPage
{
    [Parameter] public string? Section { get; set; }

    private bool isLoading;
}
