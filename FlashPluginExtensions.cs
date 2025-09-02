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
                
            // LNURL routes for flashcard support
            endpoints.MapControllerRoute(
                name: "Flash-LnurlPay",
                pattern: "plugins/{storeId}/Flash/.well-known/lnurlp/{cardId}",
                defaults: new { controller = "FlashLNURL", action = "LnurlPay" });
                
            endpoints.MapControllerRoute(
                name: "Flash-LnurlPayCallback",
                pattern: "plugins/{storeId}/Flash/lnurlp/{cardId}/callback",
                defaults: new { controller = "FlashLNURL", action = "LnurlPayCallback" });
                
            endpoints.MapControllerRoute(
                name: "Flash-FlashcardLnurl",
                pattern: "plugins/{storeId}/Flash/flashcard/{cardId}/lnurl",
                defaults: new { controller = "FlashLNURL", action = "GetFlashcardLnurl" });
                
            endpoints.MapControllerRoute(
                name: "Flash-TestLnurl",
                pattern: "plugins/{storeId}/Flash/test-lnurl",
                defaults: new { controller = "FlashLNURL", action = "TestLnurl" });
        }
    }
}