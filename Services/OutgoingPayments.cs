using System;
using System.Collections.Concurrent;
using System.Linq;
using BTCPayServer.Lightning;
using NBitcoin;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Payments this plugin has sent, by payment hash, so GetPayment can report on them exactly.
    /// Flash transactions carry no payment hash, so without this record there is no way to tell
    /// which send a status question is about.
    /// </summary>
    public static class OutgoingPayments
    {
        public class Entry
        {
            public string Bolt11 { get; init; } = string.Empty;
            public LightMoney? Amount { get; init; }
            public DateTimeOffset SubmittedAt { get; init; }
            // Pending until Flash reports a definitive outcome
            public LightningPaymentStatus Outcome { get; set; } = LightningPaymentStatus.Pending;
        }

        private static readonly TimeSpan Retention = TimeSpan.FromDays(1);
        private static readonly ConcurrentDictionary<string, Entry> _byHash = new(StringComparer.OrdinalIgnoreCase);

        public static void Record(string paymentHash, string bolt11, LightMoney? amount)
        {
            var cutoff = DateTimeOffset.UtcNow - Retention;
            foreach (var old in _byHash.Where(kvp => kvp.Value.SubmittedAt < cutoff).Select(kvp => kvp.Key).ToList())
                _byHash.TryRemove(old, out _);

            _byHash[paymentHash] = new Entry { Bolt11 = bolt11, Amount = amount, SubmittedAt = DateTimeOffset.UtcNow };
        }

        public static void SetOutcome(string paymentHash, LightningPaymentStatus outcome)
        {
            if (_byHash.TryGetValue(paymentHash, out var entry))
                entry.Outcome = outcome;
        }

        public static bool TryGet(string paymentHash, out Entry entry) =>
            _byHash.TryGetValue(paymentHash, out entry!);
    }

    public static class Bolt11
    {
        /// <summary>
        /// Reads the payment hash and amount from a BOLT11 payment request, or returns false.
        /// </summary>
        public static bool TryParse(string? bolt11, out string paymentHash, out LightMoney? amount)
        {
            paymentHash = string.Empty;
            amount = null;
            if (string.IsNullOrEmpty(bolt11))
                return false;

            var network = bolt11.StartsWith("lnbcrt", StringComparison.OrdinalIgnoreCase) ? Network.RegTest
                : bolt11.StartsWith("lntb", StringComparison.OrdinalIgnoreCase) ? Network.TestNet
                : Network.Main;
            try
            {
                var request = BOLT11PaymentRequest.Parse(bolt11, network);
                paymentHash = request.PaymentHash?.ToString() ?? string.Empty;
                amount = request.MinimumAmount;
                return paymentHash.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
