#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for handling exchange rate operations
    /// </summary>
    public interface IFlashExchangeRateService
    {
        /// <summary>
        /// Get current BTC/USD exchange rate
        /// </summary>
        Task<decimal> GetBtcUsdExchangeRateAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Convert satoshis to USD cents
        /// </summary>
        Task<decimal> ConvertSatoshisToUsdCentsAsync(long satoshis, CancellationToken cancellation = default);

        /// <summary>
        /// Get current exchange rate from Flash API
        /// </summary>
        Task<decimal> GetCurrentExchangeRateAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get fallback exchange rate from external APIs
        /// </summary>
        Task<decimal> GetFallbackExchangeRateAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Clear exchange rate cache
        /// </summary>
        void ClearCache();
    }
}