# Flash BTCPayServer Plugin

A BTCPayServer plugin that enables direct Flash wallet payments via NFC-enabled cards at merchant point-of-sale systems.

## Overview

The Flash BTCPayServer Plugin provides merchants with a seamless "tap-to-pay" solution for accepting Bitcoin Lightning Network payments directly from Flash wallet users through NFC cards. This plugin eliminates dependencies on third-party solutions by integrating directly with Flash's backend wallet APIs.

## Features

- **NFC Card Payments**: Process tap-to-pay transactions using Flash NFC cards
- **Direct Integration**: Connect directly to Flash's Lightning Network implementation
- **Card Management**: Register, view, and manage Flash NFC cards 
- **Transaction History**: Track and review card payment history
- **Built-in Documentation**: Comprehensive guides for setup and troubleshooting

## Installation

1. Download the latest release from the [releases page](https://github.com/your-organization/flash-btcpayserver-plugin/releases)
2. Install the plugin in your BTCPayServer instance:
   - Go to Server Settings > Plugins
   - Click "Choose File" and select the downloaded .btcpay file
   - Click "Upload"
3. Configure the plugin:
   - Enter your Flash API credentials
   - Set up your preferred payment settings

## Usage

### For Merchants

1. Enable "Flash NFC Card" as a payment method in your store settings
2. Configure the connection to your Flash wallet
3. At checkout, customers will be able to select "Flash NFC Card" as their payment option
4. Prompt customers to tap their Flash card on your NFC-enabled device
5. The payment will be processed automatically through the Flash wallet API

### For Customers

1. Link your NFC-enabled Flash card to your Flash wallet account via the Flash mobile app or web portal
2. At checkout, select "Flash NFC Card" as your payment method
3. Tap your card on the merchant's NFC terminal or device
4. Payment will be processed instantly from your Flash wallet

## Payment Flow

1. Customer selects "Flash NFC Card" at checkout
2. Merchant prompts for NFC tap
3. Plugin receives card UID and maps to Flash user
4. Plugin calls Flash API to initiate payment
5. Payment success/failure is reported in BTCPayServer UI
6. Invoice status is updated accordingly

## Security

- Secure storage and transmission of card UIDs and wallet mappings
- Rate limiting and authentication checks for NFC endpoints
- Detailed error handling and logging for audit purposes
- No sensitive wallet keys stored on merchant/POS hardware

## Development

This plugin is built using C# and follows the BTCPayServer plugin architecture. It leverages Flash's backend wallet APIs for payment processing and card management.

### Building from Source

```bash
git clone https://github.com/your-organization/flash-btcpayserver-plugin.git
cd flash-btcpayserver-plugin
dotnet build
```

### Testing

```bash
dotnet test
```

## Documentation

For more detailed information, please refer to the [documentation](./docs/index.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Based on the Flash Improvement Proposal FIP-04
- Inspired by the BlinkPlugin architecture
- Built on the BTCPayServer plugin template