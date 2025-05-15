# Flash Lightning Plugin for BTCPay Server

This plugin allows BTCPay Server users to connect with their Flash Lightning wallet to process Lightning Network payments.

## Features

- Connect your Flash Lightning wallet to BTCPay Server
- Create and pay Lightning invoices using your Flash wallet
- Access your Flash wallet balance and transaction history
- Secure token-based authentication with the Flash API

## Requirements

- BTCPay Server 2.0.0 or later
- A Flash Lightning wallet and valid bearer token

## Installation

### From the Plugin Manager

1. Go to your BTCPay Server admin interface
2. Select "Plugins" from the left sidebar
3. Click "Upload plugin" and select the `.btcpay` package file
4. The plugin will be installed and become available in the list of installed plugins

### From Source

1. Clone this repository
2. Build the plugin using the provided build script: `./build-package.sh`
3. Upload the resulting `.btcpay` file through the BTCPay Server plugin manager

## Configuration

1. Go to Store Settings > Lightning > Payment Methods
2. You should see Flash Lightning as an available option
3. Click "Configure Flash Lightning"
4. Enter your Flash bearer token (obtained from the Flash mobile app)
5. Test the connection to ensure your token is valid
6. Save your settings

## Technical Details

This plugin uses Flash's GraphQL API to interact with your Lightning wallet. All API requests are authenticated using your bearer token.

The plugin implements the following features:

- **Wallet Info**: Retrieves your wallet balance and status
- **Invoice Creation**: Creates Lightning invoices for receiving payments
- **Payment Processing**: Pays Lightning invoices using your Flash wallet
- **Transaction History**: Lists past transactions from your Flash wallet

## Security Considerations

- Your Flash bearer token is stored encrypted at rest
- Each token is scoped to a specific store and not shared across stores
- No sensitive information is logged during API operations

## Development

To build and test the plugin locally:

1. Clone the repository
2. Build the plugin: `dotnet build`
3. Use Docker for testing with BTCPay Server:
   ```
   docker-compose -f docker-compose.btcpay.yml up
   ```

## License

This plugin is released under the MIT License.

## Support

For support with this plugin, please open an issue on the GitHub repository or contact Flash support.