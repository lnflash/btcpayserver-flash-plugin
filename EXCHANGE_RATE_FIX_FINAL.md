# Exchange Rate Fix - Final Solution

## Problem
The Flash API returns exchange rate data as `btcSatPrice` with a base value and offset. The plugin was experiencing decimal overflow errors when trying to calculate the BTC/USD exchange rate.

## Root Cause
The original calculation incorrectly used the offset as a positive exponent:
```csharp
// WRONG: base * 10^(offset+6)
btcUsdRate = baseValue * Math.Pow(10, offset + 6);
```

This produced extremely large numbers (e.g., 1.04040398438E+17) that caused decimal overflow.

## Solution
The correct formula uses the offset as part of a negative exponent:
```csharp
// CORRECT: base * 10^(6-offset)
int exponent = 6 - offset;

if (exponent >= 0)
{
    btcUsdRateDouble = baseValue * Math.Pow(10, exponent);
}
else
{
    // Negative exponent, divide to avoid overflow
    btcUsdRateDouble = baseValue / Math.Pow(10, -exponent);
}
```

## Mathematical Explanation
- Flash API returns: cents per satoshi (with offset)
- Actual value: `base * 10^(-offset)` cents/satoshi
- To convert to BTC/USD: multiply by 10^8 (sats/BTC) and divide by 100 (cents/dollar)
- Combined formula: `base * 10^(-offset) * 10^8 / 10^2 = base * 10^(6-offset)`

## Implementation Details
The fix is implemented in `FlashGraphQLService.GetExchangeRateAsync()` (lines 299-311):
1. Calculate the exponent as `6 - offset`
2. Handle positive exponents with multiplication
3. Handle negative exponents with division to prevent overflow
4. Add sanity checks for reasonable BTC prices ($1,000 - $10,000,000)
5. Check for decimal overflow before conversion

## Result
The plugin now correctly calculates exchange rates without overflow errors. The fix has been tested and builds successfully.

## Deployment
The fixed plugin is available at:
`/Users/dread/Documents/Island-Bitcoin/Flash/claude/BTCPayServer.Plugins.Flash/bin/Release/BTCPayServer.Plugins.Flash.btcpay`

Version: 1.3.6