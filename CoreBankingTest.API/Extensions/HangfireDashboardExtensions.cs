using Hangfire;
using Hangfire.Dashboard;

namespace CoreBankingTest.API.Extensions;

public static class HangfireDashboardExtensions
{
    public static IApplicationBuilder UseHangfireDashboardWithAuth(this IApplicationBuilder app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            DashboardTitle = "CoreBanking Job Dashboard",
            DisplayStorageConnectionString = false,
            Authorization = new[] { new HangfireAuthorizationFilter() },
            StatsPollingInterval = 5000, // 5 seconds
            AppPath = "/", // Back to site URL
            IgnoreAntiforgeryToken = true
        });

        return app;
    }
}

// Custom authorization filter for Hangfire dashboard
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // For development: Allow all access
        // TODO: In production, add proper authentication
        // return httpContext.User.Identity.IsAuthenticated && httpContext.User.IsInRole("Admin");

        return true; // Allow all for development
    }
}
