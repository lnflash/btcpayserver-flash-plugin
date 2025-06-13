#!/bin/bash

# Exit on error
set -e

# Clean any previous build
rm -rf bin/Release

# Build the plugin
dotnet build -c Release
dotnet publish -c Release -o bin/Release/publish

# Set up plugin directories
PLUGIN_DIR="BTCPayServer.Plugins.Flash"
PACKAGE_DIR="bin/Release/package"
rm -rf $PACKAGE_DIR
mkdir -p $PACKAGE_DIR

# Copy all files to the package directory
cp bin/Release/publish/*.dll $PACKAGE_DIR/
cp bin/Release/publish/*.pdb $PACKAGE_DIR/
cp bin/Release/publish/*.json $PACKAGE_DIR/
cp manifest.json $PACKAGE_DIR/
cp manifest.json $PACKAGE_DIR/BTCPayServer.Plugins.Flash.json

# Copy Views files correctly
# Shared views
mkdir -p $PACKAGE_DIR/Views/Shared/Flash
cp Views/Shared/Flash/LNPaymentMethodSetupTab.cshtml $PACKAGE_DIR/Views/Shared/Flash/

# Flash views
mkdir -p $PACKAGE_DIR/Views/Flash
cp Views/Flash/LNPaymentMethodSetupTab.cshtml $PACKAGE_DIR/Views/Flash/
cp Views/Flash/Settings.cshtml $PACKAGE_DIR/Views/Flash/

# BoltcardTopup views
mkdir -p $PACKAGE_DIR/Views/BoltcardTopup
cp Views/BoltcardTopup/Topup.cshtml $PACKAGE_DIR/Views/BoltcardTopup/
cp Views/BoltcardTopup/Invoice.cshtml $PACKAGE_DIR/Views/BoltcardTopup/
cp Views/BoltcardTopup/Success.cshtml $PACKAGE_DIR/Views/BoltcardTopup/

cp _ViewImports.cshtml $PACKAGE_DIR/

# Create the BTCPay plugin package
cd bin/Release
rm -f $PLUGIN_DIR.btcpay
mkdir -p tmp
cp -r package/* tmp/
cd tmp
zip -r ../$PLUGIN_DIR.btcpay .
cd ../..

# Clean up
rm -rf bin/Release/tmp

echo "Plugin package created at bin/Release/BTCPayServer.Plugins.Flash.btcpay" 