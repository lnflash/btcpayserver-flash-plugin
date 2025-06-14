#!/bin/bash

# This script helps install the Flash plugin directly into the BTCPayServer plugins directory

# Exit on any error
set -e

# Color output helpers
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Flash Plugin Direct Installer${NC}"
echo "This script will install the Flash plugin directly into BTCPayServer's plugin directory"
echo ""

# Check if Docker is running
if ! docker ps >/dev/null 2>&1; then
  echo -e "${RED}Docker is not running or you don't have permission to use it${NC}"
  exit 1
fi

# Find the BTCPayServer container
CONTAINER=$(docker ps | grep "btcpayserver/btcpayserver" | awk '{print $1}')
if [ -z "$CONTAINER" ]; then
  echo -e "${RED}Could not find a running BTCPayServer container${NC}"
  exit 1
fi

echo -e "${GREEN}Found BTCPayServer container: $CONTAINER${NC}"

# Check if the plugin package exists
PLUGIN_PACKAGE="./releases/BTCPayServer.Plugins.Flash-v1.4.2.btcpay"
if [ ! -f "$PLUGIN_PACKAGE" ]; then
  echo -e "${RED}Plugin package not found: $PLUGIN_PACKAGE${NC}"
  exit 1
fi

echo -e "${GREEN}Found plugin package: $PLUGIN_PACKAGE${NC}"

# Create a temporary directory to extract the plugin
TEMP_DIR="./temp_plugin"
mkdir -p $TEMP_DIR
echo "Extracting plugin package to temporary directory..."

# Unzip the btcpay file
unzip -o $PLUGIN_PACKAGE -d $TEMP_DIR

# Create the destination directory in the container
echo "Creating plugin directory in container..."
docker exec $CONTAINER mkdir -p /root/.btcpayserver/Plugins/BTCPayServer.Plugins.Flash

# Copy the extracted files to the container
echo "Copying plugin files to container..."
docker cp $TEMP_DIR/. $CONTAINER:/root/.btcpayserver/Plugins/BTCPayServer.Plugins.Flash/

# Clean up
echo "Cleaning up temporary files..."
rm -rf $TEMP_DIR

echo -e "${GREEN}Plugin installation complete${NC}"
echo "To activate the plugin, restart BTCPayServer with this command:"
echo "docker restart $CONTAINER"
echo ""
echo "After restart, verify that the plugin is loaded with the correct version (1.4.2)"
echo "in the BTCPayServer logs. If it's still not loaded, check for error messages in the logs."