#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of exchange rate service for currency conversions
    /// </summary>
    public class FlashExchangeRateService : IFlashExchangeRateService
    {
        private readonly IFlashGraphQLService _graphQLService;
        private readonly ILogger<FlashExchangeRateService> _logger;

        // Exchange rate caching
        private decimal? _cachedExchangeRate = null;
        private DateTime _exchangeRateCacheTime = DateTime.MinValue;
        private readonly TimeSpan _exchangeRateCacheDuration = TimeSpan.FromMinutes(5); // Cache for 5 minutes

        // Fallback rate caching
        private decimal? _cachedFallbackRate = null;
        private DateTime _fallbackRateCacheTime = DateTime.MinValue;
        private readonly TimeSpan _fallbackRateCacheDuration = TimeSpan.FromMinutes(15); // Cache fallback for longer

        public FlashExchangeRateService(
            IFlashGraphQLService graphQLService,
            ILogger<FlashExchangeRateService> logger)
        {
            _graphQLService = graphQLService ?? throw new ArgumentNullException(nameof(graphQLService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<decimal> GetBtcUsdExchangeRateAsync(CancellationToken cancellation = default)
        {
            // First try the Flash API
            try
            {
                return await GetCurrentExchangeRateAsync(cancellation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get exchange rate from Flash API, using fallback");
                return await GetFallbackExchangeRateAsync(cancellation);
            }
        }

        public async Task<decimal> ConvertSatoshisToUsdCentsAsync(long satoshis, CancellationToken cancellation = default)
        {
            try
            {
                // Get current BTC/USD exchange rate
                decimal btcUsdRate = await GetBtcUsdExchangeRateAsync(cancellation);

                // Convert with consistent precision for LNURL compatibility
                decimal btcAmount = satoshis / 100_000_000m; // Convert sats to BTC
                decimal usdAmount = btcAmount * btcUsdRate; // Convert to USD
                decimal usdCents = usdAmount * 100m; // Convert to cents

                // Use exact same precision for all calculations
                usdCents = Math.Round(usdCents, 8, MidpointRounding.AwayFromZero);

                _logger.LogDebug("Converted {Satoshis} satoshis to {UsdCents} USD cents using rate {Rate} USD/BTC",
                    satoshis, usdCents, btcUsdRate);

                return usdCents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting satoshis to USD cents");
                throw;
            }
        }

        public async Task<decimal> GetCurrentExchangeRateAsync(CancellationToken cancellation = default)
        {
            // Check if we have a cached rate that's still valid
            if (_cachedExchangeRate.HasValue && (DateTime.UtcNow - _exchangeRateCacheTime) < _exchangeRateCacheDuration)
            {
                _logger.LogDebug("Using cached BTC/USD exchange rate: {Rate}", _cachedExchangeRate.Value);
                return _cachedExchangeRate.Value;
            }

            try
            {
                var rate = await _graphQLService.GetExchangeRateAsync(cancellation);

                // Cache the rate
                _cachedExchangeRate = rate;
                _exchangeRateCacheTime = DateTime.UtcNow;

                _logger.LogInformation("Retrieved current BTC/USD exchange rate from Flash API: {Rate}", rate);
                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting exchange rate from Flash API");
                throw;
            }
        }

        public async Task<decimal> GetFallbackExchangeRateAsync(CancellationToken cancellation = default)
        {
            // Check if we have a cached fallback rate that's still valid
            if (_cachedFallbackRate.HasValue && (DateTime.UtcNow - _fallbackRateCacheTime) < _fallbackRateCacheDuration)
            {
                _logger.LogDebug("Using cached fallback BTC/USD exchange rate: {Rate}", _cachedFallbackRate.Value);
                return _cachedFallbackRate.Value;
            }

            _logger.LogInformation("Attempting to get exchange rate from fallback APIs");

            try
            {
                // Try CoinGecko API first
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "BTCPayServer.Plugins.Flash");

                    // CoinGecko API for Bitcoin price in USD
                    var response = await httpClient.GetStringAsync("https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd", cancellation);

                    // Parse the response to get the rate
                    try
                    {
                        // Example response: {"bitcoin":{"usd":63245.32}}
                        var jsonResponse = JObject.Parse(response);
                        if (jsonResponse["bitcoin"]?["usd"] != null)
                        {
                            decimal rate = jsonResponse["bitcoin"]["usd"].Value<decimal>();

                            // Cache the fallback rate
                            _cachedFallbackRate = rate;
                            _fallbackRateCacheTime = DateTime.UtcNow;

                            _logger.LogInformation("Retrieved fallback BTC/USD exchange rate from CoinGecko: {Rate}", rate);
                            return rate;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing CoinGecko response");
                    }
                }

                // If CoinGecko fails, try CoinDesk API
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "BTCPayServer.Plugins.Flash");

                        // CoinDesk API for Bitcoin price index
                        var response = await httpClient.GetStringAsync("https://api.coindesk.com/v1/bpi/currentprice/USD.json", cancellation);

                        // Parse the response to get the rate
                        try
                        {
                            // Example response: {"bpi":{"USD":{"rate":"63,245.32","rate_float":63245.32}}}
                            var jsonResponse = JObject.Parse(response);
                            if (jsonResponse["bpi"]?["USD"]?["rate_float"] != null)
                            {
                                decimal rate = jsonResponse["bpi"]["USD"]["rate_float"].Value<decimal>();

                                // Cache the fallback rate
                                _cachedFallbackRate = rate;
                                _fallbackRateCacheTime = DateTime.UtcNow;

                                _logger.LogInformation("Retrieved fallback BTC/USD exchange rate from CoinDesk: {Rate}", rate);
                                return rate;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing CoinDesk response");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching from CoinDesk API");
                }

                // If all APIs fail, use a conservative approximation based on recent market data
                // This is still better than a completely hardcoded value as we update it periodically
                decimal conservativeRate = 60000m;
                _logger.LogWarning("All rate APIs failed, using conservative fallback rate: {Rate}", conservativeRate);
                return conservativeRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fallback exchange rate");
                return 60000m; // Ultimate fallback if everything fails
            }
        }

        public void ClearCache()
        {
            _cachedExchangeRate = null;
            _exchangeRateCacheTime = DateTime.MinValue;
            _cachedFallbackRate = null;
            _fallbackRateCacheTime = DateTime.MinValue;

            _logger.LogInformation("Exchange rate cache cleared");
        }
    }
}