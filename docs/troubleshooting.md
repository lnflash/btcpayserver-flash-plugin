# Troubleshooting

Read the BTCPay log first. On a Docker install:

```sh
docker logs --since 30m generated_btcpayserver_1 2>&1 | grep -E "Plugins.Flash|INVOICE STATUS|GetPayment|PAYMENT|^fail"
```

## Common problems

| Symptom in the log or UI | Cause | Fix |
|---|---|---|
| `[AuthHandler] Received Unauthorized response`, then `No wallet found` | The token is invalid, revoked or expired. `ory_st_` session tokens expire | Create an API key at console.flashapp.me and update the connection string |
| `Missing 'server' parameter` or `Missing 'token' parameter` | Old connection string format (`api=`, `api-token=`) | Use `type=flash;server=https://api.flashapp.me/graphql;token=fk_...` |
| `insufficient balance. Current Balance: 0.000000` when sending, though the wallet has funds | The plugin is paying from the legacy USD wallet instead of the USDT cash wallet | Update to 1.6.3 or later; the log should say `Flash reports USDT` |
| Plugins page still shows the old version after uploading | The uploaded file was renamed, so it installed beside the old one | Delete the extra folder in the plugins directory and upload `BTCPayServer.Plugins.Flash.btcpay` under that exact name |
| Card top-up paid, but the card balance hasn't changed | The balance page can lag a few seconds behind the payment | Tap again after a few seconds; check the payout list on the card's pull payment |
| Payout stuck *In progress* | Flash answered `PENDING` and has not reported a final status. Flash cannot report on invoices from other Lightning nodes | Check the payment in the Flash account and complete or cancel the payout by hand |
| `Ignoring paid notification for invoice …` | An internal check thought an invoice was paid but Flash did not confirm it | Nothing; this is the plugin refusing to mark an unconfirmed payment as paid |
| `[GetPayment] No record of sending …; reporting pending` | BTCPay asked about a payment sent before the last restart | Check the payment in Flash and settle the payout by hand |

## What a healthy payment looks like

**Receiving (top-up):**

```text
Creating invoice for 58 sats with memo: 'Boltcard Top-Up'
[INVOICE STATUS] Invoice 4622… is Paid according to Flash (amount received: 0.00000059)
BTC (Lightning): Payment detected via notification (Ak6Q…)
```

**Sending (card spend):**

```text
[PAYMENT] Payment sent successfully!
[GetPayment] Payment 433d… is Complete
```

## Checking the API key directly

```sh
curl -s https://api.flashapp.me/graphql \
  -H 'Content-Type: application/json' \
  -H "X-API-KEY: $FLASH_API_KEY" \
  -H 'X-Flash-Client-Capabilities: cash-wallet-usdt-v1' \
  -d '{"query":"{ me { defaultAccount { wallets { id walletCurrency balance } } } }"}'
```

A working key returns the account's wallets. The cash wallet shows as `USDT` with its balance in cents. A `401` means the key itself is the problem.

## Still stuck?

[Open an issue](https://github.com/lnflash/btcpayserver-flash-plugin/issues) with the plugin version, the BTCPay version, and the relevant log lines. Remove tokens and keys first.
