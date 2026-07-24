namespace RedisSample.Shared.Infrastructure.Services;

/// <summary>
/// Values for the <see cref="AppClaimTypes.FEATURES"/>
/// These features will be implemented as a policy. If the user has the specified value in the <see cref="AppClaimTypes.FEATURES"/> claim,
/// the policy will be fulfilled, granting the user access to the resource <see cref="ISharedServiceCollectionExtensions.ConfigureAuthorizationCore"/>
/// </summary>
public class AppFeatures
{
    public class Management
    {
        /// <summary>
        /// Change AI Chatbot's system prompt.
        /// The value can be anything (1.0, 1.0.0, m-1.0, m-ai etc), but it has to be unique.
        /// The reason behind small feature values is that they're stored in jwt token, so in order to keep jwt token payload small, such a short-unique values has been assigned.
        /// </summary>
        public const string SystemPrompts_Write = "1.0";

        public const string Roles_Manage = "1.1";

        public const string Users_Manage = "1.2";

    }

    public class System
    {
        /// <summary>
        /// <inheritdoc cref="SharedAppMessages.UPLOAD_DIAGNOSTIC_LOGGER_STORE" />
        /// </summary>
        public const string Logs_View = "2.0";

        /// <summary>
        /// Manage background jobs using hangfire's dashboard.
        /// </summary>
        public const string Jobs_Manage = "2.1";
    }

    public class AdminPanel
    {
        public const string Dashboard_View = "3.0";

        /// <summary>
        /// Create/Update/Delete products and categories.
        /// </summary>
        public const string ProductCatalog_Manage = "3.1";
    }

    public class Todo
    {
        /// <summary>
        /// Create/Update/Delete todo items for the user itself.
        /// </summary>
        public const string Todo_Manage_Self = "4.0";
    }

    private static (string Name, string Value, Type Group)[]? globalAdminFeatures;
    public static (string Name, string Value, Type Group)[] GetGlobalAdminFeatures()
    {
        return globalAdminFeatures ??= [.. typeof(AppFeatures)
            .GetNestedTypes()
            .SelectMany(t => t.GetFields())
            .Select(t => (t.Name, t.GetRawConstantValue()!.ToString()!, t.DeclaringType!))];
    }

    private static (string Name, string Value, Type Group)[]? demoFeatures;
    public static (string Name, string Value, Type Group)[] GetDemoFeatures()
    {
        return demoFeatures ??= [.. GetGlobalAdminFeatures().Where(f => f.Group != typeof(System) && f.Group != typeof(Management))];
    }

}
