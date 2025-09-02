#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of wallet service for Flash
    /// </summary>
    public class FlashWalletService : IFlashWalletService
    {
        private readonly IFlashGraphQLService _graphQLService;
        private readonly ILogger<FlashWalletService> _logger;
        
        private string? _walletId;
        private string? _walletCurrency;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _isInitialized;

        public string? WalletId => _walletId;
        public string? WalletCurrency => _walletCurrency;
        public bool IsInitialized => _isInitialized;

        public FlashWalletService(
            IFlashGraphQLService graphQLService,
            ILogger<FlashWalletService> logger)
        {
            _graphQLService = graphQLService ?? throw new ArgumentNullException(nameof(graphQLService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellation = default)
        {
            await _initializationLock.WaitAsync(cancellation);
            try
            {
                if (_isInitialized)
                {
                    _logger.LogDebug("Wallet already initialized");
                    return true;
                }

                _logger.LogInformation("Initializing Flash wallet...");

                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    _logger.LogError("Failed to retrieve wallet information from Flash API");
                    return false;
                }

                _walletId = walletInfo.Id;
                _walletCurrency = walletInfo.Currency;
                _isInitialized = true;

                _logger.LogInformation("Flash wallet initialized successfully. ID: {WalletId}, Currency: {Currency}",
                    _walletId, _walletCurrency);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Flash wallet");
                return false;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        public async Task<WalletInfo?> GetWalletInfoAsync(CancellationToken cancellation = default)
        {
            try
            {
                // Ensure wallet is initialized
                if (!_isInitialized)
                {
                    await InitializeAsync(cancellation);
                }

                return await _graphQLService.GetWalletInfoAsync(cancellation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet information");
                throw;
            }
        }

        public async Task<int> GetCurrentBlockHeightAsync(CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogDebug("Getting current block height");

                // Flash doesn't directly expose block height in the current API
                // This would need to be implemented if Flash adds this capability
                // For now, return a placeholder or throw NotImplementedException
                
                _logger.LogWarning("Block height query not supported by Flash API");
                return await Task.FromResult(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current block height");
                throw;
            }
        }

        public async Task<(bool isValid, string? errorMessage)> ValidateWalletAsync(CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Validating wallet configuration");

                // Try to get wallet info
                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    return (false, "Could not retrieve wallet information. Check your API credentials.");
                }

                // Validate wallet has a valid ID
                if (string.IsNullOrEmpty(walletInfo.Id))
                {
                    return (false, "Wallet ID is missing or invalid.");
                }

                // Validate wallet has a supported currency
                if (string.IsNullOrEmpty(walletInfo.Currency))
                {
                    return (false, "Wallet currency is not specified.");
                }

                if (walletInfo.Currency != "USD" && walletInfo.Currency != "BTC")
                {
                    return (false, $"Wallet currency '{walletInfo.Currency}' is not supported. Only USD and BTC are supported.");
                }

                // Check if wallet has a positive or zero balance (just informational)
                if (walletInfo.Balance < 0)
                {
                    _logger.LogWarning("Wallet has negative balance: {Balance}", walletInfo.Balance);
                }

                _logger.LogInformation("Wallet validation successful. ID: {WalletId}, Currency: {Currency}, Balance: {Balance}",
                    walletInfo.Id, walletInfo.Currency, walletInfo.Balance);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating wallet");
                return (false, $"Validation error: {ex.Message}");
            }
        }

        public async Task<WalletCapabilities> GetCapabilitiesAsync(CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogDebug("Getting wallet capabilities");

                // Ensure wallet is initialized
                if (!_isInitialized)
                {
                    await InitializeAsync(cancellation);
                }

                var capabilities = new WalletCapabilities
                {
                    SupportsLightning = true,
                    SupportsOnChain = false, // Flash is Lightning-only
                    SupportsUSD = _walletCurrency == "USD",
                    SupportsBTC = _walletCurrency == "BTC",
                    SupportsLNURL = _walletCurrency == "USD", // LNURL only supported for USD wallets
                    SupportsZeroAmountInvoices = _walletCurrency == "USD", // Zero-amount only for USD
                    SupportsBoltcards = true,
                    MinimumPaymentAmount = _walletCurrency == "USD" ? 0.01m : 1m, // $0.01 or 1 sat
                    MaximumPaymentAmount = 1000000m // Reasonable default, could be queried from API
                };

                _logger.LogInformation("Wallet capabilities determined. Currency: {Currency}, SupportsLNURL: {SupportsLNURL}",
                    _walletCurrency, capabilities.SupportsLNURL);

                return capabilities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet capabilities");
                throw;
            }
        }
    }
}