# BTCPayServer.Plugins.Flash Build Issues and Solutions

## Overview

This document outlines the build issues encountered when attempting to build the Flash plugin for BTCPayServer and their solutions.

## Resolved Issues

1. **Project Reference Path**
   - Issue: The BTCPayServer project was referenced at an incorrect path
   - Solution: Updated the project reference path from `..\btcpayserver\BTCPayServer\BTCPayServer.csproj` to `..\..\btcpayserver\BTCPayServer\BTCPayServer.csproj`

2. **Missing Razor Runtime Compilation**
   - Issue: The BTCPayServer project requires the Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation package for development
   - Solution: Added the package reference with version 8.0.11

3. **Base Class Implementation**
   - Issue: The FlashCardDbContextFactory class didn't correctly implement the abstract methods from BaseDbContextFactory<T>
   - Solution: Implemented the correct CreateContext method with Action<NpgsqlDbContextOptionsBuilder>? parameter

4. **CompositeDisposable Missing**
   - Issue: The FlashPaymentHostedService was using CompositeDisposable without the proper namespace
   - Solution: Added System.Reactive NuGet package and imported System.Reactive.Disposables namespace

5. **Authentication Schemes Constants**
   - Issue: Incorrect reference to AuthenticationSchemes.ApiKey which doesn't exist
   - Solution: Updated to use AuthenticationSchemes.GreenfieldAPIKeys

6. **_ViewImports.cshtml Issues**
   - Issue: Incorrect namespace references in _ViewImports.cshtml causing compilation errors
   - Solution: Simplified the imports to match the template plugin's approach

## Remaining Issues to Address

1. **FlashLightningClient Implementation Issues**
   - Missing property initializations
   - Incorrect API method signatures and parameter names
   - Incorrect or missing type definitions

2. **PullPayment and Card Integration**
   - Methods like GetBlob and GetPayoutAmountForPullPayment not found 
   - Need to understand the current BTCPayServer pull payment API

3. **Status Message Model**
   - Missing StatusMessageModel reference
   - Need to add correct namespace or implement the model

4. **Await void Issue**
   - Cannot await void in APIFlashCardController
   - Need to update to return Task

## Next Steps

1. Review the FlashLightningClient.cs implementation to ensure it matches the latest BTCPayServer Lightning API
2. Fix the remaining controller and service issues
3. Add missing models and update namespaces
4. Complete a clean build and test installation