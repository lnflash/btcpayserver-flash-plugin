using BTCPayServer.Abstractions.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Flash
{
    public static class FlashPluginExtensions
    {
        public static void MapFlashPluginRoutes(this IEndpointRouteBuilder endpoints)
        {
            // Map the dashboard routes
            endpoints.MapControllerRoute(
                name: "Flash-PayoutDashboard",
                pattern: "plugins/flash/{storeId}/payouts/dashboard",
                defaults: new { controller = "FlashPayout", action = "Dashboard" });
                
            endpoints.MapControllerRoute(
                name: "Flash-PayoutList",
                pattern: "plugins/flash/{storeId}/payouts/list",
                defaults: new { controller = "FlashPayout", action = "GetPayouts" });
                
            endpoints.MapControllerRoute(
                name: "Flash-PayoutExport",
                pattern: "plugins/flash/{storeId}/payouts/export",
                defaults: new { controller = "FlashPayout", action = "ExportPayouts" });
                
            endpoints.MapControllerRoute(
                name: "Flash-BoltcardStats",
                pattern: "plugins/flash/{storeId}/payouts/boltcard-stats",
                defaults: new { controller = "FlashPayout", action = "GetBoltcardStats" });
                
            endpoints.MapControllerRoute(
                name: "Flash-PayoutTimeline",
                pattern: "plugins/flash/{storeId}/payouts/timeline",
                defaults: new { controller = "FlashPayout", action = "GetPayoutTimeline" });
        }
    }
}