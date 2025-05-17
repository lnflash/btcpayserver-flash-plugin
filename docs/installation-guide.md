# Flash Plugin Installation Guide

## Introduction

The Flash Plugin for BTCPay Server enables you to accept Lightning Network payments through your Flash wallet. This guide provides detailed installation and configuration instructions.

## Requirements

Before installing the Flash plugin, make sure you have:

1. A running BTCPay Server instance (version 2.0.0 or later)
2. A Flash account with API access
3. A Flash bearer token

## Installation Steps

### Method 1: Installing via BTCPay Server UI

1. Download the latest `.btcpay` package from the [releases page](https://github.com/lnflash/btcpayserver-flash-plugin/releases)
2. In your BTCPay Server, go to **Server Settings > Plugins**
3. Click **Choose File** and select the downloaded `.btcpay` package
4. Click **Upload** to install the plugin
5. Restart your BTCPay Server when prompted

### Method 2: Manual Installation

If the UI installation doesn't work, you can install the plugin manually:

1. Download the latest release files
2. Extract the contents into the plugins directory of your BTCPay Server installation:
   ```
   /root/.btcpayserver/plugins/BTCPayServer.Plugins.Flash/
   ```
3. Restart your BTCPay Server

## Configuration

### Setting Up the Flash Lightning Method

1. After installation, go to your **Store Settings > Lightning**
2. Select **Flash** as your Lightning payment provider
3. Enter your Flash API settings:
   - **Flash Bearer Token**: Your Flash API bearer token
   - **API Endpoint**: The Flash GraphQL API endpoint (default: `https://api.flashapp.me/graphql`)
4. Click **Test Connection** to verify your settings
5. Save the configuration

## Pull Payment Configuration

The Flash plugin supports Pull Payments without additional configuration as long as:

1. Your Flash wallet is properly configured with the correct API keys
2. You have sufficient funds for expected payouts
3. Pull Payments are enabled in your BTCPay Server instance

When creating a Pull Payment, just select "BTC (Lightning)" as a payment method to enable Flash Lightning payouts.

### Creating a Pull Payment

1. Go to your store's **Pull Payments** section
2. Click **Create a new Pull Payment**
3. Fill out the required information:
   - **Name**: A descriptive name for the pull payment
   - **Amount**: The maximum amount that can be claimed
   - **Currency**: The currency of the pull payment
   - **Expiration date**: When the pull payment expires (optional)
   - **Payment Methods**: Select "BTC (Lightning)" to enable Flash payouts
4. Click **Create**
5. Share the generated link with recipients who need to claim funds

## Troubleshooting

If you encounter any issues with the Flash plugin:

1. Check the BTCPay Server logs for any error messages related to "Flash"
2. Verify that your Flash bearer token is correct and has the necessary permissions
3. Ensure that your Flash wallet has sufficient funds for any outgoing payments
4. Check your network connectivity to the Flash API endpoint

For additional support, please open an issue on our [GitHub repository](https://github.com/lnflash/btcpayserver-flash-plugin/issues).

## Security Considerations

- Keep your Flash bearer token secure. It provides access to your Flash wallet.
- Consider implementing additional security measures such as IP restrictions in your Flash account settings.
- Regularly monitor your Flash wallet activity for any unauthorized transactions. 