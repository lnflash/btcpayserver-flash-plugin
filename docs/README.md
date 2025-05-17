# Flash Lightning Plugin for BTCPay Server

This plugin enables BTCPay Server users to connect with their Flash Lightning wallet to process Lightning Network payments. Flash provides a non-custodial mobile Lightning wallet with an intuitive user experience.

## Features

- Connect your Flash Lightning wallet to BTCPay Server
- Create and pay Lightning invoices using your Flash wallet
- Process USD-denominated Lightning payments 
- Secure token-based authentication with the Flash API
- Simple setup with automatic wallet detection
- Support for LNURL and Lightning Address payments
- Support for Pull Payments with LNURL-withdraw
- Advanced payment tracking system for asynchronous payment flows

## Requirements

- BTCPay Server 2.0.0 or later
- A Flash Lightning wallet and valid bearer token
- USD wallet in your Flash account

## Installation

### From the Plugin Manager

1. Go to your BTCPay Server admin interface
2. Select "Plugins" from the left sidebar
3. Click "Upload plugin" and select the `.btcpay` package file
4. The plugin will be installed and become available in the list of installed plugins

### From Source

1. Clone this repository
2. Make sure you have the .NET SDK 8.0 or later installed
3. Build the plugin using the provided build script: `./build-package.sh`
4. Upload the resulting `.btcpay` file through the BTCPay Server plugin manager

### Installing from BTCPay Server UI

1. Download the latest `.btcpay` package from the releases
2. In your BTCPay Server, go to **Server Settings > Plugins**
3. Click **Choose File** and select the downloaded `.btcpay` package
4. Click **Upload** to install the plugin
5. Restart your BTCPay Server when prompted

### Manual Installation

If the UI installation doesn't work, you can install the plugin manually:

1. Download the latest release files
2. Extract the contents into the plugins directory of your BTCPay Server installation:
   ```
   /root/.btcpayserver/plugins/BTCPayServer.Plugins.Flash/
   ```
3. Restart your BTCPay Server

## Configuration

1. Go to Store Settings > Lightning > Payment Methods
2. You should see Flash Lightning as an available option
3. Click "Configure Flash Lightning"
4. Enter your connection details:
   ```
   type=flash;server=https://api.flashapp.me/graphql;token=your_bearer_token
   ```
5. Replace `your_bearer_token` with your Flash API token (obtained from the Flash mobile app)
6. Test the connection to ensure your token is valid
7. Save your settings

## Pull Payment Support

The Flash plugin supports BTCPay Server's Pull Payment functionality, allowing merchants to create pull payments that can be claimed via any Lightning wallet supporting LNURL-withdraw.

### Features
- LNURL-withdraw support for seamless claiming of pull payments
- Direct invoice creation and payment through the Flash GraphQL API
- Full tracking of pull payment claims and payouts within BTCPayServer
- Support for both LNURL destinations and standard BOLT11 invoices
- Robust payment tracking system for asynchronous payment flows

### Usage
1. Create a pull payment in BTCPayServer and enable the Lightning payment method
2. Share the pull payment link with recipients
3. Recipients can:
   - Scan the LNURL-withdraw QR code with their Lightning wallet
   - Paste a BOLT11 invoice directly into the claim form
4. The claim will be processed automatically

## Technical Details

This plugin uses Flash's GraphQL API to interact with your Lightning wallet. All API requests are authenticated using your bearer token.

The plugin implements the following features:

- **Wallet Detection**: Automatically identifies your USD wallet in Flash
- **Invoice Creation**: Creates USD-denominated Lightning invoices for receiving payments
- **Payment Processing**: Processes payments using Flash's LN infrastructure
- **Amount Conversion**: Handles the conversion between satoshis and USD cents
- **Invoice Type Detection**: Automatically detects and handles different types of invoices (amount/no-amount)
- **USD Wallet Support**: Full support for Lightning payments from USD wallets
- **Case-Insensitive LNURL Processing**: Handles LNURL payments regardless of case in the URL
- **Payment Status Tracking**: Sophisticated tracking system for asynchronous payment flows
- **Detailed Error Diagnostics**: Provides comprehensive error information for troubleshooting

### Implementation Notes

- The plugin uses GraphQL for API communication
- USD invoices are created using the `lnUsdInvoiceCreate` mutation
- Payments are processed using the appropriate mutation based on invoice type:
  - `lnInvoicePaymentSend` for invoices with amounts
  - `lnNoAmountInvoicePaymentSend` for no-amount invoices from BTC wallets
  - `lnNoAmountUsdInvoicePaymentSend` for no-amount invoices from USD wallets
- LNURL handling uses case-insensitive comparison by converting to lowercase
- The wallet caching mechanism ensures optimal API usage
- Enhanced error handling provides detailed diagnostic information for troubleshooting
- Implementation follows Flash mobile app patterns for optimal compatibility
- Multi-hash tracking system to handle various hash formats used by BTCPay Server

## Known Limitations

Compared to other Lightning plugins like Blink, the Flash plugin currently has the following limitations:

- **No WebSockets**: Does not support real-time updates through WebSockets
- **Limited Reporting**: Basic transaction reporting capabilities
- **No Webhook Support**: Cannot send notifications to external systems
- **Fixed Conversion Rate**: Uses a fixed conversion rate for BTC/USD rather than dynamic rates
- **Payment Verification**: The plugin can't always definitively verify that a payment was completed
- **Status Persistence**: Payment status is not stored persistently between restarts

## Important Usage Notes

When using the Flash plugin:

1. **For Receiving Payments**: Lightning Network invoices with or without amounts are supported.
2. **For Sending Payments (Payouts)**: 
   - Supports paying to standard BOLT11 invoices
   - Supports LNURL and Lightning Addresses with proper case-insensitive handling
   - Automatically detects and handles different types of destinations
3. **For Pull Payments**:
   - Both LNURL destinations and BOLT11 invoices are supported
   - For LNURL, payment status will show "payment has been initiated but is still in-flight"
   - For some payments, payment completion verification may be limited

## Troubleshooting

Common issues and solutions:

- **Connection Error**: Verify your token is valid and has not expired
- **Invoice Creation Fails**: Ensure you have a USD wallet in your Flash account
- **Authentication Error**: Check that your token has the necessary permissions
- **API URL Error**: Confirm the server URL is correct (should be https://api.flashapp.me/graphql)
- **Amount Conversion Issues**: For very small or very large amounts, the conversion may not be precise
- **Case Sensitivity in LNURL**: The plugin now handles case insensitivity automatically, but if you encounter issues, try using all lowercase for LNURL or Lightning addresses
- **Payment Status Issues**: If the payment status shows "in-flight" for LNURL pull payments, the payment may still have processed successfully
- **Pull Payment LNURL Issues**: If experiencing problems with LNURL in pull payments, try using a BOLT11 invoice instead

## Contributing

We welcome contributions to improve this plugin! Here's how to get started:

1. Fork the repository
2. Clone your fork: `git clone https://github.com/yourusername/BTCPayServer.Plugins.Flash.git`
3. Create a branch for your feature: `git checkout -b feature/my-new-feature`
4. Make your changes
5. Test thoroughly
6. Commit your changes: `git commit -am 'Add some feature'`
7. Push to the branch: `git push origin feature/my-new-feature`
8. Submit a pull request

## License

This plugin is released under the MIT License.

## Support

For support with this plugin:
- Open an issue on the GitHub repository
- Join the BTCPay Server community on Mattermost
- Contact Flash support for account-specific issues
- For API-specific questions: support@flashapp.me