# Flash Plugin v1.3.5 Release Notes

## Dedicated Boltcard Topup Feature

This release adds a dedicated Boltcard topup flow that works reliably with the Flash mobile wallet. The new approach bypasses the problematic LNURL handling in BTCPayServer by providing a direct invoice generation path specifically for Boltcard topups.

### New Features

- **Dedicated Boltcard Topup Page**: A simple, user-friendly interface for topping up Boltcards using Flash
- **Direct Lightning Invoice Generation**: Creates invoices with "Flashcard topup" memo that work reliably with Flash wallet
- **Mobile-Friendly Design**: QR codes and mobile deep links for seamless mobile wallet integration
- **Real-Time Status Updates**: Automatic payment status checking with visual feedback
- **Successful Payment Confirmation**: Clear success page with transaction details

### How to Use

1. Navigate to: `/plugins/flash/boltcard/topup/{storeId}`
2. Enter the amount you want to add to your Boltcard
3. Click "Create Invoice" to generate a Lightning invoice
4. Scan the QR code with your Flash mobile wallet or click "Open in Flash Wallet"
5. Complete the payment within your Flash app
6. The success page will confirm that your Boltcard has been topped up

### Technical Notes

- This implementation avoids the LNURL withdraw process entirely, which was causing amount validation issues
- Direct LN invoice generation provides a more reliable payment flow
- Mobile-focused UX with deep linking and QR code support
- Compatible with all recent versions of Flash mobile wallet

### Benefits

- Reliable Boltcard topups without LNURL-related errors
- Better user experience for Flash mobile wallet users
- Clear confirmation of successful payments
- Simplified technical implementation with fewer potential failure points