namespace Project_SharedClassLibrary.Security;

public static class AdminRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Editor = "Editor";
    public const string DataAnalyst = "DataAnalyst";
    public const string Developer = "Developer";

    public static readonly IReadOnlyList<string> All = [
        Admin,
        User,
        Editor,
        DataAnalyst,
        Developer
    ];
}

public static class AdminPermissions
{
    public const string DashboardView = "dashboard:view";
    public const string DashboardExport = "dashboard:export";
    public const string UsageHistoryView = "usage:view";
    public const string CategoryRead = "category:read";
    public const string CategoryManage = "category:manage";
    public const string LanguageRead = "language:read";
    public const string LanguageManage = "language:manage";
    public const string LocationRead = "location:read";
    public const string LocationManage = "location:manage";
    public const string TourRead = "tour:read";
    public const string TourManage = "tour:manage";
    public const string AudioRead = "audio:read";
    public const string AudioManage = "audio:manage";
    public const string UserRead = "user:read";
    public const string UserManage = "user:manage";
    public const string ModerationView = "moderation:view";
    public const string ModerationManage = "moderation:manage";
    public const string AnalyticsView = "analytics:view";
}

public static class AdminRolePolicies
{
    private static readonly IReadOnlyDictionary<string, int> RolePriority =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [AdminRoles.User] = 1,
            [AdminRoles.Editor] = 2,
            [AdminRoles.DataAnalyst] = 2,
            [AdminRoles.Admin] = 3,
            [AdminRoles.Developer] = 4
        };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> RolePermissions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [AdminRoles.Admin] =
            [
                AdminPermissions.DashboardView,
                AdminPermissions.DashboardExport,
                AdminPermissions.UsageHistoryView,
                AdminPermissions.CategoryRead,
                AdminPermissions.CategoryManage,
                AdminPermissions.LanguageRead,
                AdminPermissions.LanguageManage,
                AdminPermissions.LocationRead,
                AdminPermissions.LocationManage,
                AdminPermissions.TourRead,
                AdminPermissions.TourManage,
                AdminPermissions.AudioRead,
                AdminPermissions.AudioManage,
                AdminPermissions.UserRead,
                AdminPermissions.UserManage,
                AdminPermissions.ModerationView,
                AdminPermissions.ModerationManage,
                AdminPermissions.AnalyticsView
            ],
            [AdminRoles.Developer] =
            [
                AdminPermissions.DashboardView,
                AdminPermissions.DashboardExport,
                AdminPermissions.UsageHistoryView,
                AdminPermissions.CategoryRead,
                AdminPermissions.CategoryManage,
                AdminPermissions.LanguageRead,
                AdminPermissions.LanguageManage,
                AdminPermissions.LocationRead,
                AdminPermissions.LocationManage,
                AdminPermissions.TourRead,
                AdminPermissions.TourManage,
                AdminPermissions.AudioRead,
                AdminPermissions.AudioManage,
                AdminPermissions.UserRead,
                AdminPermissions.UserManage,
                AdminPermissions.ModerationView,
                AdminPermissions.ModerationManage,
                AdminPermissions.AnalyticsView
            ],
            [AdminRoles.Editor] =
            [
                AdminPermissions.DashboardView,
                AdminPermissions.AnalyticsView,
                AdminPermissions.CategoryRead,
                AdminPermissions.LocationRead,
                AdminPermissions.TourRead,
                AdminPermissions.AudioRead,
                AdminPermissions.ModerationView
            ],
            [AdminRoles.DataAnalyst] =
            [
                AdminPermissions.DashboardView,
                AdminPermissions.DashboardExport,
                AdminPermissions.UsageHistoryView,
                AdminPermissions.LocationRead,
                AdminPermissions.AudioRead,
                AdminPermissions.AnalyticsView
            ],
            [AdminRoles.User] =
            [
                AdminPermissions.DashboardView,
                AdminPermissions.AnalyticsView,
                AdminPermissions.UsageHistoryView,
                AdminPermissions.LocationRead,
                AdminPermissions.TourRead,
                AdminPermissions.AudioRead
            ]
        };

    public static IReadOnlyList<string> GetPermissions(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        return RolePermissions.TryGetValue(role, out var permissions)
            ? permissions.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    public static bool HasPermission(string? role, string permission) =>
        GetPermissions(role).Contains(permission, StringComparer.OrdinalIgnoreCase);

    public static bool HasAny(string? role, params string[] permissions) =>
        permissions.Any(permission => HasPermission(role, permission));

    public static bool IsPrivileged(string? role) =>
        string.Equals(role, AdminRoles.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, AdminRoles.Developer, StringComparison.OrdinalIgnoreCase);

    public static int GetRolePriority(string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? 0
            : RolePriority.TryGetValue(role, out var value)
                ? value
                : 0;

    public static bool CanManageUserRole(string? actorRole, string? targetRole)
    {
        if (!string.Equals(actorRole, AdminRoles.Admin, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actorRole, AdminRoles.Developer, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AdminRoles.All.Contains(targetRole ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    public static bool CanSetUserStatus(
        string? actorRole,
        int actorUserId,
        int? targetUserId,
        string? targetRole,
        int nextStatus)
    {
        if (!HasPermission(actorRole, AdminPermissions.UserManage))
        {
            return false;
        }

        if (nextStatus == 1)
        {
            return true;
        }

        if (targetUserId.HasValue && actorUserId == targetUserId.Value)
        {
            return false;
        }

        return !string.Equals(actorRole, AdminRoles.Admin, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(targetRole, AdminRoles.Developer, StringComparison.OrdinalIgnoreCase);
    }
}
