using Project_SharedClassLibrary.Security;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public static class AdminOwnershipScope
{
    public static bool IsOwnerScoped(DashboardUser? user) =>
        string.Equals(user?.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);

    public static bool CanAccessLocation(DashboardUser? user, Location? location)
    {
        if (user is null || location is null)
        {
            return false;
        }

        return !IsOwnerScoped(user) || location.OwnerId == user.UserId;
    }

    public static IQueryable<Location> ApplyLocationScope(
        IQueryable<Location> query,
        DashboardUser user) =>
        IsOwnerScoped(user)
            ? query.Where(item => item.OwnerId == user.UserId)
            : query;

    public static IQueryable<Audio> ApplyAudioScope(
        IQueryable<Audio> query,
        DashboardUser user) =>
        IsOwnerScoped(user)
            ? query.Where(item => item.Location != null && item.Location.OwnerId == user.UserId)
            : query;
}
