using CTFPlatform.Models;
using Hangfire.Dashboard;

namespace CTFPlatform.Utilities;

public class HangfireAuthorization : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        return httpContext.User.IsInRole(CtfUser.AdminRole);
    }
}
