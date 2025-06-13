# BTCPayServer.Plugins.Flash v1.3.5 Update Instructions

To ensure the correct version (1.3.5) is installed, please follow these steps:

## 1. Completely Remove the Previous Version

1. In BTCPayServer, go to **Server Settings** > **Plugins**
2. Find the "Flash" plugin
3. Click **Remove**
4. Confirm the removal
5. **Restart your BTCPayServer instance**

This complete removal is necessary to ensure all cached files from the previous version are cleared.

## 2. Install the New Version

1. Download the new plugin package: `BTCPayServer.Plugins.Flash-v1.3.5.btcpay`
2. In BTCPayServer, go to **Server Settings** > **Plugins**
3. Click **Choose File** and select the downloaded package
4. Click **Upload**
5. **Restart your BTCPayServer instance again**

## 3. Verify the Installation

1. After restart, check the logs for the line: `Flash Plugin: Starting plugin initialization`
2. Confirm the version showing is 1.3.5
3. Access the new Boltcard topup feature at: `/plugins/flash/boltcard/topup/{storeId}`

## 4. Using the Boltcard Topup Feature

1. Navigate to `/plugins/flash/boltcard/topup/{storeId}`
2. Enter the amount you want to add to your Boltcard
3. Click "Create Invoice" to generate a Lightning invoice
4. Scan the QR code with your Flash mobile wallet
5. Complete the payment within your Flash app
6. The success page will confirm the topup

If you still see version 1.3.4 in the logs after completing these steps, please clear your browser cache and try again.