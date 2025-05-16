# Flash Lightning Plugin for BTCPay Server

This plugin enables BTCPay Server users to connect with their Flash Lightning wallet to process Lightning Network payments. Flash provides a non-custodial mobile Lightning wallet with an intuitive user experience.

## Features

- Connect your Flash Lightning wallet to BTCPay Server
- Create and pay Lightning invoices using your Flash wallet
- Process USD-denominated Lightning payments 
- Secure token-based authentication with the Flash API
- Simple setup with automatic wallet detection

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

## Technical Details

This plugin uses Flash's GraphQL API to interact with your Lightning wallet. All API requests are authenticated using your bearer token.

The plugin implements the following features:

- **Wallet Detection**: Automatically identifies your USD wallet in Flash
- **Invoice Creation**: Creates USD-denominated Lightning invoices for receiving payments
- **Payment Processing**: Processes payments using Flash's LN infrastructure
- **Amount Conversion**: Handles the conversion between satoshis and USD cents

### Implementation Notes

- The plugin uses GraphQL for API communication
- USD invoices are created using the `lnUsdInvoiceCreate` mutation
- The wallet caching mechanism ensures optimal API usage
- Error handling provides detailed information for troubleshooting

## Future Enhancements

The following features are on the roadmap to bring this plugin to feature parity with other Lightning plugins like Blink:

### High Priority

- **Real-time Payment Notifications**: Implement webhook support to receive instant payment notifications
- **Invoice Status Updates**: Support for real-time invoice status changes via WebSockets
- **BTC Wallet Support**: Add compatibility with BTC-denominated wallets in addition to USD
- **Payment Refunds**: Support for refunding/returning payments when needed

### Medium Priority

- **Enhanced Transaction History**: Improved display and filtering of transaction history
- **Multi-wallet Support**: Allow configuration of multiple Flash wallets for different purposes
- **Advanced Payment Features**: Support for LNURL-pay, LNURL-withdraw, and Keysend payments
- **On-chain Payments**: Basic support for on-chain transactions where supported by Flash

### Low Priority

- **Customizable Amount Conversion**: Allow merchants to set custom exchange rates and conversion settings
- **Extended Metadata**: Support for additional invoice metadata and tags
- **Advanced Reporting**: Detailed payment analytics and reporting features
- **Admin UI Improvements**: Enhanced settings interface with more configuration options

## Security Considerations

- Your Flash bearer token is stored encrypted at rest
- Each token is scoped to a specific store and not shared across stores
- No sensitive information is logged during API operations
- All API communication uses HTTPS

## Known Limitations

Compared to other Lightning plugins like Blink, the Flash plugin currently has the following limitations:

- **USD Only**: Currently only supports USD-denominated wallets, not BTC wallets
- **No WebSockets**: Does not support real-time updates through WebSockets
- **Limited Reporting**: Basic transaction reporting capabilities
- **No Webhook Support**: Cannot send notifications to external systems
- **Fixed Conversion Rate**: Uses a fixed conversion rate for BTC/USD rather than dynamic rates

## Contributing

We welcome contributions to improve this plugin! Here's how to get started:

1. Fork the repository
2. Clone your fork: `git clone https://github.com/lnflash/btcpayserver-flash-plugin.git`
3. Create a branch for your feature: `git checkout -b feature/my-new-feature`
4. Make your changes
5. Test thoroughly
6. Commit your changes: `git commit -am 'Add some feature'`
7. Push to the branch: `git push origin feature/my-new-feature`
8. Submit a pull request

### Development Environment Setup

1. Clone the BTCPay Server repository
2. Clone this plugin repository
3. Build BTCPay Server: `dotnet build`
4. Build the plugin: `./build-package.sh`
5. For local testing, you can use Docker:
   ```
   docker-compose -f docker-compose.btcpay.yml up
   ```

### Coding Guidelines

- Follow C# coding conventions
- Use async/await pattern for asynchronous operations
- Add meaningful comments for complex logic
- Write unit tests for new features
- Keep API calls efficient and minimal

## Troubleshooting

Common issues and solutions:

- **Connection Error**: Verify your token is valid and has not expired
- **Invoice Creation Fails**: Ensure you have a USD wallet in your Flash account
- **Authentication Error**: Check that your token has the necessary permissions
- **API URL Error**: Confirm the server URL is correct (should be https://api.flashapp.me/graphql)
- **Amount Conversion Issues**: For very small or very large amounts, the conversion may not be precise

## Testing the Plugin

To verify that your plugin is working correctly:

1. After configuration, create a test invoice for a small amount (e.g., $1)
2. Check the logs for any errors during invoice creation
3. Scan the invoice with a Lightning wallet to confirm it's valid
4. Make a test payment to ensure the funds arrive in your Flash wallet

## License

This plugin is released under the MIT License.

## Support

For support with this plugin:
- Open an issue on the GitHub repository
- Join the BTCPay Server community on Mattermost
- Contact Flash support for account-specific issues
- For API-specific questions: support@flashapp.me
