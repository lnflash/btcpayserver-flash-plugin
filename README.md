# BTCPay Server Flash Plugin

Integrate your Flash Lightning wallet with BTCPay Server.

## Features

- Connect your BTCPay Server to your Flash Lightning wallet
- Create and pay Lightning invoices through Flash
- Secure token-based authentication

## Installation

### From Source

1. Clone the repository
2. Build the plugin:
   ```bash
   dotnet build
   ```
3. Copy the built plugin to your BTCPay Server plugins directory

### From Release

1. Download the latest `.btcpay` plugin file from the releases
2. Install via the BTCPay Server UI (Server Settings > Plugins)

## Local Development Setup

To test the plugin locally with BTCPay Server using Docker:

1. Clone the BTCPay Server Docker repository:
   ```bash
   git clone https://github.com/btcpayserver/btcpayserver-docker
   cd btcpayserver-docker
   ```

2. Create or modify `docker-compose.override.yml` to mount your plugin directory:
   ```yaml
   version: "3"
   
   services:
     btcpayserver:
       volumes:
         - "/path/to/BTCPayServer.Plugins.Flash:/app/plugins/BTCPayServer.Plugins.Flash"
   ```

3. Run BTCPay Server:
   ```bash
   ./btcpay-up.sh
   ```

4. Access BTCPay Server at `http://localhost:8080`
5. Enable the Flash plugin in Server Settings > Plugins
6. Configure the plugin with your Flash bearer token in Store Settings

## Configuration

1. Obtain a Flash bearer token from the Flash mobile app
2. Navigate to your store settings in BTCPay Server
3. Go to the "Flash" tab
4. Enter your bearer token and save

## Security

The Flash bearer token is encrypted at rest in the BTCPay Server database. It is only used to communicate with the Flash API and is never exposed in logs or the UI.

## Usage

Once configured, select Flash as your Lightning payment provider in your store's settings:

1. Go to Store Settings > General > Lightning Network Settings
2. Choose "Flash" as the Lightning Network implementation
3. Save your settings

Your BTCPay Server will now use your Flash Lightning wallet for processing Lightning Network payments.

## License

MIT