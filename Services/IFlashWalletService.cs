#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for wallet-specific operations and management
    /// </summary>
    public interface IFlashWalletService
    {
        /// <summary>
        /// Get the initialized wallet ID
        /// </summary>
        string? WalletId { get; }

        /// <summary>
        /// Get the wallet currency (USD or BTC)
        /// </summary>
        string? WalletCurrency { get; }

        /// <summary>
        /// Check if the wallet is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initialize wallet information
        /// </summary>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if initialization successful</returns>
        Task<bool> InitializeAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get current wallet information
        /// </summary>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Wallet information</returns>
        Task<WalletInfo?> GetWalletInfoAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get current block height (for blockchain wallets)
        /// </summary>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Current block height</returns>
        Task<int> GetCurrentBlockHeightAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Validate wallet configuration
        /// </summary>
        /// <returns>Validation result with any error messages</returns>
        Task<(bool isValid, string? errorMessage)> ValidateWalletAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get wallet capabilities
        /// </summary>
        /// <returns>Wallet capabilities</returns>
        Task<WalletCapabilities> GetCapabilitiesAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Wallet capabilities
    /// </summary>
    public class WalletCapabilities
    {
        public bool SupportsLightning { get; set; } = true;
        public bool SupportsOnChain { get; set; }
        public bool SupportsUSD { get; set; }
        public bool SupportsBTC { get; set; }
        public bool SupportsLNURL { get; set; }
        public bool SupportsZeroAmountInvoices { get; set; }
        public bool SupportsBoltcards { get; set; }
        public decimal MinimumPaymentAmount { get; set; }
        public decimal MaximumPaymentAmount { get; set; }
    }
}